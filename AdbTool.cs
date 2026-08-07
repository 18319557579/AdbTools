using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace AdbTool
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
        private const string AppDisplayName = "ADB工具";
        private const string ConfigFileName = "adb-tool.config.json";
        private const string RunLogPrefix = "adb-tool";
        private const string RemoteTempFilePrefix = "adb-tool";
        private const string ApkStageDirName = "ApkStage";
        private const double DefaultDisplayScale = 1.0;
        private static readonly double[] FontScaleOptions = { 0.0, 1.0, 2.0, 3.0, 4.0 };
        private static readonly double[] AnimationScaleOptions = { 0.0, 0.5, 1.0, 1.5, 2.0, 5.0, 10.0 };

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetShortPathName(string longPath, StringBuilder shortPath, int bufferLength);

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

        private enum SoftwareManagementOperation
        {
            Launch,
            ForceStop,
            ShowInfo,
            Disable,
            Enable,
            ClearData,
            Uninstall,
            KeepDataUninstall,
            ExtractApk
        }

        private sealed class ScreenRecordOptions
        {
            public bool HasTimeLimit;
            public int TimeLimitSeconds;
            public int BitRate;
        }

        private sealed class ApkStageInfo
        {
            public string SourcePath = "";
            public string StagePath = "";
            public long Length;
            public DateTime LastWriteUtc;
        }

        private sealed class DisplayControlInfo
        {
            public int PhysicalWidth;
            public int PhysicalHeight;
            public int OverrideWidth;
            public int OverrideHeight;
            public int PhysicalDensity;
            public int OverrideDensity;
            public double FontScale = DefaultDisplayScale;
            public double WindowAnimationScale = DefaultDisplayScale;
            public double TransitionAnimationScale = DefaultDisplayScale;
            public double AnimatorDurationScale = DefaultDisplayScale;
            public bool HasPhysicalSize;
            public bool HasOverrideSize;
            public bool HasPhysicalDensity;
            public bool HasOverrideDensity;
            public bool HasFontScale;
            public bool HasWindowAnimationScale;
            public bool HasTransitionAnimationScale;
            public bool HasAnimatorDurationScale;
            public bool IsFontScaleUnset;
            public bool IsWindowAnimationScaleUnset;
            public bool IsTransitionAnimationScaleUnset;
            public bool IsAnimatorDurationScaleUnset;

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

        private sealed class ForegroundAppInfo
        {
            public string PackageName = "";
            public string ActivityName = "";
        }

        private readonly TabControl tabControl = new TabControl();
        private readonly SplitContainer mainSplitContainer = new SplitContainer();
        private readonly SplitContainer lowerSplitContainer = new SplitContainer();
        private readonly TabPage installTab = new TabPage("APK安装");
        private readonly TabPage fileTransferTab = new TabPage("文件传输");
        private readonly TabPage screenshotTab = new TabPage("截屏/录屏");
        private readonly TabPage logRecordTab = new TabPage("日志录制");
        private readonly TabPage softwareManagementTab = new TabPage("软件管理");
        private readonly TabPage displayControlTab = new TabPage("显示控制");
        private readonly TabPage deviceInfoTab = new TabPage("设备概览");
        private readonly TextBox apkTextBox = new TextBox();
        private readonly Button browseButton = new Button();
        private readonly Button refreshButton = new Button();
        private readonly Button settingsButton = new Button();
        private readonly Button connectButton = new Button();
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

        private readonly TextBox softwarePackageTextBox = new TextBox();
        private readonly TextBox softwareForegroundPackageTextBox = new TextBox();
        private readonly TextBox softwareForegroundActivityTextBox = new TextBox();
        private readonly CheckBox softwareAutoFillCheckBox = new CheckBox();
        private readonly Button softwareLaunchButton = new Button();
        private readonly Button softwareForceStopButton = new Button();
        private readonly Button softwareInfoButton = new Button();
        private readonly Button softwareDisableButton = new Button();
        private readonly Button softwareEnableButton = new Button();
        private readonly Button softwareClearDataButton = new Button();
        private readonly Button softwareUninstallButton = new Button();
        private readonly Button softwareKeepDataUninstallButton = new Button();
        private readonly Button softwareExtractApkButton = new Button();
        private readonly Label softwareManagementStatusLabel = new Label();
        private readonly System.Windows.Forms.Timer softwareForegroundTimer = new System.Windows.Forms.Timer();
        private readonly ToolTip softwareManagementToolTip = new ToolTip();

        private readonly TextBox deviceInfoTextBox = new TextBox();
        private readonly Button queryDeviceInfoButton = new Button();
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
        private readonly Label displayFontScaleInfoLabel = new Label();
        private readonly Label displayAnimationScaleInfoLabel = new Label();
        private readonly Label displayControlStatusLabel = new Label();
        private readonly TextBox displayWidthTextBox = new TextBox();
        private readonly TextBox displayHeightTextBox = new TextBox();
        private readonly TextBox displayDensityValueTextBox = new TextBox();
        private readonly ComboBox displayDensityUnitComboBox = new ComboBox();
        private readonly TrackBar displayFontScaleTrackBar = new TrackBar();
        private readonly TrackBar windowAnimationScaleTrackBar = new TrackBar();
        private readonly TrackBar transitionAnimationScaleTrackBar = new TrackBar();
        private readonly TrackBar animatorDurationScaleTrackBar = new TrackBar();
        private readonly Label displayFontScaleValueLabel = new Label();
        private readonly Label windowAnimationScaleValueLabel = new Label();
        private readonly Label transitionAnimationScaleValueLabel = new Label();
        private readonly Label animatorDurationScaleValueLabel = new Label();
        private readonly Button refreshDisplayInfoButton = new Button();
        private readonly Button applyResolutionButton = new Button();
        private readonly Button restoreResolutionButton = new Button();
        private readonly Button applyDensityButton = new Button();
        private readonly Button restoreDensityButton = new Button();
        private readonly Button applyFontScaleButton = new Button();
        private readonly Button restoreFontScaleButton = new Button();
        private readonly Button applyAnimationScaleButton = new Button();
        private readonly Button restoreAnimationScaleButton = new Button();
        private readonly Button restoreDisplayAllButton = new Button();
        private readonly ToolTip displayControlToolTip = new ToolTip();
        private readonly ToolTip toolPathToolTip = new ToolTip();

        private readonly Dictionary<string, DeviceInfo> deviceMap = new Dictionary<string, DeviceInfo>();
        private readonly object processLock = new object();
        private readonly string appDir;
        private readonly string legacyConfigPath;
        private readonly string configPath;
        private readonly string logDir;
        private readonly string screenshotDir;
        private readonly string apkExtractDir;
        private readonly string apkStageDir;
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
        private volatile bool isSoftwareForegroundRefreshing;
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
        private ApkStageInfo currentApkStageInfo;
        private bool apkStageCleanupDone;
        private string screenRecordCurrentSerial = "";
        private string screenRecordCurrentDeviceLabel = "";
        private int screenRecordCurrentTimeLimitSeconds;
        private DateTime screenRecordStartedAt;
        private ApkInfo currentApkInfo;
        private DisplayControlInfo currentDisplayControlInfo;
        private string currentDeviceInfoSerial = "";
        private string configuredAdbPath = "";
        private string configuredAaptPath = "";
        private string lastConnectAddress = "";

        private bool IsMediaCaptureRunning
        {
            get { return isScreenshotRunning || isScreenRecordRunning; }
        }

        public MainForm()
        {
            appDir = AppDomain.CurrentDomain.BaseDirectory;
            legacyConfigPath = Path.Combine(appDir, ConfigFileName);
            var userDataDir = GetUserDataDir(appDir);
            configPath = Path.Combine(userDataDir, ConfigFileName);
            logDir = Path.Combine(userDataDir, "log");
            screenshotDir = GetDefaultScreenshotDir(appDir);
            apkExtractDir = Path.Combine(userDataDir, "ApkExt");
            apkStageDir = Path.Combine(userDataDir, ApkStageDirName);
            Text = AppDisplayName;
            ApplyWindowIcon();
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1080, 820);
            Size = new Size(1180, 900);
            Font = new Font("Microsoft YaHei UI", 9F);
            AllowDrop = true;
            BuildUi();
            WireEvents();
            EnsureDirectory(logDir);
            EnsureDirectory(screenshotDir);
            InitLogcatDefaults();
            InitTransferDefaults();
            InitScreenshotDefaults();
            LoadConfig();
            configReady = true;
            UpdateToolPathStatus();
            UpdateExecutionOptionState();
            UpdateSoftwareAutoFillState();
            UpdateSoftwareForegroundTimerState();
            UpdateTransferStatus();
            RefreshDevices();
        }

        private void ApplyWindowIcon()
        {
            try
            {
                var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (icon != null) Icon = icon;
            }
            catch
            {
            }
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
            tabControl.TabPages.Add(fileTransferTab);
            tabControl.TabPages.Add(screenshotTab);
            tabControl.TabPages.Add(logRecordTab);
            tabControl.TabPages.Add(softwareManagementTab);
            tabControl.TabPages.Add(displayControlTab);
            tabControl.TabPages.Add(deviceInfoTab);
            mainSplitContainer.Panel1.Controls.Add(tabControl);

            lowerSplitContainer.Dock = DockStyle.Fill;
            lowerSplitContainer.Orientation = Orientation.Horizontal;
            lowerSplitContainer.SplitterWidth = 6;
            lowerSplitContainer.TabStop = false;
            lowerSplitContainer.BackColor = Color.FromArgb(220, 220, 220);
            mainSplitContainer.Panel2.Controls.Add(lowerSplitContainer);

            BuildInstallTab();
            BuildFileTransferTab();
            BuildScreenshotTab();
            BuildLogRecordTab();
            BuildSoftwareManagementTab();
            BuildDisplayControlTab();
            BuildDeviceInfoTab();
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

        private void BuildSoftwareManagementTab()
        {
            softwareManagementTab.Padding = new Padding(10);
            softwareManagementTab.AutoScroll = true;

            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Top;
            panel.Height = 274;
            panel.ColumnCount = 1;
            panel.RowCount = 6;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            softwareManagementTab.Controls.Add(panel);

            var foregroundPackagePanel = new TableLayoutPanel();
            foregroundPackagePanel.Dock = DockStyle.Fill;
            foregroundPackagePanel.ColumnCount = 3;
            foregroundPackagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            foregroundPackagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foregroundPackagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 8));
            panel.Controls.Add(foregroundPackagePanel, 0, 0);
            AddLabel(foregroundPackagePanel, "当前界面包名", 0);
            ConfigureSoftwareTextBox(softwareForegroundPackageTextBox, true);
            foregroundPackagePanel.Controls.Add(softwareForegroundPackageTextBox, 1, 0);

            var foregroundActivityPanel = new TableLayoutPanel();
            foregroundActivityPanel.Dock = DockStyle.Fill;
            foregroundActivityPanel.ColumnCount = 3;
            foregroundActivityPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            foregroundActivityPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foregroundActivityPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 8));
            panel.Controls.Add(foregroundActivityPanel, 0, 1);
            AddLabel(foregroundActivityPanel, "当前界面活动", 0);
            ConfigureSoftwareTextBox(softwareForegroundActivityTextBox, true);
            foregroundActivityPanel.Controls.Add(softwareForegroundActivityTextBox, 1, 0);

            var packagePanel = new TableLayoutPanel();
            packagePanel.Dock = DockStyle.Fill;
            packagePanel.ColumnCount = 4;
            packagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            packagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            packagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            packagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 8));
            panel.Controls.Add(packagePanel, 0, 3);
            AddLabel(packagePanel, "目标包名", 0);
            ConfigureSoftwareTextBox(softwarePackageTextBox, false);
            packagePanel.Controls.Add(softwarePackageTextBox, 1, 0);
            softwareAutoFillCheckBox.Text = "自动填充";
            softwareAutoFillCheckBox.AutoSize = true;
            softwareAutoFillCheckBox.Dock = DockStyle.Fill;
            softwareAutoFillCheckBox.Margin = new Padding(8, 8, 0, 0);
            packagePanel.Controls.Add(softwareAutoFillCheckBox, 2, 0);

            var actionsPanel = new TableLayoutPanel();
            actionsPanel.Dock = DockStyle.Fill;
            actionsPanel.ColumnCount = 3;
            actionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            actionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            actionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            panel.Controls.Add(actionsPanel, 0, 4);

            FlowLayoutPanel basicPanel;
            var basicGroup = CreateSoftwareActionGroup("基础操作", out basicPanel);
            AddSoftwareActionButton(basicPanel, softwareLaunchButton, "运行");
            AddSoftwareActionButton(basicPanel, softwareForceStopButton, "强制停止");
            AddSoftwareActionButton(basicPanel, softwareInfoButton, "软件信息");
            AddSoftwareActionButton(basicPanel, softwareExtractApkButton, "提取安装包");
            actionsPanel.Controls.Add(basicGroup, 0, 0);

            FlowLayoutPanel managePanel;
            var manageGroup = CreateSoftwareActionGroup("数据与状态", out managePanel);
            AddSoftwareActionButton(managePanel, softwareDisableButton, "禁用");
            AddSoftwareActionButton(managePanel, softwareEnableButton, "启用");
            AddSoftwareActionButton(managePanel, softwareClearDataButton, "清除数据");
            actionsPanel.Controls.Add(manageGroup, 1, 0);

            FlowLayoutPanel dangerPanel;
            var dangerGroup = CreateSoftwareActionGroup("卸载与高级", out dangerPanel);
            AddSoftwareActionButton(dangerPanel, softwareUninstallButton, "卸载");
            AddSoftwareActionButton(dangerPanel, softwareKeepDataUninstallButton, "保留数据卸载");
            actionsPanel.Controls.Add(dangerGroup, 2, 0);

            softwareManagementStatusLabel.Text = "请勾选一台 device 状态设备，并输入目标包名；切换到本页后会读取当前界面包名和活动。";
            softwareManagementStatusLabel.Dock = DockStyle.Fill;
            softwareManagementStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            softwareManagementStatusLabel.ForeColor = Color.FromArgb(80, 80, 80);
            softwareManagementStatusLabel.AutoEllipsis = true;
            panel.Controls.Add(softwareManagementStatusLabel, 0, 5);

            softwareManagementToolTip.SetToolTip(softwareAutoFillCheckBox, "开启后会把当前界面包名自动填入目标包名。");
            softwareManagementToolTip.SetToolTip(softwareLaunchButton, "使用 monkey 启动该包名的默认入口。");
            softwareManagementToolTip.SetToolTip(softwareForceStopButton, "执行 am force-stop。");
            softwareManagementToolTip.SetToolTip(softwareInfoButton, "读取 dumpsys package 中的版本、路径和状态摘要。");
            softwareManagementToolTip.SetToolTip(softwareExtractApkButton, "读取 pm path 并把 APK 拉取到本机 ApkExt 目录。");
            softwareManagementToolTip.SetToolTip(softwareDisableButton, "执行 pm disable-user。");
            softwareManagementToolTip.SetToolTip(softwareEnableButton, "执行 pm enable。");
            softwareManagementToolTip.SetToolTip(softwareClearDataButton, "执行 pm clear，会清除应用数据。");
            softwareManagementToolTip.SetToolTip(softwareUninstallButton, "第三方应用正常卸载；系统应用需高风险确认后按当前用户卸载。");
            softwareManagementToolTip.SetToolTip(softwareKeepDataUninstallButton, "执行 pm uninstall -k，保留应用数据卸载。");
        }

        private void ConfigureSoftwareTextBox(TextBox textBox, bool readOnly)
        {
            textBox.Dock = DockStyle.Fill;
            textBox.Margin = new Padding(0, 4, 8, 4);
            textBox.ReadOnly = readOnly;
        }

        private TableLayoutPanel CreateSoftwareActionGroup(string title, out FlowLayoutPanel flow)
        {
            var host = new TableLayoutPanel();
            host.Dock = DockStyle.Fill;
            host.Margin = new Padding(0, 0, 12, 0);
            host.ColumnCount = 1;
            host.RowCount = 2;
            host.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var label = new Label();
            label.Text = title;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.ForeColor = Color.FromArgb(60, 60, 60);
            host.Controls.Add(label, 0, 0);

            flow = new FlowLayoutPanel();
            flow.Dock = DockStyle.Fill;
            flow.FlowDirection = FlowDirection.LeftToRight;
            flow.WrapContents = true;
            flow.Margin = Padding.Empty;
            host.Controls.Add(flow, 0, 1);

            return host;
        }

        private void AddSoftwareActionButton(FlowLayoutPanel panel, Button button, string text)
        {
            button.Text = text;
            button.Size = text.Length >= 6 ? new Size(118, 28) : new Size(88, 28);
            button.Margin = new Padding(0, 4, 8, 4);
            panel.Controls.Add(button);
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
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            deviceInfoTab.Controls.Add(panel);

            var actionPanel = new TableLayoutPanel();
            actionPanel.Dock = DockStyle.Fill;
            actionPanel.ColumnCount = 2;
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            panel.Controls.Add(actionPanel, 0, 0);

            var hintLabel = new Label();
            hintLabel.Text = "勾选一台 device 状态的目标设备后点击查询。";
            hintLabel.Dock = DockStyle.Fill;
            hintLabel.TextAlign = ContentAlignment.MiddleLeft;
            hintLabel.ForeColor = Color.FromArgb(60, 60, 60);
            hintLabel.AutoEllipsis = true;
            actionPanel.Controls.Add(hintLabel, 0, 0);

            queryDeviceInfoButton.Text = "查询";
            AddActionButton(actionPanel, queryDeviceInfoButton, 1);

            deviceInfoTextBox.Dock = DockStyle.Fill;
            deviceInfoTextBox.Multiline = true;
            deviceInfoTextBox.ScrollBars = ScrollBars.Both;
            deviceInfoTextBox.WordWrap = false;
            deviceInfoTextBox.ReadOnly = true;
            deviceInfoTextBox.Font = new Font("Consolas", 9F);
            deviceInfoTextBox.Text = "请在下方目标设备列表中勾选一台 device 状态设备，然后点击查询。";
            panel.Controls.Add(deviceInfoTextBox, 0, 1);
            deviceInfoToolTip.SetToolTip(queryDeviceInfoButton, "读取勾选设备的系统、硬件、电池、存储和网络信息。");
        }

        private void BuildDisplayControlTab()
        {
            displayControlTab.Padding = new Padding(10);
            displayControlTab.AutoScroll = true;
            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Top;
            panel.Height = 390;
            panel.ColumnCount = 1;
            panel.RowCount = 3;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 314));
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
            refreshDisplayInfoButton.Text = "读取信息";
            AddActionButton(topPanel, refreshDisplayInfoButton, 1);
            restoreDisplayAllButton.Text = "全部恢复";
            AddActionButton(topPanel, restoreDisplayAllButton, 2);

            var contentPanel = new TableLayoutPanel();
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.ColumnCount = 2;
            contentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            contentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            contentPanel.RowCount = 1;
            panel.Controls.Add(contentPanel, 0, 1);

            var leftPanel = new TableLayoutPanel();
            leftPanel.Dock = DockStyle.Fill;
            leftPanel.Margin = new Padding(0, 0, 12, 0);
            leftPanel.ColumnCount = 1;
            leftPanel.RowCount = 5;
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            contentPanel.Controls.Add(leftPanel, 0, 0);

            displayResolutionInfoLabel.Text = "屏幕分辨率";
            displayResolutionInfoLabel.Dock = DockStyle.Fill;
            displayResolutionInfoLabel.TextAlign = ContentAlignment.MiddleLeft;
            displayResolutionInfoLabel.ForeColor = Color.FromArgb(60, 60, 60);
            displayResolutionInfoLabel.AutoEllipsis = true;
            leftPanel.Controls.Add(displayResolutionInfoLabel, 0, 0);

            var resolutionPanel = new TableLayoutPanel();
            resolutionPanel.Dock = DockStyle.Fill;
            resolutionPanel.ColumnCount = 7;
            resolutionPanel.RowCount = 2;
            resolutionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
            resolutionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            resolutionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
            resolutionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
            resolutionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            resolutionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
            resolutionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            resolutionPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            resolutionPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            leftPanel.Controls.Add(resolutionPanel, 0, 1);
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
            var resolutionActionsPanel = CreateDisplayActionRow(applyResolutionButton, restoreResolutionButton);
            resolutionPanel.Controls.Add(resolutionActionsPanel, 0, 1);
            resolutionPanel.SetColumnSpan(resolutionActionsPanel, 3);

            displayDensityInfoLabel.Text = "显示密度";
            displayDensityInfoLabel.Dock = DockStyle.Fill;
            displayDensityInfoLabel.TextAlign = ContentAlignment.MiddleLeft;
            displayDensityInfoLabel.ForeColor = Color.FromArgb(60, 60, 60);
            displayDensityInfoLabel.AutoEllipsis = true;
            leftPanel.Controls.Add(displayDensityInfoLabel, 0, 2);

            var densityPanel = new TableLayoutPanel();
            densityPanel.Dock = DockStyle.Fill;
            densityPanel.ColumnCount = 4;
            densityPanel.RowCount = 2;
            densityPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
            densityPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            densityPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            densityPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            densityPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            densityPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            leftPanel.Controls.Add(densityPanel, 0, 3);
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
            var densityActionsPanel = CreateDisplayActionRow(applyDensityButton, restoreDensityButton);
            densityPanel.Controls.Add(densityActionsPanel, 0, 1);
            densityPanel.SetColumnSpan(densityActionsPanel, 3);

            var scalePanel = new TableLayoutPanel();
            scalePanel.Dock = DockStyle.Fill;
            scalePanel.ColumnCount = 1;
            scalePanel.RowCount = 8;
            scalePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            scalePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            scalePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            scalePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            scalePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            scalePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            scalePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            scalePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            contentPanel.Controls.Add(scalePanel, 1, 0);

            displayFontScaleInfoLabel.Text = "字体大小倍数";
            displayFontScaleInfoLabel.Dock = DockStyle.Fill;
            displayFontScaleInfoLabel.TextAlign = ContentAlignment.MiddleLeft;
            displayFontScaleInfoLabel.ForeColor = Color.FromArgb(60, 60, 60);
            displayFontScaleInfoLabel.AutoEllipsis = true;
            scalePanel.Controls.Add(displayFontScaleInfoLabel, 0, 0);

            ConfigureDisplayScaleTrackBar(displayFontScaleTrackBar, FontScaleOptions);
            ConfigureDisplayScaleValueLabel(displayFontScaleValueLabel);
            applyFontScaleButton.Text = "修改";
            restoreFontScaleButton.Text = "恢复";
            scalePanel.Controls.Add(CreateDisplayScaleRow("缩放", displayFontScaleTrackBar, displayFontScaleValueLabel, applyFontScaleButton, restoreFontScaleButton), 0, 1);
            scalePanel.Controls.Add(CreateDisplayScaleMarkerPanel(FontScaleOptions), 0, 2);

            displayAnimationScaleInfoLabel.Text = "动画速度";
            displayAnimationScaleInfoLabel.Dock = DockStyle.Fill;
            displayAnimationScaleInfoLabel.TextAlign = ContentAlignment.MiddleLeft;
            displayAnimationScaleInfoLabel.ForeColor = Color.FromArgb(60, 60, 60);
            displayAnimationScaleInfoLabel.AutoEllipsis = true;
            scalePanel.Controls.Add(displayAnimationScaleInfoLabel, 0, 3);

            ConfigureDisplayScaleTrackBar(windowAnimationScaleTrackBar, AnimationScaleOptions);
            ConfigureDisplayScaleTrackBar(transitionAnimationScaleTrackBar, AnimationScaleOptions);
            ConfigureDisplayScaleTrackBar(animatorDurationScaleTrackBar, AnimationScaleOptions);
            ConfigureDisplayScaleValueLabel(windowAnimationScaleValueLabel);
            ConfigureDisplayScaleValueLabel(transitionAnimationScaleValueLabel);
            ConfigureDisplayScaleValueLabel(animatorDurationScaleValueLabel);
            applyAnimationScaleButton.Text = "修改";
            restoreAnimationScaleButton.Text = "恢复";
            scalePanel.Controls.Add(CreateDisplayScaleRow("窗口", windowAnimationScaleTrackBar, windowAnimationScaleValueLabel, applyAnimationScaleButton, restoreAnimationScaleButton), 0, 4);
            scalePanel.Controls.Add(CreateDisplayScaleRow("过渡", transitionAnimationScaleTrackBar, transitionAnimationScaleValueLabel, null, null), 0, 5);
            scalePanel.Controls.Add(CreateDisplayScaleRow("程序", animatorDurationScaleTrackBar, animatorDurationScaleValueLabel, null, null), 0, 6);
            scalePanel.Controls.Add(CreateDisplayScaleMarkerPanel(AnimationScaleOptions), 0, 7);

            displayControlStatusLabel.Text = "请勾选一台 device 状态设备后点击“刷新显示信息”；修改显示参数、字体倍数或动画速度可能会短暂刷新设备画面。";
            displayControlStatusLabel.Dock = DockStyle.Fill;
            displayControlStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            displayControlStatusLabel.ForeColor = Color.FromArgb(80, 80, 80);
            displayControlStatusLabel.AutoEllipsis = true;
            panel.Controls.Add(displayControlStatusLabel, 0, 2);

            UpdateDisplayScaleValueLabels();

            displayControlToolTip.SetToolTip(refreshDisplayInfoButton, "读取当前设备的 wm size、wm density、字体大小倍数和动画速度。");
            displayControlToolTip.SetToolTip(restoreDisplayAllButton, "依次恢复分辨率、密度、字体大小倍数和动画速度。");
            displayControlToolTip.SetToolTip(applyResolutionButton, "执行 wm size 宽x高，单位为 px。");
            displayControlToolTip.SetToolTip(restoreResolutionButton, "执行 wm size reset。");
            displayControlToolTip.SetToolTip(applyDensityButton, "DPI 模式直接设置 density；dp 模式按最小宽度换算 density。");
            displayControlToolTip.SetToolTip(restoreDensityButton, "执行 wm density reset，同时恢复最小宽度表现。");
            displayControlToolTip.SetToolTip(displayFontScaleTrackBar, "字体缩放，可选 0x、1x、2x、3x、4x。");
            displayControlToolTip.SetToolTip(applyFontScaleButton, "执行 settings put system font_scale。");
            displayControlToolTip.SetToolTip(restoreFontScaleButton, "将 font_scale 恢复为 1x。");
            displayControlToolTip.SetToolTip(windowAnimationScaleTrackBar, "窗口动画速度，可选 0x、0.5x、1x、1.5x、2x、5x、10x。");
            displayControlToolTip.SetToolTip(transitionAnimationScaleTrackBar, "过渡动画速度，可选 0x、0.5x、1x、1.5x、2x、5x、10x。");
            displayControlToolTip.SetToolTip(animatorDurationScaleTrackBar, "程序/Animator 动画速度，可选 0x、0.5x、1x、1.5x、2x、5x、10x。");
            displayControlToolTip.SetToolTip(applyAnimationScaleButton, "依次设置窗口、过渡和程序动画速度。");
            displayControlToolTip.SetToolTip(restoreAnimationScaleButton, "将三项动画速度恢复为 1x。");
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
            screenshotToolTip.SetToolTip(screenshotPreviewBox, "截屏或录屏完成后，点击预览可用系统默认应用打开文件。");
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

        private TableLayoutPanel CreateDisplayActionRow(Button applyButton, Button restoreButton)
        {
            var actions = new TableLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.Margin = Padding.Empty;
            actions.ColumnCount = 3;
            actions.RowCount = 1;
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            AddActionButton(actions, applyButton, 0);
            AddActionButton(actions, restoreButton, 1);
            return actions;
        }

        private TableLayoutPanel CreateDisplayScaleRow(string labelText, TrackBar trackBar, Label valueLabel, Button applyButton, Button restoreButton)
        {
            var row = new TableLayoutPanel();
            row.Dock = DockStyle.Fill;
            row.ColumnCount = 5;
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
            AddLabel(row, labelText, 0);
            row.Controls.Add(trackBar, 1, 0);
            row.Controls.Add(valueLabel, 2, 0);
            if (applyButton != null) AddActionButton(row, applyButton, 3);
            if (restoreButton != null) AddActionButton(row, restoreButton, 4);
            return row;
        }

        private TableLayoutPanel CreateDisplayScaleMarkerPanel(double[] scaleOptions)
        {
            var markerHost = new TableLayoutPanel();
            markerHost.Dock = DockStyle.Fill;
            markerHost.Margin = Padding.Empty;
            markerHost.ColumnCount = 3;
            markerHost.RowCount = 1;
            markerHost.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
            markerHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            markerHost.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 196));
            markerHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var markers = new TableLayoutPanel();
            markers.Dock = DockStyle.Fill;
            markers.Margin = Padding.Empty;
            markers.ColumnCount = scaleOptions.Length;
            markers.RowCount = 1;
            for (var i = 0; i < scaleOptions.Length; i++)
            {
                markers.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0F / scaleOptions.Length));
                AddDisplayScaleMarkerLabel(markers, FormatDisplayScaleValue(scaleOptions[i]), i);
            }
            markers.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            markerHost.Controls.Add(markers, 1, 0);
            return markerHost;
        }

        private void AddDisplayScaleMarkerLabel(TableLayoutPanel panel, string text, int column)
        {
            var label = new Label();
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.Margin = Padding.Empty;
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.ForeColor = Color.FromArgb(90, 90, 90);
            panel.Controls.Add(label, column, 0);
        }

        private void ConfigureDisplayScaleTrackBar(TrackBar trackBar, double[] scaleOptions)
        {
            trackBar.Dock = DockStyle.Fill;
            trackBar.AutoSize = false;
            trackBar.Height = 30;
            trackBar.Minimum = 0;
            trackBar.Maximum = scaleOptions.Length - 1;
            trackBar.TickFrequency = 1;
            trackBar.SmallChange = 1;
            trackBar.LargeChange = 1;
            trackBar.Value = TrackBarValueFromDisplayScale(DefaultDisplayScale, scaleOptions);
            trackBar.Margin = new Padding(0, 3, 8, 3);
        }

        private void ConfigureDisplayScaleValueLabel(Label label)
        {
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.ForeColor = Color.FromArgb(60, 60, 60);
            label.AutoEllipsis = false;
        }

        private bool ShowToolSettingsDialog()
        {
            using (var dialog = new Form())
            using (var dialogToolTip = new ToolTip())
            {
                dialog.Text = "工具设置";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.ClientSize = new Size(720, 260);
                dialog.Font = Font;

                var panel = new TableLayoutPanel();
                panel.Dock = DockStyle.Fill;
                panel.Padding = new Padding(12);
                panel.ColumnCount = 1;
                panel.RowCount = 5;
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
                panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
                dialog.Controls.Add(panel);

                var adbTextBox = new TextBox();
                var aaptTextBox = new TextBox();
                var browseAdbButton = new Button();
                var browseAaptButton = new Button();
                var autoDetectButton = new Button();
                var clearButton = new Button();
                var statusLabel = new Label();
                var okButton = new Button();
                var cancelButton = new Button();

                adbTextBox.Text = configuredAdbPath;
                aaptTextBox.Text = configuredAaptPath;
                AddToolPathRow(panel, 0, "ADB 路径", adbTextBox, browseAdbButton);
                AddToolPathRow(panel, 1, "AAPT 路径", aaptTextBox, browseAaptButton);

                var actionPanel = new TableLayoutPanel();
                actionPanel.Dock = DockStyle.Fill;
                actionPanel.ColumnCount = 3;
                actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
                actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
                actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                panel.Controls.Add(actionPanel, 0, 2);

                autoDetectButton.Text = "自动检测";
                AddActionButton(actionPanel, autoDetectButton, 0);
                clearButton.Text = "清除自定义路径";
                AddActionButton(actionPanel, clearButton, 1);

                statusLabel.Dock = DockStyle.Fill;
                statusLabel.TextAlign = ContentAlignment.MiddleLeft;
                statusLabel.ForeColor = Color.FromArgb(60, 60, 60);
                panel.Controls.Add(statusLabel, 0, 3);

                var footerPanel = new TableLayoutPanel();
                footerPanel.Dock = DockStyle.Fill;
                footerPanel.ColumnCount = 4;
                footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
                footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
                footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 4));
                panel.Controls.Add(footerPanel, 0, 4);

                var hintLabel = new Label();
                hintLabel.Dock = DockStyle.Fill;
                hintLabel.TextAlign = ContentAlignment.MiddleLeft;
                hintLabel.ForeColor = Color.FromArgb(90, 90, 90);
                hintLabel.Text = "adb 是必需工具；aapt/aapt2 仅用于解析 APK 包名、版本和启动 Activity。";
                footerPanel.Controls.Add(hintLabel, 0, 0);

                okButton.Text = "确定";
                okButton.Dock = DockStyle.Fill;
                okButton.Margin = new Padding(0, 6, 8, 6);
                okButton.DialogResult = DialogResult.OK;
                footerPanel.Controls.Add(okButton, 1, 0);

                cancelButton.Text = "取消";
                cancelButton.Dock = DockStyle.Fill;
                cancelButton.Margin = new Padding(0, 6, 8, 6);
                cancelButton.DialogResult = DialogResult.Cancel;
                footerPanel.Controls.Add(cancelButton, 2, 0);

                dialog.AcceptButton = okButton;
                dialog.CancelButton = cancelButton;

                dialogToolTip.SetToolTip(adbTextBox, "可留空让程序自动从 PATH、ANDROID_HOME、ANDROID_SDK_ROOT 和常见 SDK 目录查找。");
                dialogToolTip.SetToolTip(aaptTextBox, "可选择 Android SDK Build Tools 中的 aapt.exe 或 aapt2.exe。");

                Action refreshStatus = delegate { UpdateToolPathStatus(statusLabel, adbTextBox.Text, aaptTextBox.Text); };
                adbTextBox.TextChanged += delegate { refreshStatus(); };
                aaptTextBox.TextChanged += delegate { refreshStatus(); };
                browseAdbButton.Click += delegate { BrowseToolPath(adbTextBox, "选择 adb.exe", "adb.exe|adb.exe|所有文件 (*.*)|*.*"); refreshStatus(); };
                browseAaptButton.Click += delegate { BrowseToolPath(aaptTextBox, "选择 aapt.exe 或 aapt2.exe", "Android APK 工具 (aapt*.exe)|aapt*.exe|所有文件 (*.*)|*.*"); refreshStatus(); };
                autoDetectButton.Click += delegate
                {
                    adbTextBox.Text = FindAdb(true) ?? "";
                    aaptTextBox.Text = FindAapt(true) ?? "";
                    refreshStatus();
                };
                clearButton.Click += delegate
                {
                    adbTextBox.Text = "";
                    aaptTextBox.Text = "";
                    refreshStatus();
                };
                refreshStatus();

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    UpdateToolPathStatus();
                    return false;
                }

                configuredAdbPath = NormalizeToolPathSetting(adbTextBox.Text);
                configuredAaptPath = NormalizeToolPathSetting(aaptTextBox.Text);
                SaveConfig();
                UpdateToolPathStatus();
                RefreshDevices();
                return true;
            }
        }

        private void AddToolPathRow(TableLayoutPanel parent, int row, string labelText, TextBox textBox, Button button)
        {
            var rowPanel = new TableLayoutPanel();
            rowPanel.Dock = DockStyle.Fill;
            rowPanel.ColumnCount = 3;
            rowPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            rowPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            rowPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            parent.Controls.Add(rowPanel, 0, row);

            var label = new Label();
            label.Text = labelText;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            rowPanel.Controls.Add(label, 0, 0);

            textBox.Dock = DockStyle.Fill;
            textBox.Margin = new Padding(0, 4, 8, 4);
            rowPanel.Controls.Add(textBox, 1, 0);

            button.Text = "选择...";
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(0, 3, 0, 3);
            rowPanel.Controls.Add(button, 2, 0);
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
            deviceHeader.ColumnCount = 3;
            deviceHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            deviceHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            deviceHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            devicePanel.Controls.Add(deviceHeader, 0, 0);
            var deviceLabel = new Label();
            deviceLabel.Text = "目标设备列表";
            deviceLabel.Dock = DockStyle.Fill;
            deviceLabel.TextAlign = ContentAlignment.MiddleLeft;
            deviceHeader.Controls.Add(deviceLabel, 0, 0);
            connectButton.Text = "连接设备";
            connectButton.Dock = DockStyle.Fill;
            connectButton.Margin = new Padding(4, 2, 4, 2);
            deviceHeader.Controls.Add(connectButton, 1, 0);
            refreshButton.Text = "刷新";
            refreshButton.Dock = DockStyle.Fill;
            refreshButton.Margin = new Padding(4, 2, 4, 2);
            deviceHeader.Controls.Add(refreshButton, 2, 0);
            deviceList.Dock = DockStyle.Fill;
            deviceList.CheckOnClick = true;
            devicePanel.Controls.Add(deviceList, 0, 1);
            deviceInfoToolTip.SetToolTip(connectButton, "打开设备连接窗口，输入无线 ADB 地址后连接或断连。");
        }

        private void BuildSharedLogArea(Control parent)
        {
            var logPanel = new TableLayoutPanel();
            logPanel.Dock = DockStyle.Fill;
            logPanel.ColumnCount = 1;
            logPanel.RowCount = 3;
            logPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            logPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            logPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
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

            var logFooter = new TableLayoutPanel();
            logFooter.Dock = DockStyle.Fill;
            logFooter.ColumnCount = 2;
            logFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            logFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            logPanel.Controls.Add(logFooter, 0, 2);
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.Text = "就绪";
            logFooter.Controls.Add(statusLabel, 0, 0);
            settingsButton.Text = "配置";
            settingsButton.Dock = DockStyle.Fill;
            settingsButton.Margin = new Padding(8, 3, 0, 3);
            logFooter.Controls.Add(settingsButton, 1, 0);
        }

        private void WireEvents()
        {
            browseButton.Click += delegate { BrowseApk(); };
            refreshButton.Click += delegate { RefreshDevices(true); };
            settingsButton.Click += delegate { ShowToolSettingsDialog(); };
            connectButton.Click += delegate { ShowDeviceConnectionDialog(); };
            clearLogButton.Click += delegate { logBox.Clear(); };
            installButton.Click += delegate { StartExecution(); };
            cancelButton.Click += delegate { RequestCancel(); };
            apkTextBox.TextChanged += delegate { UpdateApkInfo(apkTextBox.Text); SaveConfig(); };
            installModeRadioButton.CheckedChanged += delegate { UpdateExecutionOptionState(); };
            cleanInstallModeRadioButton.CheckedChanged += delegate { UpdateExecutionOptionState(); };
            uninstallModeRadioButton.CheckedChanged += delegate { UpdateExecutionOptionState(); };
            clearDataModeRadioButton.CheckedChanged += delegate { UpdateExecutionOptionState(); };
            startAppModeRadioButton.CheckedChanged += delegate { UpdateExecutionOptionState(); };
            softwarePackageTextBox.TextChanged += delegate { SaveConfig(); };
            softwareAutoFillCheckBox.CheckedChanged += delegate { UpdateSoftwareAutoFillState(); SaveConfig(); };
            softwareLaunchButton.Click += delegate { StartSoftwareManagementOperation(SoftwareManagementOperation.Launch); };
            softwareForceStopButton.Click += delegate { StartSoftwareManagementOperation(SoftwareManagementOperation.ForceStop); };
            softwareInfoButton.Click += delegate { StartSoftwareManagementOperation(SoftwareManagementOperation.ShowInfo); };
            softwareDisableButton.Click += delegate { StartSoftwareManagementOperation(SoftwareManagementOperation.Disable); };
            softwareEnableButton.Click += delegate { StartSoftwareManagementOperation(SoftwareManagementOperation.Enable); };
            softwareClearDataButton.Click += delegate { StartSoftwareManagementOperation(SoftwareManagementOperation.ClearData); };
            softwareUninstallButton.Click += delegate { StartSoftwareManagementOperation(SoftwareManagementOperation.Uninstall); };
            softwareKeepDataUninstallButton.Click += delegate { StartSoftwareManagementOperation(SoftwareManagementOperation.KeepDataUninstall); };
            softwareExtractApkButton.Click += delegate { StartSoftwareManagementOperation(SoftwareManagementOperation.ExtractApk); };
            queryDeviceInfoButton.Click += delegate { StartDeviceInfoRefresh(); };
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
            applyFontScaleButton.Click += delegate { ApplyDisplayFontScale(); };
            restoreFontScaleButton.Click += delegate { RestoreDisplayFontScale(); };
            applyAnimationScaleButton.Click += delegate { ApplyDisplayAnimationScale(); };
            restoreAnimationScaleButton.Click += delegate { RestoreDisplayAnimationScale(); };
            restoreDisplayAllButton.Click += delegate { RestoreAllDisplaySettings(); };
            displayDensityUnitComboBox.SelectedIndexChanged += delegate { UpdateDisplayDensityValueForSelectedUnit(); };
            displayFontScaleTrackBar.ValueChanged += delegate { UpdateDisplayScaleValueLabels(); };
            windowAnimationScaleTrackBar.ValueChanged += delegate { UpdateDisplayScaleValueLabels(); };
            transitionAnimationScaleTrackBar.ValueChanged += delegate { UpdateDisplayScaleValueLabels(); };
            animatorDurationScaleTrackBar.ValueChanged += delegate { UpdateDisplayScaleValueLabels(); };
            browseScreenshotOutputDirButton.Click += delegate { BrowseScreenshotOutputDir(); };
            takeScreenshotButton.Click += delegate { StartScreenshot(); };
            startScreenRecordButton.Click += delegate { StartScreenRecording(); };
            stopScreenRecordButton.Click += delegate { StopScreenRecording(); };
            saveScreenshotAsButton.Click += delegate { SaveLatestCaptureAs(); };
            openScreenshotDirButton.Click += delegate { OpenScreenshotDirectory(); };
            screenshotPreviewBox.Click += delegate { OpenLatestCapture(); };
            logRecordStatusTimer.Interval = 1000;
            logRecordStatusTimer.Tick += delegate { UpdateLogRecordStatus(); };
            screenRecordStatusTimer.Interval = 1000;
            screenRecordStatusTimer.Tick += delegate { UpdateScreenRecordStatus(); };
            softwareForegroundTimer.Interval = 1800;
            softwareForegroundTimer.Tick += delegate { RefreshSoftwareForegroundFromTimer(); };
            tabControl.SelectedIndexChanged += delegate { UpdateSoftwareForegroundTimerState(); };
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
            SaveConfig();
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

        private void BrowseToolPath(TextBox targetTextBox, string title, string filter)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = title;
                dialog.Filter = filter;
                dialog.Multiselect = false;
                var current = ExpandToolPathCandidate(targetTextBox.Text);
                if (!string.IsNullOrWhiteSpace(current) && File.Exists(current))
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(current);
                    dialog.FileName = Path.GetFileName(current);
                }
                if (dialog.ShowDialog(this) == DialogResult.OK) targetTextBox.Text = dialog.FileName;
            }
        }

        private void UpdateToolPathStatus()
        {
            if (settingsButton == null) return;
            toolPathToolTip.SetToolTip(settingsButton, BuildToolPathStatusText(configuredAdbPath, configuredAaptPath));
        }

        private void UpdateToolPathStatus(Label label, string adbPath, string aaptPath)
        {
            if (label == null) return;
            label.Text = BuildToolPathStatusText(adbPath, aaptPath);
        }

        private string BuildToolPathStatusText(string adbPath, string aaptPath)
        {
            var adb = FindAdb(adbPath, false);
            var aapt = FindAapt(aaptPath, false);
            var adbStatus = adb == null
                ? "ADB：未找到，请安装 Android SDK Platform Tools 或手动指定 adb.exe。"
                : "ADB：" + adb + GetToolFallbackNote(adbPath, adb);
            var aaptStatus = aapt == null
                ? "AAPT：未找到，APK 详情解析、启动应用和清空数据等需要包名的功能会受限。"
                : "AAPT：" + aapt + GetToolFallbackNote(aaptPath, aapt);
            return adbStatus + Environment.NewLine + aaptStatus;
        }

        private string GetToolFallbackNote(string configuredPath, string resolvedPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath)) return "";
            var configuredResolved = FindCommand(new[] { ExpandToolPathCandidate(configuredPath) });
            if (string.IsNullOrWhiteSpace(configuredResolved)) return "（自定义路径无效，已回退自动查找）";
            return string.Equals(configuredResolved, resolvedPath, StringComparison.OrdinalIgnoreCase) ? "（自定义）" : "（已回退自动查找）";
        }

        private string GetMissingAdbMessage()
        {
            return "未找到 adb.exe。\r\n\r\n请安装 Android SDK Platform Tools，或在“工具设置”中手动指定 adb.exe 路径。";
        }

        private string GetMissingAaptMessage()
        {
            return "当前操作需要解析 APK 包名，但未找到 aapt.exe 或 aapt2.exe。\r\n\r\n普通安装仍可使用；如需启动应用、清空数据或卸载，请安装 Android SDK Build Tools，或在“工具设置”中手动指定 aapt/aapt2 路径。";
        }

        private void HandleMissingAdb(bool showDialog)
        {
            AddLogLine("未找到 adb.exe。请在工具设置中指定 adb.exe，或安装 Android SDK Platform Tools。");
            statusLabel.Text = "未找到 adb，请打开工具设置配置 adb.exe。";
            UpdateToolPathStatus();
            if (showDialog)
            {
                MessageBox.Show(this, GetMissingAdbMessage(), AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                ShowToolSettingsDialog();
            }
        }

        private void StartDeviceInfoRefresh()
        {
            if (isExecuting || isDeviceCommandRunning || isLogcatRunning || IsMediaCaptureRunning) return;

            var device = GetSingleCheckedDeviceForDeviceInfo();
            if (device == null) return;

            var adb = FindAdb();
            if (adb == null)
            {
                HandleMissingAdb(true);
                return;
            }

            cancelRequested = false;
            isDeviceCommandRunning = true;
            SetDeviceCommandUi(true);
            statusLabel.Text = "正在读取设备信息...";
            AddLogLine("读取设备信息：" + device.Serial);
            var serial = device.Serial;
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

        private DeviceInfo GetSingleCheckedDeviceForDeviceInfo()
        {
            return GetSingleCheckedDevice("设备概览");
        }

        private bool TryGetSingleCheckedDeviceForDeviceInfo(out DeviceInfo device)
        {
            return TryGetSingleCheckedDevice(out device);
        }

        private DeviceInfo GetSingleCheckedDevice(string actionName)
        {
            var checkedItems = deviceList.CheckedItems.Cast<object>().Select(o => o.ToString()).ToList();
            if (checkedItems.Count == 0)
            {
                MessageBox.Show(this, "请先在目标设备列表中勾选一台设备。", AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            if (checkedItems.Count > 1)
            {
                MessageBox.Show(this, actionName + "一次只能选择一台设备。", AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            DeviceInfo device;
            if (!deviceMap.TryGetValue(checkedItems[0], out device) || device.State != "device")
            {
                MessageBox.Show(this, "请选择状态为 device 的设备。", AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            return device;
        }

        private bool TryGetSingleCheckedDevice(out DeviceInfo device)
        {
            device = null;
            var checkedItems = deviceList.CheckedItems.Cast<object>().Select(o => o.ToString()).ToList();
            if (checkedItems.Count != 1) return false;
            return deviceMap.TryGetValue(checkedItems[0], out device) && device.State == "device";
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
                MessageBox.Show(this, "显示密度必须大于等于 72 dpi。", AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        private void ApplyDisplayFontScale()
        {
            if (!EnsureSingleCheckedDeviceForDisplayControl()) return;

            var scale = GetDisplayScaleFromTrackBar(displayFontScaleTrackBar, FontScaleOptions);
            var scaleText = FormatDisplayScaleCommandValue(scale);
            RunDisplayControlOperation("正在修改字体大小倍数...", delegate(string adb, DeviceInfo device)
            {
                if (!ExecuteDisplayControlCommand(adb, device.Serial, "修改字体大小倍数", new[] { "settings", "put", "system", "font_scale", scaleText })) return;
                RefreshDisplayInfoAfterChange(adb, device.Serial, "字体大小倍数已修改：" + FormatDisplayScaleValue(scale));
            });
        }

        private void RestoreDisplayFontScale()
        {
            RunDisplayControlOperation("正在恢复字体大小倍数...", delegate(string adb, DeviceInfo device)
            {
                if (!ExecuteDisplayControlCommand(adb, device.Serial, "恢复字体大小倍数", new[] { "settings", "put", "system", "font_scale", FormatDisplayScaleCommandValue(DefaultDisplayScale) })) return;
                RefreshDisplayInfoAfterChange(adb, device.Serial, "字体大小倍数已恢复为 1x。");
            });
        }

        private void ApplyDisplayAnimationScale()
        {
            if (!EnsureSingleCheckedDeviceForDisplayControl()) return;

            var windowScale = GetDisplayScaleFromTrackBar(windowAnimationScaleTrackBar, AnimationScaleOptions);
            var transitionScale = GetDisplayScaleFromTrackBar(transitionAnimationScaleTrackBar, AnimationScaleOptions);
            var animatorScale = GetDisplayScaleFromTrackBar(animatorDurationScaleTrackBar, AnimationScaleOptions);
            RunDisplayControlOperation("正在修改动画速度...", delegate(string adb, DeviceInfo device)
            {
                if (!ApplyDisplayAnimationScale(adb, device.Serial, windowScale, transitionScale, animatorScale, "修改动画速度")) return;
                RefreshDisplayInfoAfterChange(adb, device.Serial, "动画速度已修改：窗口 " + FormatDisplayScaleValue(windowScale) + "，过渡 " + FormatDisplayScaleValue(transitionScale) + "，程序 " + FormatDisplayScaleValue(animatorScale));
            });
        }

        private void RestoreDisplayAnimationScale()
        {
            RunDisplayControlOperation("正在恢复动画速度...", delegate(string adb, DeviceInfo device)
            {
                if (!ApplyDisplayAnimationScale(adb, device.Serial, DefaultDisplayScale, DefaultDisplayScale, DefaultDisplayScale, "恢复动画速度")) return;
                RefreshDisplayInfoAfterChange(adb, device.Serial, "动画速度已恢复为 1x。");
            });
        }

        private bool ApplyDisplayAnimationScale(string adb, string serial, double windowScale, double transitionScale, double animatorScale, string title)
        {
            if (!ExecuteDisplayControlCommand(adb, serial, title + "（窗口）", new[] { "settings", "put", "global", "window_animation_scale", FormatDisplayScaleCommandValue(windowScale) })) return false;
            if (cancelRequested) return false;
            if (!ExecuteDisplayControlCommand(adb, serial, title + "（过渡）", new[] { "settings", "put", "global", "transition_animation_scale", FormatDisplayScaleCommandValue(transitionScale) })) return false;
            if (cancelRequested) return false;
            if (!ExecuteDisplayControlCommand(adb, serial, title + "（程序）", new[] { "settings", "put", "global", "animator_duration_scale", FormatDisplayScaleCommandValue(animatorScale) })) return false;
            return true;
        }

        private void RestoreAllDisplaySettings()
        {
            RunDisplayControlOperation("正在恢复全部显示设置...", delegate(string adb, DeviceInfo device)
            {
                var sizeOk = ExecuteDisplayControlCommand(adb, device.Serial, "恢复屏幕分辨率", new[] { "wm", "size", "reset" });
                if (cancelRequested) return;
                var densityOk = ExecuteDisplayControlCommand(adb, device.Serial, "恢复显示密度", new[] { "wm", "density", "reset" });
                if (cancelRequested) return;
                var fontOk = ExecuteDisplayControlCommand(adb, device.Serial, "恢复字体大小倍数", new[] { "settings", "put", "system", "font_scale", FormatDisplayScaleCommandValue(DefaultDisplayScale) });
                if (cancelRequested) return;
                var animationOk = ApplyDisplayAnimationScale(adb, device.Serial, DefaultDisplayScale, DefaultDisplayScale, DefaultDisplayScale, "恢复动画速度");
                var message = sizeOk && densityOk && fontOk && animationOk ? "显示设置已全部恢复默认。" : "显示设置恢复未全部成功，请查看日志。";
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
                HandleMissingAdb(true);
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
            var device = GetSingleCheckedDevice("显示控制");
            if (device == null) ClearDisplayControlInfo();
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
            return TryGetSingleCheckedDevice(out device);
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
            ReadDisplayControlScaleSettings(adb, serial, cancellable, info);
            if (cancelRequested) return null;
            if (!hasSize && !hasDensity)
            {
                FailDisplayControlOperation("无法解析设备显示信息。");
                return null;
            }
            if (!hasSize) AddLogLine("无法解析 wm size 输出：" + FirstUsefulLine(sizeResult.Output));
            if (!hasDensity) AddLogLine("无法解析 wm density 输出：" + FirstUsefulLine(densityResult.Output));
            return info;
        }

        private void ReadDisplayControlScaleSettings(string adb, string serial, bool cancellable, DisplayControlInfo info)
        {
            double scale;
            bool isUnset;
            if (TryReadDisplayScaleSetting(adb, serial, "system", "font_scale", "字体大小倍数", cancellable, out scale, out isUnset))
            {
                info.FontScale = scale;
                info.HasFontScale = true;
                info.IsFontScaleUnset = isUnset;
            }
            if (cancelRequested) return;
            if (TryReadDisplayScaleSetting(adb, serial, "global", "window_animation_scale", "窗口动画速度", cancellable, out scale, out isUnset))
            {
                info.WindowAnimationScale = scale;
                info.HasWindowAnimationScale = true;
                info.IsWindowAnimationScaleUnset = isUnset;
            }
            if (cancelRequested) return;
            if (TryReadDisplayScaleSetting(adb, serial, "global", "transition_animation_scale", "过渡动画速度", cancellable, out scale, out isUnset))
            {
                info.TransitionAnimationScale = scale;
                info.HasTransitionAnimationScale = true;
                info.IsTransitionAnimationScaleUnset = isUnset;
            }
            if (cancelRequested) return;
            if (TryReadDisplayScaleSetting(adb, serial, "global", "animator_duration_scale", "程序动画速度", cancellable, out scale, out isUnset))
            {
                info.AnimatorDurationScale = scale;
                info.HasAnimatorDurationScale = true;
                info.IsAnimatorDurationScaleUnset = isUnset;
            }
        }

        private bool TryReadDisplayScaleSetting(string adb, string serial, string table, string key, string title, bool cancellable, out double scale, out bool isUnset)
        {
            scale = DefaultDisplayScale;
            isUnset = false;
            var result = InvokeProcess(adb, new[] { "-s", serial, "shell", "settings", "get", table, key }, cancellable);
            if (result.Canceled)
            {
                FailDisplayControlOperation(title + "读取已中止。");
                return false;
            }
            if (result.ExitCode != 0)
            {
                AddLogLine("读取" + title + "失败：" + HumanizeAdbOutput(result.Output));
                return false;
            }
            if (TryParseDisplayScaleOutput(result.Output, out scale, out isUnset)) return true;
            AddLogLine("无法解析" + title + "输出：" + FirstUsefulLine(result.Output));
            return false;
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

            if (info.HasFontScale)
            {
                displayFontScaleInfoLabel.Text = "字体大小倍数：当前 " + FormatDisplayScaleValue(info.FontScale) + FormatDisplayScaleUnsetSource(info.IsFontScaleUnset);
                SetDisplayScaleTrackBarValue(displayFontScaleTrackBar, FontScaleOptions, info.FontScale, preserveFocusedInputs);
            }
            else
            {
                displayFontScaleInfoLabel.Text = "字体大小倍数：N/A";
                SetDisplayScaleTrackBarValue(displayFontScaleTrackBar, FontScaleOptions, DefaultDisplayScale, preserveFocusedInputs);
            }

            if (HasAnyAnimationScale(info))
            {
                displayAnimationScaleInfoLabel.Text = "动画速度：" + FormatDisplayAnimationSummary(info);
                if (info.HasWindowAnimationScale) SetDisplayScaleTrackBarValue(windowAnimationScaleTrackBar, AnimationScaleOptions, info.WindowAnimationScale, preserveFocusedInputs);
                if (info.HasTransitionAnimationScale) SetDisplayScaleTrackBarValue(transitionAnimationScaleTrackBar, AnimationScaleOptions, info.TransitionAnimationScale, preserveFocusedInputs);
                if (info.HasAnimatorDurationScale) SetDisplayScaleTrackBarValue(animatorDurationScaleTrackBar, AnimationScaleOptions, info.AnimatorDurationScale, preserveFocusedInputs);
            }
            else
            {
                displayAnimationScaleInfoLabel.Text = "动画速度：N/A";
                SetDisplayScaleTrackBarValue(windowAnimationScaleTrackBar, AnimationScaleOptions, DefaultDisplayScale, preserveFocusedInputs);
                SetDisplayScaleTrackBarValue(transitionAnimationScaleTrackBar, AnimationScaleOptions, DefaultDisplayScale, preserveFocusedInputs);
                SetDisplayScaleTrackBarValue(animatorDurationScaleTrackBar, AnimationScaleOptions, DefaultDisplayScale, preserveFocusedInputs);
            }
            UpdateDisplayScaleValueLabels();
        }

        private void ClearDisplayControlInfo()
        {
            currentDisplayControlInfo = null;
            displayResolutionInfoLabel.Text = "屏幕分辨率";
            displayDensityInfoLabel.Text = "显示密度";
            displayFontScaleInfoLabel.Text = "字体大小倍数";
            displayAnimationScaleInfoLabel.Text = "动画速度";
            displayWidthTextBox.Clear();
            displayHeightTextBox.Clear();
            displayDensityValueTextBox.Clear();
            SetDisplayScaleTrackBarValue(displayFontScaleTrackBar, FontScaleOptions, DefaultDisplayScale, false);
            SetDisplayScaleTrackBarValue(windowAnimationScaleTrackBar, AnimationScaleOptions, DefaultDisplayScale, false);
            SetDisplayScaleTrackBarValue(transitionAnimationScaleTrackBar, AnimationScaleOptions, DefaultDisplayScale, false);
            SetDisplayScaleTrackBarValue(animatorDurationScaleTrackBar, AnimationScaleOptions, DefaultDisplayScale, false);
            UpdateDisplayScaleValueLabels();
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

        private void UpdateDisplayScaleValueLabels()
        {
            displayFontScaleValueLabel.Text = FormatDisplayScaleValue(GetDisplayScaleFromTrackBar(displayFontScaleTrackBar, FontScaleOptions));
            windowAnimationScaleValueLabel.Text = FormatDisplayScaleValue(GetDisplayScaleFromTrackBar(windowAnimationScaleTrackBar, AnimationScaleOptions));
            transitionAnimationScaleValueLabel.Text = FormatDisplayScaleValue(GetDisplayScaleFromTrackBar(transitionAnimationScaleTrackBar, AnimationScaleOptions));
            animatorDurationScaleValueLabel.Text = FormatDisplayScaleValue(GetDisplayScaleFromTrackBar(animatorDurationScaleTrackBar, AnimationScaleOptions));
        }

        private void SetDisplayScaleTrackBarValue(TrackBar trackBar, double[] scaleOptions, double scale, bool preserveFocusedInput)
        {
            if (preserveFocusedInput && trackBar.Focused) return;
            trackBar.Value = TrackBarValueFromDisplayScale(scale, scaleOptions);
        }

        private static bool HasAnyAnimationScale(DisplayControlInfo info)
        {
            return info != null && (info.HasWindowAnimationScale || info.HasTransitionAnimationScale || info.HasAnimatorDurationScale);
        }

        private static string FormatDisplayAnimationSummary(DisplayControlInfo info)
        {
            var windowText = info.HasWindowAnimationScale ? FormatDisplayScaleValue(info.WindowAnimationScale) + FormatDisplayScaleUnsetSource(info.IsWindowAnimationScaleUnset) : "N/A";
            var transitionText = info.HasTransitionAnimationScale ? FormatDisplayScaleValue(info.TransitionAnimationScale) + FormatDisplayScaleUnsetSource(info.IsTransitionAnimationScaleUnset) : "N/A";
            var animatorText = info.HasAnimatorDurationScale ? FormatDisplayScaleValue(info.AnimatorDurationScale) + FormatDisplayScaleUnsetSource(info.IsAnimatorDurationScaleUnset) : "N/A";
            return "窗口 " + windowText + "，过渡 " + transitionText + "，程序 " + animatorText;
        }

        private static string FormatDisplayScaleUnsetSource(bool isUnset)
        {
            return isUnset ? "（未设置，按 1x）" : "";
        }

        private static double GetDisplayScaleFromTrackBar(TrackBar trackBar, double[] scaleOptions)
        {
            var optionIndex = ClampInt(trackBar.Value, 0, scaleOptions.Length - 1);
            return scaleOptions[optionIndex];
        }

        private static int TrackBarValueFromDisplayScale(double scale, double[] scaleOptions)
        {
            if (double.IsNaN(scale) || double.IsInfinity(scale)) scale = DefaultDisplayScale;
            var nearestIndex = 0;
            var nearestDistance = double.MaxValue;
            for (var i = 0; i < scaleOptions.Length; i++)
            {
                var distance = Math.Abs(scaleOptions[i] - scale);
                if (distance <= nearestDistance)
                {
                    nearestIndex = i;
                    nearestDistance = distance;
                }
            }
            return nearestIndex;
        }

        private static string FormatDisplayScaleValue(double scale)
        {
            return FormatDisplayScaleCommandValue(scale) + "x";
        }

        private static string FormatDisplayScaleCommandValue(double scale)
        {
            if (double.IsNaN(scale) || double.IsInfinity(scale)) scale = DefaultDisplayScale;
            return scale.ToString("0.##", CultureInfo.InvariantCulture);
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
                MessageBox.Show(this, fieldName + "必须填写正整数。", AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        private static bool TryParseDisplayScaleOutput(string output, out double scale, out bool isUnset)
        {
            scale = DefaultDisplayScale;
            isUnset = false;
            foreach (var rawLine in SplitLines(output))
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;
                if (string.Equals(line, "null", StringComparison.OrdinalIgnoreCase) || string.Equals(line, "undefined", StringComparison.OrdinalIgnoreCase))
                {
                    isUnset = true;
                    return true;
                }
                double value;
                if (double.TryParse(line, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                    double.TryParse(line, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                {
                    scale = Math.Max(0, value);
                    return true;
                }
                return false;
            }
            isUnset = true;
            return true;
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
                HandleMissingAdb(true);
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
                HandleMissingAdb(true);
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
            var canOpenCapture = !busy && HasLatestCaptureFile();
            saveScreenshotAsButton.Enabled = canOpenCapture;
            openScreenshotDirButton.Enabled = !busy;
            screenshotPreviewBox.Cursor = canOpenCapture ? Cursors.Hand : Cursors.Default;
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

        private void SetSoftwareManagementControlsEnabled(bool enabled)
        {
            softwarePackageTextBox.Enabled = enabled;
            softwareForegroundPackageTextBox.Enabled = enabled;
            softwareForegroundActivityTextBox.Enabled = enabled;
            softwareAutoFillCheckBox.Enabled = enabled;
            softwareLaunchButton.Enabled = enabled;
            softwareForceStopButton.Enabled = enabled;
            softwareInfoButton.Enabled = enabled;
            softwareDisableButton.Enabled = enabled;
            softwareEnableButton.Enabled = enabled;
            softwareClearDataButton.Enabled = enabled;
            softwareUninstallButton.Enabled = enabled;
            softwareKeepDataUninstallButton.Enabled = enabled;
            softwareExtractApkButton.Enabled = enabled;
            UpdateSoftwareAutoFillState();
        }

        private void UpdateSoftwareAutoFillState()
        {
            softwarePackageTextBox.ReadOnly = softwareAutoFillCheckBox.Checked;
            if (softwareAutoFillCheckBox.Checked && !string.IsNullOrWhiteSpace(softwareForegroundPackageTextBox.Text))
            {
                softwarePackageTextBox.Text = softwareForegroundPackageTextBox.Text.Trim();
            }
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
            displayFontScaleTrackBar.Enabled = enabled;
            applyFontScaleButton.Enabled = enabled;
            restoreFontScaleButton.Enabled = enabled;
            windowAnimationScaleTrackBar.Enabled = enabled;
            transitionAnimationScaleTrackBar.Enabled = enabled;
            animatorDurationScaleTrackBar.Enabled = enabled;
            applyAnimationScaleButton.Enabled = enabled;
            restoreAnimationScaleButton.Enabled = enabled;
        }

        private void SetDeviceInfoControlsEnabled(bool enabled)
        {
            queryDeviceInfoButton.Enabled = enabled;
        }

        private void SetLogcatUi(bool running)
        {
            var busy = running || isExecuting || isDeviceCommandRunning || IsMediaCaptureRunning;
            browseButton.Enabled = !busy;
            connectButton.Enabled = !busy;
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
            SetDeviceInfoControlsEnabled(!busy);
            SetDisplayControlControlsEnabled(!busy);
            SetSoftwareManagementControlsEnabled(!busy);
            refreshButton.Enabled = !busy;
            settingsButton.Enabled = !busy;
            deviceList.Enabled = !busy;
        }

        private string PrepareLogRecordOutputPath()
        {
            var path = ResolveLogRecordOutputPath(logRecordPathTextBox.Text);
            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show(this, "请输入日志输出路径。", AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                MessageBox.Show(this, "无法创建日志输出目录：" + ex.Message, AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    MessageBox.Show(this, "Tag 不需要填写等级。", AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                MessageBox.Show(this, "请先在目标设备列表中选择一台设备。", AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            if (checkedItems.Count > 1)
            {
                MessageBox.Show(this, "只能同时录制一台设备。", AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            DeviceInfo device;
            if (!deviceMap.TryGetValue(checkedItems[0], out device) || device.State != "device")
            {
                MessageBox.Show(this, "请选择状态为 device 的设备。", AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                MessageBox.Show(this, "请先在目标设备列表中选择一台设备。", AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            if (checkedItems.Count > 1)
            {
                MessageBox.Show(this, actionName + "一次只能选择一台设备。", AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            DeviceInfo device;
            if (!deviceMap.TryGetValue(checkedItems[0], out device) || device.State != "device")
            {
                MessageBox.Show(this, "请选择状态为 device 的设备。", AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                HandleMissingAdb(true);
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
                HandleMissingAdb(true);
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
            settingsButton.Enabled = !busy;
            connectButton.Enabled = !busy;
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
            SetDeviceInfoControlsEnabled(!busy);
            SetDisplayControlControlsEnabled(!busy);
            SetSoftwareManagementControlsEnabled(!busy);
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
            var remotePath = "/data/local/tmp/" + RemoteTempFilePrefix + "-screenshot-" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + "-" + SanitizeLocalFileName(serial) + ".png";
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
                MessageBox.Show(this, "请输入" + actionName + "保存目录。", AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            if (File.Exists(outputDir))
            {
                MessageBox.Show(this, actionName + "保存目录不能是文件。", AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                MessageBox.Show(this, "无法创建" + actionName + "保存目录：" + ex.Message, AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            return "/data/local/tmp/" + RemoteTempFilePrefix + "-screenrecord-" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + "-" + SanitizeLocalFileName(serial) + ".mp4";
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
                MessageBox.Show(this, "当前没有可另存的截屏或录屏。", AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                    MessageBox.Show(this, "另存失败：" + ex.Message, AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OpenLatestCapture()
        {
            if (isExecuting || isDeviceCommandRunning || isLogcatRunning || IsMediaCaptureRunning || !HasLatestCaptureFile()) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = lastCapturePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法使用系统默认应用打开文件：" + ex.Message, AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show(this, "打开截屏目录失败：" + ex.Message, AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        private void ShowDeviceConnectionDialog()
        {
            if (isExecuting || isDeviceCommandRunning || isLogcatRunning || IsMediaCaptureRunning) return;

            using (var dialog = new Form())
            using (var dialogToolTip = new ToolTip())
            {
                dialog.Text = "连接设备";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.ClientSize = new Size(560, 172);
                dialog.Font = Font;

                var panel = new TableLayoutPanel();
                panel.Dock = DockStyle.Fill;
                panel.Padding = new Padding(12);
                panel.ColumnCount = 1;
                panel.RowCount = 3;
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
                panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
                dialog.Controls.Add(panel);

                var addressPanel = new TableLayoutPanel();
                addressPanel.Dock = DockStyle.Fill;
                addressPanel.ColumnCount = 2;
                addressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
                addressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                panel.Controls.Add(addressPanel, 0, 0);

                var addressLabel = new Label();
                addressLabel.Text = "设备地址";
                addressLabel.Dock = DockStyle.Fill;
                addressLabel.TextAlign = ContentAlignment.MiddleLeft;
                addressPanel.Controls.Add(addressLabel, 0, 0);

                var addressTextBox = new TextBox();
                addressTextBox.Dock = DockStyle.Fill;
                addressTextBox.Margin = new Padding(0, 4, 0, 4);
                addressTextBox.Text = lastConnectAddress;
                addressPanel.Controls.Add(addressTextBox, 1, 0);

                var hintLabel = new Label();
                hintLabel.Dock = DockStyle.Fill;
                hintLabel.TextAlign = ContentAlignment.MiddleLeft;
                hintLabel.ForeColor = Color.FromArgb(90, 90, 90);
                hintLabel.Text = "可输入 IP:端口，例如 192.168.1.100:5555；省略端口时默认使用 5555。";
                panel.Controls.Add(hintLabel, 0, 1);

                var footerPanel = new TableLayoutPanel();
                footerPanel.Dock = DockStyle.Fill;
                footerPanel.ColumnCount = 4;
                footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
                footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
                footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
                panel.Controls.Add(footerPanel, 0, 2);

                var connectActionButton = new Button();
                connectActionButton.Text = "连接设备";
                connectActionButton.Dock = DockStyle.Fill;
                connectActionButton.Margin = new Padding(0, 6, 8, 6);
                footerPanel.Controls.Add(connectActionButton, 1, 0);

                var disconnectActionButton = new Button();
                disconnectActionButton.Text = "断连设备";
                disconnectActionButton.Dock = DockStyle.Fill;
                disconnectActionButton.Margin = new Padding(0, 6, 8, 6);
                footerPanel.Controls.Add(disconnectActionButton, 2, 0);

                var cancelActionButton = new Button();
                cancelActionButton.Text = "取消";
                cancelActionButton.Dock = DockStyle.Fill;
                cancelActionButton.Margin = new Padding(0, 6, 0, 6);
                cancelActionButton.DialogResult = DialogResult.Cancel;
                footerPanel.Controls.Add(cancelActionButton, 3, 0);

                dialog.AcceptButton = connectActionButton;
                dialog.CancelButton = cancelActionButton;
                dialogToolTip.SetToolTip(addressTextBox, "可粘贴 adb connect 192.168.1.100:5555，工具会自动提取地址。");

                string commandTitle = null;
                string[] commandArgs = null;
                Action<string, string> prepareCommand = delegate(string title, string adbCommand)
                {
                    var address = NormalizeAdbAddress(addressTextBox.Text);
                    if (address == null)
                    {
                        MessageBox.Show(dialog, "请输入设备地址，例如 192.168.1.100:5555。", AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        addressTextBox.Focus();
                        addressTextBox.SelectAll();
                        return;
                    }

                    lastConnectAddress = address;
                    commandTitle = title;
                    commandArgs = new[] { adbCommand, address };
                    dialog.DialogResult = DialogResult.OK;
                    dialog.Close();
                };

                connectActionButton.Click += delegate { prepareCommand("连接设备", "connect"); };
                disconnectActionButton.Click += delegate { prepareCommand("断连设备", "disconnect"); };
                dialog.Shown += delegate { addressTextBox.Focus(); addressTextBox.SelectAll(); };

                if (dialog.ShowDialog(this) == DialogResult.OK && commandArgs != null) RunDeviceCommand(commandTitle, commandArgs);
            }
        }

        private void RunDeviceCommand(string title, string[] adbArgs)
        {
            if (isExecuting || isDeviceCommandRunning || isLogcatRunning || IsMediaCaptureRunning) return;
            var adb = FindAdb();
            if (adb == null)
            {
                HandleMissingAdb(true);
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
            refreshButton.Enabled = !busy;
            settingsButton.Enabled = !busy;
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
            SetDeviceInfoControlsEnabled(!busy);
            SetDisplayControlControlsEnabled(!busy);
            SetSoftwareManagementControlsEnabled(!busy);
            deviceList.Enabled = !busy;
            if (running) statusLabel.Text = "正在执行设备连接操作...";
        }

        private void StartSoftwareManagementOperation(SoftwareManagementOperation operation)
        {
            if (isExecuting || isDeviceCommandRunning || isLogcatRunning || IsMediaCaptureRunning) return;
            var adb = FindAdb();
            if (adb == null)
            {
                HandleMissingAdb(true);
                return;
            }

            var device = GetSingleCheckedDevice("软件管理");
            if (device == null) return;

            var packageName = GetSoftwarePackageName();
            if (packageName == null) return;

            if (!ConfirmSoftwareOperationBeforeStart(operation, packageName)) return;

            var title = GetSoftwareOperationTitle(operation);
            cancelRequested = false;
            isDeviceCommandRunning = true;
            SetDeviceCommandUi(true);
            softwareManagementStatusLabel.Text = "正在" + title + "：" + packageName;
            statusLabel.Text = "正在" + title + "...";

            var thread = new Thread(new ThreadStart(delegate
            {
                var summary = "";
                try
                {
                    AddLogLine(title + "：" + packageName + "，设备：" + device.Label);
                    var success = ExecuteSoftwareManagementOperation(adb, device, packageName, operation, out summary);
                    if (cancelRequested)
                    {
                        summary = title + "已中止。";
                    }
                    else if (string.IsNullOrWhiteSpace(summary))
                    {
                        summary = success ? title + "完成。" : title + "失败。";
                    }
                    AddLogLine(summary);
                    SetStatus(summary);
                    SetSoftwareManagementStatus(summary);
                }
                catch (Exception ex)
                {
                    summary = title + "异常：" + ex.Message;
                    AddLogLine(summary);
                    SetStatus(title + "异常");
                    SetSoftwareManagementStatus(summary);
                }
                finally
                {
                    isDeviceCommandRunning = false;
                    BeginInvokeIfNeeded(delegate { SetDeviceCommandUi(false); });
                }
            }));
            thread.IsBackground = true;
            thread.Start();
        }

        private bool ExecuteSoftwareManagementOperation(string adb, DeviceInfo device, string packageName, SoftwareManagementOperation operation, out string summary)
        {
            summary = "";
            var serial = device.Serial;
            if (operation == SoftwareManagementOperation.ShowInfo) return ShowSoftwarePackageInfo(adb, serial, packageName, out summary);
            if (operation == SoftwareManagementOperation.ExtractApk) return ExtractSoftwareApk(adb, serial, packageName, out summary);
            if (operation == SoftwareManagementOperation.Uninstall) return UninstallSoftwarePackage(adb, serial, packageName, out summary);

            if (!EnsureSoftwarePackageExists(adb, serial, packageName, out summary)) return false;
            if (cancelRequested) return false;

            ProcessResult result;
            switch (operation)
            {
                case SoftwareManagementOperation.Launch:
                    result = InvokeProcess(adb, new[] { "-s", serial, "shell", "monkey", "-p", packageName, "-c", "android.intent.category.LAUNCHER", "1" }, true);
                    return CompleteSoftwareCommandResult(result, packageName, "启动成功。", "启动失败", out summary);

                case SoftwareManagementOperation.ForceStop:
                    result = InvokeProcess(adb, new[] { "-s", serial, "shell", "am", "force-stop", packageName }, true);
                    return CompleteSoftwareCommandResult(result, packageName, "已强制停止。", "强制停止失败", out summary);

                case SoftwareManagementOperation.Disable:
                    result = InvokeProcess(adb, new[] { "-s", serial, "shell", "pm", "disable-user", packageName }, true);
                    return CompleteSoftwareCommandResult(result, packageName, "已禁用。", "禁用失败", out summary);

                case SoftwareManagementOperation.Enable:
                    result = InvokeProcess(adb, new[] { "-s", serial, "shell", "pm", "enable", packageName }, true);
                    return CompleteSoftwareCommandResult(result, packageName, "已启用。", "启用失败", out summary);

                case SoftwareManagementOperation.ClearData:
                    result = InvokeProcess(adb, new[] { "-s", serial, "shell", "pm", "clear", packageName }, true);
                    if (result.Canceled) { summary = "清除数据已中止。"; return false; }
                    if (result.ExitCode == 0 && result.Output.IndexOf("Success", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        summary = "已清除 " + packageName + " 的应用数据。";
                        return true;
                    }
                    summary = "清除数据失败：" + HumanizeAdbOutput(result.Output);
                    return false;

                case SoftwareManagementOperation.KeepDataUninstall:
                    result = InvokeProcess(adb, new[] { "-s", serial, "shell", "pm", "uninstall", "-k", packageName }, true);
                    if (result.Canceled) { summary = "保留数据卸载已中止。"; return false; }
                    if (result.ExitCode == 0 && result.Output.IndexOf("Success", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        summary = "已保留数据卸载 " + packageName + "。";
                        return true;
                    }
                    summary = "保留数据卸载失败：" + HumanizeAdbOutput(result.Output);
                    return false;
            }

            summary = "未知软件管理操作。";
            return false;
        }

        private bool CompleteSoftwareCommandResult(ProcessResult result, string packageName, string successText, string failureText, out string summary)
        {
            if (result.Canceled)
            {
                summary = failureText + "：已中止。";
                return false;
            }
            if (result.ExitCode == 0 &&
                result.Output.IndexOf("Error", StringComparison.OrdinalIgnoreCase) < 0 &&
                result.Output.IndexOf("No activities found", StringComparison.OrdinalIgnoreCase) < 0)
            {
                summary = packageName + " " + successText;
                return true;
            }
            summary = failureText + "：" + HumanizeAdbOutput(result.Output);
            return false;
        }

        private bool ConfirmSoftwareOperationBeforeStart(SoftwareManagementOperation operation, string packageName)
        {
            if (operation == SoftwareManagementOperation.Disable)
            {
                return MessageBox.Show(this, "确定要禁用 " + packageName + " 吗？\r\n\r\n禁用系统组件可能影响设备功能。", AppDisplayName, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK;
            }
            if (operation == SoftwareManagementOperation.ClearData)
            {
                return MessageBox.Show(this, "确定要清除 " + packageName + " 的全部应用数据吗？", AppDisplayName, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK;
            }
            if (operation == SoftwareManagementOperation.Uninstall)
            {
                return MessageBox.Show(this, "确定要卸载 " + packageName + " 吗？\r\n\r\n如果检测到系统应用，会在执行前再次确认。", AppDisplayName, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK;
            }
            if (operation == SoftwareManagementOperation.KeepDataUninstall)
            {
                return MessageBox.Show(this, "确定要保留数据卸载 " + packageName + " 吗？", AppDisplayName, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK;
            }
            return true;
        }

        private string GetSoftwarePackageName()
        {
            var packageName = (softwarePackageTextBox.Text ?? "").Trim();
            if (packageName.Length == 0)
            {
                MessageBox.Show(this, "请输入目标包名。", AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            if (!IsValidAndroidPackageName(packageName))
            {
                MessageBox.Show(this, "目标包名格式不正确。\r\n\r\n示例：com.android.settings", AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            return packageName;
        }

        private static bool IsValidAndroidPackageName(string packageName)
        {
            return !string.IsNullOrWhiteSpace(packageName) &&
                   Regex.IsMatch(packageName.Trim(), @"^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*)+$");
        }

        private string GetSoftwareOperationTitle(SoftwareManagementOperation operation)
        {
            switch (operation)
            {
                case SoftwareManagementOperation.Launch: return "运行软件";
                case SoftwareManagementOperation.ForceStop: return "强制停止";
                case SoftwareManagementOperation.ShowInfo: return "读取软件信息";
                case SoftwareManagementOperation.Disable: return "禁用软件";
                case SoftwareManagementOperation.Enable: return "启用软件";
                case SoftwareManagementOperation.ClearData: return "清除数据";
                case SoftwareManagementOperation.Uninstall: return "卸载软件";
                case SoftwareManagementOperation.KeepDataUninstall: return "保留数据卸载";
                case SoftwareManagementOperation.ExtractApk: return "提取安装包";
                default: return "软件管理操作";
            }
        }

        private bool EnsureSoftwarePackageExists(string adb, string serial, string packageName, out string summary)
        {
            List<string> paths;
            if (TryGetSoftwarePackagePaths(adb, serial, packageName, out paths, out summary)) return true;
            if (string.IsNullOrWhiteSpace(summary)) summary = packageName + " 不存在，请检查包名。";
            return false;
        }

        private bool TryGetSoftwarePackagePaths(string adb, string serial, string packageName, out List<string> paths, out string summary)
        {
            paths = new List<string>();
            summary = "";
            var result = InvokeProcess(adb, new[] { "-s", serial, "shell", "pm", "path", packageName }, true);
            if (result.Canceled)
            {
                summary = "读取软件路径已中止。";
                return false;
            }
            if (result.ExitCode != 0)
            {
                summary = packageName + " 不存在，请检查包名：" + HumanizeAdbOutput(result.Output);
                return false;
            }

            foreach (var rawLine in SplitLines(result.Output))
            {
                var line = rawLine.Trim();
                if (!line.StartsWith("package:", StringComparison.OrdinalIgnoreCase)) continue;
                var path = line.Substring("package:".Length).Trim();
                if (path.Length > 0) paths.Add(path);
            }

            if (paths.Count == 0)
            {
                summary = packageName + " 不存在，请检查包名。";
                return false;
            }
            return true;
        }

        private bool ShowSoftwarePackageInfo(string adb, string serial, string packageName, out string summary)
        {
            summary = "";
            List<string> paths;
            if (!TryGetSoftwarePackagePaths(adb, serial, packageName, out paths, out summary)) return false;
            if (cancelRequested) return false;

            var result = InvokeProcessQuiet(adb, new[] { "-s", serial, "shell", "dumpsys", "package", packageName }, true);
            if (result.Canceled)
            {
                summary = "读取软件信息已中止。";
                return false;
            }
            if (result.ExitCode != 0)
            {
                summary = "读取软件信息失败：" + HumanizeAdbOutput(result.Output);
                return false;
            }

            var report = BuildSoftwareInfoReport(packageName, paths, result.Output);
            BeginInvokeIfNeeded(delegate { ShowSoftwareInfoDialog(packageName, report); });
            summary = "已读取软件信息：" + packageName;
            return true;
        }

        private string BuildSoftwareInfoReport(string packageName, List<string> paths, string dumpsysOutput)
        {
            var builder = new StringBuilder();
            builder.AppendLine("包名：" + packageName);
            builder.AppendLine();
            builder.AppendLine("安装路径：");
            foreach (var path in paths) builder.AppendLine("  " + path);
            builder.AppendLine();
            AppendSoftwareInfoValue(builder, "versionName", dumpsysOutput, @"versionName=(?<value>[^\s]+)");
            AppendSoftwareInfoValue(builder, "versionCode", dumpsysOutput, @"versionCode=(?<value>[^\s]+)");
            AppendSoftwareInfoValue(builder, "targetSdk", dumpsysOutput, @"targetSdk=(?<value>[^\s]+)");
            AppendSoftwareInfoValue(builder, "minSdk", dumpsysOutput, @"minSdk=(?<value>[^\s]+)");
            AppendSoftwareInfoValue(builder, "firstInstallTime", dumpsysOutput, @"firstInstallTime=(?<value>[^\r\n]+)");
            AppendSoftwareInfoValue(builder, "lastUpdateTime", dumpsysOutput, @"lastUpdateTime=(?<value>[^\r\n]+)");
            AppendSoftwareInfoValue(builder, "primaryCpuAbi", dumpsysOutput, @"primaryCpuAbi=(?<value>[^\s]+)");
            AppendSoftwareInfoValue(builder, "enabled", dumpsysOutput, @"enabled=(?<value>[^\s]+)");
            AppendSoftwareInfoValue(builder, "stopped", dumpsysOutput, @"stopped=(?<value>[^\s]+)");
            AppendSoftwareInfoValue(builder, "hidden", dumpsysOutput, @"hidden=(?<value>[^\s]+)");
            AppendSoftwareInfoValue(builder, "suspended", dumpsysOutput, @"suspended=(?<value>[^\s]+)");

            var flags = FirstRegexGroup(dumpsysOutput, @"pkgFlags=\[(?<value>[^\]]*)\]", "value");
            if (!string.IsNullOrWhiteSpace(flags)) builder.AppendLine("pkgFlags：" + flags.Trim());
            return builder.ToString();
        }

        private static void AppendSoftwareInfoValue(StringBuilder builder, string title, string text, string pattern)
        {
            var value = FirstRegexGroup(text, pattern, "value");
            if (!string.IsNullOrWhiteSpace(value)) builder.AppendLine(title + "：" + value.Trim());
        }

        private static string FirstRegexGroup(string text, string pattern, string groupName)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[groupName].Value : "";
        }

        private void ShowSoftwareInfoDialog(string packageName, string report)
        {
            using (var dialog = new Form())
            {
                dialog.Text = "软件信息 - " + packageName;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Size = new Size(760, 540);
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowIcon = false;
                dialog.Font = Font;

                var panel = new TableLayoutPanel();
                panel.Dock = DockStyle.Fill;
                panel.Padding = new Padding(12);
                panel.ColumnCount = 1;
                panel.RowCount = 2;
                panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
                dialog.Controls.Add(panel);

                var infoBox = new TextBox();
                infoBox.Dock = DockStyle.Fill;
                infoBox.Multiline = true;
                infoBox.ReadOnly = true;
                infoBox.ScrollBars = ScrollBars.Both;
                infoBox.WordWrap = false;
                infoBox.Font = new Font("Consolas", 9F);
                infoBox.Text = report;
                panel.Controls.Add(infoBox, 0, 0);

                var footer = new TableLayoutPanel();
                footer.Dock = DockStyle.Fill;
                footer.ColumnCount = 2;
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
                panel.Controls.Add(footer, 0, 1);

                var closeButton = new Button();
                closeButton.Text = "关闭";
                closeButton.DialogResult = DialogResult.OK;
                closeButton.Dock = DockStyle.Fill;
                closeButton.Margin = new Padding(8, 4, 0, 0);
                footer.Controls.Add(closeButton, 1, 0);
                dialog.AcceptButton = closeButton;
                dialog.ShowDialog(this);
            }
        }

        private bool ExtractSoftwareApk(string adb, string serial, string packageName, out string summary)
        {
            summary = "";
            List<string> paths;
            if (!TryGetSoftwarePackagePaths(adb, serial, packageName, out paths, out summary)) return false;
            if (cancelRequested) return false;

            EnsureDirectory(apkExtractDir);
            var savedFiles = new List<string>();
            for (var i = 0; i < paths.Count; i++)
            {
                if (cancelRequested)
                {
                    summary = "提取安装包已中止。";
                    return false;
                }

                var remotePath = paths[i];
                var remoteName = SanitizeLocalFileName(GetDevicePathName(remotePath));
                var localName = paths.Count == 1 ? packageName + ".apk" : packageName + "-" + remoteName;
                if (!localName.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) localName += ".apk";
                var localPath = GetAvailableLocalFilePath(Path.Combine(apkExtractDir, localName));
                AddLogLine("提取安装包：" + remotePath + " -> " + localPath);
                var result = InvokeProcess(adb, new[] { "-s", serial, "pull", remotePath, localPath }, true);
                if (result.Canceled)
                {
                    summary = "提取安装包已中止。";
                    return false;
                }
                if (result.ExitCode != 0 || !File.Exists(localPath))
                {
                    summary = "提取安装包失败：" + HumanizeAdbOutput(result.Output);
                    return false;
                }
                savedFiles.Add(localPath);
            }

            summary = "已提取 " + savedFiles.Count + " 个 APK 到：" + apkExtractDir;
            return true;
        }

        private static string GetAvailableLocalFilePath(string path)
        {
            if (!File.Exists(path)) return path;
            var dir = Path.GetDirectoryName(path);
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            for (var i = 1; i < 1000; i++)
            {
                var candidate = Path.Combine(dir, name + "-" + i.ToString() + ext);
                if (!File.Exists(candidate)) return candidate;
            }
            return Path.Combine(dir, name + "-" + DateTime.Now.ToString("yyyyMMddHHmmss") + ext);
        }

        private bool UninstallSoftwarePackage(string adb, string serial, string packageName, out string summary)
        {
            summary = "";
            var isThirdParty = IsPackageListed(adb, serial, "-3", packageName);
            if (cancelRequested) return false;
            if (isThirdParty)
            {
                var result = InvokeProcess(adb, new[] { "-s", serial, "uninstall", packageName }, true);
                if (result.Canceled)
                {
                    summary = "卸载已中止。";
                    return false;
                }
                if (result.ExitCode == 0 && result.Output.IndexOf("Success", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    summary = "已卸载 " + packageName + "。";
                    return true;
                }
                summary = "卸载失败：" + HumanizeAdbOutput(result.Output);
                return false;
            }

            var isSystem = IsPackageListed(adb, serial, "-s", packageName);
            if (cancelRequested) return false;
            if (!isSystem)
            {
                summary = packageName + " 不存在，请检查包名。";
                return false;
            }

            var confirm = ShowDialogMessage("检测到 " + packageName + " 是系统应用。\r\n\r\n卸载系统应用可能导致无法开机、功能异常或需要清除数据恢复。建议优先使用“禁用”。\r\n\r\n确定仍要按当前用户卸载吗？", "卸载系统软件", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
            if (confirm != DialogResult.OK)
            {
                summary = "已取消卸载系统应用：" + packageName;
                return false;
            }

            var systemResult = InvokeProcess(adb, new[] { "-s", serial, "shell", "pm", "uninstall", "-k", "--user", "0", packageName }, true);
            if (systemResult.Canceled)
            {
                summary = "卸载系统应用已中止。";
                return false;
            }
            if (systemResult.ExitCode == 0 && systemResult.Output.IndexOf("Success", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                summary = "已按当前用户卸载系统应用 " + packageName + "。";
                return true;
            }
            summary = "卸载系统应用失败：" + HumanizeAdbOutput(systemResult.Output);
            return false;
        }

        private bool IsPackageListed(string adb, string serial, string packageType, string packageName)
        {
            var result = InvokeProcess(adb, new[] { "-s", serial, "shell", "pm", "list", "packages", packageType, packageName }, true);
            if (result.Canceled || result.ExitCode != 0) return false;
            foreach (var rawLine in SplitLines(result.Output))
            {
                var line = rawLine.Trim();
                if (!line.StartsWith("package:", StringComparison.OrdinalIgnoreCase)) continue;
                var listedPackage = line.Substring("package:".Length).Trim();
                if (string.Equals(listedPackage, packageName, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private void RefreshSoftwareForegroundFromTimer()
        {
            if (!CanRefreshSoftwareForeground()) return;

            DeviceInfo device;
            if (!TryGetSingleCheckedDevice(out device))
            {
                softwareManagementStatusLabel.Text = "勾选一台 device 状态设备后可读取当前界面。";
                return;
            }

            var adb = FindAdb();
            if (adb == null)
            {
                softwareManagementStatusLabel.Text = "未找到 adb.exe，无法读取当前界面。";
                return;
            }

            isSoftwareForegroundRefreshing = true;
            var thread = new Thread(new ThreadStart(delegate
            {
                try
                {
                    string error;
                    var info = ReadForegroundAppInfo(adb, device.Serial, out error);
                    BeginInvokeIfNeeded(delegate
                    {
                        if (info != null)
                        {
                            ApplyForegroundAppInfo(info);
                        }
                        else if (!string.IsNullOrWhiteSpace(error))
                        {
                            softwareManagementStatusLabel.Text = error;
                        }
                    });
                }
                finally
                {
                    isSoftwareForegroundRefreshing = false;
                }
            }));
            thread.IsBackground = true;
            thread.Start();
        }

        private bool CanRefreshSoftwareForeground()
        {
            return tabControl.SelectedTab == softwareManagementTab &&
                   !isSoftwareForegroundRefreshing &&
                   !isExecuting &&
                   !isDeviceCommandRunning &&
                   !isLogcatRunning &&
                   !IsMediaCaptureRunning;
        }

        private void UpdateSoftwareForegroundTimerState()
        {
            if (tabControl.SelectedTab == softwareManagementTab)
            {
                if (!softwareForegroundTimer.Enabled) softwareForegroundTimer.Start();
                RefreshSoftwareForegroundFromTimer();
            }
            else if (softwareForegroundTimer.Enabled)
            {
                softwareForegroundTimer.Stop();
            }
        }

        private ForegroundAppInfo ReadForegroundAppInfo(string adb, string serial, out string error)
        {
            error = "";
            var windowResult = InvokeProcessSilent(adb, new[] { "-s", serial, "shell", "dumpsys", "window", "windows" }, 4500);
            if (windowResult.ExitCode == 0)
            {
                var info = ParseForegroundAppInfo(windowResult.Output);
                if (info != null) return info;
            }

            var windowFallbackResult = InvokeProcessSilent(adb, new[] { "-s", serial, "shell", "dumpsys", "window" }, 4500);
            if (windowFallbackResult.ExitCode == 0)
            {
                var info = ParseForegroundAppInfo(windowFallbackResult.Output);
                if (info != null) return info;
            }

            var activityResult = InvokeProcessSilent(adb, new[] { "-s", serial, "shell", "dumpsys", "activity", "activities" }, 4500);
            if (activityResult.ExitCode == 0)
            {
                var info = ParseForegroundAppInfo(activityResult.Output);
                if (info != null) return info;
            }

            var firstError = FirstUsefulLine(windowResult.Output) ?? FirstUsefulLine(windowFallbackResult.Output) ?? FirstUsefulLine(activityResult.Output);
            error = string.IsNullOrWhiteSpace(firstError) ? "未能解析当前界面包名和活动。" : "读取当前界面失败：" + firstError;
            return null;
        }

        private ForegroundAppInfo ParseForegroundAppInfo(string output)
        {
            if (string.IsNullOrWhiteSpace(output)) return null;
            var keys = new[] { "mCurrentFocus", "mFocusedApp", "mResumedActivity", "topResumedActivity", "ResumedActivity" };
            var lines = SplitLines(output);
            foreach (var key in keys)
            {
                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();
                    if (line.IndexOf(key, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var info = ExtractForegroundComponent(line);
                    if (info != null) return info;
                }
            }

            foreach (var rawLine in lines)
            {
                var info = ExtractForegroundComponent(rawLine);
                if (info != null) return info;
            }
            return null;
        }

        private ForegroundAppInfo ExtractForegroundComponent(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var match = Regex.Match(text, @"(?<pkg>[A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z][A-Za-z0-9_]*)+)/(?<act>[A-Za-z0-9_.$]+|\.[A-Za-z0-9_.$]+)");
            if (!match.Success) return null;
            var packageName = match.Groups["pkg"].Value;
            var activityName = match.Groups["act"].Value;
            if (activityName.StartsWith(".", StringComparison.Ordinal)) activityName = packageName + activityName;
            return new ForegroundAppInfo { PackageName = packageName, ActivityName = activityName };
        }

        private void ApplyForegroundAppInfo(ForegroundAppInfo info)
        {
            if (info == null) return;
            softwareForegroundPackageTextBox.Text = info.PackageName;
            softwareForegroundActivityTextBox.Text = info.ActivityName;
            if (softwareAutoFillCheckBox.Checked && !string.IsNullOrWhiteSpace(info.PackageName))
            {
                softwarePackageTextBox.Text = info.PackageName;
            }
            softwareManagementStatusLabel.Text = string.IsNullOrWhiteSpace(info.PackageName) ? "未识别当前界面包名。" : "当前界面：" + info.PackageName;
        }

        private void SetSoftwareManagementStatus(string text)
        {
            BeginInvokeIfNeeded(delegate { softwareManagementStatusLabel.Text = text; });
        }

        private DialogResult ShowDialogMessage(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            if (IsDisposed) return DialogResult.Cancel;
            if (InvokeRequired)
            {
                var result = DialogResult.Cancel;
                try
                {
                    Invoke(new MethodInvoker(delegate { result = MessageBox.Show(this, text, caption, buttons, icon); }));
                }
                catch
                {
                    result = DialogResult.Cancel;
                }
                return result;
            }
            return MessageBox.Show(this, text, caption, buttons, icon);
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
                var sourceConfigPath = File.Exists(configPath) ? configPath : (File.Exists(legacyConfigPath) ? legacyConfigPath : null);
                if (sourceConfigPath == null) return;
                var json = File.ReadAllText(sourceConfigPath, Encoding.UTF8);
                loadingConfig = true;

                configuredAdbPath = NormalizeToolPathSetting(ReadJsonString(json, "adbPath"));
                configuredAaptPath = NormalizeToolPathSetting(ReadJsonString(json, "aaptPath"));

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

                var softwarePackageName = ReadJsonString(json, "softwarePackageName");
                if (softwarePackageName != null) softwarePackageTextBox.Text = softwarePackageName;

                var softwareAutoFill = ReadJsonBool(json, "softwareAutoFill");
                if (softwareAutoFill.HasValue) softwareAutoFillCheckBox.Checked = softwareAutoFill.Value;

                var logRecordOutputPath = ReadJsonString(json, "logRecordOutputPath");
                if (!string.IsNullOrWhiteSpace(logRecordOutputPath) && !IsLegacyAppRuntimePath(logRecordOutputPath, "log")) logRecordPathTextBox.Text = logRecordOutputPath;

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
                if (!string.IsNullOrWhiteSpace(screenshotOutputDir) && !IsLegacyAppRuntimePath(screenshotOutputDir, "screenshots")) screenshotOutputDirTextBox.Text = screenshotOutputDir;

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
                EnsureDirectory(Path.GetDirectoryName(configPath));
                var lastApkPath = lastApkPathOverride ?? apkTextBox.Text;
                var windowSize = GetConfigWindowSize();
                var json =
                    "{\r\n" +
                    "    \"adbPath\":  \"" + EscapeJsonString(configuredAdbPath) + "\",\r\n" +
                    "    \"aaptPath\":  \"" + EscapeJsonString(configuredAaptPath) + "\",\r\n" +
                    "    \"lastApkPath\":  \"" + EscapeJsonString(lastApkPath) + "\",\r\n" +
                    "    \"softwarePackageName\":  \"" + EscapeJsonString(softwarePackageTextBox.Text) + "\",\r\n" +
                    "    \"softwareAutoFill\":  " + (softwareAutoFillCheckBox.Checked ? "true" : "false") + ",\r\n" +
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
                HandleMissingAdb(true);
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

        private static string GetUserDataDir(string fallbackDir)
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData)) return Path.Combine(localAppData, "ADBTool");
            return Path.Combine(fallbackDir, "user-data");
        }

        private static string GetDefaultScreenshotDir(string fallbackDir)
        {
            var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (!string.IsNullOrWhiteSpace(pictures)) return Path.Combine(pictures, "ADBTool");

            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrWhiteSpace(documents)) return Path.Combine(documents, "ADBTool");

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile)) return Path.Combine(userProfile, "Pictures", "ADBTool");

            return Path.Combine(fallbackDir, "screenshots");
        }

        private static void EnsureDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path)) Directory.CreateDirectory(path);
            }
            catch { }
        }

        private string NormalizeToolPathSetting(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            return path.Trim().Trim('"');
        }

        private string ExpandToolPathCandidate(string path)
        {
            path = NormalizeToolPathSetting(path);
            if (string.IsNullOrWhiteSpace(path)) return "";
            if (Path.IsPathRooted(path)) return path;
            if (path.IndexOf(Path.DirectorySeparatorChar) >= 0 || path.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
            {
                try { return Path.GetFullPath(Path.Combine(appDir, path)); } catch { return path; }
            }
            return path;
        }

        private bool IsLegacyAppRuntimePath(string path, string folderName)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                var configured = Path.GetFullPath(path.Trim().Trim('"')).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var legacy = Path.GetFullPath(Path.Combine(appDir, folderName)).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return string.Equals(configured, legacy, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
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

        private string PrepareApkToolPath(string apkPath, out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(apkPath))
            {
                error = "APK 文件路径为空。";
                return null;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(apkPath);
            }
            catch (Exception ex)
            {
                error = "APK 文件路径无效：" + ex.Message;
                return null;
            }

            if (!File.Exists(fullPath))
            {
                error = "APK 文件不存在。";
                return null;
            }

            var compatiblePath = GetToolCompatibleExistingPath(fullPath);
            if (!string.IsNullOrEmpty(compatiblePath)) return compatiblePath;
            return PrepareStagedApkToolPath(fullPath, out error);
        }

        private string PrepareStagedApkToolPath(string sourcePath, out string error)
        {
            error = "";
            FileInfo sourceInfo;
            try
            {
                sourceInfo = new FileInfo(sourcePath);
            }
            catch (Exception ex)
            {
                error = "读取 APK 文件信息失败：" + ex.Message;
                return null;
            }

            if (currentApkStageInfo != null
                && string.Equals(currentApkStageInfo.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase)
                && currentApkStageInfo.Length == sourceInfo.Length
                && currentApkStageInfo.LastWriteUtc == sourceInfo.LastWriteTimeUtc
                && File.Exists(currentApkStageInfo.StagePath))
            {
                return currentApkStageInfo.StagePath;
            }

            var stageDir = FindWritableApkStageDir(out error);
            if (string.IsNullOrEmpty(stageDir)) return null;
            CleanupOldApkStageFiles(stageDir);

            var stagePath = Path.Combine(stageDir, BuildApkStageFileName(sourcePath, sourceInfo));
            try
            {
                File.Copy(sourcePath, stagePath, true);
                var stageInfo = new FileInfo(stagePath);
                if (stageInfo.Length != sourceInfo.Length)
                {
                    error = "APK 临时副本大小不一致，请重试。";
                    return null;
                }

                currentApkStageInfo = new ApkStageInfo
                {
                    SourcePath = sourcePath,
                    StagePath = stagePath,
                    Length = sourceInfo.Length,
                    LastWriteUtc = sourceInfo.LastWriteTimeUtc
                };
                return stagePath;
            }
            catch (Exception ex)
            {
                error = "无法为 adb/aapt 创建 APK 临时副本：" + ex.Message;
                return null;
            }
        }

        private string FindWritableApkStageDir(out string error)
        {
            error = "";
            var candidates = new List<string>();
            candidates.Add(apkStageDir);

            var tempPath = Path.GetTempPath();
            if (!string.IsNullOrWhiteSpace(tempPath)) candidates.Add(Path.Combine(tempPath, "ADBTool", ApkStageDirName));

            candidates.Add(Path.Combine(appDir, ApkStageDirName));

            var commonData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (!string.IsNullOrWhiteSpace(commonData)) candidates.Add(Path.Combine(commonData, "ADBTool", ApkStageDirName));

            var appRoot = Path.GetPathRoot(appDir);
            if (!string.IsNullOrWhiteSpace(appRoot)) candidates.Add(Path.Combine(appRoot, "ADBTool", ApkStageDirName));

            foreach (var candidate in candidates.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string compatibleDir;
                if (TryPrepareWritableCompatibleDirectory(candidate, out compatibleDir)) return compatibleDir;
            }

            error = "无法创建仅包含英文字符的临时目录。请将工具放在可写英文路径，或把 APK 移到英文路径后重试。";
            return null;
        }

        private static bool TryPrepareWritableCompatibleDirectory(string directory, out string compatibleDirectory)
        {
            compatibleDirectory = null;
            try
            {
                Directory.CreateDirectory(directory);
                var testPath = Path.Combine(directory, ".write-test-" + Guid.NewGuid().ToString("N") + ".tmp");
                using (File.Create(testPath)) { }
                SafeDeleteFile(testPath);

                compatibleDirectory = GetToolCompatibleExistingPath(directory);
                return !string.IsNullOrEmpty(compatibleDirectory);
            }
            catch
            {
                return false;
            }
        }

        private void CleanupOldApkStageFiles(string stageDir)
        {
            if (apkStageCleanupDone) return;
            apkStageCleanupDone = true;
            try
            {
                var cutoff = DateTime.UtcNow.AddDays(-3);
                foreach (var file in Directory.GetFiles(stageDir, "apk-*.apk"))
                {
                    try
                    {
                        if (File.GetLastWriteTimeUtc(file) < cutoff) File.Delete(file);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static string BuildApkStageFileName(string sourcePath, FileInfo sourceInfo)
        {
            return "apk-"
                + ComputePathHash(sourcePath).ToString("x8", CultureInfo.InvariantCulture)
                + "-"
                + sourceInfo.Length.ToString(CultureInfo.InvariantCulture)
                + "-"
                + sourceInfo.LastWriteTimeUtc.Ticks.ToString("x", CultureInfo.InvariantCulture)
                + ".apk";
        }

        private static uint ComputePathHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (var b in Encoding.UTF8.GetBytes(value ?? ""))
                {
                    hash ^= b;
                    hash *= 16777619;
                }
                return hash;
            }
        }

        private static string GetToolCompatibleExistingPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (IsAsciiToolPath(path)) return path;
            return TryGetShortAsciiPath(path);
        }

        private static string TryGetShortAsciiPath(string path)
        {
            try
            {
                var buffer = new StringBuilder(260);
                var length = GetShortPathName(path, buffer, buffer.Capacity);
                if (length > buffer.Capacity)
                {
                    buffer = new StringBuilder(length + 1);
                    length = GetShortPathName(path, buffer, buffer.Capacity);
                }
                if (length <= 0) return null;

                var shortPath = buffer.ToString();
                return IsAsciiToolPath(shortPath) ? shortPath : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsAsciiToolPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            foreach (var ch in path)
            {
                if (ch < 32 || ch > 126) return false;
            }
            return true;
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
            if (aapt == null) { info.ParseError = GetMissingAaptMessage().Replace("\r\n\r\n", " "); return info; }

            string apkToolPathError;
            var apkToolPath = PrepareApkToolPath(apkPath, out apkToolPathError);
            if (string.IsNullOrEmpty(apkToolPath)) { info.ParseError = apkToolPathError; return info; }

            var result = InvokeProcess(aapt, new[] { "dump", "badging", apkToolPath }, false);
            if (result.ExitCode != 0) { info.ParseError = FirstUsefulLine(result.Output) ?? "aapt/aapt2 执行失败。"; return info; }
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
            RefreshDevices(false);
        }

        private void RefreshDevices(bool showMissingAdbDialog)
        {
            if (isExecuting || isDeviceCommandRunning || IsMediaCaptureRunning) return;
            var previousSerial = currentDeviceInfoSerial;
            DeviceInfo selectedDevice;
            if (TryGetSingleCheckedDeviceForDeviceInfo(out selectedDevice)) previousSerial = selectedDevice.Serial;
            deviceList.Items.Clear();
            deviceMap.Clear();
            var adb = FindAdb();
            if (adb == null) { HandleMissingAdb(showMissingAdbDialog); return; }
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
                deviceInfoTextBox.Text = "请在下方目标设备列表中勾选一台 device 状态设备，然后点击查询。";
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
                MessageBox.Show(this, "请选择有效的 APK 文件。", AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var checkedItems = deviceList.CheckedItems.Cast<object>().Select(o => o.ToString()).ToList();
            if (checkedItems.Count == 0)
            {
                MessageBox.Show(this, "请至少选择一台设备。", AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var adb = FindAdb();
            if (adb == null)
            {
                HandleMissingAdb(true);
                return;
            }
            var mode = GetExecutionMode();
            var launchAfterInstall = launchAfterInstallCheckBox.Checked;
            string apkToolPathError;
            var apkToolPath = PrepareApkToolPath(apkPath, out apkToolPathError);
            if (string.IsNullOrEmpty(apkToolPath))
            {
                MessageBox.Show(this, "无法为 adb/aapt 准备 APK 文件。\r\n\r\n" + apkToolPathError, AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var apkInfo = currentApkInfo ?? GetApkInfo(apkPath);
            if ((mode != "Install" || launchAfterInstall) && string.IsNullOrEmpty(apkInfo.PackageName))
            {
                var detail = string.IsNullOrWhiteSpace(apkInfo.ParseError) ? "请确认 APK 文件有效，或在“工具设置”中配置 aapt/aapt2。" : apkInfo.ParseError;
                MessageBox.Show(this, "当前操作需要 APK 包名，但解析 APK 信息失败。\r\n\r\n" + detail, AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (FindAapt() == null) ShowToolSettingsDialog();
                return;
            }
            SaveLastApkPath(apkPath);
            cancelRequested = false;
            isExecuting = true;
            SetExecutingUi(true);
            var thread = new Thread(new ThreadStart(delegate { ExecuteOnDevices(adb, apkPath, apkToolPath, apkInfo, checkedItems, mode, launchAfterInstall); }));
            thread.IsBackground = true;
            thread.Start();
        }

        private void ExecuteOnDevices(string adb, string apkPath, string apkToolPath, ApkInfo apkInfo, List<string> checkedItems, string mode, bool launchAfterInstall)
        {
            var successCount = 0;
            var failedCount = 0;
            var skippedCount = 0;
            try
            {
                AddLogLine("开始执行，APK：" + apkPath);
                if (!string.Equals(apkPath, apkToolPath, StringComparison.OrdinalIgnoreCase)) AddLogLine("已使用兼容路径执行 adb/aapt：" + apkToolPath);
                for (var index = 0; index < checkedItems.Count; index++)
                {
                    if (cancelRequested) { AddLogLine("用户已中止，停止后续设备操作。"); break; }
                    var label = checkedItems[index];
                    DeviceInfo device;
                    if (!deviceMap.TryGetValue(label, out device)) { AddLogLine("跳过未知设备：" + label); skippedCount++; continue; }
                    AddLogLine("[" + (index + 1) + "/" + checkedItems.Count + "] 处理设备：" + label);
                    if (device.State != "device") { AddLogLine("设备不可用，状态为 " + device.State + "。请检查 USB 调试授权。"); skippedCount++; continue; }
                    var ok = ExecuteForDevice(adb, apkToolPath, apkInfo, device.Serial, mode, launchAfterInstall);
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
            return InvokeProcessSilent(filePath, arguments, 0);
        }

        private ProcessResult InvokeProcessSilent(string filePath, string[] arguments, int timeoutMilliseconds)
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
                if (timeoutMilliseconds > 0)
                {
                    var waitUntil = DateTime.Now.AddMilliseconds(timeoutMilliseconds);
                    while (!process.WaitForExit(100))
                    {
                        if (DateTime.Now >= waitUntil)
                        {
                            TryKill(process);
                            return new ProcessResult { ExitCode = 124, Output = outputBuilder.ToString(), Canceled = false };
                        }
                    }
                }
                else
                {
                    process.WaitForExit();
                }
                try { process.WaitForExit(); } catch { }
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
            settingsButton.Enabled = !busy;
            connectButton.Enabled = !busy;
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
            SetDeviceInfoControlsEnabled(!busy);
            SetDisplayControlControlsEnabled(!busy);
            SetSoftwareManagementControlsEnabled(!busy);
            cancelButton.Enabled = executing || isDeviceCommandRunning || IsMediaCaptureRunning;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            statusLabel.Text = executing ? "正在执行..." : statusLabel.Text;
        }

        private void SetScreenshotUi(bool running)
        {
            var busy = running || isExecuting || isDeviceCommandRunning || isLogcatRunning;
            browseButton.Enabled = !busy;
            refreshButton.Enabled = !busy;
            settingsButton.Enabled = !busy;
            connectButton.Enabled = !busy;
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
            SetDeviceInfoControlsEnabled(!busy);
            SetDisplayControlControlsEnabled(!busy);
            SetSoftwareManagementControlsEnabled(!busy);
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
            softwareForegroundTimer.Stop();
            CaptureLayoutHeights();
            SaveConfig();
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
                var path = Path.Combine(logDir, RunLogPrefix + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
                string text = "";
                BeginInvokeIfNeeded(delegate { text = logBox.Text; });
                Thread.Sleep(50);
                File.WriteAllText(path, text, Encoding.UTF8);
            }
            catch { }
        }

        private string FindAdb()
        {
            return FindAdb(false);
        }

        private string FindAdb(bool ignoreConfiguredPath)
        {
            return FindAdb(configuredAdbPath, ignoreConfiguredPath);
        }

        private string FindAdb(string adbPath, bool ignoreConfiguredPath)
        {
            var candidates = new List<string>();
            if (!ignoreConfiguredPath) candidates.Add(ExpandToolPathCandidate(adbPath));
            candidates.Add(Path.Combine(appDir, "adb.exe"));
            candidates.Add(Path.Combine(appDir, "platform-tools", "adb.exe"));
            candidates.Add(Path.Combine(appDir, "sdk", "platform-tools", "adb.exe"));
            candidates.Add(Path.Combine(appDir, "Android", "Sdk", "platform-tools", "adb.exe"));
            candidates.Add(Path.Combine(appDir, "scrcpy", "adb.exe"));
            candidates.Add(Path.Combine(appDir, "bin", "scrcpy", "adb.exe"));
            candidates.Add("adb.exe");
            foreach (var root in GetAndroidSdkRootCandidates()) candidates.Add(Path.Combine(root, "platform-tools", "adb.exe"));
            return FindCommand(candidates);
        }

        private string FindAapt()
        {
            return FindAapt(false);
        }

        private string FindAapt(bool ignoreConfiguredPath)
        {
            return FindAapt(configuredAaptPath, ignoreConfiguredPath);
        }

        private string FindAapt(string aaptPath, bool ignoreConfiguredPath)
        {
            var candidates = new List<string>();
            if (!ignoreConfiguredPath) candidates.Add(ExpandToolPathCandidate(aaptPath));
            candidates.Add(Path.Combine(appDir, "aapt.exe"));
            candidates.Add(Path.Combine(appDir, "aapt2.exe"));
            candidates.Add(Path.Combine(appDir, "build-tools", "aapt.exe"));
            candidates.Add(Path.Combine(appDir, "build-tools", "aapt2.exe"));
            candidates.Add("aapt.exe");
            candidates.Add("aapt2.exe");
            foreach (var root in GetAndroidSdkRootCandidates()) AddBuildToolCandidates(candidates, root);
            return FindCommand(candidates);
        }

        private static IEnumerable<string> GetAndroidSdkRootCandidates()
        {
            var roots = new List<string>();
            roots.Add(Environment.GetEnvironmentVariable("ANDROID_HOME"));
            roots.Add(Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"));

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData)) roots.Add(Path.Combine(localAppData, "Android", "Sdk"));

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                roots.Add(Path.Combine(programFiles, "Android", "android-sdk"));
                roots.Add(Path.Combine(programFiles, "Android", "Sdk"));
            }

            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrWhiteSpace(programFilesX86))
            {
                roots.Add(Path.Combine(programFilesX86, "Android", "android-sdk"));
                roots.Add(Path.Combine(programFilesX86, "Android", "Sdk"));
            }

            return roots
                .Where(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p))
                .Select(p => Path.GetFullPath(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AddBuildToolCandidates(List<string> candidates, string sdkRoot)
        {
            var buildTools = Path.Combine(sdkRoot, "build-tools");
            if (!Directory.Exists(buildTools)) return;
            try
            {
                foreach (var toolName in new[] { "aapt.exe", "aapt2.exe" })
                {
                    foreach (var tool in Directory.GetFiles(buildTools, toolName, SearchOption.AllDirectories).OrderByDescending(p => p))
                    {
                        candidates.Add(tool);
                    }
                }
            }
            catch { }
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
            if (argument.IndexOfAny(new[] { ' ', '\t', '\r', '\n', '"' }) < 0) return argument;

            var builder = new StringBuilder();
            builder.Append('"');
            var backslashCount = 0;
            foreach (var ch in argument)
            {
                if (ch == '\\')
                {
                    backslashCount++;
                    continue;
                }

                if (ch == '"')
                {
                    builder.Append('\\', backslashCount * 2 + 1);
                    builder.Append('"');
                    backslashCount = 0;
                    continue;
                }

                if (backslashCount > 0)
                {
                    builder.Append('\\', backslashCount);
                    backslashCount = 0;
                }
                builder.Append(ch);
            }

            if (backslashCount > 0) builder.Append('\\', backslashCount * 2);
            builder.Append('"');
            return builder.ToString();
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
