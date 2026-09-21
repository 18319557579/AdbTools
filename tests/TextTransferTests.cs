using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using AdbTool;

internal static class TextTransferTests
{
    private static int checks;
    private static void Assert(bool value, string description)
    {
        if (!value) throw new Exception(description);
        checks++;
    }

    private static byte[] Exact(NetworkStream stream, int count)
    {
        var data = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            int n = stream.Read(data, offset, count - offset);
            if (n == 0) throw new EndOfStreamException();
            offset += n;
        }
        return data;
    }

    private static void SendText(NetworkStream stream, string value, bool fragmented)
    {
        var data = Encoding.UTF8.GetBytes(value);
        var packet = new byte[data.Length + 5];
        for (int i = 0; i < 4; i++) packet[i + 1] = (byte)(data.Length >> (24 - 8 * i));
        Buffer.BlockCopy(data, 0, packet, 5, data.Length);
        if (fragmented)
        {
            for (int i = 0; i < packet.Length; i += 3) stream.Write(packet, i, Math.Min(3, packet.Length - i));
        }
        else stream.Write(packet, 0, packet.Length);
    }

    private static void ReadRequest(NetworkStream stream)
    {
        var request = Exact(stream, 2);
        Assert(request[0] == 8 && request[1] == 0, "Read must not inject a COPY key");
    }

    private static string AcceptWrite(NetworkStream stream)
    {
        var header = Exact(stream, 14);
        Assert(header[0] == 9 && header[9] == 0, "Write must not inject PASTE");
        int length = (header[10] << 24) | (header[11] << 16) | (header[12] << 8) | header[13];
        var value = Encoding.UTF8.GetString(Exact(stream, length));
        stream.WriteByte(1);
        for (int i = 1; i < 9; i++) stream.WriteByte(header[i]);
        return value;
    }

    private static void WithPeer(Action<NetworkStream> peer, Action<ScrcpyClipboardSession> test)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var client = new TcpClient();
        client.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        var accepted = listener.AcceptTcpClient();
        listener.Stop();
        using (accepted)
        using (var session = new ScrcpyClipboardSession("unused", "unused"))
        {
            var stream = client.GetStream();
            stream.ReadTimeout = 500;
            stream.WriteTimeout = 2000;
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(ScrcpyClipboardSession).GetField("client", flags).SetValue(session, client);
            typeof(ScrcpyClipboardSession).GetField("stream", flags).SetValue(session, stream);
            accepted.GetStream().ReadTimeout = 3000;
            var peerTask = Task.Factory.StartNew(delegate { peer(accepted.GetStream()); });
            test(session);
            Assert(peerTask.Wait(4000), "Fake device timed out");
        }
    }

    private static void Expect<T>(Action operation, string description) where T : Exception
    {
        try { operation(); }
        catch (T) { checks++; return; }
        throw new Exception(description);
    }

    private static void ProtocolTests()
    {
        string sample = "中文 English 😀\n\r\n\t trailing  '\" & % | $ `";
        WithPeer(delegate(NetworkStream peer) { ReadRequest(peer); SendText(peer, sample, true); },
            delegate(ScrcpyClipboardSession session) { Assert(session.ReadClipboard() == sample, "Fragmented UTF-8 read lost content"); });
        WithPeer(delegate(NetworkStream peer) { Assert(AcceptWrite(peer) == sample, "Write lost content"); ReadRequest(peer); SendText(peer, sample, true); },
            delegate(ScrcpyClipboardSession session) { session.WriteClipboard(sample); });
        WithPeer(delegate(NetworkStream peer) { AcceptWrite(peer); ReadRequest(peer); SendText(peer, "ROM rejected write", false); },
            delegate(ScrcpyClipboardSession session) { Expect<ClipboardTransferException>(delegate { session.WriteClipboard(sample); }, "ACK alone must not mean success"); });
        WithPeer(delegate(NetworkStream peer) { ReadRequest(peer); peer.Write(new byte[] { 0, 0x7f, 0xff, 0xff, 0xff }, 0, 5); },
            delegate(ScrcpyClipboardSession session) { Expect<IOException>(delegate { session.ReadClipboard(); }, "Invalid size must be rejected before allocation"); });
        WithPeer(delegate(NetworkStream peer) { ReadRequest(peer); peer.Write(new byte[] { 0, 0, 0, 0, 5, 65 }, 0, 6); peer.Close(); },
            delegate(ScrcpyClipboardSession session) { Expect<IOException>(delegate { session.ReadClipboard(); }, "Truncated packet must fail"); });
        WithPeer(delegate(NetworkStream peer) { ReadRequest(peer); SendText(peer, "", true); },
            delegate(ScrcpyClipboardSession session) { Assert(session.ReadClipboard() == "", "Empty string must remain empty"); });
        WithPeer(delegate(NetworkStream peer) { ReadRequest(peer); System.Threading.Thread.Sleep(650); },
            delegate(ScrcpyClipboardSession session) { Assert(session.ReadClipboard() == null, "No text reply must not become empty string"); });
        WithPeer(delegate(NetworkStream peer) { ReadRequest(peer); SendText(peer, new string('a', (1 << 18) - 5), false); },
            delegate(ScrcpyClipboardSession session) { Expect<ClipboardTransferException>(delegate { session.ReadClipboard(); }, "Possibly truncated device text must be rejected"); });
        using (var session = new ScrcpyClipboardSession("unused", "unused"))
            Expect<ClipboardTransferException>(delegate { session.WriteClipboard(new string('中', 90000)); }, "Oversized text must fail before transport writes");
        string boundary = new string('a', ScrcpyClipboardSession.MaxTextBytes);
        WithPeer(delegate(NetworkStream peer) { Assert(AcceptWrite(peer) == boundary, "Boundary text altered"); ReadRequest(peer); SendText(peer, boundary, false); },
            delegate(ScrcpyClipboardSession session) { session.WriteClipboard(boundary); });
        WithPeer(delegate(NetworkStream peer) { ReadRequest(peer); peer.Write(new byte[] { 0, 0, 0, 0, 1, 0xff }, 0, 6); },
            delegate(ScrcpyClipboardSession session) { Expect<DecoderFallbackException>(delegate { session.ReadClipboard(); }, "Invalid UTF-8 must not be silently replaced"); });
    }

    private static void DeviceTests(string adb, string serial)
    {
        using (var session = new ScrcpyClipboardSession(adb, serial))
        {
            session.Open();
            var original = session.ReadClipboard();
            if (original == null) throw new Exception("Copy a plain text item on the phone before running device tests; no changes made.");
            try
            {
                string[] cases = { "文本互传测试 中文 😀\r\n\n第二行\t  '\" & | % $ `", "", new string('中', 60000), new string('a', ScrcpyClipboardSession.MaxTextBytes) };
                foreach (var value in cases)
                {
                    session.WriteClipboard(value);
                    Assert(session.ReadClipboard() == value, "Device roundtrip mismatch");
                    Console.WriteLine("Device roundtrip PASS: " + Encoding.UTF8.GetByteCount(value) + " UTF-8 bytes");
                }
                Assert(session.IsAlive, "Session unexpectedly disconnected");
                Expect<ClipboardTransferException>(delegate { session.WriteClipboard(new string('a', ScrcpyClipboardSession.MaxTextBytes + 1)); }, "Oversized device write must fail");
                Assert(session.ReadClipboard() == cases[cases.Length - 1], "Rejected write modified phone clipboard");
            }
            finally
            {
                session.WriteClipboard(original);
                Console.WriteLine("Original phone clipboard restored.");
            }
        }
    }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            ProtocolTests();
            if (args.Length == 2) DeviceTests(args[0], args[1]);
            if (args.Length == 3 && args[2] == "--ui")
            {
                DeviceTests(args[0], args[1]);
                TextTransferUiTests.Run(args[0], args[1]);
            }
            Console.WriteLine("PASS: " + checks + " assertions");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
