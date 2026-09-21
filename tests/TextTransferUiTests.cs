using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using AdbTool;

// 在测试自己的 Form 实例中验证手动收发及异步回调，不改写用户电脑剪贴板。
internal static class TextTransferUiTests
{
    private static T Field<T>(MainForm form, string name)
    {
        return (T)typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
    }
    private static void Call(MainForm form, string name)
    {
        typeof(MainForm).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
    }
    private static void Assert(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }
    private static void Wait(MainForm form, Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new Exception("UI timeout: " + Field<Label>(form, "textStatusLabel").Text);
            Application.DoEvents();
            Thread.Sleep(15);
        }
        Application.DoEvents();
    }
    private static void Idle(MainForm form)
    {
        Wait(form, delegate { return !Field<bool>(form, "textOperationBusy"); });
    }
    private static void AssertControls(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            Assert(!(control is CheckBox), "Unexpected synchronization checkbox");
            if (control is Button)
                Assert(control.Text == "连接文本服务" || control.Text == "断开连接" || control.Text == "发送到手机剪贴板"
                    || control.Text == "读取手机剪贴板" || control.Text == "清空", "Unexpected button: " + control.Text);
            AssertControls(control);
        }
    }
    private static void ClearColumn(Button action)
    {
        foreach (Control control in action.Parent.Controls)
        {
            if (control is Button && control.Text == "清空")
            {
                // 测试窗体不显示，直接调用按钮的点击事件，避免依赖前台焦点。
                typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(control, new object[] { EventArgs.Empty });
                return;
            }
        }
        throw new Exception("Clear button missing");
    }

    internal static void Run(string adb, string serial)
    {
        using (var phone = new ScrcpyClipboardSession(adb, serial))
        {
            phone.Open();
            string original = phone.ReadClipboard();
            if (original == null) throw new Exception("UI device tests require an existing plain text clipboard.");
            var form = new MainForm();
            try
            {
                // 建立 UI 线程消息句柄，使测试覆盖真实 BeginInvoke 回调，但不显示测试窗口。
                var handle = form.Handle;
                typeof(MainForm).GetField("configuredAdbPath", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(form, adb);
                typeof(MainForm).GetMethod("RefreshDevices", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(bool) }, null).Invoke(form, new object[] { false });
                var selectedDevices = Field<CheckedListBox>(form, "deviceList");
                bool found = false;
                for (int i = 0; i < selectedDevices.Items.Count; i++)
                {
                    bool target = selectedDevices.Items[i].ToString().Contains(serial);
                    selectedDevices.SetItemChecked(i, target);
                    found |= target;
                }
                Assert(found, "Target test device not listed");
                var timer = Field<System.Windows.Forms.Timer>(form, "textConnectionTimer");
                var draft = Field<TextBox>(form, "textSendBox");
                var preview = Field<TextBox>(form, "textReceiveBox");
                var readButton = Field<Button>(form, "textReadButton");
                var sendButton = Field<Button>(form, "textSendButton");
                AssertControls(Field<TabPage>(form, "textTransferTab"));
                Assert(draft.ShortcutsEnabled && preview.ShortcutsEnabled && !draft.ReadOnly && preview.ReadOnly,
                    "Standard text editing/copy shortcuts must remain enabled");
                Assert(draft.ContextMenu == null && draft.ContextMenuStrip == null && preview.ContextMenu == null && preview.ContextMenuStrip == null,
                    "Native context menus must remain available");
                Assert(!timer.Enabled && !readButton.Enabled && !sendButton.Enabled, "Disconnected initial controls are wrong");
                Call(form, "StartTextTransfer");
                Wait(form, delegate { return !Field<bool>(form, "textSessionStarting"); });
                Assert(Field<ScrcpyClipboardSession>(form, "textSession") != null && readButton.Enabled && sendButton.Enabled && timer.Enabled, "Connect failed");

                draft.Text = "UI 草稿 中文 😀\r\n下一行";
                string savedDraft = draft.Text;
                Call(form, "SendTextManually");
                Assert(!readButton.Enabled && !sendButton.Enabled, "Concurrent operations must be disabled");
                Idle(form);
                Assert(phone.ReadClipboard() == savedDraft, "Manual UI send failed");
                phone.WriteClipboard("手机手动接收 😀");
                Call(form, "ReadTextManually"); Idle(form);
                Assert(preview.Text == "手机手动接收 😀" && draft.Text == savedDraft, "Manual receive must preserve the draft");
                preview.SelectAll();
                Assert(preview.SelectedText == preview.Text, "Received text must remain selectable");

                phone.WriteClipboard("尚未点击读取的新文本");
                var checkUntil = DateTime.UtcNow.AddMilliseconds(1500);
                while (DateTime.UtcNow < checkUntil) { Application.DoEvents(); Thread.Sleep(20); }
                Assert(preview.Text == "手机手动接收 😀" && draft.Text == savedDraft && phone.ReadClipboard() == "尚未点击读取的新文本",
                    "Connection monitor must not transfer clipboard text");
                ClearColumn(readButton);
                Assert(preview.Text == "" && draft.Text == savedDraft, "Clear receive affected draft");
                ClearColumn(sendButton);
                Assert(draft.Text == "" && phone.ReadClipboard() == "尚未点击读取的新文本", "Clear draft must not clear phone clipboard");
                Call(form, "SendTextManually"); Idle(form);
                Assert(phone.ReadClipboard() == "", "Explicit empty send failed");
                draft.Text = new string('中', 90000);
                Call(form, "SendTextManually");
                Assert(!Field<bool>(form, "textOperationBusy") && phone.ReadClipboard() == "", "Oversized draft must be rejected");
                draft.Text = savedDraft;

                var devices = Field<CheckedListBox>(form, "deviceList");
                for (int i = 0; i < devices.Items.Count; i++) devices.SetItemChecked(i, false);
                Wait(form, delegate { return Field<ScrcpyClipboardSession>(form, "textSession") == null; });
                Assert(!timer.Enabled && !readButton.Enabled && !sendButton.Enabled, "Selection change did not stop the session and monitor");
                for (int i = 0; i < devices.Items.Count; i++)
                    if (devices.Items[i].ToString().Contains(serial)) devices.SetItemChecked(i, true);
                Call(form, "StartTextTransfer");
                Wait(form, delegate { return !Field<bool>(form, "textSessionStarting"); });
                Assert(Field<ScrcpyClipboardSession>(form, "textSession") != null && timer.Enabled && preview.Text == "" && draft.Text == savedDraft,
                    "Reconnect must preserve draft and clear preview");
                Field<ScrcpyClipboardSession>(form, "textSession").Cancel();
                Wait(form, delegate { return Field<ScrcpyClipboardSession>(form, "textSession") == null; });
                Assert(!timer.Enabled && !readButton.Enabled, "Broken transport did not stop the session and monitor");
                Console.WriteLine("PASS: simplified controls, native editing configuration, manual send/read, clear, size limit, idle monitor, selection change, reconnect and disconnect.");
            }
            finally
            {
                typeof(MainForm).GetMethod("StopTextTransfer", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { "Test cleanup", true });
                form.Dispose();
                phone.WriteClipboard(original);
                Console.WriteLine("UI tests restored original phone clipboard; Windows clipboard was not touched.");
            }
        }
    }
}
