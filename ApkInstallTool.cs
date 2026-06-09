using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace ApkInstallTool
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly TabControl tabControl = new TabControl();
        private readonly TabPage installTab = new TabPage("APK 安装");
        private readonly TabPage connectionTab = new TabPage("设备连接");
        private readonly TabPage logRecordTab = new TabPage("日志录制");
        private readonly TextBox apkTextBox = new TextBox();
        private readonly Button browseButton = new Button();
        private readonly Button refreshButton = new Button();
        private readonly Button toggleDevicesButton = new Button();
        private readonly TextBox connectAddressTextBox = new TextBox();
        private readonly Button connectButton = new Button();
        private readonly Button disconnectButton = new Button();
        private readonly CheckedListBox deviceList = new CheckedListBox();
        private readonly RadioButton installModeRadioButton = new RadioButton();
        private readonly RadioButton cleanInstallModeRadioButton = new RadioButton();
        private readonly RadioButton uninstallModeRadioButton = new RadioButton();
        private readonly RadioButton clearDataModeRadioButton = new RadioButton();
        private readonly RadioButton startAppModeRadioButton = new RadioButton();
        private readonly CheckBox launchAfterInstallCheckBox = new CheckBox();
        private readonly Button installButton = new Button();
        private readonly Button cancelButton = new Button();
        private readonly Button clearLogButton = new Button();
        private readonly Label apkInfoLabel = new Label();
        private readonly Label statusLabel = new Label();
        private readonly TextBox logBox = new TextBox();

        private readonly TextBox logRecordPathTextBox = new TextBox();
        private readonly TextBox logRecordTagTextBox = new TextBox();
        private readonly TextBox logRecordPackageTextBox = new TextBox();
        private readonly ComboBox logRecordLevelComboBox = new ComboBox();
        private readonly CheckBox logRecordThreadInfoCheckBox = new CheckBox();
        private readonly CheckBox logRecordTimeInfoCheckBox = new CheckBox();
        private readonly Button browseLogRecordFileButton = new Button();
        private readonly Button browseLogRecordFolderButton = new Button();
        private readonly Button clearLogcatCacheButton = new Button();
        private readonly Button exportLogcatCacheButton = new Button();
        private readonly Button startLogRecordButton = new Button();
        private readonly Button stopLogRecordButton = new Button();

        private readonly Dictionary<string, DeviceInfo> deviceMap = new Dictionary<string, DeviceInfo>();
        private readonly object processLock = new object();
        private readonly string appDir;
        private readonly string configPath;
        private readonly string logDir;
        private Process currentProcess;
        private Process logcatProcess;
        private StreamWriter logcatWriter;
        private HashSet<string> logcatPidFilter;
        private bool logcatIncludeThreadInfo = true;
        private bool logcatIncludeTimeInfo = true;
        private readonly object logcatLock = new object();
        private volatile bool cancelRequested;
        private volatile bool isExecuting;
        private volatile bool isDeviceCommandRunning;
        private volatile bool isLogcatRunning;
        private bool loadingConfig;
        private bool configReady;
        private ApkInfo currentApkInfo;

        public MainForm()
        {
            appDir = AppDomain.CurrentDomain.BaseDirectory;
            configPath = Path.Combine(appDir, "install-apk.config.json");
            logDir = Path.Combine(appDir, "log");
            Text = "APK安装工具";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1080, 820);
            Size = new Size(1180, 900);
            Font = new Font("Microsoft YaHei UI", 9F);
            AllowDrop = true;
            BuildUi();
            WireEvents();
            Directory.CreateDirectory(logDir);
            InitLogcatDefaults();
            LoadConfig();
            configReady = true;
            UpdateExecutionOptionState();
            RefreshDevices();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(12);
            root.ColumnCount = 1;
            root.RowCount = 5;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 300));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 66));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            Controls.Add(root);

            tabControl.Dock = DockStyle.Fill;
            tabControl.TabPages.Add(installTab);
            tabControl.TabPages.Add(connectionTab);
            tabControl.TabPages.Add(logRecordTab);
            root.Controls.Add(tabControl, 0, 0);
            BuildInstallTab();
            BuildConnectionTab();
            BuildLogRecordTab();
            BuildSharedDeviceArea(root);
            BuildSharedLogArea(root);
        }

        private void BuildInstallTab()
        {
            installTab.Padding = new Padding(10);
            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 1;
            panel.RowCount = 4;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            installTab.Controls.Add(panel);

            var apkPanel = new TableLayoutPanel();
            apkPanel.Dock = DockStyle.Fill;
            apkPanel.ColumnCount = 3;
            apkPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            apkPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            apkPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            panel.Controls.Add(apkPanel, 0, 0);
            var apkLabel = new Label();
            apkLabel.Text = "APK 文件";
            apkLabel.Dock = DockStyle.Fill;
            apkLabel.TextAlign = ContentAlignment.MiddleLeft;
            apkPanel.Controls.Add(apkLabel, 0, 0);
            apkTextBox.Dock = DockStyle.Fill;
            apkPanel.Controls.Add(apkTextBox, 1, 0);
            browseButton.Text = "选择...";
            browseButton.Dock = DockStyle.Fill;
            apkPanel.Controls.Add(browseButton, 2, 0);

            apkInfoLabel.Dock = DockStyle.Fill;
            apkInfoLabel.TextAlign = ContentAlignment.MiddleLeft;
            apkInfoLabel.ForeColor = Color.FromArgb(60, 60, 60);
            panel.Controls.Add(apkInfoLabel, 0, 1);

            var optionsPanel = new FlowLayoutPanel();
            optionsPanel.Dock = DockStyle.Fill;
            optionsPanel.FlowDirection = FlowDirection.LeftToRight;
            optionsPanel.WrapContents = false;
            panel.Controls.Add(optionsPanel, 0, 2);
            AddModeOption(optionsPanel, installModeRadioButton, "安装/覆盖", true);
            AddModeOption(optionsPanel, cleanInstallModeRadioButton, "卸载后安装", false);
            AddModeOption(optionsPanel, uninstallModeRadioButton, "仅卸载", false);
            AddModeOption(optionsPanel, clearDataModeRadioButton, "清空数据", false);
            AddModeOption(optionsPanel, startAppModeRadioButton, "启动应用", false);
            launchAfterInstallCheckBox.Text = "安装后启动";
            launchAfterInstallCheckBox.AutoSize = true;
            launchAfterInstallCheckBox.Margin = new Padding(0, 12, 10, 0);
            optionsPanel.Controls.Add(launchAfterInstallCheckBox);

            var actionPanel = new TableLayoutPanel();
            actionPanel.Dock = DockStyle.Fill;
            actionPanel.ColumnCount = 3;
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.Controls.Add(actionPanel, 0, 3);
            installButton.Text = "开始执行";
            installButton.Dock = DockStyle.None;
            installButton.Size = new Size(88, 28);
            installButton.Margin = new Padding(0, 4, 8, 0);
            actionPanel.Controls.Add(installButton, 0, 0);
            cancelButton.Text = "中止执行";
            cancelButton.Dock = DockStyle.None;
            cancelButton.Size = new Size(88, 28);
            cancelButton.Margin = new Padding(0, 4, 8, 0);
            cancelButton.Enabled = false;
            actionPanel.Controls.Add(cancelButton, 1, 0);
        }

        private void AddModeOption(FlowLayoutPanel panel, RadioButton button, string text, bool isChecked)
        {
            button.Text = text;
            button.Checked = isChecked;
            button.AutoSize = true;
            button.Margin = new Padding(0, 12, 14, 0);
            panel.Controls.Add(button);
        }

        private void BuildConnectionTab()
        {
            connectionTab.Padding = new Padding(10);
            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Top;
            panel.Height = 118;
            panel.ColumnCount = 1;
            panel.RowCount = 3;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            connectionTab.Controls.Add(panel);
            var title = new Label();
            title.Text = "设备连接";
            title.Dock = DockStyle.Fill;
            title.TextAlign = ContentAlignment.MiddleLeft;
            panel.Controls.Add(title, 0, 0);
            var connectPanel = new TableLayoutPanel();
            connectPanel.Dock = DockStyle.Fill;
            connectPanel.ColumnCount = 4;
            connectPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            connectPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            connectPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            connectPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            panel.Controls.Add(connectPanel, 0, 1);
            var connectLabel = new Label();
            connectLabel.Text = "设备地址";
            connectLabel.Dock = DockStyle.Fill;
            connectLabel.TextAlign = ContentAlignment.MiddleLeft;
            connectPanel.Controls.Add(connectLabel, 0, 0);
            connectAddressTextBox.Dock = DockStyle.Fill;
            connectAddressTextBox.Margin = new Padding(0, 4, 8, 4);
            connectPanel.Controls.Add(connectAddressTextBox, 1, 0);
            connectButton.Text = "连接设备";
            connectButton.Dock = DockStyle.Fill;
            connectButton.Margin = new Padding(0, 3, 8, 3);
            connectPanel.Controls.Add(connectButton, 2, 0);
            disconnectButton.Text = "断连设备";
            disconnectButton.Dock = DockStyle.Fill;
            disconnectButton.Margin = new Padding(0, 3, 0, 3);
            connectPanel.Controls.Add(disconnectButton, 3, 0);
            var hint = new Label();
            hint.Text = "单击下方目标设备列表会同步设备 ID；无线连接可输入 IP:端口，例如 192.168.1.100:5555。";
            hint.Dock = DockStyle.Fill;
            hint.TextAlign = ContentAlignment.MiddleLeft;
            hint.ForeColor = Color.FromArgb(80, 80, 80);
            panel.Controls.Add(hint, 0, 2);
        }

        private void BuildLogRecordTab()
        {
            logRecordTab.Padding = new Padding(10);
            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Top;
            panel.Height = 194;
            panel.ColumnCount = 1;
            panel.RowCount = 5;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            logRecordTab.Controls.Add(panel);

            var pathPanel = new TableLayoutPanel();
            pathPanel.Dock = DockStyle.Fill;
            pathPanel.ColumnCount = 4;
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 106));
            panel.Controls.Add(pathPanel, 0, 0);

            var pathLabel = new Label();
            pathLabel.Text = "输出路径";
            pathLabel.Dock = DockStyle.Fill;
            pathLabel.TextAlign = ContentAlignment.MiddleLeft;
            pathPanel.Controls.Add(pathLabel, 0, 0);
            logRecordPathTextBox.Dock = DockStyle.Fill;
            logRecordPathTextBox.Margin = new Padding(0, 4, 8, 4);
            pathPanel.Controls.Add(logRecordPathTextBox, 1, 0);
            browseLogRecordFileButton.Text = "选择文件";
            browseLogRecordFileButton.Dock = DockStyle.Fill;
            browseLogRecordFileButton.Margin = new Padding(0, 3, 8, 3);
            pathPanel.Controls.Add(browseLogRecordFileButton, 2, 0);
            browseLogRecordFolderButton.Text = "选择文件夹";
            browseLogRecordFolderButton.Dock = DockStyle.Fill;
            browseLogRecordFolderButton.Margin = new Padding(0, 3, 0, 3);
            pathPanel.Controls.Add(browseLogRecordFolderButton, 3, 0);

            var tagPanel = new TableLayoutPanel();
            tagPanel.Dock = DockStyle.Fill;
            tagPanel.ColumnCount = 4;
            tagPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            tagPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tagPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            tagPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            panel.Controls.Add(tagPanel, 0, 1);

            var tagLabel = new Label();
            tagLabel.Text = "过滤 Tag";
            tagLabel.Dock = DockStyle.Fill;
            tagLabel.TextAlign = ContentAlignment.MiddleLeft;
            tagPanel.Controls.Add(tagLabel, 0, 0);
            logRecordTagTextBox.Dock = DockStyle.Fill;
            logRecordTagTextBox.Margin = new Padding(0, 4, 8, 4);
            tagPanel.Controls.Add(logRecordTagTextBox, 1, 0);
            var levelLabel = new Label();
            levelLabel.Text = "\u65e5\u5fd7\u7b49\u7ea7";
            levelLabel.Dock = DockStyle.Fill;
            levelLabel.TextAlign = ContentAlignment.MiddleLeft;
            tagPanel.Controls.Add(levelLabel, 2, 0);
            logRecordLevelComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            logRecordLevelComboBox.Dock = DockStyle.Fill;
            logRecordLevelComboBox.Margin = new Padding(0, 4, 0, 4);
            logRecordLevelComboBox.Items.AddRange(new object[]
            {
                "\u5168\u90e8 Verbose (V)",
                "\u8c03\u8bd5 Debug (D)",
                "\u4fe1\u606f Info (I)",
                "\u8b66\u544a Warn (W)",
                "\u9519\u8bef Error (E)",
                "\u4e25\u91cd Fatal (F)"
            });
            tagPanel.Controls.Add(logRecordLevelComboBox, 3, 0);

            var packagePanel = new TableLayoutPanel();
            packagePanel.Dock = DockStyle.Fill;
            packagePanel.ColumnCount = 5;
            packagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            packagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            packagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            packagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            packagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12));
            panel.Controls.Add(packagePanel, 0, 2);

            var packageLabel = new Label();
            packageLabel.Text = "包名";
            packageLabel.Dock = DockStyle.Fill;
            packageLabel.TextAlign = ContentAlignment.MiddleLeft;
            packagePanel.Controls.Add(packageLabel, 0, 0);
            logRecordPackageTextBox.Dock = DockStyle.Fill;
            logRecordPackageTextBox.Margin = new Padding(0, 4, 8, 4);
            packagePanel.Controls.Add(logRecordPackageTextBox, 1, 0);
            logRecordThreadInfoCheckBox.Text = "线程信息";
            logRecordThreadInfoCheckBox.Dock = DockStyle.Fill;
            logRecordThreadInfoCheckBox.TextAlign = ContentAlignment.MiddleLeft;
            logRecordThreadInfoCheckBox.Margin = new Padding(0, 4, 8, 4);
            packagePanel.Controls.Add(logRecordThreadInfoCheckBox, 2, 0);
            logRecordTimeInfoCheckBox.Text = "时间信息";
            logRecordTimeInfoCheckBox.Dock = DockStyle.Fill;
            logRecordTimeInfoCheckBox.TextAlign = ContentAlignment.MiddleLeft;
            logRecordTimeInfoCheckBox.Margin = new Padding(0, 4, 0, 4);
            packagePanel.Controls.Add(logRecordTimeInfoCheckBox, 3, 0);

            var actionPanel = new TableLayoutPanel();
            actionPanel.Dock = DockStyle.Fill;
            actionPanel.ColumnCount = 5;
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.Controls.Add(actionPanel, 0, 3);
            clearLogcatCacheButton.Text = "清除缓存";
            exportLogcatCacheButton.Text = "导出缓存";
            startLogRecordButton.Text = "开始录制";
            stopLogRecordButton.Text = "退出录制";
            AddActionButton(actionPanel, clearLogcatCacheButton, 0);
            AddActionButton(actionPanel, exportLogcatCacheButton, 1);
            AddActionButton(actionPanel, startLogRecordButton, 2);
            AddActionButton(actionPanel, stopLogRecordButton, 3);
            stopLogRecordButton.Enabled = false;

            var hint = new Label();
            hint.Text = "文件夹会自动保存为 log-时间.txt；多个 Tag 可用空格、逗号或分号分隔。";
            hint.Dock = DockStyle.Fill;
            hint.TextAlign = ContentAlignment.MiddleLeft;
            hint.ForeColor = Color.FromArgb(80, 80, 80);
            panel.Controls.Add(hint, 0, 4);
        }

        private void AddLabel(TableLayoutPanel panel, string text, int column)
        {
            var label = new Label();
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            panel.Controls.Add(label, column, 0);
        }

        private void AddActionButton(TableLayoutPanel panel, Button button, int column)
        {
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(0, 3, 8, 3);
            panel.Controls.Add(button, column, 0);
        }

        private void BuildSharedDeviceArea(TableLayoutPanel root)
        {
            var devicePanel = new TableLayoutPanel();
            devicePanel.Dock = DockStyle.Fill;
            devicePanel.ColumnCount = 1;
            devicePanel.RowCount = 2;
            devicePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            devicePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(devicePanel, 0, 1);
            var deviceHeader = new TableLayoutPanel();
            deviceHeader.Dock = DockStyle.Fill;
            deviceHeader.ColumnCount = 3;
            deviceHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            deviceHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            deviceHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
            devicePanel.Controls.Add(deviceHeader, 0, 0);
            var deviceLabel = new Label();
            deviceLabel.Text = "目标设备列表";
            deviceLabel.Dock = DockStyle.Fill;
            deviceLabel.TextAlign = ContentAlignment.MiddleLeft;
            deviceHeader.Controls.Add(deviceLabel, 0, 0);
            refreshButton.Text = "刷新";
            refreshButton.Dock = DockStyle.Fill;
            refreshButton.Margin = new Padding(4, 2, 4, 2);
            deviceHeader.Controls.Add(refreshButton, 1, 0);
            toggleDevicesButton.Text = "全选/反选";
            toggleDevicesButton.Dock = DockStyle.Fill;
            toggleDevicesButton.Margin = new Padding(4, 2, 0, 2);
            deviceHeader.Controls.Add(toggleDevicesButton, 2, 0);
            deviceList.Dock = DockStyle.Fill;
            deviceList.CheckOnClick = true;
            devicePanel.Controls.Add(deviceList, 0, 1);
        }

        private void BuildSharedLogArea(TableLayoutPanel root)
        {
            var logHeader = new TableLayoutPanel();
            logHeader.Dock = DockStyle.Fill;
            logHeader.ColumnCount = 2;
            logHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            logHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            root.Controls.Add(logHeader, 0, 2);
            var logLabel = new Label();
            logLabel.Text = "运行日志";
            logLabel.Dock = DockStyle.Fill;
            logLabel.TextAlign = ContentAlignment.MiddleLeft;
            logHeader.Controls.Add(logLabel, 0, 0);
            clearLogButton.Text = "清空日志";
            clearLogButton.Dock = DockStyle.Fill;
            clearLogButton.Margin = new Padding(4, 2, 0, 2);
            logHeader.Controls.Add(clearLogButton, 1, 0);
            logBox.Dock = DockStyle.Fill;
            logBox.Multiline = true;
            logBox.ScrollBars = ScrollBars.Both;
            logBox.WordWrap = false;
            logBox.ReadOnly = true;
            logBox.Font = new Font("Consolas", 9F);
            root.Controls.Add(logBox, 0, 3);
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.Text = "就绪";
            root.Controls.Add(statusLabel, 0, 4);
        }

        private void WireEvents()
        {
            browseButton.Click += delegate { BrowseApk(); };
            refreshButton.Click += delegate { RefreshDevices(); };
            toggleDevicesButton.Click += delegate { ToggleDevices(); };
            connectButton.Click += delegate { ConnectDevice(); };
            disconnectButton.Click += delegate { DisconnectDevice(); };
            clearLogButton.Click += delegate { logBox.Clear(); };
            installButton.Click += delegate { StartExecution(); };
            cancelButton.Click += delegate { RequestCancel(); };
            apkTextBox.TextChanged += delegate { UpdateApkInfo(apkTextBox.Text); SaveConfig(); };
            installModeRadioButton.CheckedChanged += delegate { UpdateExecutionOptionState(); };
            cleanInstallModeRadioButton.CheckedChanged += delegate { UpdateExecutionOptionState(); };
            uninstallModeRadioButton.CheckedChanged += delegate { UpdateExecutionOptionState(); };
            clearDataModeRadioButton.CheckedChanged += delegate { UpdateExecutionOptionState(); };
            startAppModeRadioButton.CheckedChanged += delegate { UpdateExecutionOptionState(); };
            deviceList.SelectedIndexChanged += delegate { BeginSyncAddressFromCurrentDevice(); };
            deviceList.ItemCheck += delegate { BeginSyncAddressFromCurrentDevice(); };
            deviceList.Click += delegate { BeginSyncAddressFromCurrentDevice(); };
            deviceList.MouseUp += delegate { BeginSyncAddressFromCurrentDevice(); };
            browseLogRecordFileButton.Click += delegate { BrowseLogRecordFile(); };
            browseLogRecordFolderButton.Click += delegate { BrowseLogRecordFolder(); };
            clearLogcatCacheButton.Click += delegate { ClearLogcatCache(); };
            exportLogcatCacheButton.Click += delegate { ExportLogcatCache(); };
            startLogRecordButton.Click += delegate { StartLogRecording(); };
            stopLogRecordButton.Click += delegate { StopLogcatRecording(); };
            logRecordPathTextBox.TextChanged += delegate { SaveConfig(); };
            logRecordTagTextBox.TextChanged += delegate { SaveConfig(); };
            logRecordPackageTextBox.TextChanged += delegate { SaveConfig(); };
            logRecordLevelComboBox.SelectedIndexChanged += delegate { SaveConfig(); };
            logRecordThreadInfoCheckBox.CheckedChanged += delegate { SaveConfig(); };
            logRecordTimeInfoCheckBox.CheckedChanged += delegate { SaveConfig(); };
            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            FormClosing += OnFormClosing;
        }

        private void InitLogcatDefaults()
        {
            logRecordPathTextBox.Text = logDir;
            logRecordLevelComboBox.SelectedIndex = 0;
            logRecordThreadInfoCheckBox.Checked = true;
            logRecordTimeInfoCheckBox.Checked = true;
        }

        private void BrowseLogRecordFile()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "选择日志输出文件";
                dialog.Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*";
                dialog.FileName = "log.txt";
                var currentDir = GetInitialDirectoryFromPath(logRecordPathTextBox.Text);
                if (!string.IsNullOrEmpty(currentDir)) dialog.InitialDirectory = currentDir;
                if (dialog.ShowDialog(this) == DialogResult.OK) logRecordPathTextBox.Text = dialog.FileName;
            }
        }

        private void BrowseLogRecordFolder()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择日志输出文件夹";
                var currentDir = GetInitialDirectoryFromPath(logRecordPathTextBox.Text);
                if (!string.IsNullOrEmpty(currentDir)) dialog.SelectedPath = currentDir;
                if (dialog.ShowDialog(this) == DialogResult.OK) logRecordPathTextBox.Text = dialog.SelectedPath;
            }
        }

        private void StartLogRecording()
        {
            if (isLogcatRunning) return;
            var device = GetSingleCheckedDeviceForLogRecording();
            if (device == null) return;
            var outputPath = PrepareLogRecordOutputPath();
            if (outputPath == null) return;
            List<string> tags;
            if (!TryGetLogRecordTags(out tags)) return;
            SaveConfig();
            var args = BuildSimpleLogRecordArgs(device.Serial, tags, GetSelectedLogRecordLevel());
            StartLogcatProcess(device, outputPath, args, false, "开始日志录制：", GetLogRecordPackageName(), logRecordThreadInfoCheckBox.Checked, logRecordTimeInfoCheckBox.Checked);
        }

        private void ClearLogcatCache()
        {
            if (isLogcatRunning || isExecuting || isDeviceCommandRunning) return;
            var device = GetSingleCheckedDeviceForLogRecording();
            if (device == null) return;
            RunDeviceCommand("清除日志缓存", new[] { "-s", device.Serial, "logcat", "-c" });
        }

        private void ExportLogcatCache()
        {
            if (isLogcatRunning || isExecuting || isDeviceCommandRunning) return;
            var device = GetSingleCheckedDeviceForLogRecording();
            if (device == null) return;
            var outputPath = PrepareLogRecordOutputPath();
            if (outputPath == null) return;
            List<string> tags;
            if (!TryGetLogRecordTags(out tags)) return;
            var adb = FindAdb();
            if (adb == null)
            {
                MessageBox.Show(this, "未找到 adb.exe。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            SaveConfig();
            var args = BuildLogcatCacheExportArgs(device.Serial, tags, GetSelectedLogRecordLevel());
            var packageName = GetLogRecordPackageName();
            var includeThreadInfo = logRecordThreadInfoCheckBox.Checked;
            var includeTimeInfo = logRecordTimeInfoCheckBox.Checked;
            cancelRequested = false;
            isDeviceCommandRunning = true;
            SetDeviceCommandUi(true);
            SetStatus("正在导出日志缓存...");
            AddLogLine("导出日志缓存：" + outputPath);
            var thread = new Thread(new ThreadStart(delegate
            {
                try
                {
                    var pidFilter = ResolveLogRecordPidFilter(adb, device.Serial, packageName);
                    var result = ExportLogcatCacheToFile(adb, args.ToArray(), outputPath, pidFilter, includeThreadInfo, includeTimeInfo);
                    if (result.Canceled)
                    {
                        AddLogLine("导出日志缓存已中止。");
                        SetStatus("导出日志缓存已中止。");
                    }
                    else if (result.ExitCode == 0)
                    {
                        var summary = "日志缓存已导出：" + outputPath;
                        AddLogLine(summary);
                        SetStatus(summary);
                    }
                    else
                    {
                        var error = FirstUsefulLine(result.Output) ?? "导出失败。";
                        AddLogLine("导出日志缓存失败：" + error);
                        SetStatus("导出日志缓存失败");
                    }
                }
                finally
                {
                    isDeviceCommandRunning = false;
                    cancelRequested = false;
                    BeginInvokeIfNeeded(delegate { SetDeviceCommandUi(false); });
                }
            }));
            thread.IsBackground = true;
            thread.Start();
        }

        private void StartLogcatProcess(DeviceInfo device, string outputPath, List<string> args, bool clearBefore, string startMessage, string packageName, bool includeThreadInfo, bool includeTimeInfo)
        {
            var adb = FindAdb();
            if (adb == null)
            {
                MessageBox.Show(this, "未找到 adb.exe。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (clearBefore)
            {
                InvokeProcess(adb, new[] { "-s", device.Serial, "logcat", "-c" }, false);
            }
            var pidFilter = ResolveLogRecordPidFilter(adb, device.Serial, packageName);
            isLogcatRunning = true;
            SetLogcatUi(true);
            SetStatus("正在录制日志...");
            AddLogLine(startMessage + outputPath);
            try
            {
                logcatWriter = new StreamWriter(outputPath, false, Encoding.UTF8);
                lock (logcatLock)
                {
                    logcatPidFilter = pidFilter;
                    logcatIncludeThreadInfo = includeThreadInfo;
                    logcatIncludeTimeInfo = includeTimeInfo;
                }
                var process = CreateAdbProcess(adb, args.ToArray());
                process.OutputDataReceived += WriteLogcatData;
                process.ErrorDataReceived += WriteLogcatData;
                process.EnableRaisingEvents = true;
                process.Exited += delegate { FinishLogcatRecording(); };
                logcatProcess = process;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                AddLogLine("启动 logcat 失败：" + ex.Message);
                FinishLogcatRecording();
            }
        }

        private void StopLogcatRecording()
        {
            Process process;
            lock (logcatLock) process = logcatProcess;
            TryKill(process);
            FinishLogcatRecording();
        }

        private void FinishLogcatRecording()
        {
            lock (logcatLock)
            {
                if (!isLogcatRunning && logcatProcess == null && logcatWriter == null) return;
                isLogcatRunning = false;
                try { if (logcatWriter != null) logcatWriter.Dispose(); } catch { }
                logcatWriter = null;
                logcatProcess = null;
                logcatPidFilter = null;
                logcatIncludeThreadInfo = true;
                logcatIncludeTimeInfo = true;
            }
            BeginInvokeIfNeeded(delegate
            {
                SetLogcatUi(false);
                SetStatus("日志录制已停止。");
                AddLogLine("logcat 录制已停止。");
            });
        }

        private void WriteLogcatData(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;
            lock (logcatLock)
            {
                try
                {
                    if (logcatWriter != null && ShouldWriteLogcatLine(e.Data))
                    {
                        logcatWriter.WriteLine(FormatLogcatLine(e.Data, logcatIncludeThreadInfo, logcatIncludeTimeInfo));
                        logcatWriter.Flush();
                    }
                }
                catch { }
            }
        }

        private string GetLogRecordPackageName()
        {
            return string.IsNullOrWhiteSpace(logRecordPackageTextBox.Text) ? "" : logRecordPackageTextBox.Text.Trim();
        }

        private HashSet<string> ResolveLogRecordPidFilter(string adb, string serial, string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName)) return null;
            packageName = packageName.Trim();

            var result = InvokeProcessSilent(adb, new[] { "-s", serial, "shell", "ps", "-A" });
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Output))
            {
                result = InvokeProcessSilent(adb, new[] { "-s", serial, "shell", "ps" });
            }
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Output))
            {
                AddLogLine("获取包名进程失败，按当前条件继续录制。");
                return null;
            }

            var pids = ParsePidsForPackage(result.Output, packageName);
            if (pids.Count == 0)
            {
                AddLogLine("未找到包名进程，按当前条件继续录制：" + packageName);
                return null;
            }
            AddLogLine("限定包名录制：" + packageName + "，pid=" + string.Join(",", pids.ToArray()));
            return pids;
        }

        private static HashSet<string> ParsePidsForPackage(string psOutput, string packageName)
        {
            var pids = new HashSet<string>();
            if (string.IsNullOrWhiteSpace(psOutput) || string.IsNullOrWhiteSpace(packageName)) return pids;

            var lines = psOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var pidIndex = 1;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                var parts = Regex.Split(trimmed, @"\s+");
                if (parts.Length < 2) continue;
                if (string.Equals(parts[0], "USER", StringComparison.OrdinalIgnoreCase))
                {
                    for (var i = 0; i < parts.Length; i++)
                    {
                        if (string.Equals(parts[i], "PID", StringComparison.OrdinalIgnoreCase))
                        {
                            pidIndex = i;
                            break;
                        }
                    }
                    continue;
                }
                if (pidIndex >= parts.Length || !Regex.IsMatch(parts[pidIndex], @"^\d+$")) continue;
                var processName = parts[parts.Length - 1];
                if (string.Equals(processName, packageName, StringComparison.Ordinal) ||
                    processName.StartsWith(packageName + ":", StringComparison.Ordinal))
                {
                    pids.Add(parts[pidIndex]);
                }
            }
            return pids;
        }

        private bool ShouldWriteLogcatLine(string line)
        {
            return ShouldWriteLogcatLine(line, logcatPidFilter);
        }

        private static bool ShouldWriteLogcatLine(string line, HashSet<string> pidFilter)
        {
            if (pidFilter == null || pidFilter.Count == 0) return true;
            var match = Regex.Match(line, @"^\S+\s+\S+\s+(?<pid>\d+)\s+");
            return match.Success && pidFilter.Contains(match.Groups["pid"].Value);
        }

        private static string FormatLogcatLine(string line, bool includeThreadInfo, bool includeTimeInfo)
        {
            if (includeThreadInfo && includeTimeInfo) return line;
            var match = Regex.Match(line, @"^(?<date>\d{2}-\d{2})\s+(?<time>\d{2}:\d{2}:\d{2}\.\d+)\s+(?<pid>\d+)\s+(?<tid>\d+)\s+(?<rest>[VDIWEFS]\s+.*)$");
            if (!match.Success) return line;

            var parts = new List<string>();
            if (includeTimeInfo) parts.Add(match.Groups["date"].Value + " " + match.Groups["time"].Value);
            if (includeThreadInfo) parts.Add(match.Groups["pid"].Value + " " + match.Groups["tid"].Value);
            parts.Add(match.Groups["rest"].Value);
            return string.Join(" ", parts.ToArray());
        }

        private void SetLogcatUi(bool running)
        {
            clearLogcatCacheButton.Enabled = !running && !isExecuting && !isDeviceCommandRunning;
            exportLogcatCacheButton.Enabled = !running && !isExecuting && !isDeviceCommandRunning;
            startLogRecordButton.Enabled = !running;
            stopLogRecordButton.Enabled = running;
            browseLogRecordFileButton.Enabled = !running;
            browseLogRecordFolderButton.Enabled = !running;
            logRecordPathTextBox.Enabled = !running;
            logRecordTagTextBox.Enabled = !running;
            logRecordPackageTextBox.Enabled = !running;
            logRecordLevelComboBox.Enabled = !running;
            logRecordThreadInfoCheckBox.Enabled = !running;
            logRecordTimeInfoCheckBox.Enabled = !running;
            refreshButton.Enabled = !running && !isExecuting && !isDeviceCommandRunning;
            toggleDevicesButton.Enabled = !running && !isExecuting && !isDeviceCommandRunning;
            deviceList.Enabled = !running && !isExecuting;
        }

        private string PrepareLogRecordOutputPath()
        {
            var path = ResolveLogRecordOutputPath(logRecordPathTextBox.Text);
            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show(this, "请输入日志输出路径。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                return path;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法创建日志输出目录：" + ex.Message, "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        private string ResolveOutputPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            path = path.Trim().Trim('"');
            return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(appDir, path));
        }

        private string ResolveLogRecordOutputPath(string path)
        {
            var resolved = ResolveOutputPath(path);
            if (string.IsNullOrWhiteSpace(resolved)) return null;
            if (Directory.Exists(resolved) || IsDirectoryLikePath(resolved)) return Path.Combine(resolved, GenerateLogRecordFileName());
            return resolved;
        }

        private static string GenerateLogRecordFileName()
        {
            return "log-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
        }

        private string GetInitialDirectoryFromPath(string path)
        {
            var resolved = ResolveOutputPath(path);
            if (string.IsNullOrWhiteSpace(resolved)) return Directory.Exists(logDir) ? logDir : appDir;
            if (Directory.Exists(resolved)) return resolved;
            var dir = Path.GetDirectoryName(resolved);
            return !string.IsNullOrEmpty(dir) && Directory.Exists(dir) ? dir : appDir;
        }

        private static bool IsDirectoryLikePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            var trimmed = path.Trim();
            if (trimmed.EndsWith(Path.DirectorySeparatorChar.ToString()) || trimmed.EndsWith(Path.AltDirectorySeparatorChar.ToString())) return true;
            return string.IsNullOrEmpty(Path.GetExtension(trimmed));
        }

        private List<string> BuildSimpleLogRecordArgs(string serial, List<string> tags, string level)
        {
            if (string.IsNullOrWhiteSpace(level)) level = "V";
            var args = new List<string> { "-s", serial, "logcat", "-v", "threadtime" };
            AddLogRecordFilters(args, tags, level);
            return args;
        }

        private List<string> BuildLogcatCacheExportArgs(string serial, List<string> tags, string level)
        {
            if (string.IsNullOrWhiteSpace(level)) level = "V";
            var args = new List<string> { "-s", serial, "logcat", "-d", "-v", "threadtime" };
            AddLogRecordFilters(args, tags, level);
            return args;
        }

        private static void AddLogRecordFilters(List<string> args, List<string> tags, string level)
        {
            if (tags != null && tags.Count > 0)
            {
                foreach (var tag in tags) args.Add(tag + ":" + level);
                args.Add("*:S");
            }
            else
            {
                args.Add("*:" + level);
            }
        }

        private string GetSelectedLogRecordLevel()
        {
            var selected = logRecordLevelComboBox.SelectedItem as string;
            if (string.IsNullOrEmpty(selected)) return "V";
            var match = Regex.Match(selected, @"\(([VDIWEF])\)");
            return match.Success ? match.Groups[1].Value : "V";
        }

        private void SetSelectedLogRecordLevel(string level)
        {
            if (string.IsNullOrWhiteSpace(level)) return;
            level = level.Trim().ToUpperInvariant();
            if (!Regex.IsMatch(level, "^[VDIWEF]$")) return;
            for (var i = 0; i < logRecordLevelComboBox.Items.Count; i++)
            {
                var item = logRecordLevelComboBox.Items[i] as string;
                if (item != null && Regex.IsMatch(item, @"\(" + Regex.Escape(level) + @"\)"))
                {
                    logRecordLevelComboBox.SelectedIndex = i;
                    return;
                }
            }
        }

        private bool TryGetLogRecordTags(out List<string> tags)
        {
            tags = new List<string>();
            var text = logRecordTagTextBox.Text;
            if (string.IsNullOrWhiteSpace(text)) return true;

            var parts = text.Split(new[] { ' ', '\t', '\r', '\n', ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var tag = part.Trim();
                if (tag.Length == 0) continue;
                if (tag.IndexOf(':') >= 0)
                {
                    MessageBox.Show(this, "Tag 不需要填写等级。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                if (!tags.Contains(tag)) tags.Add(tag);
            }
            return true;
        }

        private DeviceInfo GetSingleCheckedDeviceForLogRecording()
        {
            var checkedItems = deviceList.CheckedItems.Cast<object>().Select(o => o.ToString()).ToList();
            if (checkedItems.Count == 0)
            {
                MessageBox.Show(this, "请先在目标设备列表中选择一台设备。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            if (checkedItems.Count > 1)
            {
                MessageBox.Show(this, "只能同时录制一台设备。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            DeviceInfo device;
            if (!deviceMap.TryGetValue(checkedItems[0], out device) || device.State != "device")
            {
                MessageBox.Show(this, "请选择状态为 device 的设备。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            return device;
        }

        private Process CreateAdbProcess(string adb, string[] args)
        {
            var startInfo = new ProcessStartInfo();
            startInfo.FileName = adb;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            foreach (var arg in args) startInfo.Arguments += (startInfo.Arguments.Length == 0 ? "" : " ") + QuoteArgument(arg);
            AddLogLine("> " + QuoteArgument(adb) + " " + startInfo.Arguments);
            var process = new Process();
            process.StartInfo = startInfo;
            return process;
        }

        private void BeginSyncAddressFromCurrentDevice() { BeginInvokeIfNeeded(delegate { FillAddressFromCurrentDevice(); }); }

        private void BrowseApk()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "选择 APK 文件";
                dialog.Filter = "APK 文件 (*.apk)|*.apk|所有文件 (*.*)|*.*";
                dialog.Multiselect = false;
                if (!string.IsNullOrWhiteSpace(apkTextBox.Text) && File.Exists(apkTextBox.Text))
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(apkTextBox.Text);
                    dialog.FileName = Path.GetFileName(apkTextBox.Text);
                }
                if (dialog.ShowDialog(this) == DialogResult.OK) SetApkPath(dialog.FileName);
            }
        }

        private void ConnectDevice()
        {
            var address = NormalizeAdbAddress(connectAddressTextBox.Text.Trim());
            if (address == null)
            {
                MessageBox.Show(this, "请输入设备地址，例如 192.168.1.100:5555。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            RunDeviceCommand("连接设备", new[] { "connect", address });
        }

        private void DisconnectDevice()
        {
            var address = connectAddressTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(address) && deviceList.SelectedItem != null)
            {
                DeviceInfo device;
                if (deviceMap.TryGetValue(deviceList.SelectedItem.ToString(), out device)) address = device.Serial;
            }
            if (string.IsNullOrWhiteSpace(address))
            {
                MessageBox.Show(this, "请输入要断连的设备地址，或在目标设备列表中选中一个设备。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            RunDeviceCommand("断连设备", new[] { "disconnect", address });
        }

        private void RunDeviceCommand(string title, string[] adbArgs)
        {
            if (isExecuting || isDeviceCommandRunning) return;
            var adb = FindAdb();
            if (adb == null)
            {
                MessageBox.Show(this, "未找到 adb.exe。请安装 Android SDK Platform Tools，或把 adb.exe 加入 PATH。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            isDeviceCommandRunning = true;
            SetDeviceCommandUi(true);
            var thread = new Thread(new ThreadStart(delegate
            {
                try
                {
                    AddLogLine(title + "...");
                    var result = InvokeProcess(adb, adbArgs, true);
                    var summary = FirstUsefulLine(result.Output) ?? ("退出码：" + result.ExitCode);
                    AddLogLine(title + "结果：" + summary);
                    SetStatus(summary);
                }
                finally
                {
                    isDeviceCommandRunning = false;
                    BeginInvokeIfNeeded(delegate { SetDeviceCommandUi(false); RefreshDevices(); });
                }
            }));
            thread.IsBackground = true;
            thread.Start();
        }

        private void SetDeviceCommandUi(bool running)
        {
            connectButton.Enabled = !running && !isExecuting;
            disconnectButton.Enabled = !running && !isExecuting;
            refreshButton.Enabled = !running && !isExecuting && !isLogcatRunning;
            toggleDevicesButton.Enabled = !running && !isExecuting && !isLogcatRunning;
            connectAddressTextBox.Enabled = !running && !isExecuting;
            cancelButton.Enabled = running || isExecuting;
            clearLogcatCacheButton.Enabled = !running && !isExecuting && !isLogcatRunning;
            exportLogcatCacheButton.Enabled = !running && !isExecuting && !isLogcatRunning;
            startLogRecordButton.Enabled = !running && !isExecuting && !isLogcatRunning;
            browseLogRecordFileButton.Enabled = !running && !isExecuting && !isLogcatRunning;
            browseLogRecordFolderButton.Enabled = !running && !isExecuting && !isLogcatRunning;
            logRecordPathTextBox.Enabled = !running && !isExecuting && !isLogcatRunning;
            logRecordTagTextBox.Enabled = !running && !isExecuting && !isLogcatRunning;
            logRecordPackageTextBox.Enabled = !running && !isExecuting && !isLogcatRunning;
            logRecordLevelComboBox.Enabled = !running && !isExecuting && !isLogcatRunning;
            logRecordThreadInfoCheckBox.Enabled = !running && !isExecuting && !isLogcatRunning;
            logRecordTimeInfoCheckBox.Enabled = !running && !isExecuting && !isLogcatRunning;
            deviceList.Enabled = !running && !isExecuting && !isLogcatRunning;
            if (running) statusLabel.Text = "正在执行设备连接操作...";
        }

        private static string NormalizeAdbAddress(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            input = input.Trim();
            if (input.StartsWith("adb connect ", StringComparison.OrdinalIgnoreCase)) input = input.Substring("adb connect ".Length).Trim();
            if (input.StartsWith("adb disconnect ", StringComparison.OrdinalIgnoreCase)) input = input.Substring("adb disconnect ".Length).Trim();
            if (!input.Contains(":")) input += ":5555";
            return Regex.IsMatch(input, @"^[A-Za-z0-9_.-]+:\d{1,5}$") ? input : null;
        }

        private void FillAddressFromCurrentDevice()
        {
            var label = deviceList.SelectedItem as string;
            if (label == null && deviceList.CheckedItems.Count > 0) label = deviceList.CheckedItems[deviceList.CheckedItems.Count - 1].ToString();
            if (label == null) return;
            DeviceInfo device;
            if (deviceMap.TryGetValue(label, out device)) connectAddressTextBox.Text = device.Serial;
        }

        private void OnDragEnter(object sender, DragEventArgs e) { e.Effect = GetDroppedApkPath(e.Data) == null ? DragDropEffects.None : DragDropEffects.Copy; }
        private void OnDragDrop(object sender, DragEventArgs e) { var path = GetDroppedApkPath(e.Data); if (path != null) SetApkPath(path); }
        private string GetDroppedApkPath(IDataObject data)
        {
            if (!data.GetDataPresent(DataFormats.FileDrop)) return null;
            var files = data.GetData(DataFormats.FileDrop) as string[];
            if (files == null) return null;
            return files.FirstOrDefault(f => File.Exists(f) && string.Equals(Path.GetExtension(f), ".apk", StringComparison.OrdinalIgnoreCase));
        }

        private void SetApkPath(string path) { apkTextBox.Text = path; SaveConfig(path); }

        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(configPath)) return;
                var json = File.ReadAllText(configPath, Encoding.UTF8);
                loadingConfig = true;

                var lastApkPath = ReadJsonString(json, "lastApkPath");
                if (!string.IsNullOrEmpty(lastApkPath) && File.Exists(lastApkPath)) apkTextBox.Text = lastApkPath;

                var logRecordOutputPath = ReadJsonString(json, "logRecordOutputPath");
                if (!string.IsNullOrWhiteSpace(logRecordOutputPath)) logRecordPathTextBox.Text = logRecordOutputPath;

                var logRecordFilterTags = ReadJsonString(json, "logRecordFilterTags");
                if (logRecordFilterTags != null) logRecordTagTextBox.Text = logRecordFilterTags;

                var logRecordPackageName = ReadJsonString(json, "logRecordPackageName");
                if (logRecordPackageName != null) logRecordPackageTextBox.Text = logRecordPackageName;

                var logRecordLevel = ReadJsonString(json, "logRecordLevel");
                if (!string.IsNullOrWhiteSpace(logRecordLevel)) SetSelectedLogRecordLevel(logRecordLevel);

                var includeThreadInfo = ReadJsonBool(json, "logRecordIncludeThreadInfo");
                if (includeThreadInfo.HasValue) logRecordThreadInfoCheckBox.Checked = includeThreadInfo.Value;

                var includeTimeInfo = ReadJsonBool(json, "logRecordIncludeTimeInfo");
                if (includeTimeInfo.HasValue) logRecordTimeInfoCheckBox.Checked = includeTimeInfo.Value;
            }
            catch { AddLogLine("Read config failed, ignored."); }
            finally { loadingConfig = false; }
        }

        private void SaveLastApkPath(string path)
        {
            SaveConfig(path);
        }

        private void SaveConfig(string lastApkPathOverride = null)
        {
            try
            {
                if (!configReady || loadingConfig) return;
                var lastApkPath = lastApkPathOverride ?? apkTextBox.Text;
                var json =
                    "{\r\n" +
                    "    \"lastApkPath\":  \"" + EscapeJsonString(lastApkPath) + "\",\r\n" +
                    "    \"logRecordOutputPath\":  \"" + EscapeJsonString(logRecordPathTextBox.Text) + "\",\r\n" +
                    "    \"logRecordFilterTags\":  \"" + EscapeJsonString(logRecordTagTextBox.Text) + "\",\r\n" +
                    "    \"logRecordPackageName\":  \"" + EscapeJsonString(logRecordPackageTextBox.Text) + "\",\r\n" +
                    "    \"logRecordLevel\":  \"" + EscapeJsonString(GetSelectedLogRecordLevel()) + "\",\r\n" +
                    "    \"logRecordIncludeThreadInfo\":  " + (logRecordThreadInfoCheckBox.Checked ? "true" : "false") + ",\r\n" +
                    "    \"logRecordIncludeTimeInfo\":  " + (logRecordTimeInfoCheckBox.Checked ? "true" : "false") + ",\r\n" +
                    "    \"updatedAt\":  \"" + DateTime.Now.ToString("s") + "\"\r\n" +
                    "}\r\n";
                File.WriteAllText(configPath, json, Encoding.UTF8);
            }
            catch (Exception ex) { AddLogLine("Save config failed: " + ex.Message); }
        }

        private static string ReadJsonString(string json, string name)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(name)) return null;
            var pattern = "\"" + Regex.Escape(name) + "\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"])*)\"";
            var match = Regex.Match(json, pattern);
            return match.Success ? Regex.Unescape(match.Groups["value"].Value) : null;
        }

        private static bool? ReadJsonBool(string json, string name)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(name)) return null;
            var pattern = "\"" + Regex.Escape(name) + "\"\\s*:\\s*(?<value>true|false)";
            var match = Regex.Match(json, pattern, RegexOptions.IgnoreCase);
            if (!match.Success) return null;
            return string.Equals(match.Groups["value"].Value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static string EscapeJsonString(string value)
        {
            if (value == null) return "";
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        private void UpdateApkInfo(string apkPath)
        {
            if (string.IsNullOrWhiteSpace(apkPath)) { currentApkInfo = null; apkInfoLabel.Text = "请选择 APK 文件。"; return; }
            if (!File.Exists(apkPath)) { currentApkInfo = null; apkInfoLabel.Text = "APK 文件不存在。"; return; }
            if (!string.Equals(Path.GetExtension(apkPath), ".apk", StringComparison.OrdinalIgnoreCase)) { currentApkInfo = null; apkInfoLabel.Text = "请选择 .apk 文件。"; return; }
            var info = GetApkInfo(apkPath);
            currentApkInfo = info;
            if (!string.IsNullOrEmpty(info.ParseError)) { apkInfoLabel.Text = "APK 已选择，但解析信息失败：" + info.ParseError; return; }
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(info.Label)) parts.Add("应用：" + info.Label);
            if (!string.IsNullOrEmpty(info.PackageName)) parts.Add("包名：" + info.PackageName);
            if (!string.IsNullOrEmpty(info.VersionName)) parts.Add("版本：" + info.VersionName);
            if (!string.IsNullOrEmpty(info.VersionCode)) parts.Add("versionCode：" + info.VersionCode);
            if (info.IsTestOnly) parts.Add("testOnly");
            apkInfoLabel.Text = parts.Count == 0 ? "APK 已选择。" : string.Join(" | ", parts.ToArray());
        }

        private ApkInfo GetApkInfo(string apkPath)
        {
            var info = new ApkInfo();
            var aapt = FindAapt();
            if (aapt == null) { info.ParseError = "未找到 aapt，无法读取 APK 详情。"; return info; }
            var result = InvokeProcess(aapt, new[] { "dump", "badging", apkPath }, false);
            if (result.ExitCode != 0) { info.ParseError = FirstUsefulLine(result.Output) ?? "aapt 执行失败。"; return info; }
            var packageMatch = Regex.Match(result.Output, @"package: name='(?<name>[^']+)'\s+versionCode='(?<code>[^']*)'\s+versionName='(?<version>[^']*)'");
            if (packageMatch.Success)
            {
                info.PackageName = packageMatch.Groups["name"].Value;
                info.VersionCode = packageMatch.Groups["code"].Value;
                info.VersionName = packageMatch.Groups["version"].Value;
            }
            var labelMatch = Regex.Match(result.Output, @"application-label(?:-[^:]+)?:'(?<label>[^']*)'");
            if (labelMatch.Success) info.Label = labelMatch.Groups["label"].Value;
            var activityMatch = Regex.Match(result.Output, @"launchable-activity: name='(?<activity>[^']+)'");
            if (activityMatch.Success) info.LaunchActivity = activityMatch.Groups["activity"].Value;
            info.IsTestOnly = result.Output.IndexOf("testOnly", StringComparison.OrdinalIgnoreCase) >= 0;
            return info;
        }

        private void RefreshDevices()
        {
            if (isExecuting || isDeviceCommandRunning) return;
            deviceList.Items.Clear();
            deviceMap.Clear();
            var adb = FindAdb();
            if (adb == null) { AddLogLine("未找到 adb。请安装 Android SDK Platform Tools，或把 adb.exe 加入 PATH。"); statusLabel.Text = "未找到 adb"; return; }
            AddLogLine("刷新设备...");
            var devices = GetConnectedDevices(adb);
            foreach (var device in devices)
            {
                deviceMap[device.Label] = device;
                deviceList.Items.Add(device.Label, device.State == "device");
            }
            statusLabel.Text = "检测到 " + devices.Count + " 台设备，可用 " + devices.Count(d => d.State == "device") + " 台。";
        }

        private List<DeviceInfo> GetConnectedDevices(string adb)
        {
            var devices = new List<DeviceInfo>();
            var result = InvokeProcess(adb, new[] { "devices", "-l" }, false);
            foreach (var rawLine in SplitLines(result.Output))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("List of devices attached")) continue;
                var match = Regex.Match(line, @"^(?<serial>\S+)\s+(?<state>\S+)(?<detail>.*)$");
                if (!match.Success) continue;
                var serial = match.Groups["serial"].Value;
                var state = match.Groups["state"].Value;
                var detail = match.Groups["detail"].Value.Trim();
                var model = GetDetailValue(detail, "model");
                var product = GetDetailValue(detail, "product");
                var deviceName = GetDetailValue(detail, "device");
                var manufacturer = "";
                var androidVersion = "";
                if (state == "device")
                {
                    manufacturer = FirstUsefulLine(InvokeProcess(adb, new[] { "-s", serial, "shell", "getprop", "ro.product.manufacturer" }, false).Output) ?? "";
                    androidVersion = FirstUsefulLine(InvokeProcess(adb, new[] { "-s", serial, "shell", "getprop", "ro.build.version.release" }, false).Output) ?? "";
                }
                var nameParts = new[] { manufacturer, model }.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                var displayName = nameParts.Length > 0 ? string.Join(" ", nameParts) : serial;
                var detailParts = new List<string>();
                if (!string.IsNullOrEmpty(androidVersion)) detailParts.Add("Android " + androidVersion);
                if (!string.IsNullOrEmpty(deviceName)) detailParts.Add("device:" + deviceName);
                if (!string.IsNullOrEmpty(product)) detailParts.Add("product:" + product);
                var label = displayName + " | " + serial + " | " + state + (detailParts.Count > 0 ? " | " + string.Join(" | ", detailParts.ToArray()) : "");
                devices.Add(new DeviceInfo { Serial = serial, State = state, Label = label });
            }
            return devices;
        }

        private static string GetDetailValue(string detail, string key)
        {
            var match = Regex.Match(detail, @"(?:^|\s)" + Regex.Escape(key) + @":(?<value>\S+)");
            return match.Success ? match.Groups["value"].Value : "";
        }

        private void ToggleDevices()
        {
            var allChecked = deviceList.Items.Count > 0;
            for (var i = 0; i < deviceList.Items.Count; i++) if (!deviceList.GetItemChecked(i)) { allChecked = false; break; }
            for (var i = 0; i < deviceList.Items.Count; i++) deviceList.SetItemChecked(i, !allChecked);
            BeginSyncAddressFromCurrentDevice();
        }

        private void UpdateExecutionOptionState()
        {
            var isInstallOperation = !uninstallModeRadioButton.Checked && !clearDataModeRadioButton.Checked && !startAppModeRadioButton.Checked;
            launchAfterInstallCheckBox.Enabled = isInstallOperation;
            if (!isInstallOperation) launchAfterInstallCheckBox.Checked = false;
        }

        private string GetExecutionMode()
        {
            if (cleanInstallModeRadioButton.Checked) return "CleanInstall";
            if (uninstallModeRadioButton.Checked) return "Uninstall";
            if (clearDataModeRadioButton.Checked) return "ClearData";
            if (startAppModeRadioButton.Checked) return "StartApp";
            return "Install";
        }

        private void StartExecution()
        {
            if (isExecuting || isDeviceCommandRunning) return;
            var apkPath = apkTextBox.Text.Trim();
            if (!File.Exists(apkPath) || !string.Equals(Path.GetExtension(apkPath), ".apk", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "请选择有效的 APK 文件。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var checkedItems = deviceList.CheckedItems.Cast<object>().Select(o => o.ToString()).ToList();
            if (checkedItems.Count == 0)
            {
                MessageBox.Show(this, "请至少选择一台设备。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var adb = FindAdb();
            if (adb == null)
            {
                MessageBox.Show(this, "未找到 adb.exe。请安装 Android SDK Platform Tools，或把 adb.exe 加入 PATH。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var mode = GetExecutionMode();
            var launchAfterInstall = launchAfterInstallCheckBox.Checked;
            var apkInfo = currentApkInfo ?? GetApkInfo(apkPath);
            if ((mode != "Install" || launchAfterInstall) && string.IsNullOrEmpty(apkInfo.PackageName))
            {
                MessageBox.Show(this, "当前操作需要 APK 包名，但解析 APK 信息失败。请确认 Android SDK Build Tools 中存在 aapt.exe。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            SaveLastApkPath(apkPath);
            cancelRequested = false;
            isExecuting = true;
            SetExecutingUi(true);
            var thread = new Thread(new ThreadStart(delegate { ExecuteOnDevices(adb, apkPath, apkInfo, checkedItems, mode, launchAfterInstall); }));
            thread.IsBackground = true;
            thread.Start();
        }

        private void ExecuteOnDevices(string adb, string apkPath, ApkInfo apkInfo, List<string> checkedItems, string mode, bool launchAfterInstall)
        {
            var successCount = 0;
            var failedCount = 0;
            var skippedCount = 0;
            try
            {
                AddLogLine("开始执行，APK：" + apkPath);
                for (var index = 0; index < checkedItems.Count; index++)
                {
                    if (cancelRequested) { AddLogLine("用户已中止，停止后续设备操作。"); break; }
                    var label = checkedItems[index];
                    DeviceInfo device;
                    if (!deviceMap.TryGetValue(label, out device)) { AddLogLine("跳过未知设备：" + label); skippedCount++; continue; }
                    AddLogLine("[" + (index + 1) + "/" + checkedItems.Count + "] 处理设备：" + label);
                    if (device.State != "device") { AddLogLine("设备不可用，状态为 " + device.State + "。请检查 USB 调试授权。"); skippedCount++; continue; }
                    var ok = ExecuteForDevice(adb, apkPath, apkInfo, device.Serial, mode, launchAfterInstall);
                    if (cancelRequested) break;
                    if (ok) successCount++; else failedCount++;
                }
                var summary = cancelRequested ? "已中止：成功 " + successCount + "，失败 " + failedCount + "，跳过 " + skippedCount : "执行完成：成功 " + successCount + "，失败 " + failedCount + "，跳过 " + skippedCount;
                AddLogLine(summary);
                SetStatus(summary);
                SaveRunLog();
            }
            catch (Exception ex) { AddLogLine("执行异常：" + ex.Message); SetStatus("执行异常"); }
            finally
            {
                isExecuting = false;
                cancelRequested = false;
                ClearCurrentProcess();
                BeginInvokeIfNeeded(delegate { SetExecutingUi(false); });
            }
        }

        private bool ExecuteForDevice(string adb, string apkPath, ApkInfo apkInfo, string serial, string mode, bool launchAfterInstall)
        {
            if (mode == "StartApp")
            {
                AddLogLine("启动应用：" + apkInfo.PackageName);
                if (StartAppOnDevice(adb, serial, apkInfo)) { AddLogLine("启动应用成功。"); return true; }
                if (!cancelRequested) AddLogLine("启动应用失败。");
                return false;
            }
            if (mode == "ClearData")
            {
                AddLogLine("清空应用数据：" + apkInfo.PackageName);
                var clearResult = InvokeProcess(adb, new[] { "-s", serial, "shell", "pm", "clear", apkInfo.PackageName }, true);
                if (cancelRequested) return false;
                if (clearResult.ExitCode == 0 && clearResult.Output.IndexOf("Success", StringComparison.OrdinalIgnoreCase) >= 0) { AddLogLine("清空数据成功。"); return true; }
                AddLogLine("清空数据失败：" + HumanizeAdbOutput(clearResult.Output));
                return false;
            }
            if (mode == "CleanInstall")
            {
                AddLogLine("卸载旧版本：" + apkInfo.PackageName);
                var uninstallResult = InvokeProcess(adb, new[] { "-s", serial, "uninstall", apkInfo.PackageName }, true);
                if (cancelRequested) return false;
                if (uninstallResult.ExitCode != 0) AddLogLine("卸载返回：" + HumanizeAdbOutput(uninstallResult.Output));
            }
            if (mode == "Uninstall")
            {
                AddLogLine("卸载应用：" + apkInfo.PackageName);
                var uninstallResult = InvokeProcess(adb, new[] { "-s", serial, "uninstall", apkInfo.PackageName }, true);
                if (cancelRequested) return false;
                if (uninstallResult.ExitCode == 0 && uninstallResult.Output.IndexOf("Success", StringComparison.OrdinalIgnoreCase) >= 0) { AddLogLine("卸载成功。"); return true; }
                AddLogLine("卸载失败：" + HumanizeAdbOutput(uninstallResult.Output));
                return false;
            }
            AddLogLine("安装 APK...");
            var installResult = InvokeProcess(adb, new[] { "-s", serial, "install", "-r", "-d", "-t", apkPath }, true);
            if (cancelRequested) return false;
            if (installResult.ExitCode != 0 || installResult.Output.IndexOf("Success", StringComparison.OrdinalIgnoreCase) < 0)
            {
                AddLogLine("安装失败：" + HumanizeAdbOutput(installResult.Output));
                return false;
            }
            AddLogLine("安装成功。");
            if (launchAfterInstall)
            {
                if (StartAppOnDevice(adb, serial, apkInfo)) AddLogLine("启动应用成功。");
                else if (!cancelRequested) AddLogLine("启动应用失败。");
            }
            return true;
        }

        private bool StartAppOnDevice(string adb, string serial, ApkInfo apkInfo)
        {
            if (!string.IsNullOrEmpty(apkInfo.LaunchActivity))
            {
                var component = apkInfo.PackageName + "/" + apkInfo.LaunchActivity;
                var startResult = InvokeProcess(adb, new[] { "-s", serial, "shell", "am", "start", "-n", component }, true);
                if (cancelRequested) return false;
                if (startResult.ExitCode == 0) return true;
                AddLogLine("按 Activity 启动失败，尝试 monkey。");
            }
            var monkeyResult = InvokeProcess(adb, new[] { "-s", serial, "shell", "monkey", "-p", apkInfo.PackageName, "-c", "android.intent.category.LAUNCHER", "1" }, true);
            return !cancelRequested && monkeyResult.ExitCode == 0;
        }

        private void RequestCancel()
        {
            if (!isExecuting && !isDeviceCommandRunning)
            {
                if (isLogcatRunning) StopLogcatRecording();
                else Close();
                return;
            }
            cancelRequested = true;
            cancelButton.Enabled = false;
            statusLabel.Text = "正在中止...";
            AddLogLine("收到中止请求，正在结束当前 adb/aapt 进程...");
            KillCurrentProcess();
        }

        private ProcessResult InvokeProcess(string filePath, string[] arguments, bool cancellable)
        {
            var outputBuilder = new StringBuilder();
            var process = CreateAdbProcess(filePath, arguments);
            process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) { lock (outputBuilder) outputBuilder.AppendLine(e.Data); AddLogLine(e.Data); } };
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) { lock (outputBuilder) outputBuilder.AppendLine(e.Data); AddLogLine(e.Data); } };
            try
            {
                process.Start();
                if (cancellable) SetCurrentProcess(process);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                while (!process.WaitForExit(150)) if (cancellable && cancelRequested) { TryKill(process); break; }
                try { process.WaitForExit(); } catch { }
                return new ProcessResult { ExitCode = cancelRequested && cancellable ? 130 : process.ExitCode, Output = outputBuilder.ToString(), Canceled = cancelRequested && cancellable };
            }
            catch (Exception ex) { return new ProcessResult { ExitCode = 1, Output = ex.Message, Canceled = cancelRequested && cancellable }; }
            finally { if (cancellable) ClearCurrentProcess(process); try { process.Dispose(); } catch { } }
        }

        private ProcessResult InvokeProcessSilent(string filePath, string[] arguments)
        {
            var outputBuilder = new StringBuilder();
            var startInfo = new ProcessStartInfo();
            startInfo.FileName = filePath;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            foreach (var arg in arguments) startInfo.Arguments += (startInfo.Arguments.Length == 0 ? "" : " ") + QuoteArgument(arg);
            var process = new Process();
            process.StartInfo = startInfo;
            process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) lock (outputBuilder) outputBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) lock (outputBuilder) outputBuilder.AppendLine(e.Data); };
            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                return new ProcessResult { ExitCode = process.ExitCode, Output = outputBuilder.ToString(), Canceled = false };
            }
            catch (Exception ex) { return new ProcessResult { ExitCode = 1, Output = ex.Message, Canceled = false }; }
            finally { try { process.Dispose(); } catch { } }
        }

        private ProcessResult ExportLogcatCacheToFile(string filePath, string[] arguments, string outputPath, HashSet<string> pidFilter, bool includeThreadInfo, bool includeTimeInfo)
        {
            var outputBuilder = new StringBuilder();
            var process = CreateAdbProcess(filePath, arguments);
            try
            {
                using (var writer = new StreamWriter(outputPath, false, Encoding.UTF8))
                {
                    process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                    {
                        if (e.Data == null) return;
                        if (ShouldWriteLogcatLine(e.Data, pidFilter))
                        {
                            lock (writer) writer.WriteLine(FormatLogcatLine(e.Data, includeThreadInfo, includeTimeInfo));
                        }
                    };
                    process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                    {
                        if (e.Data != null) lock (outputBuilder) outputBuilder.AppendLine(e.Data);
                    };
                    process.Start();
                    SetCurrentProcess(process);
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    while (!process.WaitForExit(150)) if (cancelRequested) { TryKill(process); break; }
                    try { process.WaitForExit(); } catch { }
                    writer.Flush();
                    return new ProcessResult { ExitCode = cancelRequested ? 130 : process.ExitCode, Output = outputBuilder.ToString(), Canceled = cancelRequested };
                }
            }
            catch (Exception ex) { return new ProcessResult { ExitCode = 1, Output = ex.Message, Canceled = cancelRequested }; }
            finally { ClearCurrentProcess(process); try { process.Dispose(); } catch { } }
        }

        private void SetCurrentProcess(Process process) { lock (processLock) currentProcess = process; }
        private void ClearCurrentProcess(Process process) { lock (processLock) if (ReferenceEquals(currentProcess, process)) currentProcess = null; }
        private void ClearCurrentProcess() { lock (processLock) currentProcess = null; }
        private void KillCurrentProcess() { Process process; lock (processLock) process = currentProcess; if (process != null) TryKill(process); }
        private static void TryKill(Process process) { try { if (process != null && !process.HasExited) process.Kill(); } catch { } }

        private void SetExecutingUi(bool executing)
        {
            browseButton.Enabled = !executing;
            refreshButton.Enabled = !executing && !isDeviceCommandRunning && !isLogcatRunning;
            toggleDevicesButton.Enabled = !executing && !isDeviceCommandRunning && !isLogcatRunning;
            connectButton.Enabled = !executing && !isDeviceCommandRunning;
            disconnectButton.Enabled = !executing && !isDeviceCommandRunning;
            connectAddressTextBox.Enabled = !executing && !isDeviceCommandRunning;
            installButton.Enabled = !executing;
            clearLogButton.Enabled = !executing;
            installModeRadioButton.Enabled = !executing;
            cleanInstallModeRadioButton.Enabled = !executing;
            uninstallModeRadioButton.Enabled = !executing;
            clearDataModeRadioButton.Enabled = !executing;
            startAppModeRadioButton.Enabled = !executing;
            apkTextBox.Enabled = !executing;
            deviceList.Enabled = !executing && !isLogcatRunning;
            launchAfterInstallCheckBox.Enabled = !executing && !uninstallModeRadioButton.Checked && !clearDataModeRadioButton.Checked && !startAppModeRadioButton.Checked;
            clearLogcatCacheButton.Enabled = !executing && !isLogcatRunning && !isDeviceCommandRunning;
            exportLogcatCacheButton.Enabled = !executing && !isLogcatRunning && !isDeviceCommandRunning;
            startLogRecordButton.Enabled = !executing && !isLogcatRunning;
            stopLogRecordButton.Enabled = isLogcatRunning;
            logRecordPathTextBox.Enabled = !executing && !isLogcatRunning;
            logRecordTagTextBox.Enabled = !executing && !isLogcatRunning;
            logRecordPackageTextBox.Enabled = !executing && !isLogcatRunning;
            logRecordLevelComboBox.Enabled = !executing && !isLogcatRunning;
            logRecordThreadInfoCheckBox.Enabled = !executing && !isLogcatRunning;
            logRecordTimeInfoCheckBox.Enabled = !executing && !isLogcatRunning;
            browseLogRecordFileButton.Enabled = !executing && !isLogcatRunning;
            browseLogRecordFolderButton.Enabled = !executing && !isLogcatRunning;
            cancelButton.Enabled = executing || isDeviceCommandRunning;
            Cursor = executing ? Cursors.WaitCursor : Cursors.Default;
            statusLabel.Text = executing ? "正在执行..." : statusLabel.Text;
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (isLogcatRunning) StopLogcatRecording();
            if (isExecuting || isDeviceCommandRunning) { cancelRequested = true; KillCurrentProcess(); }
        }

        private void AddLogLine(string text) { BeginInvokeIfNeeded(delegate { logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text + Environment.NewLine); }); }
        private void SetStatus(string text) { BeginInvokeIfNeeded(delegate { statusLabel.Text = text; }); }
        private void BeginInvokeIfNeeded(Action action)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke(action); } catch { } }
            else action();
        }

        private void SaveRunLog()
        {
            try
            {
                var path = Path.Combine(logDir, "apk-install-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
                string text = "";
                BeginInvokeIfNeeded(delegate { text = logBox.Text; });
                Thread.Sleep(50);
                File.WriteAllText(path, text, Encoding.UTF8);
            }
            catch { }
        }

        private string FindAdb()
        {
            var candidates = new List<string>();
            candidates.Add("adb.exe");
            var androidHome = Environment.GetEnvironmentVariable("ANDROID_HOME");
            var androidSdkRoot = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(androidHome)) candidates.Add(Path.Combine(androidHome, "platform-tools", "adb.exe"));
            if (!string.IsNullOrEmpty(androidSdkRoot)) candidates.Add(Path.Combine(androidSdkRoot, "platform-tools", "adb.exe"));
            if (!string.IsNullOrEmpty(localAppData)) candidates.Add(Path.Combine(localAppData, "Android", "Sdk", "platform-tools", "adb.exe"));
            return FindCommand(candidates);
        }

        private string FindAapt()
        {
            var candidates = new List<string>();
            candidates.Add("aapt.exe");
            var androidHome = Environment.GetEnvironmentVariable("ANDROID_HOME");
            var androidSdkRoot = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var sdkRoots = new[] { androidHome, androidSdkRoot, string.IsNullOrEmpty(localAppData) ? null : Path.Combine(localAppData, "Android", "Sdk") }.Where(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var root in sdkRoots)
            {
                var buildTools = Path.Combine(root, "build-tools");
                if (!Directory.Exists(buildTools)) continue;
                try
                {
                    var aapt = Directory.GetFiles(buildTools, "aapt.exe", SearchOption.AllDirectories).OrderByDescending(p => p).FirstOrDefault();
                    if (aapt != null) candidates.Add(aapt);
                }
                catch { }
            }
            return FindCommand(candidates);
        }

        private static string FindCommand(IEnumerable<string> candidates)
        {
            foreach (var candidate in candidates.Where(c => !string.IsNullOrWhiteSpace(c)))
            {
                if (candidate.IndexOf(Path.DirectorySeparatorChar) >= 0 || candidate.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
                {
                    if (File.Exists(candidate)) return candidate;
                }
                else
                {
                    var path = Environment.GetEnvironmentVariable("PATH") ?? "";
                    foreach (var dir in path.Split(Path.PathSeparator))
                    {
                        try { var full = Path.Combine(dir.Trim(), candidate); if (File.Exists(full)) return full; } catch { }
                    }
                }
            }
            return null;
        }

        private static string QuoteArgument(string argument)
        {
            if (argument == null || argument.Length == 0) return "\"\"";
            if (argument.IndexOfAny(new[] { ' ', '\t', '"', '&', '(', ')', '[', ']', '{', '}', '^', ';' }) < 0) return argument;
            return "\"" + argument.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static IEnumerable<string> SplitLines(string text) { return (text ?? "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None); }
        private static string FirstUsefulLine(string text) { return SplitLines(text).Select(s => s.Trim()).FirstOrDefault(s => s.Length > 0); }
        private static string HumanizeAdbOutput(string output)
        {
            var text = output ?? "";
            if (text.IndexOf("INSTALL_FAILED_VERSION_DOWNGRADE", StringComparison.OrdinalIgnoreCase) >= 0) return "安装失败：手机上已有更高版本，请卸载旧应用或提高 versionCode。";
            if (text.IndexOf("INSTALL_FAILED_UPDATE_INCOMPATIBLE", StringComparison.OrdinalIgnoreCase) >= 0) return "安装失败：签名不一致，请卸载旧版本后再安装。";
            if (text.IndexOf("INSTALL_FAILED_TEST_ONLY", StringComparison.OrdinalIgnoreCase) >= 0) return "安装失败：这是 testOnly APK，需要允许测试包安装。";
            if (text.IndexOf("device unauthorized", StringComparison.OrdinalIgnoreCase) >= 0) return "设备未授权：请在手机上确认 USB 调试授权。";
            if (text.IndexOf("more than one device", StringComparison.OrdinalIgnoreCase) >= 0) return "存在多台设备：请选择指定设备后重试。";
            if (text.IndexOf("no devices", StringComparison.OrdinalIgnoreCase) >= 0) return "未检测到设备：请检查数据线、USB 调试和 adb 连接。";
            if (text.IndexOf("Unknown package", StringComparison.OrdinalIgnoreCase) >= 0) return "清空数据失败：设备上没有安装这个包名。";
            return FirstUsefulLine(text) ?? "未知错误。";
        }
    }

    internal sealed class DeviceInfo
    {
        public string Serial;
        public string State;
        public string Label;
    }

    internal sealed class ApkInfo
    {
        public string Label = "";
        public string PackageName = "";
        public string VersionCode = "";
        public string VersionName = "";
        public string LaunchActivity = "";
        public bool IsTestOnly;
        public string ParseError = "";
    }

    internal sealed class ProcessResult
    {
        public int ExitCode;
        public string Output = "";
        public bool Canceled;
    }
}
