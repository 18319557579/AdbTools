using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace AdbTool
{
    internal sealed partial class MainForm
    {
        private readonly TabPage textTransferTab = new TabPage("文本互传");
        private readonly TextBox textSendBox = new TextBox();
        private readonly TextBox textReceiveBox = new TextBox();
        private readonly Button textConnectButton = new Button();
        private readonly Button textDisconnectButton = new Button();
        private readonly Button textSendButton = new Button();
        private readonly Button textReadButton = new Button();
        private readonly Label textDeviceLabel = new Label();
        private readonly Label textStatusLabel = new Label();
        private readonly System.Windows.Forms.Timer textConnectionTimer = new System.Windows.Forms.Timer();
        private ScrcpyClipboardSession textSession;
        private int textSessionGeneration;
        private bool textSessionStarting;
        private bool textOperationBusy;
        private bool textTransferClosing;
        private readonly List<ScrcpyClipboardSession> textRetiringSessions = new List<ScrcpyClipboardSession>();

        private void BuildTextTransferTab()
        {
            textTransferTab.Padding = new Padding(10);
            textTransferTab.AutoScroll = true;
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, MinimumSize = new Size(700, 245) };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            textTransferTab.Controls.Add(panel);

            var connection = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            ConfigureTextButton(textConnectButton, "连接文本服务", 108);
            ConfigureTextButton(textDisconnectButton, "断开连接", 88);
            textDeviceLabel.Text = "请在下方勾选一台设备";
            textDeviceLabel.AutoSize = true;
            textDeviceLabel.Margin = new Padding(6, 8, 0, 0);
            connection.Controls.AddRange(new Control[] { textConnectButton, textDisconnectButton, textDeviceLabel });
            panel.Controls.Add(connection, 0, 0);

            var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            panel.Controls.Add(columns, 0, 1);
            var sendActions = BuildTextColumn(columns, 0, "电脑 → 手机", textSendBox, false);
            var receiveActions = BuildTextColumn(columns, 1, "手机 → 电脑", textReceiveBox, true);
            var clearSend = new Button();
            var clearReceive = new Button();
            ConfigureTextButton(textSendButton, "发送到手机剪贴板", 140);
            ConfigureTextButton(clearSend, "清空", 60);
            ConfigureTextButton(textReadButton, "读取手机剪贴板", 125);
            ConfigureTextButton(clearReceive, "清空", 60);
            sendActions.Controls.AddRange(new Control[] { textSendButton, clearSend });
            receiveActions.Controls.AddRange(new Control[] { textReadButton, clearReceive });

            textStatusLabel.Text = "未连接。连接后可手动发送或读取手机剪贴板。";
            textStatusLabel.Dock = DockStyle.Fill;
            textStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            textStatusLabel.AutoEllipsis = true;
            panel.Controls.Add(textStatusLabel, 0, 2);
            panel.Controls.Add(new Label {
                Text = "左侧可用 Ctrl+V 粘贴，右侧选中文本后可用 Ctrl+C 复制。仅传纯文本，单次约 256 KB，文本不保存到日志。",
                Dock = DockStyle.Fill, ForeColor = Color.DimGray, AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 3);

            textConnectButton.Click += delegate { StartTextTransfer(); };
            textDisconnectButton.Click += delegate { StopTextTransfer("已断开文本连接。", false); };
            textSendButton.Click += delegate { SendTextManually(); };
            textReadButton.Click += delegate { ReadTextManually(); };
            clearSend.Click += delegate { textSendBox.Clear(); };
            clearReceive.Click += delegate { textReceiveBox.Clear(); };
            textConnectionTimer.Interval = 700;
            textConnectionTimer.Tick += delegate { CheckTextConnection(); };
            UpdateTextTransferControls();
        }

        private static void ConfigureTextButton(Button button, string label, int width)
        {
            button.Text = label;
            button.Size = new Size(width, 28);
            button.Margin = new Padding(0, 2, 6, 2);
        }

        private static FlowLayoutPanel BuildTextColumn(TableLayoutPanel parent, int column, string title, TextBox box, bool readOnly)
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(column == 0 ? 0 : 6, 0, column == 0 ? 6 : 0, 0) };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            panel.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            box.Dock = DockStyle.Fill;
            box.Multiline = true;
            box.AcceptsReturn = true;
            box.AcceptsTab = !readOnly;
            box.ScrollBars = ScrollBars.Both;
            box.WordWrap = false;
            box.ReadOnly = readOnly;
            box.MaxLength = int.MaxValue;
            box.BackColor = SystemColors.Window;
            box.Margin = new Padding(0, 0, 0, 3);
            panel.Controls.Add(box, 0, 1);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            panel.Controls.Add(actions, 0, 2);
            parent.Controls.Add(panel, column, 0);
            return actions;
        }

        private void StartTextTransfer()
        {
            if (textSession != null || textTransferClosing) return;
            var device = GetSingleCheckedDevice("文本互传");
            if (device == null) return;
            var adb = FindAdb();
            if (adb == null) { HandleMissingAdb(true); return; }
            var session = new ScrcpyClipboardSession(adb, device.Serial);
            textSession = session;
            textReceiveBox.Clear();
            textSessionStarting = true;
            textConnectionTimer.Start();
            var generation = ++textSessionGeneration;
            textDeviceLabel.Text = "设备：" + device.Serial;
            SetTextTransferStatus("正在连接手机文本服务…", false);
            UpdateTextTransferControls();
            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception failure = null;
                try { session.Open(); }
                catch (Exception ex) { failure = ex; }
                BeginInvokeIfNeeded(delegate
                {
                    if (textTransferClosing || generation != textSessionGeneration) return;
                    textSessionStarting = false;
                    if (failure != null) { StopTextTransfer("连接失败：" + failure.Message, false); return; }
                    SetTextTransferStatus("已连接，可手动收发文本。", true);
                    UpdateTextTransferControls();
                });
            });
        }

        private void StopTextTransfer(string status, bool closing)
        {
            if (closing) textTransferClosing = true;
            textConnectionTimer.Stop();
            ++textSessionGeneration;
            textSessionStarting = false;
            textOperationBusy = false;
            var session = textSession;
            textSession = null;
            if (session != null)
            {
                session.Cancel();
                if (closing) session.Dispose();
                else
                {
                    lock (textRetiringSessions) textRetiringSessions.Add(session);
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        session.Dispose();
                        lock (textRetiringSessions) textRetiringSessions.Remove(session);
                    });
                }
            }
            if (closing)
            {
                ScrcpyClipboardSession[] retiring;
                lock (textRetiringSessions) retiring = textRetiringSessions.ToArray();
                foreach (var oldSession in retiring) oldSession.Dispose();
                textConnectionTimer.Dispose();
            }
            textDeviceLabel.Text = "未连接";
            SetTextTransferStatus(status, session != null && !closing);
            UpdateTextTransferControls();
        }

        private bool TextDeviceMatches()
        {
            if (textSession == null) return false;
            DeviceInfo device;
            if (TryGetSingleCheckedDevice(out device) && device.Serial == textSession.Serial) return true;
            StopTextTransfer("设备选择已变化，文本连接已停止，请重新连接。", false);
            return false;
        }

        private void UpdateTextTransferControls()
        {
            bool connected = textSession != null && !textSessionStarting;
            bool manual = connected && !textOperationBusy;
            textConnectButton.Enabled = textSession == null && !textTransferClosing;
            textDisconnectButton.Enabled = textSession != null;
            textSendButton.Enabled = manual;
            textReadButton.Enabled = manual;
        }

        private void RunTextOperation(Func<ScrcpyClipboardSession, string> operation, Action<string> success)
        {
            if (textOperationBusy || !TextDeviceMatches() || textSessionStarting) return;
            var session = textSession;
            int generation = textSessionGeneration;
            textOperationBusy = true;
            UpdateTextTransferControls();
            ThreadPool.QueueUserWorkItem(delegate
            {
                string result = null;
                Exception failure = null;
                try { result = operation(session); }
                catch (Exception ex) { failure = ex; }
                BeginInvokeIfNeeded(delegate
                {
                    if (textTransferClosing || generation != textSessionGeneration) return;
                    textOperationBusy = false;
                    if (!TextDeviceMatches()) return;
                    if (failure != null)
                    {
                        if (failure is ClipboardTransferException)
                        {
                            SetTextTransferStatus(failure.Message, true);
                        }
                        else StopTextTransfer("文本连接异常，已断开连接：" + failure.Message, false);
                    }
                    else success(result);
                    UpdateTextTransferControls();
                });
            });
        }

        private void SendTextManually()
        {
            var value = textSendBox.Text;
            if (!ValidateTextLength(value)) return;
            SetTextTransferStatus("正在发送并核对手机剪贴板…", false);
            RunTextOperation(delegate(ScrcpyClipboardSession session) { session.WriteClipboard(value); return value; }, delegate(string result)
            {
                SetTextTransferStatus("已发送并核对 " + Encoding.UTF8.GetByteCount(result) + " 字节，请在手机中粘贴。", true);
            });
        }

        private void ReadTextManually()
        {
            SetTextTransferStatus("正在读取手机剪贴板…", false);
            RunTextOperation(delegate(ScrcpyClipboardSession session) { return session.ReadClipboard(); }, delegate(string result)
            {
                if (result == null) { SetTextTransferStatus("未读到纯文本：手机剪贴板可能为空、包含非文本，或系统限制了读取。请在手机复制文本后重试。", false); return; }
                textReceiveBox.Text = result;
                SetTextTransferStatus("已读取 " + Encoding.UTF8.GetByteCount(result) + " 字节，可在右侧选中文本后按 Ctrl+C 或右键复制。", true);
            });
        }

        private bool ValidateTextLength(string value)
        {
            if (Encoding.UTF8.GetByteCount(value) <= ScrcpyClipboardSession.MaxTextBytes) return true;
            SetTextTransferStatus("文本过长：UTF-8 编码后最多 " + ScrcpyClipboardSession.MaxTextBytes + " 字节，请分段发送。", false);
            return false;
        }

        private void CheckTextConnection()
        {
            if (textTransferClosing || textSession == null || !TextDeviceMatches() || textSessionStarting) return;
            // 定时器只检查连接，不读取两端剪贴板；文本传输由用户点击按钮触发。
            if (!textOperationBusy && !textSession.IsAlive) StopTextTransfer("手机文本连接已断开，请重新连接。", false);
        }

        private void SetTextTransferStatus(string message, bool log)
        {
            textStatusLabel.Text = message;
            if (log) AddLogLine("[文本互传] " + message);
        }
    }

}
