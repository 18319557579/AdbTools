using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace AdbTool
{
    // 只实现固定版本的剪贴板协议；音视频和按键注入均不启用。
    internal sealed class ScrcpyClipboardSession : IDisposable
    {
        internal const int MaxTextBytes = (1 << 18) - 14;
        private const int MaxReceivedBytes = (1 << 18) - 5;
        private const string ServerHash = "84924bd564a1eb6089c872c7521f968058977f91f5ff02514a8c74aff3210f3a";
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly object lifetimeGate = new object();
        private readonly object ioGate = new object();
        private readonly string adb;
        internal readonly string Serial;
        private readonly string id = Guid.NewGuid().ToString("N");
        private string remotePath;
        private string localPath;
        private string forwardPort;
        private Process serverProcess;
        private TcpClient client;
        private NetworkStream stream;
        private volatile bool disposed;
        private bool cleaned;
        private ulong sequence;

        internal ScrcpyClipboardSession(string adbPath, string serial)
        {
            adb = adbPath;
            Serial = serial;
        }

        internal void Open()
        {
            lock (lifetimeGate)
            {
                try
                {
                    CheckDisposed();
                    localPath = Path.Combine(Path.GetTempPath(), "adb-tool-text-" + id + ".jar");
                    using (var source = Assembly.GetExecutingAssembly().GetManifestResourceStream("AdbTool.scrcpy-server-v4.0"))
                    {
                        if (source == null) throw new IOException("缺少内置文本服务，请使用完整编译版本。");
                        using (var destination = File.Create(localPath)) source.CopyTo(destination);
                    }
                    using (var sha = SHA256.Create())
                    using (var file = File.OpenRead(localPath))
                    {
                        var hash = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
                        if (hash != ServerHash) throw new IOException("内置文本服务校验失败。");
                    }
                    remotePath = "/data/local/tmp/adb-tool-text-" + id + ".jar";
                    RunAdb("push", localPath, remotePath);
                    CheckDisposed();
                    // tcp:0 由 ADB 分配空闲端口，随机 socket 名避免干扰其他 scrcpy 实例。
                    var socketId = (BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 0) & 0x7fffffff).ToString("x8");
                    forwardPort = RunAdb("forward", "tcp:0", "localabstract:scrcpy_" + socketId).Trim();
                    int port;
                    if (!int.TryParse(forwardPort, out port) || port < 1 || port > 65535)
                        throw new IOException("ADB 未能分配文本连接端口。");
                    CheckDisposed();
                    serverProcess = CreateProcess("shell", "CLASSPATH=" + remotePath, "app_process", "/",
                        "com.genymobile.scrcpy.Server", "4.0", "scid=" + socketId,
                        "video=false", "audio=false", "control=true", "clipboard_autosync=false",
                        "power_on=false", "tunnel_forward=true", "send_device_meta=false", "log_level=warn");
                    // 持续排空输出，既避免管道阻塞，也不把手机剪贴板内容写入日志。
                    serverProcess.OutputDataReceived += delegate { };
                    serverProcess.ErrorDataReceived += delegate { };
                    serverProcess.Start();
                    serverProcess.BeginOutputReadLine();
                    serverProcess.BeginErrorReadLine();
                    var deadline = DateTime.UtcNow.AddSeconds(10);
                    while (DateTime.UtcNow < deadline)
                    {
                        CheckDisposed();
                        if (serverProcess.HasExited) throw new IOException("手机文本服务启动失败，请检查 USB 调试授权及系统兼容性。");
                        try
                        {
                            client = new TcpClient();
                            var connect = client.BeginConnect("127.0.0.1", port, null, null);
                            using (connect.AsyncWaitHandle)
                            {
                                if (!connect.AsyncWaitHandle.WaitOne(1000)) throw new IOException("连接超时。");
                                client.EndConnect(connect);
                            }
                            client.NoDelay = true;
                            stream = client.GetStream();
                            stream.ReadTimeout = 1500;
                            stream.WriteTimeout = 4000;
                            if (stream.ReadByte() != 0) throw new IOException("文本服务握手失败。");
                            CheckDisposed();
                            try { File.Delete(localPath); } catch { }
                            return;
                        }
                        catch (Exception ex)
                        {
                            if (!(ex is IOException) && !(ex is SocketException)) throw;
                            CloseTransport();
                            Thread.Sleep(150);
                        }
                    }
                    throw new IOException("连接手机文本服务超时，请重新连接设备后重试。");
                }
                catch
                {
                    disposed = true;
                    Cleanup();
                    throw;
                }
            }
        }

        internal bool IsAlive
        {
            get
            {
                try
                {
                    return !disposed && stream != null && serverProcess != null && !serverProcess.HasExited
                        && !(client.Client.Poll(0, SelectMode.SelectRead) && client.Client.Available == 0);
                }
                catch { return false; }
            }
        }

        internal string ReadClipboard()
        {
            lock (ioGate)
            {
                CheckDisposed();
                return ReadClipboardCore();
            }
        }

        private string ReadClipboardCore()
        {
            stream.Write(new byte[] { 8, 0 }, 0, 2); // GET_CLIPBOARD，0 表示不触发复制按键。
            int type;
            try { type = stream.ReadByte(); }
            catch (IOException ex)
            {
                var socketError = ex.InnerException as SocketException;
                // scrcpy 对“没有纯文本/无读取权限”不返回消息，不能把它报告成空字符串。
                if (socketError != null && socketError.SocketErrorCode == SocketError.TimedOut) return null;
                throw;
            }
            if (type != 0) throw new IOException("手机文本连接已断开或协议响应异常。");
            var lengthBytes = ReadExact(4);
            int length = (lengthBytes[0] << 24) | (lengthBytes[1] << 16) | (lengthBytes[2] << 8) | lengthBytes[3];
            if (length < 0 || length > MaxReceivedBytes) throw new IOException("手机返回了无效的文本长度。");
            var data = ReadExact(length);
            // 达到协议边界时无法证明内容未被服务端截断，因此拒绝默默交付不完整文本。
            if (length >= MaxReceivedBytes - 3) throw new ClipboardTransferException("手机文本接近协议上限，可能被截断，请分段复制。");
            return Utf8.GetString(data);
        }

        internal void WriteClipboard(string text)
        {
            if (Utf8.GetByteCount(text) > MaxTextBytes) throw new ClipboardTransferException("文本过长：UTF-8 编码后最多支持 " + MaxTextBytes + " 字节，请分段发送。");
            var data = Utf8.GetBytes(text);
            lock (ioGate)
            {
                CheckDisposed();
                var packet = new byte[14 + data.Length];
                packet[0] = 9;
                var currentSequence = ++sequence;
                for (int i = 0; i < 8; i++) packet[1 + i] = (byte)(currentSequence >> ((7 - i) * 8));
                packet[9] = 0; // 只写剪贴板，不向手机当前应用注入粘贴操作。
                for (int i = 0; i < 4; i++) packet[10 + i] = (byte)(data.Length >> ((3 - i) * 8));
                Buffer.BlockCopy(data, 0, packet, 14, data.Length);
                stream.Write(packet, 0, packet.Length);
                if (stream.ReadByte() != 1) throw new IOException("手机未确认文本请求。");
                var ack = ReadExact(8);
                for (int i = 0; i < 8; i++)
                    if (ack[i] != packet[i + 1]) throw new IOException("手机文本确认序号不匹配。");
                // ACK 只确认请求被处理，写入被 ROM 拒绝时也可能返回，必须读回校验。
                var actual = ReadClipboardCore();
                if (!string.Equals(actual, text, StringComparison.Ordinal))
                    throw new ClipboardTransferException("未能确认手机剪贴板内容：系统可能限制了访问，或内容已被手机其他操作修改。请重新读取后检查。");
            }
        }

        private byte[] ReadExact(int length)
        {
            var data = new byte[length];
            int offset = 0;
            while (offset < length)
            {
                int count = stream.Read(data, offset, length - offset);
                if (count == 0) throw new IOException("手机文本连接已断开。");
                offset += count;
            }
            return data;
        }

        internal void Cancel()
        {
            disposed = true;
            CloseTransport();
        }

        public void Dispose()
        {
            Cancel();
            lock (lifetimeGate) Cleanup();
        }

        private void CloseTransport()
        {
            try { if (client != null) client.Close(); } catch { }
        }

        private void Cleanup()
        {
            if (cleaned) return;
            cleaned = true;
            CloseTransport();
            if (serverProcess != null)
            {
                try
                {
                    if (!serverProcess.WaitForExit(1500)) serverProcess.Kill();
                }
                catch { }
                try { serverProcess.Dispose(); } catch { }
            }
            // 只删除本会话创建的映射和文件，绝不调用 forward --remove-all 或 kill-server。
            if (!string.IsNullOrEmpty(forwardPort)) TryRunAdb("forward", "--remove", "tcp:" + forwardPort);
            if (!string.IsNullOrEmpty(remotePath)) TryRunAdb("shell", "rm", "-f", remotePath);
            try { if (localPath != null) File.Delete(localPath); } catch { }
        }

        private void CheckDisposed()
        {
            if (disposed) throw new ObjectDisposedException("文本连接");
        }

        private Process CreateProcess(params string[] arguments)
        {
            var info = new ProcessStartInfo(adb);
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            info.Arguments = "-s " + Quote(Serial);
            foreach (var argument in arguments) info.Arguments += " " + Quote(argument);
            return new Process { StartInfo = info };
        }

        private string RunAdb(params string[] arguments)
        {
            using (var process = CreateProcess(arguments))
            {
                var output = new StringBuilder();
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null) lock (output) { if (output.Length < 8192) output.AppendLine(e.Data); }
                };
                process.ErrorDataReceived += delegate { };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                if (!process.WaitForExit(8000))
                {
                    try { process.Kill(); } catch { }
                    throw new IOException("ADB 文本连接操作超时。");
                }
                process.WaitForExit();
                if (process.ExitCode != 0) throw new IOException("ADB 文本连接操作失败，请检查设备连接和调试授权。");
                return output.ToString();
            }
        }

        private void TryRunAdb(params string[] arguments)
        {
            try { RunAdb(arguments); } catch { }
        }

        private static string Quote(string value)
        {
            var builder = new StringBuilder("\"");
            int backslashes = 0;
            foreach (char ch in value)
            {
                if (ch == '\\') { backslashes++; continue; }
                builder.Append('\\', ch == '"' ? backslashes * 2 + 1 : backslashes);
                builder.Append(ch);
                backslashes = 0;
            }
            builder.Append('\\', backslashes * 2);
            return builder.Append('"').ToString();
        }
    }

    internal sealed class ClipboardTransferException : Exception
    {
        internal ClipboardTransferException(string message) : base(message) { }
    }
}
