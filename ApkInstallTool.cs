using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        private enum TransferDirection
        {
            ToDevice,
            ToComputer
        }

        private enum CaptureMediaType
        {
            None,
            Screenshot,
            ScreenRecord
        }

        private sealed class ScreenRecordOptions
        {
            public bool HasTimeLimit;
            public int TimeLimitSeconds;
            public int BitRate;
        }

        private sealed class DisplayControlInfo
        {
            public int PhysicalWidth;
            public int PhysicalHeight;
            public int OverrideWidth;
            public int OverrideHeight;
            public int PhysicalDensity;
            public int OverrideDensity;
            public bool HasPhysicalSize;
            public bool HasOverrideSize;
            public bool HasPhysicalDensity;
            public bool HasOverrideDensity;

            public bool HasCurrentSize
            {
                get { return HasOverrideSize || HasPhysicalSize; }
            }

            public bool HasCurrentDensity
            {
                get { return HasOverrideDensity || HasPhysicalDensity; }
            }

            public int CurrentWidth
            {
                get { return HasOverrideSize ? OverrideWidth : PhysicalWidth; }
            }

            public int CurrentHeight
            {
                get { return HasOverrideSize ? OverrideHeight : PhysicalHeight; }
            }

            public int CurrentDensity
            {
                get { return HasOverrideDensity ? OverrideDensity : PhysicalDensity; }
            }
        }

        private readonly TabControl tabControl = new TabControl();
        private readonly SplitContainer mainSplitContainer = new SplitContainer();
        private readonly SplitContainer lowerSplitContainer = new SplitContainer();
        private readonly TabPage installTab = new TabPage("APK 安装");
        private readonly TabPage deviceInfoTab = new TabPage("设备概览");
        private readonly TabPage displayControlTab = new TabPage("显示控制");
        private readonly TabPage logRecordTab = new TabPage("日志录制");
        private readonly TabPage fileTransferTab = new TabPage("文件传输");
        private readonly TabPage screenshotTab = new TabPage("截屏/录屏");
        private readonly TextBox apkTextBox = new TextBox();
        private readonly Button browseButton = new Button();
        private readonly Button refreshButton = new Button();
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

        private readonly TextBox deviceInfoTextBox = new TextBox();
        private readonly ToolTip deviceInfoToolTip = new ToolTip();

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
        private readonly Label logRecordStatusLabel = new Label();
        private readonly System.Windows.Forms.Timer logRecordStatusTimer = new System.Windows.Forms.Timer();

        private readonly RadioButton transferToDeviceRadioButton = new RadioButton();
        private readonly RadioButton transferToComputerRadioButton = new RadioButton();
        private readonly Label transferSourceLabel = new Label();
        private readonly Label transferTargetLabel = new Label();
        private readonly TextBox transferPathTextBox = new TextBox();
        private readonly TextBox transferTargetDirTextBox = new TextBox();
        private readonly Button browseTransferButton = new Button();
        private readonly Button browseTransferTargetButton = new Button();
        private readonly Button sendTransferButton = new Button();
        private readonly Label transferStatusLabel = new Label();
        private readonly ContextMenuStrip transferBrowseMenu = new ContextMenuStrip();
        private readonly ToolStripMenuItem browseTransferFileMenuItem = new ToolStripMenuItem("选择文件");
        private readonly ToolStripMenuItem browseTransferFolderMenuItem = new ToolStripMenuItem("选择文件夹");

        private readonly TextBox screenshotOutputDirTextBox = new TextBox();
        private readonly Button browseScreenshotOutputDirButton = new Button();
        private readonly Button takeScreenshotButton = new Button();
        private readonly Button startScreenRecordButton = new Button();
        private readonly Button stopScreenRecordButton = new Button();
        private readonly Button saveScreenshotAsButton = new Button();
        private readonly Button openScreenshotDirButton = new Button();
        private readonly CheckBox screenRecordTimeLimitCheckBox = new CheckBox();
        private readonly NumericUpDown screenRecordTimeLimitNumeric = new NumericUpDown();
        private readonly NumericUpDown screenRecordBitRateNumeric = new NumericUpDown();
        private readonly Label screenshotStatusLabel = new Label();
        private readonly PictureBox screenshotPreviewBox = new PictureBox();
        private readonly System.Windows.Forms.Timer screenRecordStatusTimer = new System.Windows.Forms.Timer();
        private readonly ToolTip screenshotToolTip = new ToolTip();

        private readonly Label displayResolutionInfoLabel = new Label();
        private readonly Label displayDensityInfoLabel = new Label();
        private readonly Label displayControlStatusLabel = new Label();
        private readonly TextBox displayWidthTextBox = new TextBox();
        private readonly TextBox displayHeightTextBox = new TextBox();
        private readonly TextBox displayDensityValueTextBox = new TextBox();
        private readonly ComboBox displayDensityUnitComboBox = new ComboBox();
        private readonly Button refreshDisplayInfoButton = new Button();
        private readonly Button applyResolutionButton = new Button();
        private readonly Button restoreResolutionButton = new Button();
        private readonly Button applyDensityButton = new Button();
        private readonly Button restoreDensityButton = new Button();
        private readonly Button restoreDisplayAllButton = new Button();
        private readonly ToolTip displayControlToolTip = new ToolTip();
        private readonly System.Windows.Forms.Timer displayControlRefreshTimer = new System.Windows.Forms.Timer();

        private readonly Dictionary<string, DeviceInfo> deviceMap = new Dictionary<string, DeviceInfo>();
        private readonly object processLock = new object();
        private readonly string appDir;
        private readonly string configPath;
        private readonly string logDir;
        private readonly string screenshotDir;
        private const int DefaultTabAreaHeight = 300;
        private const int DefaultDeviceAreaHeight = 162;
        private const int DefaultLogAreaHeight = 376;
        private const int MinTabAreaHeight = 150;
        private const int MinDeviceAreaHeight = 96;
        private const int MinLogAreaHeight = 150;
        private Process currentProcess;
        private Process logcatProcess;
        private StreamWriter logcatWriter;
        private HashSet<string> logcatPidFilter;
        private bool logcatIncludeThreadInfo = true;
        private bool logcatIncludeTimeInfo = true;
        private DateTime logRecordStartedAt;
        private string logRecordCurrentOutputPath = "";
        private string logRecordCurrentDeviceLabel = "";
        private long logRecordWrittenLines;
        private long logRecordLastFileSize;
        private readonly object logcatLock = new object();
        private volatile bool cancelRequested;
        private volatile bool isExecuting;
        private volatile bool isDeviceCommandRunning;
        private volatile bool isLogcatRunning;
        private volatile bool isScreenshotRunning;
        private volatile bool isScreenRecordRunning;
        private volatile bool screenRecordStopRequested;
        private bool loadingConfig;
        private bool configReady;
        private bool applyingLayoutConfig;
        private bool updatingTransferFields;
        private int savedTabAreaHeight = DefaultTabAreaHeight;
        private int savedDeviceAreaHeight = DefaultDeviceAreaHeight;
        private int savedLogAreaHeight = DefaultLogAreaHeight;
        private string lastPushSourcePath = "";
        private string lastPushTargetDir = "/sdcard/Download";
        private string lastPullSourcePath = "/sdcard/Download";
        private string lastPullTargetDir = "";
        private string lastCapturePath = "";
        private CaptureMediaType lastCaptureType = CaptureMediaType.None;
        private string screenRecordCurrentSerial = "";
        private string screenRecordCurrentDeviceLabel = "";
        private int screenRecordCurrentTimeLimitSeconds;
        private DateTime screenRecordStartedAt;
        private ApkInfo currentApkInfo;
        private DisplayControlInfo currentDisplayControlInfo;
        private volatile bool isDisplayControlAutoRefreshing;
        private string currentDeviceInfoSerial = "";

        private bool IsMediaCaptureRunning
        {
            get { return isScreenshotRunning || isScreenRecordRunning; }
        }

        public MainForm()
        {
            appDir = AppDomain.CurrentDomain.BaseDirectory;
            configPath = Path.Combine(appDir, "install-apk.config.json");
            logDir = Path.Combine(appDir, "log");
            screenshotDir = Path.Combine(appDir, "screenshots");
            Text = "APK安装工具";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1080, 820);
            Size = new Size(1180, 900);
            Font = new Font("Microsoft YaHei UI", 9F);
            AllowDrop = true;
            BuildUi();
            WireEvents();
            Directory.CreateDirectory(logDir);
            Directory.CreateDirectory(screenshotDir);
            InitLogcatDefaults();
            InitTransferDefaults();
            InitScreenshotDefaults();
            LoadConfig();
            configReady = true;
            UpdateExecutionOptionState();
            UpdateTransferStatus();
            RefreshDevices();
        }

        private void BuildUi()
        {
            var root = new Panel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(12);
            Controls.Add(root);

            mainSplitContainer.Dock = DockStyle.Fill;
            mainSplitContainer.Orientation = Orientation.Horizontal;
            mainSplitContainer.SplitterWidth = 6;
            mainSplitContainer.TabStop = false;
            mainSplitContainer.BackColor = Color.FromArgb(220, 220, 220);
            root.Controls.Add(mainSplitContainer);

            tabControl.Dock = DockStyle.Fill;
            tabControl.TabPages.Add(installTab);
            tabControl.TabPages.Add(deviceInfoTab);
            tabControl.TabPages.Add(displayControlTab);
            tabControl.TabPages.Add(logRecordTab);
            tabControl.TabPages.Add(fileTransferTab);
            tabControl.TabPages.Add(screenshotTab);
            mainSplitContainer.Panel1.Controls.Add(tabControl);

            lowerSplitContainer.Dock = DockStyle.Fill;
            lowerSplitContainer.Orientation = Orientation.Horizontal;
            lowerSplitContainer.SplitterWidth = 6;
            lowerSplitContainer.TabStop = false;
            lowerSplitContainer.BackColor = Color.FromArgb(220, 220, 220);
            mainSplitContainer.Panel2.Controls.Add(lowerSplitContainer);

            BuildInstallTab();
            BuildDeviceInfoTab();
            BuildDisplayControlTab();
            BuildLogRecordTab();
            BuildFileTransferTab();
            BuildScreenshotTab();
            BuildSharedDeviceArea(lowerSplitContainer.Panel1);
            BuildSharedLogArea(lowerSplitContainer.Panel2);
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

        private void BuildDeviceInfoTab()
        {
            deviceInfoTab.Padding = new Padding(10);
            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 1;
            panel.RowCount = 2;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            deviceInfoTab.Controls.Add(panel);

            var connectPanel = new TableLayoutPanel();
            connectPanel.Dock = DockStyle.Fill;
            connectPanel.ColumnCount = 4;
            connectPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            connectPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            connectPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            connectPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            panel.Controls.Add(connectPanel, 0, 0);

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

            deviceInfoTextBox.Dock = DockStyle.Fill;
            deviceInfoTextBox.Multiline = true;
            deviceInfoTextBox.ScrollBars = ScrollBars.Both;
            deviceInfoTextBox.WordWrap = false;
            deviceInfoTextBox.ReadOnly = true;
            deviceInfoTextBox.Font = new Font("Consolas", 9F);
            deviceInfoTextBox.Text = "请点击下方目标设备列表中的一台 device 状态设备。";
            panel.Controls.Add(deviceInfoTextBox, 0, 1);

            deviceInfoToolTip.SetToolTip(connectAddressTextBox, "可输入 IP:端口，例如 192.168.1.100:5555；点击设备列表会同步设备 ID。");
        }

        private void BuildDisplayControlTab()
        {
            displayControlTab.Padding = new Padding(10);
            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Top;
            panel.Height = 240;
            panel.ColumnCount = 1;
            panel.RowCount = 6;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            displayControlTab.Controls.Add(panel);

            var topPanel = new TableLayoutPanel();
            topPanel.Dock = DockStyle.Fill;
            topPanel.ColumnCount = 4;
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 8));
            panel.Controls.Add(topPanel, 0, 0);
            var title = new Label();
            title.Text = "显示控制";
            title.Dock = DockStyle.Fill;
            title.TextAlign = ContentAlignment.MiddleLeft;
            topPanel.Controls.Add(title, 0, 0);
            refreshDisplayInfoButton.Text = "读取信息";
            AddActionButton(topPanel, refreshDisplayInfoButton, 1);
            restoreDisplayAllButton.Text = "全部恢复";
            AddActionButton(topPanel, restoreDisplayAllButton, 2);

            displayResolutionInfoLabel.Text = "屏幕分辨率：请选择一台 device 状态的设备后读取。";
            displayResolutionInfoLabel.Dock = DockStyle.Fill;
            displayResolutionInfoLabel.TextAlign = ContentAlignment.MiddleLeft;
            displayResolutionInfoLabel.ForeColor = Color.FromArgb(60, 60, 60);
            displayResolutionInfoLabel.AutoEllipsis = true;
            panel.Controls.Add(displayResolutionInfoLabel, 0, 1);

            var resolutionPanel = new TableLayoutPanel();
            resolutionPanel.Dock = DockStyle.Fill;
            resolutionPanel.ColumnCount = 9;
            resolutionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            resolutionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            resolutionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32));
            resolutionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            resolutionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            resolutionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32));
            resolutionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            resolutionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            resolutionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.Controls.Add(resolutionPanel, 0, 2);
            AddLabel(resolutionPanel, "横向像素", 0);
            displayWidthTextBox.Dock = DockStyle.Fill;
            displayWidthTextBox.Margin = new Padding(0, 4, 8, 4);
            resolutionPanel.Controls.Add(displayWidthTextBox, 1, 0);
            AddLabel(resolutionPanel, "px", 2);
            AddLabel(resolutionPanel, "纵向像素", 3);
            displayHeightTextBox.Dock = DockStyle.Fill;
            displayHeightTextBox.Margin = new Padding(0, 4, 8, 4);
            resolutionPanel.Controls.Add(displayHeightTextBox, 4, 0);
            AddLabel(resolutionPanel, "px", 5);
            applyResolutionButton.Text = "修改";
            restoreResolutionButton.Text = "恢复";
            AddActionButton(resolutionPanel, applyResolutionButton, 6);
            AddActionButton(resolutionPanel, restoreResolutionButton, 7);

            displayDensityInfoLabel.Text = "显示密度：请选择一台 device 状态的设备后读取。";
            displayDensityInfoLabel.Dock = DockStyle.Fill;
            displayDensityInfoLabel.TextAlign = ContentAlignment.MiddleLeft;
            displayDensityInfoLabel.ForeColor = Color.FromArgb(60, 60, 60);
            displayDensityInfoLabel.AutoEllipsis = true;
            panel.Controls.Add(displayDensityInfoLabel, 0, 3);

            var densityPanel = new TableLayoutPanel();
            densityPanel.Dock = DockStyle.Fill;
            densityPanel.ColumnCount = 7;
            densityPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            densityPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            densityPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
            densityPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            densityPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            densityPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 16));
            densityPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.Controls.Add(densityPanel, 0, 4);
            AddLabel(densityPanel, "密度/宽度", 0);
            displayDensityValueTextBox.Dock = DockStyle.Fill;
            displayDensityValueTextBox.Margin = new Padding(0, 4, 8, 4);
            densityPanel.Controls.Add(displayDensityValueTextBox, 1, 0);
            displayDensityUnitComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            displayDensityUnitComboBox.Dock = DockStyle.Fill;
            displayDensityUnitComboBox.Margin = new Padding(0, 4, 8, 4);
            displayDensityUnitComboBox.Items.AddRange(new object[] { "DPI", "dp" });
            displayDensityUnitComboBox.SelectedIndex = 0;
            densityPanel.Controls.Add(displayDensityUnitComboBox, 2, 0);
            applyDensityButton.Text = "修改";
            restoreDensityButton.Text = "恢复";
            AddActionButton(densityPanel, applyDensityButton, 3);
            AddActionButton(densityPanel, restoreDensityButton, 4);

            displayControlStatusLabel.Text = "修改显示参数可能会短暂刷新设备画面；异常时可使用恢复按钮。";
            displayControlStatusLabel.Dock = DockStyle.Fill;
            displayControlStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            displayControlStatusLabel.ForeColor = Color.FromArgb(80, 80, 80);
            displayControlStatusLabel.AutoEllipsis = true;
            panel.Controls.Add(displayControlStatusLabel, 0, 5);

            displayControlToolTip.SetToolTip(refreshDisplayInfoButton, "读取当前设备的 wm size 和 wm density。");
            displayControlToolTip.SetToolTip(restoreDisplayAllButton, "依次执行 wm size reset 和 wm density reset。");
            displayControlToolTip.SetToolTip(applyResolutionButton, "执行 wm size 宽x高，单位为 px。");
            displayControlToolTip.SetToolTip(restoreResolutionButton, "执行 wm size reset。");
            displayControlToolTip.SetToolTip(applyDensityButton, "DPI 模式直接设置 density；dp 模式按最小宽度换算 density。");
            displayControlToolTip.SetToolTip(restoreDensityButton, "执行 wm density reset，同时恢复最小宽度表现。");
        }

        private void BuildLogRecordTab()
        {
            logRecordTab.Padding = new Padding(10);
            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Top;
            panel.Height = 224;
            panel.ColumnCount = 1;
            panel.RowCount = 6;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
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

            logRecordStatusLabel.Text = "\u672a\u5f00\u59cb\u5f55\u5236";
            logRecordStatusLabel.Dock = DockStyle.Fill;
            logRecordStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            logRecordStatusLabel.ForeColor = Color.FromArgb(60, 60, 60);
            logRecordStatusLabel.AutoEllipsis = true;
            panel.Controls.Add(logRecordStatusLabel, 0, 4);

            var hint = new Label();
            hint.Text = "文件夹会自动保存为 log-时间.txt；多个 Tag 可用空格、逗号或分号分隔。";
            hint.Dock = DockStyle.Fill;
            hint.TextAlign = ContentAlignment.MiddleLeft;
            hint.ForeColor = Color.FromArgb(80, 80, 80);
            panel.Controls.Add(hint, 0, 5);
        }

        private void BuildFileTransferTab()
        {
            fileTransferTab.Padding = new Padding(10);
            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Top;
            panel.Height = 214;
            panel.ColumnCount = 1;
            panel.RowCount = 5;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            fileTransferTab.Controls.Add(panel);

            var directionPanel = new FlowLayoutPanel();
            directionPanel.Dock = DockStyle.Fill;
            directionPanel.FlowDirection = FlowDirection.LeftToRight;
            directionPanel.WrapContents = false;
            panel.Controls.Add(directionPanel, 0, 0);
            transferToDeviceRadioButton.Text = "电脑 -> 手机";
            transferToDeviceRadioButton.Checked = true;
            transferToDeviceRadioButton.AutoSize = true;
            transferToDeviceRadioButton.Margin = new Padding(0, 10, 24, 0);
            directionPanel.Controls.Add(transferToDeviceRadioButton);
            transferToComputerRadioButton.Text = "手机 -> 电脑";
            transferToComputerRadioButton.AutoSize = true;
            transferToComputerRadioButton.Margin = new Padding(0, 10, 24, 0);
            directionPanel.Controls.Add(transferToComputerRadioButton);

            var pathPanel = new TableLayoutPanel();
            pathPanel.Dock = DockStyle.Fill;
            pathPanel.ColumnCount = 3;
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            panel.Controls.Add(pathPanel, 0, 1);

            transferSourceLabel.Text = "本地路径";
            transferSourceLabel.Dock = DockStyle.Fill;
            transferSourceLabel.TextAlign = ContentAlignment.MiddleLeft;
            pathPanel.Controls.Add(transferSourceLabel, 0, 0);
            transferPathTextBox.Dock = DockStyle.Fill;
            transferPathTextBox.Margin = new Padding(0, 4, 8, 4);
            pathPanel.Controls.Add(transferPathTextBox, 1, 0);
            browseTransferButton.Text = "选择...";
            browseTransferButton.Dock = DockStyle.Fill;
            browseTransferButton.Margin = new Padding(0, 3, 0, 3);
            pathPanel.Controls.Add(browseTransferButton, 2, 0);

            var targetPanel = new TableLayoutPanel();
            targetPanel.Dock = DockStyle.Fill;
            targetPanel.ColumnCount = 3;
            targetPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            targetPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            targetPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            panel.Controls.Add(targetPanel, 0, 2);

            transferTargetLabel.Text = "设备目录";
            transferTargetLabel.Dock = DockStyle.Fill;
            transferTargetLabel.TextAlign = ContentAlignment.MiddleLeft;
            targetPanel.Controls.Add(transferTargetLabel, 0, 0);
            transferTargetDirTextBox.Dock = DockStyle.Fill;
            transferTargetDirTextBox.Margin = new Padding(0, 4, 8, 4);
            targetPanel.Controls.Add(transferTargetDirTextBox, 1, 0);
            browseTransferTargetButton.Text = "选择...";
            browseTransferTargetButton.Dock = DockStyle.Fill;
            browseTransferTargetButton.Margin = new Padding(0, 3, 0, 3);
            targetPanel.Controls.Add(browseTransferTargetButton, 2, 0);

            var actionPanel = new TableLayoutPanel();
            actionPanel.Dock = DockStyle.Fill;
            actionPanel.ColumnCount = 2;
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.Controls.Add(actionPanel, 0, 3);
            sendTransferButton.Text = "发送";
            sendTransferButton.Dock = DockStyle.None;
            sendTransferButton.Size = new Size(88, 28);
            sendTransferButton.Margin = new Padding(0, 4, 8, 0);
            actionPanel.Controls.Add(sendTransferButton, 0, 0);

            transferStatusLabel.Text = "请选择要发送的文件或文件夹。";
            transferStatusLabel.Dock = DockStyle.Fill;
            transferStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            transferStatusLabel.ForeColor = Color.FromArgb(60, 60, 60);
            transferStatusLabel.AutoEllipsis = true;
            panel.Controls.Add(transferStatusLabel, 0, 4);

            transferBrowseMenu.Items.AddRange(new ToolStripItem[] { browseTransferFileMenuItem, browseTransferFolderMenuItem });
        }

        private void BuildScreenshotTab()
        {
            screenshotTab.Padding = new Padding(10);
            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 1;
            panel.RowCount = 5;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            screenshotTab.Controls.Add(panel);

            var pathPanel = new TableLayoutPanel();
            pathPanel.Dock = DockStyle.Fill;
            pathPanel.ColumnCount = 3;
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            panel.Controls.Add(pathPanel, 0, 0);

            var outputLabel = new Label();
            outputLabel.Text = "保存目录";
            outputLabel.Dock = DockStyle.Fill;
            outputLabel.TextAlign = ContentAlignment.MiddleLeft;
            pathPanel.Controls.Add(outputLabel, 0, 0);
            screenshotOutputDirTextBox.Dock = DockStyle.Fill;
            screenshotOutputDirTextBox.Margin = new Padding(0, 4, 8, 4);
            pathPanel.Controls.Add(screenshotOutputDirTextBox, 1, 0);
            browseScreenshotOutputDirButton.Text = "选择...";
            browseScreenshotOutputDirButton.Dock = DockStyle.Fill;
            browseScreenshotOutputDirButton.Margin = new Padding(0, 3, 0, 3);
            pathPanel.Controls.Add(browseScreenshotOutputDirButton, 2, 0);

            var actionPanel = new TableLayoutPanel();
            actionPanel.Dock = DockStyle.Fill;
            actionPanel.ColumnCount = 6;
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            panel.Controls.Add(actionPanel, 0, 1);
            takeScreenshotButton.Text = "截屏";
            startScreenRecordButton.Text = "开始录屏";
            stopScreenRecordButton.Text = "停止录屏";
            saveScreenshotAsButton.Text = "另存为";
            openScreenshotDirButton.Text = "打开目录";
            AddActionButton(actionPanel, takeScreenshotButton, 0);
            AddActionButton(actionPanel, startScreenRecordButton, 1);
            AddActionButton(actionPanel, stopScreenRecordButton, 2);
            AddActionButton(actionPanel, saveScreenshotAsButton, 4);
            AddActionButton(actionPanel, openScreenshotDirButton, 5);
            stopScreenRecordButton.Enabled = false;
            saveScreenshotAsButton.Enabled = false;
            openScreenshotDirButton.Enabled = true;

            var recordOptionsPanel = new TableLayoutPanel();
            recordOptionsPanel.Dock = DockStyle.Fill;
            recordOptionsPanel.ColumnCount = 8;
            recordOptionsPanel.RowCount = 1;
            recordOptionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
            recordOptionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
            recordOptionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
            recordOptionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
            recordOptionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            recordOptionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
            recordOptionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
            recordOptionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            recordOptionsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            recordOptionsPanel.Margin = new Padding(0, 2, 0, 2);
            recordOptionsPanel.Padding = new Padding(8, 0, 8, 0);
            panel.Controls.Add(recordOptionsPanel, 0, 2);

            var recordOptionsLabel = new Label();
            recordOptionsLabel.Text = "录屏选项";
            recordOptionsLabel.Dock = DockStyle.Fill;
            recordOptionsLabel.TextAlign = ContentAlignment.MiddleLeft;
            recordOptionsLabel.ForeColor = Color.FromArgb(70, 70, 70);
            recordOptionsPanel.Controls.Add(recordOptionsLabel, 0, 0);

            screenRecordTimeLimitCheckBox.Text = "时长上限";
            screenRecordTimeLimitCheckBox.Dock = DockStyle.Fill;
            screenRecordTimeLimitCheckBox.TextAlign = ContentAlignment.MiddleLeft;
            screenRecordTimeLimitCheckBox.Margin = new Padding(0, 5, 8, 5);
            recordOptionsPanel.Controls.Add(screenRecordTimeLimitCheckBox, 1, 0);
            screenRecordTimeLimitNumeric.Minimum = 1;
            screenRecordTimeLimitNumeric.Maximum = 180;
            screenRecordTimeLimitNumeric.Value = 180;
            screenRecordTimeLimitNumeric.Dock = DockStyle.Fill;
            screenRecordTimeLimitNumeric.Margin = new Padding(0, 5, 8, 5);
            screenRecordTimeLimitNumeric.Enabled = false;
            recordOptionsPanel.Controls.Add(screenRecordTimeLimitNumeric, 2, 0);

            var secondsLabel = new Label();
            secondsLabel.Text = "秒";
            secondsLabel.Dock = DockStyle.Fill;
            secondsLabel.TextAlign = ContentAlignment.MiddleLeft;
            secondsLabel.ForeColor = Color.FromArgb(70, 70, 70);
            recordOptionsPanel.Controls.Add(secondsLabel, 3, 0);

            var bitRateLabel = new Label();
            bitRateLabel.Text = "码率";
            bitRateLabel.Dock = DockStyle.Fill;
            bitRateLabel.TextAlign = ContentAlignment.MiddleLeft;
            bitRateLabel.ForeColor = Color.FromArgb(70, 70, 70);
            recordOptionsPanel.Controls.Add(bitRateLabel, 4, 0);
            screenRecordBitRateNumeric.Minimum = 1;
            screenRecordBitRateNumeric.Maximum = 100;
            screenRecordBitRateNumeric.Value = 8;
            screenRecordBitRateNumeric.Dock = DockStyle.Fill;
            screenRecordBitRateNumeric.Margin = new Padding(0, 5, 8, 5);
            recordOptionsPanel.Controls.Add(screenRecordBitRateNumeric, 5, 0);

            var bitRateUnitLabel = new Label();
            bitRateUnitLabel.Text = "Mbps";
            bitRateUnitLabel.Dock = DockStyle.Fill;
            bitRateUnitLabel.TextAlign = ContentAlignment.MiddleLeft;
            bitRateUnitLabel.ForeColor = Color.FromArgb(70, 70, 70);
            recordOptionsPanel.Controls.Add(bitRateUnitLabel, 6, 0);

            screenshotStatusLabel.Text = "请选择一台 device 状态的目标设备后截屏或录屏。";
            screenshotStatusLabel.Dock = DockStyle.Fill;
            screenshotStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            screenshotStatusLabel.ForeColor = Color.FromArgb(60, 60, 60);
            screenshotStatusLabel.AutoEllipsis = true;
            panel.Controls.Add(screenshotStatusLabel, 0, 3);

            screenshotPreviewBox.Dock = DockStyle.Fill;
            screenshotPreviewBox.BackColor = Color.FromArgb(250, 250, 250);
            screenshotPreviewBox.BorderStyle = BorderStyle.FixedSingle;
            screenshotPreviewBox.SizeMode = PictureBoxSizeMode.Zoom;
            screenshotPreviewBox.TabStop = false;
            panel.Controls.Add(screenshotPreviewBox, 0, 4);

            screenshotToolTip.SetToolTip(takeScreenshotButton, "截取当前设备画面并保存为 PNG。");
            screenshotToolTip.SetToolTip(startScreenRecordButton, "开始录制当前设备画面并保存为 MP4。");
            screenshotToolTip.SetToolTip(stopScreenRecordButton, "停止录屏并拉取 MP4 到保存目录。");
            screenshotToolTip.SetToolTip(saveScreenshotAsButton, "另存最近一次截屏或录屏。");
            screenshotToolTip.SetToolTip(openScreenshotDirButton, "打开保存目录；已有结果时会选中最近文件。");
            screenshotToolTip.SetToolTip(screenRecordTimeLimitCheckBox, "勾选后会向 screenrecord 传入 --time-limit。");
            screenshotToolTip.SetToolTip(screenRecordBitRateNumeric, "码率越高画质通常越好，文件也越大。");
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

        private void BuildSharedDeviceArea(Control parent)
        {
            var devicePanel = new TableLayoutPanel();
            devicePanel.Dock = DockStyle.Fill;
            devicePanel.ColumnCount = 1;
            devicePanel.RowCount = 2;
            devicePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            devicePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            parent.Controls.Add(devicePanel);
            var deviceHeader = new TableLayoutPanel();
            deviceHeader.Dock = DockStyle.Fill;
            deviceHeader.ColumnCount = 2;
            deviceHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            deviceHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
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
            deviceList.Dock = DockStyle.Fill;
            deviceList.CheckOnClick = true;
            devicePanel.Controls.Add(deviceList, 0, 1);
        }

        private void BuildSharedLogArea(Control parent)
        {
            var logPanel = new TableLayoutPanel();
            logPanel.Dock = DockStyle.Fill;
            logPanel.ColumnCount = 1;
            logPanel.RowCount = 3;
            logPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            logPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            logPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            parent.Controls.Add(logPanel);

            var logHeader = new TableLayoutPanel();
            logHeader.Dock = DockStyle.Fill;
            logHeader.ColumnCount = 2;
            logHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            logHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            logPanel.Controls.Add(logHeader, 0, 0);
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
            logPanel.Controls.Add(logBox, 0, 1);
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.Text = "就绪";
            logPanel.Controls.Add(statusLabel, 0, 2);
        }

        private void WireEvents()
        {
            browseButton.Click += delegate { BrowseApk(); };
            refreshButton.Click += delegate { RefreshDevices(); };
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
            tabControl.SelectedIndexChanged += delegate { UpdateDisplayControlAutoRefreshState(); BeginDeviceInfoAutoRefresh(false); };
            deviceList.SelectedIndexChanged += delegate { OnDeviceListSelectionChanged(); };
            deviceList.ItemCheck += delegate { BeginSyncAddressFromCurrentDevice(); BeginDisplayControlAutoRefresh(); };
            deviceList.Click += delegate { BeginSyncAddressFromCurrentDevice(); };
            deviceList.MouseUp += delegate(object sender, MouseEventArgs e) { OnDeviceListMouseUp(e); };
            browseLogRecordFileButton.Click += delegate { BrowseLogRecordFile(); };
            browseLogRecordFolderButton.Click += delegate { BrowseLogRecordFolder(); };
            clearLogcatCacheButton.Click += delegate { ClearLogcatCache(); };
            exportLogcatCacheButton.Click += delegate { ExportLogcatCache(); };
            startLogRecordButton.Click += delegate { StartLogRecording(); };
            stopLogRecordButton.Click += delegate { StopLogcatRecording(); };
            transferToDeviceRadioButton.CheckedChanged += delegate { OnTransferDirectionChanged(); };
            transferToComputerRadioButton.CheckedChanged += delegate { OnTransferDirectionChanged(); };
            browseTransferButton.Click += delegate { ShowTransferBrowseMenu(); };
            browseTransferTargetButton.Click += delegate { BrowseTransferTarget(); };
            browseTransferFileMenuItem.Click += delegate { BrowseTransferFile(); };
            browseTransferFolderMenuItem.Click += delegate { BrowseTransferFolder(); };
            sendTransferButton.Click += delegate { StartFileTransfer(); };
            refreshDisplayInfoButton.Click += delegate { RefreshDisplayControlInfo(); };
            applyResolutionButton.Click += delegate { ApplyDisplayResolution(); };
            restoreResolutionButton.Click += delegate { RestoreDisplayResolution(); };
            applyDensityButton.Click += delegate { ApplyDisplayDensity(); };
            restoreDensityButton.Click += delegate { RestoreDisplayDensity(); };
            restoreDisplayAllButton.Click += delegate { RestoreAllDisplaySettings(); };
            displayDensityUnitComboBox.SelectedIndexChanged += delegate { UpdateDisplayDensityValueForSelectedUnit(); };
            browseScreenshotOutputDirButton.Click += delegate { BrowseScreenshotOutputDir(); };
            takeScreenshotButton.Click += delegate { StartScreenshot(); };
            startScreenRecordButton.Click += delegate { StartScreenRecording(); };
            stopScreenRecordButton.Click += delegate { StopScreenRecording(); };
            saveScreenshotAsButton.Click += delegate { SaveLatestCaptureAs(); };
            openScreenshotDirButton.Click += delegate { OpenScreenshotDirectory(); };
            logRecordStatusTimer.Interval = 1000;
            logRecordStatusTimer.Tick += delegate { UpdateLogRecordStatus(); };
            screenRecordStatusTimer.Interval = 1000;
            screenRecordStatusTimer.Tick += delegate { UpdateScreenRecordStatus(); };
            displayControlRefreshTimer.Interval = 3000;
            displayControlRefreshTimer.Tick += delegate { BeginDisplayControlAutoRefresh(); };
            logRecordPathTextBox.TextChanged += delegate { SaveConfig(); };
            logRecordTagTextBox.TextChanged += delegate { SaveConfig(); };
            logRecordPackageTextBox.TextChanged += delegate { SaveConfig(); };
            logRecordLevelComboBox.SelectedIndexChanged += delegate { SaveConfig(); };
            logRecordThreadInfoCheckBox.CheckedChanged += delegate { SaveConfig(); };
            logRecordTimeInfoCheckBox.CheckedChanged += delegate { SaveConfig(); };
            transferPathTextBox.TextChanged += delegate { OnTransferFieldChanged(); };
            transferTargetDirTextBox.TextChanged += delegate { OnTransferFieldChanged(); };
            screenshotOutputDirTextBox.TextChanged += delegate { SaveConfig(); };
            screenRecordTimeLimitCheckBox.CheckedChanged += delegate { UpdateScreenRecordOptionState(); SaveConfig(); };
            screenRecordTimeLimitNumeric.ValueChanged += delegate { SaveConfig(); };
            screenRecordBitRateNumeric.ValueChanged += delegate { SaveConfig(); };
            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            Load += delegate { ApplySavedLayoutHeights(); };
            ResizeEnd += delegate { CaptureLayoutHeights(); SaveConfig(); };
            mainSplitContainer.SplitterMoved += delegate { OnLayoutSplitterMoved(); };
            lowerSplitContainer.SplitterMoved += delegate { OnLayoutSplitterMoved(); };
            FormClosing += OnFormClosing;
        }

        private void OnLayoutSplitterMoved()
        {
            if (applyingLayoutConfig || loadingConfig) return;
            CaptureLayoutHeights();
            SaveConfig();
        }

        private void ApplySavedLayoutHeights()
        {
            if (mainSplitContainer.Height <= 0 || lowerSplitContainer.Height <= 0) return;
            applyingLayoutConfig = true;
            try
            {
                ConfigureLayoutMinSizes();
                SetHorizontalSplitterDistance(mainSplitContainer, savedTabAreaHeight);
                SetHorizontalSplitterDistance(lowerSplitContainer, savedDeviceAreaHeight);
                CaptureLayoutHeights();
            }
            finally
            {
                applyingLayoutConfig = false;
            }
        }

        private void ConfigureLayoutMinSizes()
        {
            mainSplitContainer.Panel1MinSize = MinTabAreaHeight;
            mainSplitContainer.Panel2MinSize = MinDeviceAreaHeight + MinLogAreaHeight + lowerSplitContainer.SplitterWidth;
            lowerSplitContainer.Panel1MinSize = MinDeviceAreaHeight;
            lowerSplitContainer.Panel2MinSize = MinLogAreaHeight;
        }

        private static void SetHorizontalSplitterDistance(SplitContainer splitContainer, int distance)
        {
            var maxDistance = splitContainer.Height - splitContainer.SplitterWidth - splitContainer.Panel2MinSize;
            var minDistance = splitContainer.Panel1MinSize;
            if (maxDistance < minDistance) return;
            splitContainer.SplitterDistance = ClampInt(distance, minDistance, maxDistance);
        }

        private void CaptureLayoutHeights()
        {
            if (mainSplitContainer.Height > 0)
            {
                savedTabAreaHeight = Math.Max(0, mainSplitContainer.Panel1.Height);
            }
            if (lowerSplitContainer.Height > 0)
            {
                savedDeviceAreaHeight = Math.Max(0, lowerSplitContainer.Panel1.Height);
                savedLogAreaHeight = Math.Max(0, lowerSplitContainer.Panel2.Height);
            }
        }

        private Size GetConfigWindowSize()
        {
            if (WindowState == FormWindowState.Normal) return Size;
            return RestoreBounds.Size;
        }

        private void ApplySavedWindowSize(int width, int height)
        {
            if (width <= 0 || height <= 0) return;
            var workingArea = Screen.PrimaryScreen.WorkingArea;
            var targetWidth = ClampInt(width, MinimumSize.Width, Math.Max(MinimumSize.Width, workingArea.Width));
            var targetHeight = ClampInt(height, MinimumSize.Height, Math.Max(MinimumSize.Height, workingArea.Height));
            Size = new Size(targetWidth, targetHeight);
        }

        private void InitLogcatDefaults()
        {
            logRecordPathTextBox.Text = logDir;
            logRecordLevelComboBox.SelectedIndex = 0;
            logRecordThreadInfoCheckBox.Checked = true;
            logRecordTimeInfoCheckBox.Checked = true;
        }

        private void InitTransferDefaults()
        {
            lastPushTargetDir = "/sdcard/Download";
            lastPullSourcePath = "/sdcard/Download";
            lastPullTargetDir = GetDefaultTransferPullTargetDir();
            ApplyTransferDirectionUi();
        }

        private void InitScreenshotDefaults()
        {
            screenshotOutputDirTextBox.Text = screenshotDir;
            screenshotStatusLabel.Text = "请选择一台 device 状态的目标设备后截屏或录屏。";
            screenRecordTimeLimitCheckBox.Checked = false;
            UpdateScreenRecordOptionState();
        }

        private void BeginDeviceInfoAutoRefresh()
        {
            BeginDeviceInfoAutoRefresh(false);
        }

        private void BeginDeviceInfoAutoRefresh(bool force)
        {
            if (IsDisposed) return;
            try { BeginInvoke(new Action(delegate { StartDeviceInfoRefresh(true, force); })); } catch { }
        }

        private void StartDeviceInfoRefresh(bool automatic, bool force)
        {
            if (automatic && tabControl.SelectedTab != deviceInfoTab) return;
            if (isExecuting || isDeviceCommandRunning || isLogcatRunning || IsMediaCaptureRunning) return;

            DeviceInfo device;
            if (automatic)
            {
                if (!TryGetSelectedDeviceForDeviceInfo(out device))
                {
                    if (deviceList.SelectedItem != null) statusLabel.Text = "请选择状态为 device 的设备。";
                    return;
                }
                if (!force && string.Equals(currentDeviceInfoSerial, device.Serial, StringComparison.Ordinal) && deviceInfoTextBox.TextLength > 0) return;
            }
            else
            {
                device = GetSelectedDeviceForDeviceInfo();
                if (device == null) return;
            }

            var adb = FindAdb();
            if (adb == null)
            {
                var message = "未找到 adb.exe。请安装 Android SDK Platform Tools，或把 adb.exe 加入 PATH。";
                if (automatic) statusLabel.Text = message;
                else MessageBox.Show(this, message, "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            cancelRequested = false;
            isDeviceCommandRunning = true;
            SetDeviceCommandUi(true);
            statusLabel.Text = "正在读取设备信息...";
            AddLogLine("读取设备信息：" + device.Serial);
            var serial = device.Serial;
            var deviceLabel = device.Label;
            var thread = new Thread(new ThreadStart(delegate
            {
                var error = "";
                var report = "";
                try
                {
                    report = BuildDeviceInfoReport(adb, device, out error);
                    BeginInvokeIfNeeded(delegate
                    {
                        var canceled = cancelRequested || string.Equals(error, "Canceled", StringComparison.Ordinal);
                        if (automatic)
                        {
                            DeviceInfo currentDevice;
                            if (tabControl.SelectedTab != deviceInfoTab || !TryGetSelectedDeviceForDeviceInfo(out currentDevice) || !string.Equals(currentDevice.Serial, serial, StringComparison.Ordinal)) return;
                        }
                        if (canceled)
                        {
                            statusLabel.Text = "设备信息读取已中止。";
                            return;
                        }
                        if (!string.IsNullOrWhiteSpace(report))
                        {
                            deviceInfoTextBox.Text = report;
                            currentDeviceInfoSerial = serial;
                            statusLabel.Text = "设备信息已刷新。";
                        }
                        else
                        {
                            var message = string.IsNullOrWhiteSpace(error) ? "设备信息读取失败。" : error;
                            statusLabel.Text = message;
                        }
                    });
                }
                catch
                {
                    BeginInvokeIfNeeded(delegate
                    {
                        statusLabel.Text = "设备信息读取失败。";
                    });
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

        private DeviceInfo GetSelectedDeviceForDeviceInfo()
        {
            var label = GetDeviceInfoTargetLabel();
            if (string.IsNullOrWhiteSpace(label))
            {
                MessageBox.Show(this, "请先在目标设备列表中选择一台设备。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            DeviceInfo device;
            if (!deviceMap.TryGetValue(label, out device) || device.State != "device")
            {
                MessageBox.Show(this, "请选择状态为 device 的设备。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            return device;
        }

        private bool TryGetSelectedDeviceForDeviceInfo(out DeviceInfo device)
        {
            device = null;
            var label = GetDeviceInfoTargetLabel();
            if (string.IsNullOrWhiteSpace(label)) return false;
            return deviceMap.TryGetValue(label, out device) && device.State == "device";
        }

        private string GetDeviceInfoTargetLabel()
        {
            var label = deviceList.SelectedItem as string;
            if (!string.IsNullOrWhiteSpace(label)) return label;
            if (deviceList.CheckedItems.Count == 1) return deviceList.CheckedItems[0].ToString();
            return null;
        }

        private string BuildDeviceInfoReport(string adb, DeviceInfo device, out string error)
        {
            error = "";
            var serial = device.Serial;
            var builder = new StringBuilder();

            var getpropResult = InvokeProcessQuiet(adb, new[] { "-s", serial, "shell", "getprop" }, true);
            if (getpropResult.Canceled)
            {
                error = "Canceled";
                return "";
            }
            var props = ParseGetpropOutput(getpropResult.Output);
            var batteryOutput = ReadDeviceInfoShellOutput(adb, serial, new[] { "dumpsys", "battery" });
            var memoryOutput = ReadDeviceInfoShellOutput(adb, serial, new[] { "cat", "/proc/meminfo" });
            var dataStorageOutput = ReadDeviceInfoShellOutput(adb, serial, new[] { "df", "-h", "/data" });
            var displaySizeOutput = ReadDeviceInfoShellOutput(adb, serial, new[] { "wm", "size" });
            var displayDensityOutput = ReadDeviceInfoShellOutput(adb, serial, new[] { "wm", "density" });
            var networkOutput = ReadDeviceInfoShellOutput(adb, serial, new[] { "ip", "-f", "inet", "addr", "show" });
            var kernelVersion = ReadDeviceInfoShellValue(adb, serial, new[] { "uname", "-r" });
            var cpuCoreCount = ReadDeviceInfoShellValue(adb, serial, new[] { "nproc" });
            var selinuxState = ReadDeviceInfoShellValue(adb, serial, new[] { "getenforce" });
            var currentUser = ReadDeviceInfoShellValue(adb, serial, new[] { "am", "get-current-user" });
            if (cancelRequested)
            {
                error = "Canceled";
                return "";
            }

            builder.AppendLine("设备关键信息");
            builder.AppendLine("生成时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.AppendLine();

            AppendDeviceInfoSection(builder, "基本信息");
            AppendInfoLine(builder, "设备名称", FormatDeviceDisplayName(props));
            AppendInfoLine(builder, "厂商", GetFirstProperty(props, "ro.product.manufacturer", "ro.vendor.product.manufacturer", "ro.product.system.manufacturer"));
            AppendInfoLine(builder, "品牌", GetFirstProperty(props, "ro.product.brand", "ro.vendor.product.brand", "ro.product.system.brand"));
            AppendInfoLine(builder, "型号", GetFirstProperty(props, "ro.product.model", "ro.product.vendor.model", "ro.product.system.model"));
            AppendInfoLine(builder, "设备代号", GetFirstProperty(props, "ro.product.device", "ro.product.vendor.device", "ro.product.system.device"));
            AppendInfoLine(builder, "ADB 序列号", serial);
            builder.AppendLine();

            AppendDeviceInfoSection(builder, "系统版本");
            AppendInfoLine(builder, "Android 版本", GetFirstProperty(props, "ro.build.version.release"));
            AppendInfoLine(builder, "SDK 版本", GetFirstProperty(props, "ro.build.version.sdk"));
            AppendInfoLine(builder, "安全补丁", GetFirstProperty(props, "ro.build.version.security_patch", "ro.vendor.build.security_patch"));
            AppendInfoLine(builder, "构建版本", GetFirstProperty(props, "ro.build.display.id", "ro.build.id", "ro.build.version.incremental"));
            AppendInfoLine(builder, "系统类型", JoinNonEmpty(" / ", GetFirstProperty(props, "ro.build.type"), GetFirstProperty(props, "ro.build.tags")));
            AppendInfoLine(builder, "语言/地区", GetFirstProperty(props, "persist.sys.locale", "ro.product.locale", "ro.product.locale.language"));
            AppendInfoLine(builder, "时区", GetFirstProperty(props, "persist.sys.timezone"));
            AppendInfoLine(builder, "当前用户", currentUser);
            builder.AppendLine();

            AppendDeviceInfoSection(builder, "硬件与性能");
            AppendInfoLine(builder, "SoC/平台", JoinNonEmpty(" ", GetFirstProperty(props, "ro.soc.manufacturer", "ro.hardware.chipname"), GetFirstProperty(props, "ro.soc.model", "ro.board.platform", "ro.hardware")));
            AppendInfoLine(builder, "CPU 核心数", cpuCoreCount);
            AppendInfoLine(builder, "CPU ABI", GetFirstProperty(props, "ro.product.cpu.abilist", "ro.product.cpu.abi"));
            AppendInfoLine(builder, "内存", FormatMemorySummary(memoryOutput));
            AppendInfoLine(builder, "内核版本", kernelVersion);
            AppendInfoLine(builder, "低内存设备", GetFirstProperty(props, "ro.config.low_ram"));
            builder.AppendLine();

            AppendDeviceInfoSection(builder, "显示、电池与存储");
            AppendInfoLine(builder, "屏幕分辨率", FormatCompactOutput(displaySizeOutput));
            AppendInfoLine(builder, "显示密度", FormatCompactOutput(displayDensityOutput));
            AppendInfoLine(builder, "电池", FormatBatterySummary(batteryOutput));
            AppendInfoLine(builder, "内部存储", FormatStorageSummary(dataStorageOutput));
            builder.AppendLine();

            AppendDeviceInfoSection(builder, "网络与安全");
            AppendInfoLine(builder, "IPv4 地址", FormatIpAddressSummary(networkOutput));
            AppendInfoLine(builder, "SELinux", selinuxState);
            AppendInfoLine(builder, "Verified Boot", GetFirstProperty(props, "ro.boot.verifiedbootstate", "ro.boot.veritymode"));
            AppendInfoLine(builder, "设备锁状态", FormatDeviceLockState(GetFirstProperty(props, "ro.boot.flash.locked", "ro.boot.vbmeta.device_state")));
            AppendInfoLine(builder, "加密状态", JoinNonEmpty(" / ", GetFirstProperty(props, "ro.crypto.state"), GetFirstProperty(props, "ro.crypto.type")));
            return builder.ToString().TrimEnd();
        }

        private string ReadDeviceInfoShellValue(string adb, string serial, string[] shellArgs)
        {
            if (cancelRequested) return "";
            var output = ReadDeviceInfoShellOutput(adb, serial, shellArgs);
            return FirstUsefulLine(output) ?? "";
        }

        private string ReadDeviceInfoShellOutput(string adb, string serial, string[] shellArgs)
        {
            if (cancelRequested) return "";
            var args = new List<string> { "-s", serial, "shell" };
            args.AddRange(shellArgs);
            var result = InvokeProcessQuiet(adb, args.ToArray(), true);
            if (result.Canceled) return "";
            if (result.ExitCode != 0) return "";
            return (result.Output ?? "").Trim();
        }

        private static void AppendDeviceInfoSection(StringBuilder builder, string title)
        {
            builder.AppendLine("[" + title + "]");
        }

        private static void AppendInfoLine(StringBuilder builder, string label, string value)
        {
            builder.AppendLine(label + "：" + (string.IsNullOrWhiteSpace(value) ? "N/A" : value.Trim()));
        }

        private static Dictionary<string, string> ParseGetpropOutput(string output)
        {
            var props = new Dictionary<string, string>();
            foreach (var rawLine in SplitLines(output))
            {
                var line = rawLine.Trim();
                var match = Regex.Match(line, @"^\[(?<key>[^\]]+)\]: \[(?<value>.*)\]$");
                if (match.Success) props[match.Groups["key"].Value] = match.Groups["value"].Value;
            }
            return props;
        }

        private static string GetFirstProperty(Dictionary<string, string> props, params string[] keys)
        {
            foreach (var key in keys)
            {
                string value;
                if (props.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value)) return value;
            }
            return "";
        }

        private static string FormatDeviceDisplayName(Dictionary<string, string> props)
        {
            var marketName = GetFirstProperty(props, "ro.config.marketing_name", "ro.product.marketname", "ro.vendor.product.marketname");
            var manufacturer = GetFirstProperty(props, "ro.product.manufacturer", "ro.vendor.product.manufacturer", "ro.product.system.manufacturer");
            var model = GetFirstProperty(props, "ro.product.model", "ro.product.vendor.model", "ro.product.system.model");
            if (!string.IsNullOrWhiteSpace(marketName) && !string.Equals(marketName, model, StringComparison.OrdinalIgnoreCase))
            {
                var modelName = JoinNonEmpty(" ", manufacturer, model);
                return string.IsNullOrWhiteSpace(modelName) ? marketName : marketName + " (" + modelName + ")";
            }
            return JoinNonEmpty(" ", manufacturer, model);
        }

        private static string JoinNonEmpty(string separator, params string[] values)
        {
            var parts = values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct().ToArray();
            return parts.Length == 0 ? "" : string.Join(separator, parts);
        }

        private static string FormatCompactOutput(string output)
        {
            var lines = SplitLines(output).Select(s => s.Trim()).Where(s => s.Length > 0).Take(3).ToArray();
            return lines.Length == 0 ? "" : string.Join("；", lines);
        }

        private static string FormatMemorySummary(string output)
        {
            var totalKb = ReadMemInfoKb(output, "MemTotal");
            var availableKb = ReadMemInfoKb(output, "MemAvailable");
            if (availableKb <= 0) availableKb = ReadMemInfoKb(output, "MemFree");
            if (totalKb <= 0) return "";
            if (availableKb > 0) return "总计 " + FormatKbSize(totalKb) + "，可用 " + FormatKbSize(availableKb);
            return "总计 " + FormatKbSize(totalKb);
        }

        private static long ReadMemInfoKb(string output, string key)
        {
            var match = Regex.Match(output ?? "", @"^" + Regex.Escape(key) + @":\s*(?<value>\d+)\s*kB", RegexOptions.Multiline);
            long value;
            return match.Success && long.TryParse(match.Groups["value"].Value, out value) ? value : 0;
        }

        private static string FormatKbSize(long kb)
        {
            var bytes = kb * 1024.0;
            var gb = bytes / 1024 / 1024 / 1024;
            if (gb >= 1) return gb.ToString("0.##") + " GB";
            var mb = bytes / 1024 / 1024;
            return mb.ToString("0.##") + " MB";
        }

        private static string FormatStorageSummary(string output)
        {
            var line = SplitLines(output).Select(s => s.Trim()).Where(s => s.Length > 0 && !s.StartsWith("Filesystem", StringComparison.OrdinalIgnoreCase)).LastOrDefault();
            if (string.IsNullOrWhiteSpace(line)) return "";
            var parts = Regex.Split(line, @"\s+").Where(s => s.Length > 0).ToArray();
            if (parts.Length < 5) return line;
            return "总计 " + parts[1] + "，已用 " + parts[2] + "，可用 " + parts[3] + "，使用率 " + parts[4];
        }

        private static string FormatBatterySummary(string output)
        {
            if (string.IsNullOrWhiteSpace(output)) return "";
            var level = GetBatteryField(output, "level");
            var status = MapBatteryStatus(GetBatteryField(output, "status"));
            var health = MapBatteryHealth(GetBatteryField(output, "health"));
            var temperature = FormatBatteryTemperature(GetBatteryField(output, "temperature"));
            var power = FormatBatteryPower(output);
            return JoinNonEmpty("，", string.IsNullOrWhiteSpace(level) ? "" : level + "%", status, health, temperature, power);
        }

        private static string GetBatteryField(string output, string key)
        {
            var match = Regex.Match(output ?? "", @"^\s*" + Regex.Escape(key) + @":\s*(?<value>.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["value"].Value.Trim() : "";
        }

        private static string MapBatteryStatus(string value)
        {
            switch ((value ?? "").Trim())
            {
                case "1": return "状态未知";
                case "2": return "充电中";
                case "3": return "放电中";
                case "4": return "未充电";
                case "5": return "已充满";
                default: return value;
            }
        }

        private static string MapBatteryHealth(string value)
        {
            switch ((value ?? "").Trim())
            {
                case "1": return "健康状态未知";
                case "2": return "健康良好";
                case "3": return "过热";
                case "4": return "电池失效";
                case "5": return "电压过高";
                case "6": return "健康异常";
                case "7": return "温度过低";
                default: return value;
            }
        }

        private static string FormatBatteryTemperature(string value)
        {
            int temperature;
            if (!int.TryParse(value, out temperature)) return "";
            return (temperature / 10.0).ToString("0.#") + " °C";
        }

        private static string FormatBatteryPower(string output)
        {
            var powers = new List<string>();
            if (string.Equals(GetBatteryField(output, "AC powered"), "true", StringComparison.OrdinalIgnoreCase)) powers.Add("AC供电");
            if (string.Equals(GetBatteryField(output, "USB powered"), "true", StringComparison.OrdinalIgnoreCase)) powers.Add("USB供电");
            if (string.Equals(GetBatteryField(output, "Wireless powered"), "true", StringComparison.OrdinalIgnoreCase)) powers.Add("无线供电");
            return powers.Count == 0 ? "电池供电" : string.Join("/", powers.ToArray());
        }

        private static string FormatIpAddressSummary(string output)
        {
            var currentInterface = "";
            foreach (var rawLine in SplitLines(output))
            {
                var line = rawLine.Trim();
                var interfaceMatch = Regex.Match(line, @"^\d+:\s*(?<name>[^:@]+)");
                if (interfaceMatch.Success) currentInterface = interfaceMatch.Groups["name"].Value;
                var ipMatch = Regex.Match(line, @"\binet\s+(?<ip>\d+\.\d+\.\d+\.\d+)(?:/\d+)?");
                if (!ipMatch.Success) continue;
                var ip = ipMatch.Groups["ip"].Value;
                if (ip.StartsWith("127.", StringComparison.Ordinal)) continue;
                return string.IsNullOrWhiteSpace(currentInterface) ? ip : ip + " (" + currentInterface + ")";
            }
            return "";
        }

        private static string FormatDeviceLockState(string value)
        {
            value = (value ?? "").Trim();
            if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "locked", StringComparison.OrdinalIgnoreCase)) return "已锁定";
            if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "unlocked", StringComparison.OrdinalIgnoreCase)) return "已解锁";
            return value;
        }

        private void RefreshDisplayControlInfo()
        {
            RunDisplayControlOperation("正在读取显示信息...", delegate(string adb, DeviceInfo device)
            {
                var info = ReadDisplayControlInfo(adb, device.Serial, true);
                if (info == null) return;
                BeginInvokeIfNeeded(delegate
                {
                    ApplyDisplayControlInfo(info);
                    displayControlStatusLabel.Text = "显示信息已刷新。";
                });
                SetStatus("显示信息已刷新。");
            });
        }

        private void UpdateDisplayControlAutoRefreshState()
        {
            if (tabControl.SelectedTab == displayControlTab)
            {
                displayControlRefreshTimer.Start();
                BeginDisplayControlAutoRefresh();
            }
            else
            {
                displayControlRefreshTimer.Stop();
            }
        }

        private void BeginDisplayControlAutoRefresh()
        {
            if (IsDisposed) return;
            try { BeginInvoke(new Action(StartDisplayControlAutoRefresh)); } catch { }
        }

        private void StartDisplayControlAutoRefresh()
        {
            if (tabControl.SelectedTab != displayControlTab) return;

            DeviceInfo device;
            if (!TryGetSingleCheckedDeviceForDisplayControl(out device))
            {
                ClearDisplayControlInfo();
                displayControlStatusLabel.Text = "请选择一台 device 状态的目标设备，显示参数会自动刷新。";
                return;
            }

            if (isDisplayControlAutoRefreshing || isExecuting || isDeviceCommandRunning || isLogcatRunning || IsMediaCaptureRunning) return;

            var adb = FindAdb();
            if (adb == null)
            {
                displayControlStatusLabel.Text = "未找到 adb.exe，无法自动读取显示信息。";
                return;
            }

            var serial = device.Serial;
            isDisplayControlAutoRefreshing = true;
            var thread = new Thread(new ThreadStart(delegate
            {
                string error;
                var info = ReadDisplayControlInfoSilent(adb, serial, out error);
                if (IsDisposed)
                {
                    isDisplayControlAutoRefreshing = false;
                    return;
                }
                BeginInvokeIfNeeded(delegate
                {
                    try
                    {
                        DeviceInfo currentDevice;
                        if (isExecuting || isDeviceCommandRunning || isLogcatRunning || IsMediaCaptureRunning) return;
                        if (tabControl.SelectedTab != displayControlTab) return;
                        if (!TryGetSingleCheckedDeviceForDisplayControl(out currentDevice))
                        {
                            ClearDisplayControlInfo();
                            displayControlStatusLabel.Text = "请选择一台 device 状态的目标设备，显示参数会自动刷新。";
                            return;
                        }
                        if (!string.Equals(currentDevice.Serial, serial, StringComparison.Ordinal)) return;
                        if (info == null)
                        {
                            displayControlStatusLabel.Text = string.IsNullOrWhiteSpace(error) ? "自动刷新显示信息失败。" : error;
                            return;
                        }
                        ApplyDisplayControlInfo(info, true);
                        displayControlStatusLabel.Text = "显示信息已自动刷新：" + DateTime.Now.ToString("HH:mm:ss");
                    }
                    finally
                    {
                        isDisplayControlAutoRefreshing = false;
                    }
                });
            }));
            thread.IsBackground = true;
            thread.Start();
        }

        private void ApplyDisplayResolution()
        {
            if (!EnsureSingleCheckedDeviceForDisplayControl()) return;

            int width;
            int height;
            if (!TryReadPositiveInt(displayWidthTextBox, "横向像素", out width)) return;
            if (!TryReadPositiveInt(displayHeightTextBox, "纵向像素", out height)) return;

            var target = width.ToString() + "x" + height.ToString();
            RunDisplayControlOperation("正在修改屏幕分辨率...", delegate(string adb, DeviceInfo device)
            {
                if (!ExecuteDisplayControlCommand(adb, device.Serial, "修改屏幕分辨率", new[] { "wm", "size", target })) return;
                RefreshDisplayInfoAfterChange(adb, device.Serial, "屏幕分辨率已修改：" + target + " px");
            });
        }

        private void RestoreDisplayResolution()
        {
            RunDisplayControlOperation("正在恢复屏幕分辨率...", delegate(string adb, DeviceInfo device)
            {
                if (!ExecuteDisplayControlCommand(adb, device.Serial, "恢复屏幕分辨率", new[] { "wm", "size", "reset" })) return;
                RefreshDisplayInfoAfterChange(adb, device.Serial, "屏幕分辨率已恢复默认。");
            });
        }

        private void ApplyDisplayDensity()
        {
            if (!EnsureSingleCheckedDeviceForDisplayControl()) return;

            int value;
            var unit = GetSelectedDisplayDensityUnit();
            if (!TryReadPositiveInt(displayDensityValueTextBox, unit == "dp" ? "最小宽度" : "显示密度", out value)) return;
            if (unit == "DPI" && value < 72)
            {
                MessageBox.Show(this, "显示密度必须大于等于 72 dpi。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RunDisplayControlOperation("正在修改显示密度...", delegate(string adb, DeviceInfo device)
            {
                var targetDensity = value;
                var successMessage = "显示密度已修改：" + targetDensity + " dpi";
                if (unit == "dp")
                {
                    var info = ReadDisplayControlInfo(adb, device.Serial, true);
                    if (info == null || !info.HasCurrentSize)
                    {
                        FailDisplayControlOperation("无法读取当前分辨率，不能按最小宽度换算 density。");
                        return;
                    }
                    targetDensity = CalculateDensityForSmallestWidth(info.CurrentWidth, info.CurrentHeight, value);
                    if (targetDensity < 72)
                    {
                        FailDisplayControlOperation("最小宽度换算后的 density 为 " + targetDensity + " dpi，小于 Android 允许的 72 dpi。");
                        return;
                    }
                    successMessage = "最小宽度已按 " + value + " dp 修改（density " + targetDensity + " dpi）。";
                }

                if (!ExecuteDisplayControlCommand(adb, device.Serial, "修改显示密度", new[] { "wm", "density", targetDensity.ToString() })) return;
                RefreshDisplayInfoAfterChange(adb, device.Serial, successMessage);
            });
        }

        private void RestoreDisplayDensity()
        {
            RunDisplayControlOperation("正在恢复显示密度...", delegate(string adb, DeviceInfo device)
            {
                if (!ExecuteDisplayControlCommand(adb, device.Serial, "恢复显示密度", new[] { "wm", "density", "reset" })) return;
                RefreshDisplayInfoAfterChange(adb, device.Serial, "显示密度已恢复默认。");
            });
        }

        private void RestoreAllDisplaySettings()
        {
            RunDisplayControlOperation("正在恢复全部显示设置...", delegate(string adb, DeviceInfo device)
            {
                var sizeOk = ExecuteDisplayControlCommand(adb, device.Serial, "恢复屏幕分辨率", new[] { "wm", "size", "reset" });
                if (cancelRequested) return;
                var densityOk = ExecuteDisplayControlCommand(adb, device.Serial, "恢复显示密度", new[] { "wm", "density", "reset" });
                var message = sizeOk && densityOk ? "显示设置已全部恢复默认。" : "显示设置恢复未全部成功，请查看日志。";
                RefreshDisplayInfoAfterChange(adb, device.Serial, message);
            });
        }

        private void RunDisplayControlOperation(string busyText, Action<string, DeviceInfo> operation)
        {
            if (isExecuting || isDeviceCommandRunning || isLogcatRunning || IsMediaCaptureRunning) return;
            var device = GetSingleCheckedDeviceForDisplayControl();
            if (device == null) return;
            var adb = FindAdb();
            if (adb == null)
            {
                MessageBox.Show(this, "未找到 adb.exe。请安装 Android SDK Platform Tools，或把 adb.exe 加入 PATH。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            cancelRequested = false;
            isDeviceCommandRunning = true;
            SetDeviceCommandUi(true);
            displayControlStatusLabel.Text = busyText;
            statusLabel.Text = busyText;
            var thread = new Thread(new ThreadStart(delegate
            {
                try
                {
                    AddLogLine(busyText + " " + device.Serial);
                    operation(adb, device);
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

        private DeviceInfo GetSingleCheckedDeviceForDisplayControl()
        {
            var checkedItems = deviceList.CheckedItems.Cast<object>().Select(o => o.ToString()).ToList();
            if (checkedItems.Count == 0)
            {
                ClearDisplayControlInfo();
                MessageBox.Show(this, "请先在目标设备列表中选择一台设备。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            if (checkedItems.Count > 1)
            {
                ClearDisplayControlInfo();
                MessageBox.Show(this, "显示控制一次只能选择一台设备。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            DeviceInfo device;
            if (!deviceMap.TryGetValue(checkedItems[0], out device) || device.State != "device")
            {
                ClearDisplayControlInfo();
                MessageBox.Show(this, "请选择状态为 device 的设备。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            return device;
        }

        private bool EnsureSingleCheckedDeviceForDisplayControl()
        {
            DeviceInfo device;
            if (TryGetSingleCheckedDeviceForDisplayControl(out device)) return true;
            return GetSingleCheckedDeviceForDisplayControl() != null;
        }

        private bool TryGetSingleCheckedDeviceForDisplayControl(out DeviceInfo device)
        {
            device = null;
            var checkedItems = deviceList.CheckedItems.Cast<object>().Select(o => o.ToString()).ToList();
            if (checkedItems.Count != 1) return false;
            return deviceMap.TryGetValue(checkedItems[0], out device) && device.State == "device";
        }

        private DisplayControlInfo ReadDisplayControlInfo(string adb, string serial, bool cancellable)
        {
            var sizeResult = InvokeProcess(adb, new[] { "-s", serial, "shell", "wm", "size" }, cancellable);
            if (sizeResult.Canceled)
            {
                FailDisplayControlOperation("显示信息读取已中止。");
                return null;
            }
            if (sizeResult.ExitCode != 0)
            {
                FailDisplayControlOperation("读取屏幕分辨率失败：" + HumanizeAdbOutput(sizeResult.Output));
                return null;
            }

            var densityResult = InvokeProcess(adb, new[] { "-s", serial, "shell", "wm", "density" }, cancellable);
            if (densityResult.Canceled)
            {
                FailDisplayControlOperation("显示信息读取已中止。");
                return null;
            }
            if (densityResult.ExitCode != 0)
            {
                FailDisplayControlOperation("读取显示密度失败：" + HumanizeAdbOutput(densityResult.Output));
                return null;
            }

            var info = new DisplayControlInfo();
            var hasSize = TryParseDisplaySizeOutput(sizeResult.Output, info);
            var hasDensity = TryParseDisplayDensityOutput(densityResult.Output, info);
            if (!hasSize && !hasDensity)
            {
                FailDisplayControlOperation("无法解析设备显示信息。");
                return null;
            }
            if (!hasSize) AddLogLine("无法解析 wm size 输出：" + FirstUsefulLine(sizeResult.Output));
            if (!hasDensity) AddLogLine("无法解析 wm density 输出：" + FirstUsefulLine(densityResult.Output));
            return info;
        }

        private DisplayControlInfo ReadDisplayControlInfoSilent(string adb, string serial, out string error)
        {
            error = "";
            var sizeResult = InvokeProcessSilent(adb, new[] { "-s", serial, "shell", "wm", "size" });
            if (sizeResult.ExitCode != 0)
            {
                error = "自动读取屏幕分辨率失败：" + HumanizeAdbOutput(sizeResult.Output);
                return null;
            }

            var densityResult = InvokeProcessSilent(adb, new[] { "-s", serial, "shell", "wm", "density" });
            if (densityResult.ExitCode != 0)
            {
                error = "自动读取显示密度失败：" + HumanizeAdbOutput(densityResult.Output);
                return null;
            }

            var info = new DisplayControlInfo();
            var hasSize = TryParseDisplaySizeOutput(sizeResult.Output, info);
            var hasDensity = TryParseDisplayDensityOutput(densityResult.Output, info);
            if (!hasSize && !hasDensity)
            {
                error = "自动读取到的显示信息无法解析。";
                return null;
            }
            return info;
        }

        private bool ExecuteDisplayControlCommand(string adb, string serial, string title, string[] shellArgs)
        {
            var args = new List<string> { "-s", serial, "shell" };
            args.AddRange(shellArgs);
            var result = InvokeProcess(adb, args.ToArray(), true);
            if (result.Canceled)
            {
                FailDisplayControlOperation(title + "已中止。");
                return false;
            }
            if (result.ExitCode == 0) return true;
            FailDisplayControlOperation(title + "失败：" + HumanizeAdbOutput(result.Output));
            return false;
        }

        private void RefreshDisplayInfoAfterChange(string adb, string serial, string message)
        {
            if (cancelRequested) return;
            var info = ReadDisplayControlInfo(adb, serial, true);
            BeginInvokeIfNeeded(delegate
            {
                if (info != null) ApplyDisplayControlInfo(info);
                displayControlStatusLabel.Text = info == null ? message + " 但刷新显示信息失败。" : message;
            });
            SetStatus(info == null ? message + " 但刷新失败。" : message);
            AddLogLine(message);
        }

        private void ApplyDisplayControlInfo(DisplayControlInfo info)
        {
            ApplyDisplayControlInfo(info, false);
        }

        private void ApplyDisplayControlInfo(DisplayControlInfo info, bool preserveFocusedInputs)
        {
            currentDisplayControlInfo = info;
            if (info.HasCurrentSize)
            {
                displayResolutionInfoLabel.Text = "屏幕分辨率：当前 " + FormatDisplaySize(info.CurrentWidth, info.CurrentHeight) + " px" + FormatDisplaySizeSource(info);
                if (!preserveFocusedInputs || !displayWidthTextBox.Focused) displayWidthTextBox.Text = info.CurrentWidth.ToString();
                if (!preserveFocusedInputs || !displayHeightTextBox.Focused) displayHeightTextBox.Text = info.CurrentHeight.ToString();
            }
            else
            {
                displayResolutionInfoLabel.Text = "屏幕分辨率：N/A";
                if (!preserveFocusedInputs || !displayWidthTextBox.Focused) displayWidthTextBox.Clear();
                if (!preserveFocusedInputs || !displayHeightTextBox.Focused) displayHeightTextBox.Clear();
            }

            if (info.HasCurrentDensity)
            {
                var smallestWidth = info.HasCurrentSize ? CalculateSmallestWidthDp(info.CurrentWidth, info.CurrentHeight, info.CurrentDensity) : 0;
                displayDensityInfoLabel.Text = "显示密度：当前 " + info.CurrentDensity + " dpi" + (smallestWidth > 0 ? "，最小宽度 " + smallestWidth + " dp" : "") + FormatDisplayDensitySource(info);
                UpdateDisplayDensityValueForSelectedUnit(preserveFocusedInputs);
            }
            else
            {
                displayDensityInfoLabel.Text = "显示密度：N/A";
                if (!preserveFocusedInputs || !displayDensityValueTextBox.Focused) displayDensityValueTextBox.Clear();
            }
        }

        private void ClearDisplayControlInfo()
        {
            currentDisplayControlInfo = null;
            displayResolutionInfoLabel.Text = "屏幕分辨率：请选择一台 device 状态的设备后读取。";
            displayDensityInfoLabel.Text = "显示密度：请选择一台 device 状态的设备后读取。";
            displayWidthTextBox.Clear();
            displayHeightTextBox.Clear();
            displayDensityValueTextBox.Clear();
        }

        private void UpdateDisplayDensityValueForSelectedUnit()
        {
            UpdateDisplayDensityValueForSelectedUnit(false);
        }

        private void UpdateDisplayDensityValueForSelectedUnit(bool preserveFocusedInput)
        {
            if (currentDisplayControlInfo == null || !currentDisplayControlInfo.HasCurrentDensity)
            {
                if (!preserveFocusedInput || !displayDensityValueTextBox.Focused) displayDensityValueTextBox.Clear();
                return;
            }
            if (preserveFocusedInput && displayDensityValueTextBox.Focused) return;
            if (GetSelectedDisplayDensityUnit() == "dp")
            {
                if (!currentDisplayControlInfo.HasCurrentSize)
                {
                    displayDensityValueTextBox.Clear();
                    return;
                }
                displayDensityValueTextBox.Text = CalculateSmallestWidthDp(currentDisplayControlInfo.CurrentWidth, currentDisplayControlInfo.CurrentHeight, currentDisplayControlInfo.CurrentDensity).ToString();
            }
            else
            {
                displayDensityValueTextBox.Text = currentDisplayControlInfo.CurrentDensity.ToString();
            }
        }

        private void FailDisplayControlOperation(string message)
        {
            AddLogLine(message);
            BeginInvokeIfNeeded(delegate { displayControlStatusLabel.Text = message; });
            SetStatus(message);
        }

        private bool TryReadPositiveInt(TextBox textBox, string fieldName, out int value)
        {
            value = 0;
            var text = textBox.Text.Trim();
            if (!int.TryParse(text, out value) || value <= 0)
            {
                MessageBox.Show(this, fieldName + "必须填写正整数。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        private string GetSelectedDisplayDensityUnit()
        {
            var item = displayDensityUnitComboBox.SelectedItem as string;
            return string.Equals(item, "dp", StringComparison.OrdinalIgnoreCase) ? "dp" : "DPI";
        }

        private static bool TryParseDisplaySizeOutput(string output, DisplayControlInfo info)
        {
            if (info == null) return false;
            var physical = Regex.Match(output ?? "", @"Physical\s+size:\s*(?<width>\d+)\s*x\s*(?<height>\d+)", RegexOptions.IgnoreCase);
            if (physical.Success)
            {
                info.PhysicalWidth = int.Parse(physical.Groups["width"].Value);
                info.PhysicalHeight = int.Parse(physical.Groups["height"].Value);
                info.HasPhysicalSize = true;
            }

            var forced = Regex.Match(output ?? "", @"Override\s+size:\s*(?<width>\d+)\s*x\s*(?<height>\d+)", RegexOptions.IgnoreCase);
            if (forced.Success)
            {
                info.OverrideWidth = int.Parse(forced.Groups["width"].Value);
                info.OverrideHeight = int.Parse(forced.Groups["height"].Value);
                info.HasOverrideSize = true;
            }
            return info.HasCurrentSize;
        }

        private static bool TryParseDisplayDensityOutput(string output, DisplayControlInfo info)
        {
            if (info == null) return false;
            var physical = Regex.Match(output ?? "", @"Physical\s+density:\s*(?<density>\d+)", RegexOptions.IgnoreCase);
            if (physical.Success)
            {
                info.PhysicalDensity = int.Parse(physical.Groups["density"].Value);
                info.HasPhysicalDensity = true;
            }

            var forced = Regex.Match(output ?? "", @"Override\s+density:\s*(?<density>\d+)", RegexOptions.IgnoreCase);
            if (forced.Success)
            {
                info.OverrideDensity = int.Parse(forced.Groups["density"].Value);
                info.HasOverrideDensity = true;
            }
            return info.HasCurrentDensity;
        }

        private static int CalculateSmallestWidthDp(int width, int height, int density)
        {
            if (width <= 0 || height <= 0 || density <= 0) return 0;
            return (int)Math.Round(Math.Min(width, height) * 160.0 / density);
        }

        private static int CalculateDensityForSmallestWidth(int width, int height, int smallestWidthDp)
        {
            if (width <= 0 || height <= 0 || smallestWidthDp <= 0) return 0;
            return (int)Math.Round(Math.Min(width, height) * 160.0 / smallestWidthDp);
        }

        private static string FormatDisplaySize(int width, int height)
        {
            return width.ToString() + "x" + height.ToString();
        }

        private static string FormatDisplaySizeSource(DisplayControlInfo info)
        {
            if (info == null) return "";
            if (info.HasPhysicalSize && info.HasOverrideSize)
            {
                return "（物理 " + FormatDisplaySize(info.PhysicalWidth, info.PhysicalHeight) + " px，覆盖 " + FormatDisplaySize(info.OverrideWidth, info.OverrideHeight) + " px）";
            }
            if (info.HasPhysicalSize) return "（物理值）";
            if (info.HasOverrideSize) return "（覆盖值）";
            return "";
        }

        private static string FormatDisplayDensitySource(DisplayControlInfo info)
        {
            if (info == null) return "";
            if (info.HasPhysicalDensity && info.HasOverrideDensity)
            {
                return "（物理 " + info.PhysicalDensity + " dpi，覆盖 " + info.OverrideDensity + " dpi）";
            }
            if (info.HasPhysicalDensity) return "（物理值）";
            if (info.HasOverrideDensity) return "（覆盖值）";
            return "";
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
            if (isLogcatRunning || isExecuting || isDeviceCommandRunning || IsMediaCaptureRunning) return;
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
            if (isLogcatRunning || isExecuting || isDeviceCommandRunning || IsMediaCaptureRunning) return;
            var device = GetSingleCheckedDeviceForLogRecording();
            if (device == null) return;
            RunDeviceCommand("清除日志缓存", new[] { "-s", device.Serial, "logcat", "-c" });
        }

        private void ExportLogcatCache()
        {
            if (isLogcatRunning || isExecuting || isDeviceCommandRunning || IsMediaCaptureRunning) return;
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
            SetLogcatCacheExportStatus(device, outputPath);
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
                        BeginInvokeIfNeeded(delegate { SetLogcatCacheExportCanceledStatus(outputPath, result.WrittenLines); });
                        SetStatus("导出日志缓存已中止。");
                    }
                    else if (result.ExitCode == 0)
                    {
                        var summary = "日志缓存已导出：" + outputPath;
                        AddLogLine(summary);
                        BeginInvokeIfNeeded(delegate { SetLogcatCacheExportFinishedStatus(outputPath, result.WrittenLines); });
                        SetStatus(summary);
                    }
                    else
                    {
                        var error = FirstUsefulLine(result.Output) ?? "导出失败。";
                        AddLogLine("导出日志缓存失败：" + error);
                        BeginInvokeIfNeeded(delegate { SetLogcatCacheExportFailedStatus(error); });
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
                    logRecordStartedAt = DateTime.Now;
                    logRecordCurrentOutputPath = outputPath;
                    logRecordCurrentDeviceLabel = string.IsNullOrWhiteSpace(device.Label) ? device.Serial : device.Label;
                    logRecordWrittenLines = 0;
                    logRecordLastFileSize = 0;
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
                BeginInvokeIfNeeded(delegate
                {
                    UpdateLogRecordStatus();
                    logRecordStatusTimer.Start();
                });
            }
            catch (Exception ex)
            {
                AddLogLine("启动 logcat 失败：" + ex.Message);
                FinishLogcatRecording();
                BeginInvokeIfNeeded(delegate
                {
                    logRecordStatusTimer.Stop();
                    ResetLogRecordStatus();
                });
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
            string outputPath;
            long writtenLines;
            long fileSize;
            lock (logcatLock)
            {
                if (!isLogcatRunning && logcatProcess == null && logcatWriter == null) return;
                isLogcatRunning = false;
                try { if (logcatWriter != null) logcatWriter.Dispose(); } catch { }
                outputPath = logRecordCurrentOutputPath;
                writtenLines = logRecordWrittenLines;
                fileSize = GetLogRecordFileSize(outputPath);
                logcatWriter = null;
                logcatProcess = null;
                logcatPidFilter = null;
                logcatIncludeThreadInfo = true;
                logcatIncludeTimeInfo = true;
                logRecordLastFileSize = fileSize;
            }
            BeginInvokeIfNeeded(delegate
            {
                logRecordStatusTimer.Stop();
                SetLogcatUi(false);
                SetLogRecordFinishedStatus(outputPath, writtenLines, fileSize);
                SetStatus("\u65e5\u5fd7\u5f55\u5236\u5df2\u505c\u6b62\u3002");
                AddLogLine("logcat \u5f55\u5236\u5df2\u505c\u6b62\u3002");
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
                        logRecordWrittenLines++;
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

        private void UpdateLogRecordStatus()
        {
            string deviceLabel;
            string outputPath;
            DateTime startedAt;
            long writtenLines;
            long fileSize;
            lock (logcatLock)
            {
                deviceLabel = logRecordCurrentDeviceLabel;
                outputPath = logRecordCurrentOutputPath;
                startedAt = logRecordStartedAt;
                writtenLines = logRecordWrittenLines;
                fileSize = logRecordLastFileSize;
            }

            if (string.IsNullOrWhiteSpace(outputPath) || startedAt == DateTime.MinValue)
            {
                logRecordStatusLabel.Text = "\u672a\u5f00\u59cb\u5f55\u5236";
                return;
            }

            fileSize = GetLogRecordFileSize(outputPath);
            lock (logcatLock) logRecordLastFileSize = fileSize;

            var duration = DateTime.Now - startedAt;
            var fileName = Path.GetFileName(outputPath);
            logRecordStatusLabel.Text =
                "\u8bbe\u5907\uff1a" + deviceLabel +
                "    \u65f6\u957f\uff1a" + FormatDuration(duration) +
                "    \u5927\u5c0f\uff1a" + FormatFileSize(fileSize) +
                "    \u884c\u6570\uff1a" + writtenLines +
                "    \u6587\u4ef6\uff1a" + fileName;
        }

        private void SetLogRecordFinishedStatus(string outputPath, long writtenLines, long fileSize)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                logRecordStatusLabel.Text = "\u672a\u5f00\u59cb\u5f55\u5236";
                return;
            }
            logRecordStatusLabel.Text =
                "\u5f55\u5236\u5df2\u505c\u6b62\uff1a" + writtenLines +
                " \u884c\uff0c" + FormatFileSize(fileSize) +
                "\uff0c\u4fdd\u5b58\u5230 " + outputPath;
        }

        private void SetLogcatCacheExportStatus(DeviceInfo device, string outputPath)
        {
            var deviceLabel = device == null || string.IsNullOrWhiteSpace(device.Label) ? "" : device.Label;
            if (device != null && string.IsNullOrWhiteSpace(deviceLabel)) deviceLabel = device.Serial;
            logRecordStatusLabel.Text =
                "\u6b63\u5728\u5bfc\u51fa\u7f13\u5b58\uff1a\u8bbe\u5907 " + deviceLabel +
                "\uff0c\u6587\u4ef6 " + Path.GetFileName(outputPath);
        }

        private void SetLogcatCacheExportFinishedStatus(string outputPath, long writtenLines)
        {
            var fileSize = GetLogRecordFileSize(outputPath);
            logRecordStatusLabel.Text =
                "\u7f13\u5b58\u5df2\u5bfc\u51fa\uff1a" + writtenLines +
                " \u884c\uff0c" + FormatFileSize(fileSize) +
                "\uff0c\u4fdd\u5b58\u5230 " + outputPath;
        }

        private void SetLogcatCacheExportCanceledStatus(string outputPath, long writtenLines)
        {
            var fileSize = GetLogRecordFileSize(outputPath);
            logRecordStatusLabel.Text =
                "\u5bfc\u51fa\u5df2\u4e2d\u6b62\uff1a\u5df2\u5199\u5165 " + writtenLines +
                " \u884c\uff0c" + FormatFileSize(fileSize) +
                "\uff0c\u4fdd\u5b58\u5230 " + outputPath;
        }

        private void SetLogcatCacheExportFailedStatus(string error)
        {
            logRecordStatusLabel.Text = "\u5bfc\u51fa\u7f13\u5b58\u5931\u8d25\uff1a" + error;
        }

        private void ResetLogRecordStatus()
        {
            lock (logcatLock)
            {
                logRecordStartedAt = DateTime.MinValue;
                logRecordCurrentOutputPath = "";
                logRecordCurrentDeviceLabel = "";
                logRecordWrittenLines = 0;
                logRecordLastFileSize = 0;
            }
            logRecordStatusLabel.Text = "\u672a\u5f00\u59cb\u5f55\u5236";
        }

        private long GetLogRecordFileSize(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath)) return logRecordLastFileSize;
            try
            {
                if (File.Exists(outputPath)) return new FileInfo(outputPath).Length;
            }
            catch { }
            return logRecordLastFileSize;
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
            return ((int)duration.TotalHours).ToString("00") + ":" +
                   duration.Minutes.ToString("00") + ":" +
                   duration.Seconds.ToString("00");
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 0) bytes = 0;
            string[] units = { "B", "KB", "MB", "GB" };
            double value = bytes;
            var unitIndex = 0;
            while (value >= 1024 && unitIndex < units.Length - 1)
            {
                value /= 1024;
                unitIndex++;
            }
            return unitIndex == 0 ? bytes + " " + units[unitIndex] : value.ToString("0.0") + " " + units[unitIndex];
        }

        private bool HasLatestCaptureFile()
        {
            return !string.IsNullOrWhiteSpace(lastCapturePath) && File.Exists(lastCapturePath);
        }

        private void UpdateCaptureActionButtons(bool busy)
        {
            saveScreenshotAsButton.Enabled = !busy && HasLatestCaptureFile();
            openScreenshotDirButton.Enabled = !busy;
        }

        private void UpdateScreenRecordOptionState()
        {
            var busy = isExecuting || isDeviceCommandRunning || isLogcatRunning || IsMediaCaptureRunning;
            SetScreenRecordOptionControlsEnabled(busy);
        }

        private void SetScreenRecordOptionControlsEnabled(bool busy)
        {
            screenRecordTimeLimitCheckBox.Enabled = !busy;
            screenRecordTimeLimitNumeric.Enabled = !busy && screenRecordTimeLimitCheckBox.Checked;
            screenRecordBitRateNumeric.Enabled = !busy;
        }

        private void SetDisplayControlControlsEnabled(bool enabled)
        {
            refreshDisplayInfoButton.Enabled = enabled;
            restoreDisplayAllButton.Enabled = enabled;
            displayWidthTextBox.Enabled = enabled;
            displayHeightTextBox.Enabled = enabled;
            applyResolutionButton.Enabled = enabled;
            restoreResolutionButton.Enabled = enabled;
            displayDensityValueTextBox.Enabled = enabled;
            displayDensityUnitComboBox.Enabled = enabled;
            applyDensityButton.Enabled = enabled;
            restoreDensityButton.Enabled = enabled;
        }

        private void SetLogcatUi(bool running)
        {
            var busy = running || isExecuting || isDeviceCommandRunning || IsMediaCaptureRunning;
            browseButton.Enabled = !busy;
            connectButton.Enabled = !busy;
            disconnectButton.Enabled = !busy;
            connectAddressTextBox.Enabled = !busy;
            installButton.Enabled = !busy;
            installModeRadioButton.Enabled = !busy;
            cleanInstallModeRadioButton.Enabled = !busy;
            uninstallModeRadioButton.Enabled = !busy;
            clearDataModeRadioButton.Enabled = !busy;
            startAppModeRadioButton.Enabled = !busy;
            apkTextBox.Enabled = !busy;
            launchAfterInstallCheckBox.Enabled = !busy && !uninstallModeRadioButton.Checked && !clearDataModeRadioButton.Checked && !startAppModeRadioButton.Checked;
            clearLogcatCacheButton.Enabled = !busy;
            exportLogcatCacheButton.Enabled = !busy;
            startLogRecordButton.Enabled = !busy;
            stopLogRecordButton.Enabled = running;
            browseLogRecordFileButton.Enabled = !busy;
            browseLogRecordFolderButton.Enabled = !busy;
            logRecordPathTextBox.Enabled = !busy;
            logRecordTagTextBox.Enabled = !busy;
            logRecordPackageTextBox.Enabled = !busy;
            logRecordLevelComboBox.Enabled = !busy;
            logRecordThreadInfoCheckBox.Enabled = !busy;
            logRecordTimeInfoCheckBox.Enabled = !busy;
            transferPathTextBox.Enabled = !busy;
            transferTargetDirTextBox.Enabled = !busy;
            transferToDeviceRadioButton.Enabled = !busy;
            transferToComputerRadioButton.Enabled = !busy;
            UpdateTransferBrowseButtons();
            sendTransferButton.Enabled = !busy;
            screenshotOutputDirTextBox.Enabled = !busy;
            browseScreenshotOutputDirButton.Enabled = !busy;
            takeScreenshotButton.Enabled = !busy;
            startScreenRecordButton.Enabled = !busy;
            stopScreenRecordButton.Enabled = isScreenRecordRunning;
            SetScreenRecordOptionControlsEnabled(busy);
            UpdateCaptureActionButtons(busy);
            SetDisplayControlControlsEnabled(!busy);
            refreshButton.Enabled = !busy;
            deviceList.Enabled = !busy;
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

        private DeviceInfo GetSingleCheckedDeviceForScreenshot()
        {
            return GetSingleCheckedDeviceForMediaCapture("截屏");
        }

        private DeviceInfo GetSingleCheckedDeviceForScreenRecording()
        {
            return GetSingleCheckedDeviceForMediaCapture("录屏");
        }

        private DeviceInfo GetSingleCheckedDeviceForMediaCapture(string actionName)
        {
            var checkedItems = deviceList.CheckedItems.Cast<object>().Select(o => o.ToString()).ToList();
            if (checkedItems.Count == 0)
            {
                MessageBox.Show(this, "请先在目标设备列表中选择一台设备。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            if (checkedItems.Count > 1)
            {
                MessageBox.Show(this, actionName + "一次只能选择一台设备。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        private void BrowseScreenshotOutputDir()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择截屏/录屏保存目录";
                var currentDir = ResolveScreenshotOutputDir(screenshotOutputDirTextBox.Text);
                if (!string.IsNullOrWhiteSpace(currentDir) && Directory.Exists(currentDir)) dialog.SelectedPath = currentDir;
                if (dialog.ShowDialog(this) == DialogResult.OK) screenshotOutputDirTextBox.Text = dialog.SelectedPath;
            }
        }

        private void StartScreenshot()
        {
            if (isExecuting || isDeviceCommandRunning || isLogcatRunning || IsMediaCaptureRunning) return;
            var device = GetSingleCheckedDeviceForScreenshot();
            if (device == null) return;
            var outputDir = PrepareScreenshotOutputDir();
            if (outputDir == null) return;
            var adb = FindAdb();
            if (adb == null)
            {
                MessageBox.Show(this, "未找到 adb.exe。请安装 Android SDK Platform Tools，或把 adb.exe 加入 PATH。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var outputPath = Path.Combine(outputDir, GenerateScreenshotFileName(device.Serial));
            SaveConfig();
            cancelRequested = false;
            isScreenshotRunning = true;
            SetScreenshotUi(true);
            var thread = new Thread(new ThreadStart(delegate { ExecuteScreenshot(adb, device, outputPath); }));
            thread.IsBackground = true;
            thread.Start();
        }

        private void ExecuteScreenshot(string adb, DeviceInfo device, string outputPath)
        {
            var success = false;
            try
            {
                var deviceLabel = string.IsNullOrWhiteSpace(device.Label) ? device.Serial : device.Label;
                AddLogLine("开始截屏：" + deviceLabel);
                success = TryCaptureScreenshotExecOut(adb, device.Serial, outputPath);
                if (!success && !cancelRequested)
                {
                    AddLogLine("exec-out 截屏失败，尝试兼容模式...");
                    success = TryCaptureScreenshotFallback(adb, device.Serial, outputPath);
                }

                if (cancelRequested)
                {
                    SafeDeleteFile(outputPath);
                    AddLogLine("截屏已中止。");
                    SetStatus("截屏已中止");
                    return;
                }

                if (success && LoadScreenshotPreview(outputPath, "截屏成功"))
                {
                    AddLogLine("截屏保存成功：" + outputPath);
                    SetStatus("截屏完成");
                }
                else
                {
                    SafeDeleteFile(outputPath);
                    AddLogLine("截屏失败：未生成有效 PNG。");
                    SetStatus("截屏失败");
                }
                SaveRunLog();
            }
            catch (Exception ex)
            {
                SafeDeleteFile(outputPath);
                AddLogLine("截屏异常：" + ex.Message);
                SetStatus("截屏异常");
            }
            finally
            {
                isScreenshotRunning = false;
                cancelRequested = false;
                ClearCurrentProcess();
                BeginInvokeIfNeeded(delegate { SetScreenshotUi(false); });
            }
        }

        private void StartScreenRecording()
        {
            if (isExecuting || isDeviceCommandRunning || isLogcatRunning || IsMediaCaptureRunning) return;
            var device = GetSingleCheckedDeviceForScreenRecording();
            if (device == null) return;
            var outputDir = PrepareScreenRecordOutputDir();
            if (outputDir == null) return;
            var adb = FindAdb();
            if (adb == null)
            {
                MessageBox.Show(this, "未找到 adb.exe。请安装 Android SDK Platform Tools，或把 adb.exe 加入 PATH。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var options = GetScreenRecordOptions();
            var outputPath = Path.Combine(outputDir, GenerateScreenRecordFileName(device.Serial));
            var remotePath = GenerateScreenRecordRemotePath(device.Serial);
            var previewPath = Path.Combine(Path.GetTempPath(), GenerateScreenRecordPreviewFileName(device.Serial));
            var deviceLabel = string.IsNullOrWhiteSpace(device.Label) ? device.Serial : device.Label;

            SaveConfig();
            cancelRequested = false;
            screenRecordStopRequested = false;
            isScreenRecordRunning = true;
            screenRecordStartedAt = DateTime.Now;
            screenRecordCurrentSerial = device.Serial;
            screenRecordCurrentDeviceLabel = deviceLabel;
            screenRecordCurrentTimeLimitSeconds = options.HasTimeLimit ? options.TimeLimitSeconds : 0;
            SetScreenRecordUi(true);
            screenRecordStatusTimer.Start();

            var thread = new Thread(new ThreadStart(delegate { ExecuteScreenRecording(adb, device, outputPath, remotePath, previewPath, options); }));
            thread.IsBackground = true;
            thread.Start();
        }

        private void ExecuteScreenRecording(string adb, DeviceInfo device, string outputPath, string remotePath, string previewPath, ScreenRecordOptions options)
        {
            try
            {
                var deviceLabel = string.IsNullOrWhiteSpace(device.Label) ? device.Serial : device.Label;
                SafeDeleteFile(outputPath);
                SafeDeleteFile(previewPath);
                SafeDeleteRemoteFile(adb, device.Serial, remotePath);
                AddLogLine("开始录屏：" + deviceLabel + "，" + FormatScreenRecordOptionSummary(options));
                AddLogLine("正在生成录屏预览帧...");
                var hasPreviewFrame = TryCaptureScreenshotExecOut(adb, device.Serial, previewPath);
                if (!hasPreviewFrame) AddLogLine("录屏预览帧生成失败，录屏仍将继续。");
                BeginInvokeIfNeeded(delegate { UpdateScreenRecordStatus(); });

                var result = InvokeProcess(adb, BuildScreenRecordArgs(device.Serial, remotePath, options), true);
                var stoppedByUser = screenRecordStopRequested;
                if (cancelRequested && !stoppedByUser)
                {
                    AddLogLine("录屏已中止。");
                    BeginInvokeIfNeeded(delegate { screenshotStatusLabel.Text = "录屏已中止。"; });
                    SetStatus("录屏已中止");
                    return;
                }

                if (result.ExitCode != 0 && !stoppedByUser)
                {
                    AddLogLine("录屏失败：" + HumanizeAdbOutput(result.Output));
                    BeginInvokeIfNeeded(delegate { screenshotStatusLabel.Text = "录屏失败：" + HumanizeAdbOutput(result.Output); });
                    SetStatus("录屏失败");
                    return;
                }

                BeginInvokeIfNeeded(delegate { screenshotStatusLabel.Text = "正在保存录屏到本机..."; });
                AddLogLine("录屏结束，正在拉取文件：" + remotePath);
                if (!TryPullScreenRecord(adb, device.Serial, remotePath, outputPath))
                {
                    BeginInvokeIfNeeded(delegate { screenshotStatusLabel.Text = "录屏保存失败，请查看运行日志。"; });
                    SetStatus("录屏保存失败");
                    return;
                }

                if (!IsValidMp4File(outputPath))
                {
                    SafeDeleteFile(outputPath);
                    AddLogLine("录屏失败：未生成有效 MP4。");
                    BeginInvokeIfNeeded(delegate { screenshotStatusLabel.Text = "录屏失败：未生成有效 MP4。"; });
                    SetStatus("录屏失败");
                    return;
                }

                var elapsed = DateTime.Now - screenRecordStartedAt;
                var size = new FileInfo(outputPath).Length;
                var status = "录屏完成（视频预览帧）：" + FormatDuration(elapsed) + "，" + FormatFileSize(size) + "，" + outputPath;
                if (hasPreviewFrame && LoadScreenRecordPreview(outputPath, previewPath, status))
                {
                    AddLogLine("录屏保存成功：" + outputPath);
                }
                else
                {
                    BeginInvokeIfNeeded(delegate
                    {
                        lastCapturePath = outputPath;
                        lastCaptureType = CaptureMediaType.ScreenRecord;
                        screenshotStatusLabel.Text = "录屏完成（未生成预览帧）：" + FormatDuration(elapsed) + "，" + FormatFileSize(size) + "，" + outputPath;
                        UpdateCaptureActionButtons(IsMediaCaptureRunning);
                    });
                    AddLogLine("录屏保存成功：" + outputPath);
                }
                SetStatus("录屏完成");
            }
            catch (Exception ex)
            {
                SafeDeleteFile(outputPath);
                AddLogLine("录屏异常：" + ex.Message);
                BeginInvokeIfNeeded(delegate { screenshotStatusLabel.Text = "录屏异常：" + ex.Message; });
                SetStatus("录屏异常");
            }
            finally
            {
                SafeDeleteFile(previewPath);
                SafeDeleteRemoteFile(adb, device.Serial, remotePath);
                isScreenRecordRunning = false;
                screenRecordStopRequested = false;
                cancelRequested = false;
                screenRecordCurrentSerial = "";
                screenRecordCurrentDeviceLabel = "";
                screenRecordCurrentTimeLimitSeconds = 0;
                ClearCurrentProcess();
                SaveRunLog();
                BeginInvokeIfNeeded(delegate
                {
                    screenRecordStatusTimer.Stop();
                    SetScreenRecordUi(false);
                });
            }
        }

        private bool TryPullScreenRecord(string adb, string serial, string remotePath, string outputPath)
        {
            SafeDeleteFile(outputPath);
            var result = InvokeProcess(adb, new[] { "-s", serial, "pull", remotePath, outputPath }, true);
            if (cancelRequested) return false;
            if (result.ExitCode != 0)
            {
                SafeDeleteFile(outputPath);
                AddLogLine("拉取录屏失败：" + HumanizeAdbOutput(result.Output));
                return false;
            }
            return true;
        }

        private ScreenRecordOptions GetScreenRecordOptions()
        {
            var options = new ScreenRecordOptions();
            options.HasTimeLimit = screenRecordTimeLimitCheckBox.Checked;
            options.TimeLimitSeconds = (int)screenRecordTimeLimitNumeric.Value;
            options.BitRate = (int)screenRecordBitRateNumeric.Value * 1000 * 1000;
            return options;
        }

        private string[] BuildScreenRecordArgs(string serial, string remotePath, ScreenRecordOptions options)
        {
            var args = new List<string>();
            args.Add("-s");
            args.Add(serial);
            args.Add("shell");
            args.Add("screenrecord");
            if (options.HasTimeLimit)
            {
                args.Add("--time-limit");
                args.Add(options.TimeLimitSeconds.ToString());
            }
            args.Add("--bit-rate");
            args.Add(options.BitRate.ToString());
            args.Add(ShellQuote(remotePath));
            return args.ToArray();
        }

        private string FormatScreenRecordOptionSummary(ScreenRecordOptions options)
        {
            var parts = new List<string>();
            parts.Add(options.HasTimeLimit ? "上限 " + FormatDuration(TimeSpan.FromSeconds(options.TimeLimitSeconds)) : "未显式设置时长上限");
            parts.Add("码率 " + (options.BitRate / 1000000) + " Mbps");
            return string.Join("，", parts.ToArray());
        }

        private void StopScreenRecording()
        {
            if (!isScreenRecordRunning) return;
            if (screenRecordStopRequested) return;
            screenRecordStopRequested = true;
            stopScreenRecordButton.Enabled = false;
            screenshotStatusLabel.Text = "正在停止录屏并保存...";
            statusLabel.Text = "正在停止录屏...";
            AddLogLine("收到停止录屏请求，正在结束设备端 screenrecord...");

            var adb = FindAdb();
            var serial = screenRecordCurrentSerial;
            var thread = new Thread(new ThreadStart(delegate
            {
                var signaled = false;
                if (!string.IsNullOrWhiteSpace(adb) && !string.IsNullOrWhiteSpace(serial))
                {
                    signaled = SendScreenRecordStopSignal(adb, serial);
                }

                var waitUntil = DateTime.Now.AddSeconds(signaled ? 4 : 1);
                while (isScreenRecordRunning && DateTime.Now < waitUntil) Thread.Sleep(150);
                if (isScreenRecordRunning)
                {
                    AddLogLine("录屏未及时停止，结束当前 adb 进程...");
                    KillCurrentProcess();
                }
            }));
            thread.IsBackground = true;
            thread.Start();
        }

        private bool SendScreenRecordStopSignal(string adb, string serial)
        {
            var script = "PID=$(pidof screenrecord 2>/dev/null); if [ -n \"$PID\" ]; then kill -2 $PID; exit 0; fi; pkill -2 screenrecord 2>/dev/null && exit 0; killall -2 screenrecord 2>/dev/null";
            var result = InvokeProcessSilent(adb, new[] { "-s", serial, "shell", "sh", "-c", script });
            if (result.ExitCode == 0) return true;
            AddLogLine("发送录屏停止信号失败，准备回退结束 adb 进程：" + HumanizeAdbOutput(result.Output));
            return false;
        }

        private void UpdateScreenRecordStatus()
        {
            if (!isScreenRecordRunning) return;
            var elapsed = DateTime.Now - screenRecordStartedAt;
            var deviceLabel = string.IsNullOrWhiteSpace(screenRecordCurrentDeviceLabel) ? "目标设备" : screenRecordCurrentDeviceLabel;
            var prefix = screenRecordStopRequested ? "正在停止录屏" : "正在录屏";
            var limitText = screenRecordCurrentTimeLimitSeconds > 0 ? " / " + FormatDuration(TimeSpan.FromSeconds(screenRecordCurrentTimeLimitSeconds)) : "";
            screenshotStatusLabel.Text = prefix + "：" + FormatDuration(elapsed) + limitText + "，" + deviceLabel;
            statusLabel.Text = prefix + "：" + FormatDuration(elapsed);
        }

        private void SetScreenRecordUi(bool running)
        {
            var busy = running || isExecuting || isDeviceCommandRunning || isLogcatRunning || isScreenshotRunning;
            browseButton.Enabled = !busy;
            refreshButton.Enabled = !busy;
            connectButton.Enabled = !busy;
            disconnectButton.Enabled = !busy;
            connectAddressTextBox.Enabled = !busy;
            installButton.Enabled = !busy;
            clearLogButton.Enabled = !busy;
            installModeRadioButton.Enabled = !busy;
            cleanInstallModeRadioButton.Enabled = !busy;
            uninstallModeRadioButton.Enabled = !busy;
            clearDataModeRadioButton.Enabled = !busy;
            startAppModeRadioButton.Enabled = !busy;
            apkTextBox.Enabled = !busy;
            deviceList.Enabled = !busy;
            launchAfterInstallCheckBox.Enabled = !busy && !uninstallModeRadioButton.Checked && !clearDataModeRadioButton.Checked && !startAppModeRadioButton.Checked;
            clearLogcatCacheButton.Enabled = !busy;
            exportLogcatCacheButton.Enabled = !busy;
            startLogRecordButton.Enabled = !busy;
            stopLogRecordButton.Enabled = isLogcatRunning && !running && !isExecuting && !isDeviceCommandRunning;
            logRecordPathTextBox.Enabled = !busy;
            logRecordTagTextBox.Enabled = !busy;
            logRecordPackageTextBox.Enabled = !busy;
            logRecordLevelComboBox.Enabled = !busy;
            logRecordThreadInfoCheckBox.Enabled = !busy;
            logRecordTimeInfoCheckBox.Enabled = !busy;
            browseLogRecordFileButton.Enabled = !busy;
            browseLogRecordFolderButton.Enabled = !busy;
            transferPathTextBox.Enabled = !busy;
            transferTargetDirTextBox.Enabled = !busy;
            transferToDeviceRadioButton.Enabled = !busy;
            transferToComputerRadioButton.Enabled = !busy;
            UpdateTransferBrowseButtons();
            sendTransferButton.Enabled = !busy;
            screenshotOutputDirTextBox.Enabled = !busy;
            browseScreenshotOutputDirButton.Enabled = !busy;
            takeScreenshotButton.Enabled = !busy;
            startScreenRecordButton.Enabled = !busy && !running;
            stopScreenRecordButton.Enabled = running && !screenRecordStopRequested;
            SetScreenRecordOptionControlsEnabled(busy);
            UpdateCaptureActionButtons(busy);
            SetDisplayControlControlsEnabled(!busy);
            cancelButton.Enabled = running || isExecuting || isDeviceCommandRunning || isScreenshotRunning;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            if (running) UpdateScreenRecordStatus();
        }

        private bool TryCaptureScreenshotExecOut(string adb, string serial, string outputPath)
        {
            var result = InvokeProcessBinaryToFile(adb, new[] { "-s", serial, "exec-out", "screencap", "-p" }, outputPath, true);
            if (cancelRequested) return false;
            if (result.ExitCode != 0)
            {
                AddLogLine("exec-out 截屏失败：" + HumanizeAdbOutput(result.Output));
                SafeDeleteFile(outputPath);
                return false;
            }
            if (!IsValidPngFile(outputPath))
            {
                AddLogLine("exec-out 输出不是有效 PNG。");
                SafeDeleteFile(outputPath);
                return false;
            }
            return true;
        }

        private bool TryCaptureScreenshotFallback(string adb, string serial, string outputPath)
        {
            var remotePath = "/data/local/tmp/adb-tools-screenshot-" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + "-" + SanitizeLocalFileName(serial) + ".png";
            try
            {
                var captureResult = InvokeProcess(adb, new[] { "-s", serial, "shell", "screencap", "-p", ShellQuote(remotePath) }, true);
                if (cancelRequested) return false;
                if (captureResult.ExitCode != 0)
                {
                    AddLogLine("兼容模式截屏失败：" + HumanizeAdbOutput(captureResult.Output));
                    return false;
                }

                var pullResult = InvokeProcess(adb, new[] { "-s", serial, "pull", remotePath, outputPath }, true);
                if (cancelRequested) return false;
                if (pullResult.ExitCode != 0)
                {
                    AddLogLine("拉取截屏失败：" + HumanizeAdbOutput(pullResult.Output));
                    SafeDeleteFile(outputPath);
                    return false;
                }
                if (!IsValidPngFile(outputPath))
                {
                    AddLogLine("兼容模式输出不是有效 PNG。");
                    SafeDeleteFile(outputPath);
                    return false;
                }
                return true;
            }
            finally
            {
                InvokeProcessSilent(adb, new[] { "-s", serial, "shell", "rm", "-f", ShellQuote(remotePath) });
            }
        }

        private string PrepareScreenshotOutputDir()
        {
            return PrepareCaptureOutputDir("截屏");
        }

        private string PrepareScreenRecordOutputDir()
        {
            return PrepareCaptureOutputDir("录屏");
        }

        private string PrepareCaptureOutputDir(string actionName)
        {
            var outputDir = ResolveScreenshotOutputDir(screenshotOutputDirTextBox.Text);
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                MessageBox.Show(this, "请输入" + actionName + "保存目录。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            if (File.Exists(outputDir))
            {
                MessageBox.Show(this, actionName + "保存目录不能是文件。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            try
            {
                Directory.CreateDirectory(outputDir);
                screenshotOutputDirTextBox.Text = outputDir;
                return outputDir;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法创建" + actionName + "保存目录：" + ex.Message, "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        private string ResolveScreenshotOutputDir(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return screenshotDir;
            path = path.Trim().Trim('"');
            return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(appDir, path));
        }

        private static string GenerateScreenshotFileName(string serial)
        {
            return "screenshot-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + SanitizeLocalFileName(serial) + ".png";
        }

        private static string GenerateScreenRecordFileName(string serial)
        {
            return "screenrecord-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + SanitizeLocalFileName(serial) + ".mp4";
        }

        private static string GenerateScreenRecordRemotePath(string serial)
        {
            return "/data/local/tmp/adb-tools-screenrecord-" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + "-" + SanitizeLocalFileName(serial) + ".mp4";
        }

        private static string GenerateScreenRecordPreviewFileName(string serial)
        {
            return "screenrecord-preview-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + SanitizeLocalFileName(serial) + ".png";
        }

        private bool LoadScreenshotPreview(string path, string statusPrefix)
        {
            try
            {
                if (!File.Exists(path)) return false;
                var size = new FileInfo(path).Length;
                int width;
                int height;
                Bitmap previewImage;
                using (var image = Image.FromFile(path))
                {
                    width = image.Width;
                    height = image.Height;
                    previewImage = new Bitmap(image);
                }

                var status = statusPrefix + "：" + width + " x " + height + "，" + FormatFileSize(size) + "，" + path;
                BeginInvokeIfNeeded(delegate
                {
                    var oldImage = screenshotPreviewBox.Image;
                    screenshotPreviewBox.Image = previewImage;
                    if (oldImage != null) oldImage.Dispose();
                    lastCapturePath = path;
                    lastCaptureType = CaptureMediaType.Screenshot;
                    screenshotStatusLabel.Text = status;
                    UpdateCaptureActionButtons(IsMediaCaptureRunning);
                });
                return true;
            }
            catch (Exception ex)
            {
                AddLogLine("加载截屏预览失败：" + ex.Message);
                return false;
            }
        }

        private bool LoadScreenRecordPreview(string videoPath, string previewImagePath, string status)
        {
            try
            {
                if (!File.Exists(videoPath) || !File.Exists(previewImagePath)) return false;
                Bitmap previewImage;
                using (var image = Image.FromFile(previewImagePath))
                {
                    previewImage = new Bitmap(image);
                }
                DrawVideoPreviewBadge(previewImage);

                BeginInvokeIfNeeded(delegate
                {
                    var oldImage = screenshotPreviewBox.Image;
                    screenshotPreviewBox.Image = previewImage;
                    if (oldImage != null) oldImage.Dispose();
                    lastCapturePath = videoPath;
                    lastCaptureType = CaptureMediaType.ScreenRecord;
                    screenshotStatusLabel.Text = status;
                    UpdateCaptureActionButtons(IsMediaCaptureRunning);
                });
                return true;
            }
            catch (Exception ex)
            {
                AddLogLine("加载录屏预览帧失败：" + ex.Message);
                return false;
            }
        }

        private void DrawVideoPreviewBadge(Bitmap image)
        {
            using (var graphics = Graphics.FromImage(image))
            using (var font = new Font(Font.FontFamily, 11F, FontStyle.Bold))
            using (var backgroundBrush = new SolidBrush(Color.FromArgb(190, 0, 0, 0)))
            using (var textBrush = new SolidBrush(Color.White))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var text = "视频预览帧";
                var textSize = graphics.MeasureString(text, font);
                var width = (int)Math.Ceiling(textSize.Width) + 42;
                var height = (int)Math.Ceiling(textSize.Height) + 12;
                var x = Math.Max(8, image.Width - width - 16);
                var y = Math.Max(8, image.Height - height - 16);
                graphics.FillRectangle(backgroundBrush, x, y, width, height);

                var playX = x + 12;
                var playY = y + height / 2;
                var points = new[]
                {
                    new Point(playX, playY - 8),
                    new Point(playX, playY + 8),
                    new Point(playX + 13, playY)
                };
                graphics.FillPolygon(textBrush, points);
                graphics.DrawString(text, font, textBrush, x + 30, y + 6);
            }
        }

        private void SaveLatestCaptureAs()
        {
            if (!HasLatestCaptureFile())
            {
                MessageBox.Show(this, "当前没有可另存的截屏或录屏。", "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "另存为";
                dialog.Filter = lastCaptureType == CaptureMediaType.ScreenRecord ? "MP4 视频 (*.mp4)|*.mp4|所有文件 (*.*)|*.*" : "PNG 图片 (*.png)|*.png|所有文件 (*.*)|*.*";
                dialog.FileName = Path.GetFileName(lastCapturePath);
                var currentDir = Path.GetDirectoryName(lastCapturePath);
                if (!string.IsNullOrEmpty(currentDir) && Directory.Exists(currentDir)) dialog.InitialDirectory = currentDir;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var savedType = lastCaptureType;
                    File.Copy(lastCapturePath, dialog.FileName, true);
                    if (savedType == CaptureMediaType.Screenshot)
                    {
                        LoadScreenshotPreview(dialog.FileName, "截屏已另存");
                    }
                    else
                    {
                        lastCapturePath = dialog.FileName;
                        lastCaptureType = CaptureMediaType.ScreenRecord;
                        screenshotStatusLabel.Text = "录屏已另存（视频预览帧）：" + FormatFileSize(new FileInfo(dialog.FileName).Length) + "，" + dialog.FileName;
                        UpdateCaptureActionButtons(IsMediaCaptureRunning);
                    }
                    AddLogLine("另存为：" + dialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "另存失败：" + ex.Message, "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OpenScreenshotDirectory()
        {
            try
            {
                if (HasLatestCaptureFile())
                {
                    Process.Start("explorer.exe", "/select,\"" + lastCapturePath + "\"");
                    return;
                }

                var outputDir = ResolveScreenshotOutputDir(screenshotOutputDirTextBox.Text);
                if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
                Process.Start("explorer.exe", outputDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "打开截屏目录失败：" + ex.Message, "APK安装工具", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static bool IsValidPngFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                var bytes = new byte[8];
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Read(bytes, 0, bytes.Length) != bytes.Length) return false;
                }
                return bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
                       bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;
            }
            catch { return false; }
        }

        private static bool IsValidMp4File(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                var info = new FileInfo(path);
                if (info.Length < 12) return false;
                var bufferLength = (int)Math.Min(128, info.Length);
                var bytes = new byte[bufferLength];
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Read(bytes, 0, bytes.Length) != bytes.Length) return false;
                }
                for (var i = 0; i <= bytes.Length - 4; i++)
                {
                    if (bytes[i] == 0x66 && bytes[i + 1] == 0x74 && bytes[i + 2] == 0x79 && bytes[i + 3] == 0x70) return true;
                }
                return false;
            }
            catch { return false; }
        }

        private static void SafeDeleteFile(string path)
        {
            try { if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) File.Delete(path); } catch { }
        }

        private void SafeDeleteRemoteFile(string adb, string serial, string remotePath)
        {
            if (string.IsNullOrWhiteSpace(adb) || string.IsNullOrWhiteSpace(serial) || string.IsNullOrWhiteSpace(remotePath)) return;
            InvokeProcessSilent(adb, new[] { "-s", serial, "shell", "rm", "-f", ShellQuote(remotePath) });
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

        private void OnDeviceListSelectionChanged()
        {
            BeginSyncAddressFromCurrentDevice();
            BeginDisplayControlAutoRefresh();
            BeginDeviceInfoAutoRefresh(false);
        }

        private void OnDeviceListMouseUp(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            var index = deviceList.IndexFromPoint(e.Location);
            if (index < 0 || index >= deviceList.Items.Count) return;
            if (deviceList.SelectedIndex != index) deviceList.SelectedIndex = index;
            BeginSyncAddressFromCurrentDevice();
            BeginDisplayControlAutoRefresh();
            BeginDeviceInfoAutoRefresh(true);
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
            if (isExecuting || isDeviceCommandRunning || isLogcatRunning || IsMediaCaptureRunning) return;
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
            var busy = running || isExecuting || isLogcatRunning || IsMediaCaptureRunning;
            browseButton.Enabled = !busy;
            connectButton.Enabled = !busy;
            disconnectButton.Enabled = !busy;
            refreshButton.Enabled = !busy;
            connectAddressTextBox.Enabled = !busy;
            cancelButton.Enabled = running || isExecuting || IsMediaCaptureRunning;
            installButton.Enabled = !busy;
            installModeRadioButton.Enabled = !busy;
            cleanInstallModeRadioButton.Enabled = !busy;
            uninstallModeRadioButton.Enabled = !busy;
            clearDataModeRadioButton.Enabled = !busy;
            startAppModeRadioButton.Enabled = !busy;
            apkTextBox.Enabled = !busy;
            launchAfterInstallCheckBox.Enabled = !busy && !uninstallModeRadioButton.Checked && !clearDataModeRadioButton.Checked && !startAppModeRadioButton.Checked;
            clearLogcatCacheButton.Enabled = !busy;
            exportLogcatCacheButton.Enabled = !busy;
            startLogRecordButton.Enabled = !busy;
            browseLogRecordFileButton.Enabled = !busy;
            browseLogRecordFolderButton.Enabled = !busy;
            logRecordPathTextBox.Enabled = !busy;
            logRecordTagTextBox.Enabled = !busy;
            logRecordPackageTextBox.Enabled = !busy;
            logRecordLevelComboBox.Enabled = !busy;
            logRecordThreadInfoCheckBox.Enabled = !busy;
            logRecordTimeInfoCheckBox.Enabled = !busy;
            transferPathTextBox.Enabled = !busy;
            transferTargetDirTextBox.Enabled = !busy;
            transferToDeviceRadioButton.Enabled = !busy;
            transferToComputerRadioButton.Enabled = !busy;
            UpdateTransferBrowseButtons();
            sendTransferButton.Enabled = !busy;
            screenshotOutputDirTextBox.Enabled = !busy;
            browseScreenshotOutputDirButton.Enabled = !busy;
            takeScreenshotButton.Enabled = !busy;
            startScreenRecordButton.Enabled = !busy;
            stopScreenRecordButton.Enabled = isScreenRecordRunning;
            SetScreenRecordOptionControlsEnabled(busy);
            UpdateCaptureActionButtons(busy);
            SetDisplayControlControlsEnabled(!busy);
            deviceList.Enabled = !busy;
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

                var windowWidth = ReadJsonInt(json, "windowWidth");
                var windowHeight = ReadJsonInt(json, "windowHeight");
                if (windowWidth.HasValue && windowHeight.HasValue) ApplySavedWindowSize(windowWidth.Value, windowHeight.Value);

                var tabAreaHeight = ReadJsonInt(json, "layoutTabHeight");
                if (tabAreaHeight.HasValue && tabAreaHeight.Value > 0) savedTabAreaHeight = tabAreaHeight.Value;

                var deviceAreaHeight = ReadJsonInt(json, "layoutDeviceHeight");
                if (deviceAreaHeight.HasValue && deviceAreaHeight.Value > 0) savedDeviceAreaHeight = deviceAreaHeight.Value;

                var logAreaHeight = ReadJsonInt(json, "layoutLogHeight");
                if (logAreaHeight.HasValue && logAreaHeight.Value > 0) savedLogAreaHeight = logAreaHeight.Value;

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

                var lastTransferPath = ReadJsonString(json, "lastTransferPath");
                if (!string.IsNullOrWhiteSpace(lastTransferPath) && (File.Exists(lastTransferPath) || Directory.Exists(lastTransferPath))) lastPushSourcePath = lastTransferPath;

                var lastTransferTargetDir = ReadJsonString(json, "lastTransferTargetDir");
                if (!string.IsNullOrWhiteSpace(lastTransferTargetDir)) lastPushTargetDir = lastTransferTargetDir;

                var pullSourcePath = ReadJsonString(json, "lastPullSourcePath");
                if (!string.IsNullOrWhiteSpace(pullSourcePath)) lastPullSourcePath = pullSourcePath;

                var pullTargetDir = ReadJsonString(json, "lastPullTargetDir");
                if (!string.IsNullOrWhiteSpace(pullTargetDir)) lastPullTargetDir = pullTargetDir;

                var transferDirection = ReadJsonString(json, "lastTransferDirection");
                transferToComputerRadioButton.Checked = string.Equals(transferDirection, "toComputer", StringComparison.OrdinalIgnoreCase);
                transferToDeviceRadioButton.Checked = !transferToComputerRadioButton.Checked;
                ApplyTransferDirectionUi();

                var screenshotOutputDir = ReadJsonString(json, "screenshotOutputDir");
                if (!string.IsNullOrWhiteSpace(screenshotOutputDir)) screenshotOutputDirTextBox.Text = screenshotOutputDir;

                var screenRecordUseTimeLimit = ReadJsonBool(json, "screenRecordUseTimeLimit");
                if (screenRecordUseTimeLimit.HasValue) screenRecordTimeLimitCheckBox.Checked = screenRecordUseTimeLimit.Value;

                var screenRecordTimeLimit = ReadJsonInt(json, "screenRecordTimeLimitSeconds");
                if (screenRecordTimeLimit.HasValue) screenRecordTimeLimitNumeric.Value = ClampDecimal(screenRecordTimeLimit.Value, screenRecordTimeLimitNumeric.Minimum, screenRecordTimeLimitNumeric.Maximum);

                var screenRecordBitRate = ReadJsonInt(json, "screenRecordBitRateMbps");
                if (screenRecordBitRate.HasValue) screenRecordBitRateNumeric.Value = ClampDecimal(screenRecordBitRate.Value, screenRecordBitRateNumeric.Minimum, screenRecordBitRateNumeric.Maximum);
                UpdateScreenRecordOptionState();
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
                CaptureLayoutHeights();
                StoreTransferFieldsForCurrentDirection();
                var lastApkPath = lastApkPathOverride ?? apkTextBox.Text;
                var windowSize = GetConfigWindowSize();
                var json =
                    "{\r\n" +
                    "    \"lastApkPath\":  \"" + EscapeJsonString(lastApkPath) + "\",\r\n" +
                    "    \"windowWidth\":  " + windowSize.Width.ToString() + ",\r\n" +
                    "    \"windowHeight\":  " + windowSize.Height.ToString() + ",\r\n" +
                    "    \"layoutTabHeight\":  " + savedTabAreaHeight.ToString() + ",\r\n" +
                    "    \"layoutDeviceHeight\":  " + savedDeviceAreaHeight.ToString() + ",\r\n" +
                    "    \"layoutLogHeight\":  " + savedLogAreaHeight.ToString() + ",\r\n" +
                    "    \"logRecordOutputPath\":  \"" + EscapeJsonString(logRecordPathTextBox.Text) + "\",\r\n" +
                    "    \"logRecordFilterTags\":  \"" + EscapeJsonString(logRecordTagTextBox.Text) + "\",\r\n" +
                    "    \"logRecordPackageName\":  \"" + EscapeJsonString(logRecordPackageTextBox.Text) + "\",\r\n" +
                    "    \"logRecordLevel\":  \"" + EscapeJsonString(GetSelectedLogRecordLevel()) + "\",\r\n" +
                    "    \"logRecordIncludeThreadInfo\":  " + (logRecordThreadInfoCheckBox.Checked ? "true" : "false") + ",\r\n" +
                    "    \"logRecordIncludeTimeInfo\":  " + (logRecordTimeInfoCheckBox.Checked ? "true" : "false") + ",\r\n" +
                    "    \"lastTransferDirection\":  \"" + EscapeJsonString(CurrentTransferDirection == TransferDirection.ToComputer ? "toComputer" : "toDevice") + "\",\r\n" +
                    "    \"lastTransferPath\":  \"" + EscapeJsonString(lastPushSourcePath) + "\",\r\n" +
                    "    \"lastTransferTargetDir\":  \"" + EscapeJsonString(lastPushTargetDir) + "\",\r\n" +
                    "    \"lastPullSourcePath\":  \"" + EscapeJsonString(lastPullSourcePath) + "\",\r\n" +
                    "    \"lastPullTargetDir\":  \"" + EscapeJsonString(lastPullTargetDir) + "\",\r\n" +
                    "    \"screenshotOutputDir\":  \"" + EscapeJsonString(screenshotOutputDirTextBox.Text) + "\",\r\n" +
                    "    \"screenRecordUseTimeLimit\":  " + (screenRecordTimeLimitCheckBox.Checked ? "true" : "false") + ",\r\n" +
                    "    \"screenRecordTimeLimitSeconds\":  " + ((int)screenRecordTimeLimitNumeric.Value).ToString() + ",\r\n" +
                    "    \"screenRecordBitRateMbps\":  " + ((int)screenRecordBitRateNumeric.Value).ToString() + ",\r\n" +
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

        private static int? ReadJsonInt(string json, string name)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(name)) return null;
            var pattern = "\"" + Regex.Escape(name) + "\"\\s*:\\s*(?<value>-?\\d+)";
            var match = Regex.Match(json, pattern);
            if (!match.Success) return null;
            int value;
            return int.TryParse(match.Groups["value"].Value, out value) ? (int?)value : null;
        }

        private static decimal ClampDecimal(decimal value, decimal min, decimal max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
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

        private TransferDirection CurrentTransferDirection
        {
            get { return transferToComputerRadioButton.Checked ? TransferDirection.ToComputer : TransferDirection.ToDevice; }
        }

        private void OnTransferDirectionChanged()
        {
            if (updatingTransferFields) return;
            ApplyTransferDirectionUi();
            SaveConfig();
        }

        private void OnTransferFieldChanged()
        {
            if (updatingTransferFields) return;
            StoreTransferFieldsForCurrentDirection();
            UpdateTransferStatus();
            SaveConfig();
        }

        private void StoreTransferFieldsForCurrentDirection()
        {
            if (CurrentTransferDirection == TransferDirection.ToComputer)
            {
                lastPullSourcePath = transferPathTextBox.Text.Trim();
                lastPullTargetDir = transferTargetDirTextBox.Text.Trim();
            }
            else
            {
                lastPushSourcePath = transferPathTextBox.Text.Trim();
                lastPushTargetDir = transferTargetDirTextBox.Text.Trim();
            }
        }

        private void ApplyTransferDirectionUi()
        {
            updatingTransferFields = true;
            try
            {
                if (CurrentTransferDirection == TransferDirection.ToComputer)
                {
                    transferSourceLabel.Text = "\u8bbe\u5907\u8def\u5f84";
                    transferTargetLabel.Text = "\u672c\u5730\u76ee\u5f55";
                    transferPathTextBox.Text = lastPullSourcePath;
                    transferTargetDirTextBox.Text = lastPullTargetDir;
                    sendTransferButton.Text = "\u5bfc\u51fa";
                    transferStatusLabel.Text = "\u8bf7\u8f93\u5165\u8bbe\u5907\u4e0a\u7684\u6587\u4ef6\u6216\u6587\u4ef6\u5939\u8def\u5f84\u3002";
                }
                else
                {
                    transferSourceLabel.Text = "\u672c\u5730\u8def\u5f84";
                    transferTargetLabel.Text = "\u8bbe\u5907\u76ee\u5f55";
                    transferPathTextBox.Text = lastPushSourcePath;
                    transferTargetDirTextBox.Text = lastPushTargetDir;
                    sendTransferButton.Text = "\u53d1\u9001";
                    transferStatusLabel.Text = "\u8bf7\u9009\u62e9\u8981\u53d1\u9001\u7684\u6587\u4ef6\u6216\u6587\u4ef6\u5939\u3002";
                }
            }
            finally
            {
                updatingTransferFields = false;
            }
            UpdateTransferBrowseButtons();
            UpdateTransferStatus();
        }

        private void UpdateTransferBrowseButtons()
        {
            var canBrowseSource = !isExecuting && !isDeviceCommandRunning && !isLogcatRunning && !IsMediaCaptureRunning && CurrentTransferDirection == TransferDirection.ToDevice;
            var canBrowseTarget = !isExecuting && !isDeviceCommandRunning && !isLogcatRunning && !IsMediaCaptureRunning && CurrentTransferDirection == TransferDirection.ToComputer;
            browseTransferButton.Enabled = canBrowseSource;
            browseTransferButton.Visible = canBrowseSource;
            browseTransferTargetButton.Enabled = canBrowseTarget;
            browseTransferTargetButton.Visible = canBrowseTarget;
        }

        private void ShowTransferBrowseMenu()
        {
            if (CurrentTransferDirection == TransferDirection.ToComputer) return;
            transferBrowseMenu.Show(browseTransferButton, new Point(0, browseTransferButton.Height));
        }

        private void BrowseTransferTarget()
        {
            if (CurrentTransferDirection != TransferDirection.ToComputer) return;
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "\u9009\u62e9\u4fdd\u5b58\u5230\u7535\u8111\u7684\u6587\u4ef6\u5939";
                var currentDir = GetInitialDirectoryFromPath(transferTargetDirTextBox.Text);
                if (!string.IsNullOrEmpty(currentDir)) dialog.SelectedPath = currentDir;
                if (dialog.ShowDialog(this) == DialogResult.OK) transferTargetDirTextBox.Text = dialog.SelectedPath;
            }
        }

        private void BrowseTransferFile()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "\u9009\u62e9\u8981\u53d1\u9001\u7684\u6587\u4ef6";
                dialog.Filter = "\u6240\u6709\u6587\u4ef6 (*.*)|*.*";
                dialog.Multiselect = false;
                var currentDir = GetInitialDirectoryFromPath(transferPathTextBox.Text);
                if (!string.IsNullOrEmpty(currentDir)) dialog.InitialDirectory = currentDir;
                if (dialog.ShowDialog(this) == DialogResult.OK) transferPathTextBox.Text = dialog.FileName;
            }
        }

        private void BrowseTransferFolder()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "\u9009\u62e9\u8981\u53d1\u9001\u7684\u6587\u4ef6\u5939";
                var currentDir = GetInitialDirectoryFromPath(transferPathTextBox.Text);
                if (!string.IsNullOrEmpty(currentDir)) dialog.SelectedPath = currentDir;
                if (dialog.ShowDialog(this) == DialogResult.OK) transferPathTextBox.Text = dialog.SelectedPath;
            }
        }

        private void UpdateTransferStatus()
        {
            if (CurrentTransferDirection == TransferDirection.ToComputer)
            {
                var remotePath = NormalizeDevicePath(transferPathTextBox.Text);
                var localTargetDir = ResolveTransferLocalPath(transferTargetDirTextBox.Text);
                if (string.IsNullOrWhiteSpace(remotePath))
                {
                    transferStatusLabel.Text = "\u8bf7\u8f93\u5165\u8bbe\u5907\u4e0a\u7684\u6587\u4ef6\u6216\u6587\u4ef6\u5939\u8def\u5f84\u3002";
                    return;
                }
                if (string.IsNullOrWhiteSpace(localTargetDir))
                {
                    transferStatusLabel.Text = "\u8bf7\u9009\u62e9\u672c\u5730\u4fdd\u5b58\u76ee\u5f55\u3002";
                    return;
                }
                if (File.Exists(localTargetDir))
                {
                    transferStatusLabel.Text = "\u672c\u5730\u76ee\u6807\u4e0d\u80fd\u662f\u6587\u4ef6\u3002";
                    return;
                }

                transferStatusLabel.Text = "\u8bbe\u5907\u8def\u5f84\uff1a" + remotePath + "  ->  " + Path.Combine(localTargetDir, SanitizeLocalFileName(GetDevicePathName(remotePath)));
                return;
            }

            var localPath = ResolveTransferLocalPath(transferPathTextBox.Text);
            var targetDir = NormalizeDeviceDirectory(transferTargetDirTextBox.Text);
            if (string.IsNullOrWhiteSpace(localPath))
            {
                transferStatusLabel.Text = "\u8bf7\u9009\u62e9\u8981\u53d1\u9001\u7684\u6587\u4ef6\u6216\u6587\u4ef6\u5939\u3002";
                return;
            }
            if (!File.Exists(localPath) && !Directory.Exists(localPath))
            {
                transferStatusLabel.Text = "\u672c\u5730\u8def\u5f84\u4e0d\u5b58\u5728\u3002";
                return;
            }
            if (string.IsNullOrWhiteSpace(targetDir))
            {
                transferStatusLabel.Text = "\u8bf7\u8f93\u5165\u8bbe\u5907\u76ee\u5f55\u3002";
                return;
            }

            var name = GetTransferLocalName(localPath);
            var type = File.Exists(localPath) ? "\u6587\u4ef6" : "\u6587\u4ef6\u5939";
            transferStatusLabel.Text = type + "\uff1a" + localPath + "  ->  " + JoinDevicePath(targetDir, name);
        }

        private void StartFileTransfer()
        {
            if (isExecuting || isDeviceCommandRunning || isLogcatRunning || IsMediaCaptureRunning) return;
            StoreTransferFieldsForCurrentDirection();
            var direction = CurrentTransferDirection;
            var sourcePath = direction == TransferDirection.ToComputer ? NormalizeDevicePath(transferPathTextBox.Text) : ResolveTransferLocalPath(transferPathTextBox.Text);
            var targetPath = direction == TransferDirection.ToComputer ? ResolveTransferLocalPath(transferTargetDirTextBox.Text) : NormalizeDeviceDirectory(transferTargetDirTextBox.Text);
            if (direction == TransferDirection.ToComputer)
            {
                if (string.IsNullOrWhiteSpace(sourcePath))
                {
                    MessageBox.Show(this, "\u8bf7\u8f93\u5165\u8bbe\u5907\u4e0a\u7684\u6587\u4ef6\u6216\u6587\u4ef6\u5939\u8def\u5f84\u3002", "APK\u5b89\u88c5\u5de5\u5177", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(targetPath) || File.Exists(targetPath))
                {
                    MessageBox.Show(this, "\u8bf7\u9009\u62e9\u6709\u6548\u7684\u672c\u5730\u4fdd\u5b58\u76ee\u5f55\u3002", "APK\u5b89\u88c5\u5de5\u5177", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(sourcePath) || (!File.Exists(sourcePath) && !Directory.Exists(sourcePath)))
                {
                    MessageBox.Show(this, "\u8bf7\u9009\u62e9\u6709\u6548\u7684\u672c\u5730\u6587\u4ef6\u6216\u6587\u4ef6\u5939\u3002", "APK\u5b89\u88c5\u5de5\u5177", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(targetPath))
                {
                    MessageBox.Show(this, "\u8bf7\u8f93\u5165\u8bbe\u5907\u76ee\u5f55\u3002", "APK\u5b89\u88c5\u5de5\u5177", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            var checkedItems = deviceList.CheckedItems.Cast<object>().Select(o => o.ToString()).ToList();
            if (checkedItems.Count == 0)
            {
                MessageBox.Show(this, "\u8bf7\u81f3\u5c11\u9009\u62e9\u4e00\u53f0\u8bbe\u5907\u3002", "APK\u5b89\u88c5\u5de5\u5177", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var adb = FindAdb();
            if (adb == null)
            {
                MessageBox.Show(this, "\u672a\u627e\u5230 adb.exe\u3002\u8bf7\u5b89\u88c5 Android SDK Platform Tools\uff0c\u6216\u628a adb.exe \u52a0\u5165 PATH\u3002", "APK\u5b89\u88c5\u5de5\u5177", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            transferTargetDirTextBox.Text = targetPath;
            SaveConfig();
            cancelRequested = false;
            isExecuting = true;
            SetExecutingUi(true);
            var thread = new Thread(new ThreadStart(delegate { ExecuteFileTransferOnDevices(adb, sourcePath, targetPath, checkedItems, direction); }));
            thread.IsBackground = true;
            thread.Start();
        }

        private void ExecuteFileTransferOnDevices(string adb, string sourcePath, string targetPath, List<string> checkedItems, TransferDirection direction)
        {
            var successCount = 0;
            var failedCount = 0;
            var skippedCount = 0;
            var toComputer = direction == TransferDirection.ToComputer;
            try
            {
                AddLogLine("\u5f00\u59cb\u6587\u4ef6\u4f20\u8f93\uff1a" + sourcePath + " -> " + targetPath);
                for (var index = 0; index < checkedItems.Count; index++)
                {
                    if (cancelRequested) { AddLogLine("\u7528\u6237\u5df2\u4e2d\u6b62\uff0c\u505c\u6b62\u540e\u7eed\u8bbe\u5907\u4f20\u8f93\u3002"); break; }
                    var label = checkedItems[index];
                    DeviceInfo device;
                    if (!deviceMap.TryGetValue(label, out device)) { AddLogLine("\u8df3\u8fc7\u672a\u77e5\u8bbe\u5907\uff1a" + label); skippedCount++; continue; }
                    AddLogLine("[" + (index + 1) + "/" + checkedItems.Count + "] " + (toComputer ? "\u4ece\u8bbe\u5907\u5bfc\u51fa\uff1a" : "\u4f20\u8f93\u5230\u8bbe\u5907\uff1a") + label);
                    if (device.State != "device") { AddLogLine("\u8bbe\u5907\u4e0d\u53ef\u7528\uff0c\u72b6\u6001\u4e3a " + device.State + "\u3002\u8bf7\u68c0\u67e5 USB \u8c03\u8bd5\u6388\u6743\u3002"); skippedCount++; continue; }
                    var ok = ExecuteFileTransferForDevice(adb, sourcePath, targetPath, device.Serial, direction, checkedItems.Count > 1);
                    if (cancelRequested) break;
                    if (ok) successCount++; else failedCount++;
                }
                var summary = cancelRequested ? "\u6587\u4ef6\u4f20\u8f93\u5df2\u4e2d\u6b62\uff1a\u6210\u529f " + successCount + "\uff0c\u5931\u8d25 " + failedCount + "\uff0c\u8df3\u8fc7 " + skippedCount : "\u6587\u4ef6\u4f20\u8f93\u5b8c\u6210\uff1a\u6210\u529f " + successCount + "\uff0c\u5931\u8d25 " + failedCount + "\uff0c\u8df3\u8fc7 " + skippedCount;
                AddLogLine(summary);
                SetStatus(summary);
                SaveRunLog();
            }
            catch (Exception ex) { AddLogLine("\u6587\u4ef6\u4f20\u8f93\u5f02\u5e38\uff1a" + ex.Message); SetStatus("\u6587\u4ef6\u4f20\u8f93\u5f02\u5e38"); }
            finally
            {
                isExecuting = false;
                cancelRequested = false;
                ClearCurrentProcess();
                BeginInvokeIfNeeded(delegate { SetExecutingUi(false); UpdateTransferStatus(); });
            }
        }

        private bool ExecuteFileTransferForDevice(string adb, string sourcePath, string targetPath, string serial, TransferDirection direction, bool useDeviceSubfolder)
        {
            if (direction == TransferDirection.ToComputer) return PullDevicePath(adb, serial, sourcePath, targetPath, useDeviceSubfolder);
            if (File.Exists(sourcePath)) return PushSingleFile(adb, serial, sourcePath, targetPath);
            if (Directory.Exists(sourcePath)) return PushDirectory(adb, serial, sourcePath, targetPath);
            AddLogLine("\u672c\u5730\u8def\u5f84\u4e0d\u5b58\u5728\uff1a" + sourcePath);
            return false;
        }

        private bool PushSingleFile(string adb, string serial, string filePath, string targetDir)
        {
            var remotePath = JoinDevicePath(targetDir, Path.GetFileName(filePath));
            if (!EnsureDeviceDirectory(adb, serial, targetDir)) return false;
            AddLogLine("\u53d1\u9001\u6587\u4ef6\uff1a" + filePath + " -> " + remotePath);
            var result = InvokeProcess(adb, new[] { "-s", serial, "push", filePath, remotePath }, true);
            if (cancelRequested) return false;
            if (result.ExitCode == 0) { AddLogLine("\u53d1\u9001\u6210\u529f\uff1a" + remotePath); return true; }
            AddLogLine("\u53d1\u9001\u5931\u8d25\uff1a" + HumanizeAdbOutput(result.Output));
            return false;
        }

        private bool PushDirectory(string adb, string serial, string folderPath, string targetDir)
        {
            var folderName = GetTransferLocalName(folderPath);
            var remoteRoot = JoinDevicePath(targetDir, folderName);
            AddLogLine("\u53d1\u9001\u6587\u4ef6\u5939\uff1a" + folderPath + " -> " + remoteRoot);
            if (!EnsureDeviceDirectory(adb, serial, remoteRoot)) return false;

            var directories = Directory.GetDirectories(folderPath, "*", SearchOption.AllDirectories);
            foreach (var directory in directories)
            {
                if (cancelRequested) return false;
                var remoteDir = JoinDevicePath(remoteRoot, GetRelativeDevicePath(folderPath, directory));
                if (!EnsureDeviceDirectory(adb, serial, remoteDir)) return false;
            }

            var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                if (cancelRequested) return false;
                var relative = GetRelativeDevicePath(folderPath, file);
                var remoteFile = JoinDevicePath(remoteRoot, relative);
                AddLogLine("\u53d1\u9001\u6587\u4ef6\uff1a" + relative);
                var result = InvokeProcess(adb, new[] { "-s", serial, "push", file, remoteFile }, true);
                if (cancelRequested) return false;
                if (result.ExitCode != 0)
                {
                    AddLogLine("\u53d1\u9001\u5931\u8d25\uff1a" + relative + "\uff0c" + HumanizeAdbOutput(result.Output));
                    return false;
                }
            }
            AddLogLine("\u6587\u4ef6\u5939\u53d1\u9001\u6210\u529f\uff1a" + remoteRoot + "\uff0c\u6587\u4ef6 " + files.Length + " \u4e2a\uff0c\u76ee\u5f55 " + directories.Length + " \u4e2a\u3002");
            return true;
        }

        private bool PullDevicePath(string adb, string serial, string devicePath, string localTargetDir, bool useDeviceSubfolder)
        {
            var remotePath = NormalizeDevicePath(devicePath);
            if (string.IsNullOrWhiteSpace(remotePath))
            {
                AddLogLine("\u8bbe\u5907\u8def\u5f84\u4e3a\u7a7a\u3002");
                return false;
            }

            try
            {
                if (File.Exists(localTargetDir))
                {
                    AddLogLine("\u672c\u5730\u76ee\u6807\u4e0d\u80fd\u662f\u6587\u4ef6\uff1a" + localTargetDir);
                    return false;
                }

                var finalTargetDir = localTargetDir;
                if (useDeviceSubfolder) finalTargetDir = Path.Combine(localTargetDir, SanitizeLocalFileName(serial));
                Directory.CreateDirectory(finalTargetDir);

                AddLogLine("\u5bfc\u51fa\u8bbe\u5907\u8def\u5f84\uff1a" + remotePath + " -> " + Path.Combine(finalTargetDir, SanitizeLocalFileName(GetDevicePathName(remotePath))));
                var result = InvokeProcess(adb, new[] { "-s", serial, "pull", remotePath, finalTargetDir }, true);
                if (cancelRequested) return false;
                if (result.ExitCode == 0) { AddLogLine("\u5bfc\u51fa\u6210\u529f\uff1a" + finalTargetDir); return true; }
                AddLogLine("\u5bfc\u51fa\u5931\u8d25\uff1a" + HumanizeAdbOutput(result.Output));
                return false;
            }
            catch (Exception ex)
            {
                AddLogLine("\u521b\u5efa\u672c\u5730\u76ee\u5f55\u5931\u8d25\uff1a" + ex.Message);
                return false;
            }
        }

        private bool EnsureDeviceDirectory(string adb, string serial, string remoteDir)
        {
            remoteDir = NormalizeDeviceDirectory(remoteDir);
            if (string.IsNullOrWhiteSpace(remoteDir)) return false;
            AddLogLine("\u521b\u5efa\u8bbe\u5907\u76ee\u5f55\uff1a" + remoteDir);
            var result = InvokeProcess(adb, new[] { "-s", serial, "shell", "mkdir", "-p", ShellQuote(remoteDir) }, true);
            if (cancelRequested) return false;
            if (result.ExitCode == 0) return true;
            AddLogLine("\u521b\u5efa\u8bbe\u5907\u76ee\u5f55\u5931\u8d25\uff1a" + HumanizeAdbOutput(result.Output));
            return false;
        }

        private string ResolveTransferLocalPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            path = path.Trim().Trim('"');
            return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(appDir, path));
        }

        private static string NormalizeDeviceDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            path = path.Trim().Trim('"').Replace('\\', '/');
            while (path.Length > 1 && path.EndsWith("/", StringComparison.Ordinal)) path = path.Substring(0, path.Length - 1);
            return path;
        }

        private static string NormalizeDevicePath(string path)
        {
            return NormalizeDeviceDirectory(path);
        }

        private static string JoinDevicePath(string left, string right)
        {
            left = NormalizeDeviceDirectory(left) ?? "";
            right = (right ?? "").Replace('\\', '/').Trim('/');
            if (left.Length == 0) return right;
            if (right.Length == 0) return left;
            return left + "/" + right;
        }

        private static string GetTransferLocalName(string path)
        {
            if (File.Exists(path)) return Path.GetFileName(path);
            return new DirectoryInfo(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).Name;
        }

        private static string GetDevicePathName(string path)
        {
            path = NormalizeDevicePath(path);
            if (string.IsNullOrWhiteSpace(path) || path == "/") return "device-files";
            var index = path.LastIndexOf('/');
            var name = index >= 0 ? path.Substring(index + 1) : path;
            return string.IsNullOrWhiteSpace(name) ? "device-files" : name;
        }

        private static string SanitizeLocalFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "device";
            var invalidChars = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (var ch in value) builder.Append(invalidChars.Contains(ch) ? '_' : ch);
            var name = builder.ToString().Trim();
            return name.Length == 0 ? "device" : name;
        }

        private static string GetDefaultTransferPullTargetDir()
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile))
            {
                var downloads = Path.Combine(userProfile, "Downloads");
                if (Directory.Exists(downloads)) return downloads;
            }

            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrWhiteSpace(documents)) return documents;
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static string GetRelativeDevicePath(string rootPath, string childPath)
        {
            var root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var child = Path.GetFullPath(childPath);
            if (!child.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return Path.GetFileName(childPath);
            return child.Substring(root.Length).Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }

        private static string ShellQuote(string value)
        {
            if (value == null) return "''";
            return "'" + value.Replace("'", "'\\''") + "'";
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
            if (isExecuting || isDeviceCommandRunning || IsMediaCaptureRunning) return;
            var previousSerial = currentDeviceInfoSerial;
            DeviceInfo selectedDevice;
            if (TryGetSelectedDeviceForDeviceInfo(out selectedDevice)) previousSerial = selectedDevice.Serial;
            deviceList.Items.Clear();
            deviceMap.Clear();
            var adb = FindAdb();
            if (adb == null) { AddLogLine("未找到 adb。请安装 Android SDK Platform Tools，或把 adb.exe 加入 PATH。"); statusLabel.Text = "未找到 adb"; return; }
            AddLogLine("刷新设备...");
            var devices = GetConnectedDevices(adb);
            var selectedIndex = -1;
            foreach (var device in devices)
            {
                deviceMap[device.Label] = device;
                deviceList.Items.Add(device.Label, device.State == "device");
                if (selectedIndex < 0 && !string.IsNullOrEmpty(previousSerial) && string.Equals(device.Serial, previousSerial, StringComparison.Ordinal)) selectedIndex = deviceList.Items.Count - 1;
            }
            if (selectedIndex >= 0) deviceList.SelectedIndex = selectedIndex;
            else if (!string.IsNullOrEmpty(currentDeviceInfoSerial))
            {
                currentDeviceInfoSerial = "";
                deviceInfoTextBox.Text = "请点击下方目标设备列表中的一台 device 状态设备。";
            }
            statusLabel.Text = "检测到 " + devices.Count + " 台设备，可用 " + devices.Count(d => d.State == "device") + " 台。";
            BeginDisplayControlAutoRefresh();
            BeginDeviceInfoAutoRefresh();
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
            if (isExecuting || isDeviceCommandRunning || isLogcatRunning || IsMediaCaptureRunning) return;
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
            if (!isExecuting && !isDeviceCommandRunning && !IsMediaCaptureRunning)
            {
                if (isLogcatRunning) StopLogcatRecording();
                else Close();
                return;
            }
            if (isScreenRecordRunning)
            {
                StopScreenRecording();
                return;
            }
            cancelRequested = true;
            cancelButton.Enabled = false;
            statusLabel.Text = "正在中止...";
            AddLogLine("收到中止请求，正在结束当前进程...");
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

        private ProcessResult InvokeProcessQuiet(string filePath, string[] arguments, bool cancellable)
        {
            var outputBuilder = new StringBuilder();
            var process = CreateAdbProcess(filePath, arguments);
            process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) lock (outputBuilder) outputBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) lock (outputBuilder) outputBuilder.AppendLine(e.Data); };
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

        private ProcessResult InvokeProcessBinaryToFile(string filePath, string[] arguments, string outputPath, bool cancellable)
        {
            var outputBuilder = new StringBuilder();
            var process = CreateAdbProcess(filePath, arguments);
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null)
                {
                    lock (outputBuilder) outputBuilder.AppendLine(e.Data);
                    AddLogLine(e.Data);
                }
            };
            try
            {
                process.Start();
                if (cancellable) SetCurrentProcess(process);
                process.BeginErrorReadLine();
                using (var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[81920];
                    while (true)
                    {
                        if (cancellable && cancelRequested)
                        {
                            TryKill(process);
                            break;
                        }
                        var read = process.StandardOutput.BaseStream.Read(buffer, 0, buffer.Length);
                        if (read <= 0) break;
                        output.Write(buffer, 0, read);
                    }
                }
                while (!process.WaitForExit(150))
                {
                    if (cancellable && cancelRequested)
                    {
                        TryKill(process);
                        break;
                    }
                }
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
            long writtenLines = 0;
            try
            {
                using (var writer = new StreamWriter(outputPath, false, Encoding.UTF8))
                {
                    process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                    {
                        if (e.Data == null) return;
                        if (ShouldWriteLogcatLine(e.Data, pidFilter))
                        {
                            lock (writer)
                            {
                                writer.WriteLine(FormatLogcatLine(e.Data, includeThreadInfo, includeTimeInfo));
                                writtenLines++;
                            }
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
                    return new ProcessResult { ExitCode = cancelRequested ? 130 : process.ExitCode, Output = outputBuilder.ToString(), Canceled = cancelRequested, WrittenLines = writtenLines };
                }
            }
            catch (Exception ex) { return new ProcessResult { ExitCode = 1, Output = ex.Message, Canceled = cancelRequested, WrittenLines = writtenLines }; }
            finally { ClearCurrentProcess(process); try { process.Dispose(); } catch { } }
        }

        private void SetCurrentProcess(Process process) { lock (processLock) currentProcess = process; }
        private void ClearCurrentProcess(Process process) { lock (processLock) if (ReferenceEquals(currentProcess, process)) currentProcess = null; }
        private void ClearCurrentProcess() { lock (processLock) currentProcess = null; }
        private void KillCurrentProcess() { Process process; lock (processLock) process = currentProcess; if (process != null) TryKill(process); }
        private static void TryKill(Process process) { try { if (process != null && !process.HasExited) process.Kill(); } catch { } }

        private void SetExecutingUi(bool executing)
        {
            var busy = executing || isDeviceCommandRunning || isLogcatRunning || IsMediaCaptureRunning;
            browseButton.Enabled = !busy;
            refreshButton.Enabled = !busy;
            connectButton.Enabled = !busy;
            disconnectButton.Enabled = !busy;
            connectAddressTextBox.Enabled = !busy;
            installButton.Enabled = !busy;
            clearLogButton.Enabled = !busy;
            installModeRadioButton.Enabled = !busy;
            cleanInstallModeRadioButton.Enabled = !busy;
            uninstallModeRadioButton.Enabled = !busy;
            clearDataModeRadioButton.Enabled = !busy;
            startAppModeRadioButton.Enabled = !busy;
            apkTextBox.Enabled = !busy;
            deviceList.Enabled = !busy;
            launchAfterInstallCheckBox.Enabled = !busy && !uninstallModeRadioButton.Checked && !clearDataModeRadioButton.Checked && !startAppModeRadioButton.Checked;
            clearLogcatCacheButton.Enabled = !busy;
            exportLogcatCacheButton.Enabled = !busy;
            startLogRecordButton.Enabled = !busy;
            stopLogRecordButton.Enabled = isLogcatRunning && !executing && !isDeviceCommandRunning && !IsMediaCaptureRunning;
            logRecordPathTextBox.Enabled = !busy;
            logRecordTagTextBox.Enabled = !busy;
            logRecordPackageTextBox.Enabled = !busy;
            logRecordLevelComboBox.Enabled = !busy;
            logRecordThreadInfoCheckBox.Enabled = !busy;
            logRecordTimeInfoCheckBox.Enabled = !busy;
            browseLogRecordFileButton.Enabled = !busy;
            browseLogRecordFolderButton.Enabled = !busy;
            transferPathTextBox.Enabled = !busy;
            transferTargetDirTextBox.Enabled = !busy;
            transferToDeviceRadioButton.Enabled = !busy;
            transferToComputerRadioButton.Enabled = !busy;
            UpdateTransferBrowseButtons();
            sendTransferButton.Enabled = !busy;
            screenshotOutputDirTextBox.Enabled = !busy;
            browseScreenshotOutputDirButton.Enabled = !busy;
            takeScreenshotButton.Enabled = !busy;
            startScreenRecordButton.Enabled = !busy;
            stopScreenRecordButton.Enabled = isScreenRecordRunning;
            SetScreenRecordOptionControlsEnabled(busy);
            UpdateCaptureActionButtons(busy);
            SetDisplayControlControlsEnabled(!busy);
            cancelButton.Enabled = executing || isDeviceCommandRunning || IsMediaCaptureRunning;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            statusLabel.Text = executing ? "正在执行..." : statusLabel.Text;
        }

        private void SetScreenshotUi(bool running)
        {
            var busy = running || isExecuting || isDeviceCommandRunning || isLogcatRunning;
            browseButton.Enabled = !busy;
            refreshButton.Enabled = !busy;
            connectButton.Enabled = !busy;
            disconnectButton.Enabled = !busy;
            connectAddressTextBox.Enabled = !busy;
            installButton.Enabled = !busy;
            clearLogButton.Enabled = !busy;
            installModeRadioButton.Enabled = !busy;
            cleanInstallModeRadioButton.Enabled = !busy;
            uninstallModeRadioButton.Enabled = !busy;
            clearDataModeRadioButton.Enabled = !busy;
            startAppModeRadioButton.Enabled = !busy;
            apkTextBox.Enabled = !busy;
            deviceList.Enabled = !busy;
            launchAfterInstallCheckBox.Enabled = !busy && !uninstallModeRadioButton.Checked && !clearDataModeRadioButton.Checked && !startAppModeRadioButton.Checked;
            clearLogcatCacheButton.Enabled = !busy;
            exportLogcatCacheButton.Enabled = !busy;
            startLogRecordButton.Enabled = !busy;
            stopLogRecordButton.Enabled = isLogcatRunning && !running && !isExecuting && !isDeviceCommandRunning;
            logRecordPathTextBox.Enabled = !busy;
            logRecordTagTextBox.Enabled = !busy;
            logRecordPackageTextBox.Enabled = !busy;
            logRecordLevelComboBox.Enabled = !busy;
            logRecordThreadInfoCheckBox.Enabled = !busy;
            logRecordTimeInfoCheckBox.Enabled = !busy;
            browseLogRecordFileButton.Enabled = !busy;
            browseLogRecordFolderButton.Enabled = !busy;
            transferPathTextBox.Enabled = !busy;
            transferTargetDirTextBox.Enabled = !busy;
            transferToDeviceRadioButton.Enabled = !busy;
            transferToComputerRadioButton.Enabled = !busy;
            UpdateTransferBrowseButtons();
            sendTransferButton.Enabled = !busy;
            screenshotOutputDirTextBox.Enabled = !busy;
            browseScreenshotOutputDirButton.Enabled = !busy;
            takeScreenshotButton.Enabled = !busy;
            startScreenRecordButton.Enabled = !busy && !isScreenRecordRunning;
            stopScreenRecordButton.Enabled = isScreenRecordRunning;
            SetScreenRecordOptionControlsEnabled(busy);
            UpdateCaptureActionButtons(busy);
            SetDisplayControlControlsEnabled(!busy);
            cancelButton.Enabled = running || isExecuting || isDeviceCommandRunning || isScreenRecordRunning;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            if (running)
            {
                statusLabel.Text = "正在截屏...";
                screenshotStatusLabel.Text = "正在截屏，请稍候...";
            }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            CaptureLayoutHeights();
            SaveConfig();
            displayControlRefreshTimer.Stop();
            if (isScreenRecordRunning)
            {
                e.Cancel = true;
                StopScreenRecording();
                return;
            }
            if (isLogcatRunning) StopLogcatRecording();
            if (isExecuting || isDeviceCommandRunning || isScreenshotRunning) { cancelRequested = true; KillCurrentProcess(); }
            if (screenshotPreviewBox.Image != null)
            {
                screenshotPreviewBox.Image.Dispose();
                screenshotPreviewBox.Image = null;
            }
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
            candidates.Add(Path.Combine(appDir, "adb.exe"));
            candidates.Add(Path.Combine(appDir, "scrcpy", "adb.exe"));
            candidates.Add(Path.Combine(appDir, "bin", "scrcpy", "adb.exe"));
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
        public long WrittenLines;
    }
}
