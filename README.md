# ADB 工具

这是一个面向 Windows 的 Android ADB 图形化工具。它把常用的 `adb`、`aapt/aapt2` 操作整理成可视化界面，适合 Android 开发、测试、调试和批量设备维护场景使用。

工具支持同时选择多台设备执行操作，并提供设备列表刷新、无线 ADB 连接、运行日志、配置记忆等能力。

## 主要功能

### 设备连接与管理

- 自动刷新并显示 `adb devices -l` 检测到的设备。
- 支持勾选一台或多台 `device` 状态设备作为操作目标。
- 支持无线 ADB 连接和断连，可输入 `IP:端口`，省略端口时默认使用 `5555`。
- 连接设备弹窗会记忆上次使用的设备地址，关闭应用后再次打开仍会自动填入。
- 提供运行日志区域，便于查看每一步 ADB 执行结果。

### APK 安装

- 选择本地 APK 文件并读取应用名称、包名、版本号、版本码和启动 Activity。
- 支持安装/覆盖安装。
- 支持卸载后安装。
- 支持仅卸载、清空数据、启动应用。
- 支持安装完成后自动启动应用。
- 支持对多台设备批量执行，并可在执行过程中中止。

### 软件管理

- 自动识别当前前台界面的包名和 Activity，并可自动填充目标包名。
- 支持启动应用、强制停止、查看软件信息。
- 支持禁用、启用、清除数据。
- 支持普通卸载、保留数据卸载。
- 支持提取已安装应用的 APK 到本机目录。
- 对系统应用卸载会按当前用户执行，并带有风险确认。

### 文件传输

- 支持电脑到手机：通过 `adb push` 发送文件或文件夹到设备目录。
- 支持手机到电脑：通过 `adb pull` 导出设备上的文件或文件夹到本地目录。
- 支持从设备目录或设备路径直接打开手机文件夹；设备路径为文件时会打开其所在文件夹。
- 支持记忆上次使用的传输方向、源路径和目标路径。
- 多设备导出时会按设备区分保存，避免文件互相覆盖。

### 截屏与录屏

- 支持截取设备当前画面并保存为 PNG。
- 支持录制设备屏幕并保存为 MP4。
- 录屏支持设置时长上限和码率。
- 支持另存最近一次截屏或录屏结果。
- 支持直接打开保存目录。

### 日志录制

- 支持清除设备 logcat 缓存。
- 支持导出当前 logcat 缓存。
- 支持持续录制 logcat 到本地文件。
- 支持按 Tag、包名和日志等级过滤。
- 支持选择是否保留线程信息和时间信息。

### 显示控制

- 支持读取当前设备的屏幕分辨率、显示密度、字体大小倍数和动画速度。
- 支持修改屏幕分辨率。
- 支持按 DPI 或最小宽度 dp 修改显示密度。
- 支持调整字体大小倍数。
- 支持调整窗口动画、过渡动画和 Animator 动画速度。
- 支持单项恢复或全部恢复默认显示设置。

### 设置页跳转

- 支持在页面顶部输入设备文件夹路径并点击“跳转指定文件夹”打开；为空时默认打开 `/sdcard`。
- 输入设备文件路径时会自动打开该文件所在文件夹。
- 支持记忆上次成功跳转的设备目录，关闭应用后再次打开仍会自动填入。
- “最近使用”分类保留最近打开的 10 个设置入口，并按最新到最旧实时排序。
- 支持跳转设置首页、Wi-Fi、蓝牙、移动网络、VPN、NFC、网络共享、USB 连接用途等网络与连接页面。
- 支持跳转显示、声音、通知、勿扰模式、位置、安全、隐私、辅助功能等系统页面。
- 支持跳转应用列表、默认应用、通知使用权、使用情况访问、账号与同步等应用与账号页面。
- 支持跳转开发者选项、关于手机、系统更新、电池优化等高级页面。
- 设置页跳转一次只操作一台 `device` 状态设备；系统或厂商 ROM 不支持入口时会显示具体原因，部分入口会回退到最接近的设置页。

### 设备概览

- 支持查询设备系统、硬件、电池、存储和网络信息。
- 信息来源包括 `getprop`、`dumpsys battery`、网络地址等常见 ADB 查询结果。

## 运行环境

- Windows 系统。
- 已启用 USB 调试的 Android 设备，或已开启无线调试/网络 ADB 的设备。
- 必需：`adb.exe`，来自 Android SDK Platform Tools。
- 可选：`aapt.exe` 或 `aapt2.exe`，来自 Android SDK Build Tools。

`adb.exe` 是执行设备操作的必需工具。`aapt/aapt2` 用于解析 APK 包名、版本信息和启动 Activity；如果未配置，普通安装仍可使用，但启动应用、清空数据、卸载等依赖包名解析的操作可能不可用。

## 快速开始

1. 安装 Android SDK Platform Tools，确保本机有 `adb.exe`。
2. 打开 Android 设备的开发者选项和 USB 调试。
3. 运行 `ADB工具.exe`。
4. 点击工具设置，自动检测或手动指定 `adb.exe`、`aapt.exe/aapt2.exe` 路径。
5. 通过 USB 连接设备，或点击“连接设备”输入无线 ADB 地址。
6. 点击“刷新”，在目标设备列表中勾选需要操作的设备。
7. 切换到对应功能页执行 APK 安装、文件传输、截屏录屏、日志录制或设置页跳转等操作。

## 工具路径查找规则

程序会按以下方式查找 ADB 和 AAPT 工具：

- 优先使用工具设置中手动指定的路径。
- 查找程序目录下的 `adb.exe`、`platform-tools\adb.exe`、`sdk\platform-tools\adb.exe` 等常见位置。
- 查找系统 `PATH` 中的 `adb.exe`、`aapt.exe`、`aapt2.exe`。
- 查找 `ANDROID_HOME`、`ANDROID_SDK_ROOT` 以及常见 Android SDK 安装目录。

## 数据与输出目录

程序会把运行配置和输出文件保存到用户目录，避免污染程序目录：

- 配置文件：`%LOCALAPPDATA%\ADBTool\adb-tool.config.json`
- 运行日志目录：`%LOCALAPPDATA%\ADBTool\log`
- APK 提取目录：`%LOCALAPPDATA%\ADBTool\ApkExt`
- APK 临时暂存目录：`%LOCALAPPDATA%\ADBTool\ApkStage`
- 默认截屏/录屏目录：`%USERPROFILE%\Pictures\ADBTool`

旧版本若在程序目录存在 `adb-tool.config.json`，程序会尝试兼容读取。
连接设备弹窗中的上次设备地址也会保存在配置文件中，用于下次自动填入。
“跳转”页中上次成功打开的设备目录也会保存在配置文件中，用于下次自动填入。

## 项目结构

```text
D:\adb_tools
├── AdbTool.cs                 # WinForms 主程序源码
├── AssemblyInfo.cs            # 程序程序集信息
├── ADB工具.exe                # 已编译的可执行文件
├── app.ico                    # 程序图标
├── assets\
│   ├── app-icon.svg           # 图标 SVG 源文件
│   └── build-app-icon.ps1     # 生成 app.ico 的脚本
├── adb-tool.config.json       # 本地运行配置示例/旧版配置
└── install-apk.config.json    # 旧版安装工具配置
```

## 从源码编译

项目是单文件 WinForms 程序，没有依赖 NuGet 包。可在 Visual Studio Developer PowerShell 或已配置 .NET Framework 编译器的命令行中执行：

```powershell
csc /target:winexe /platform:anycpu /out:"ADB工具.exe" /win32icon:app.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll AdbTool.cs AssemblyInfo.cs
```

如需重新生成图标：

```powershell
powershell -ExecutionPolicy Bypass -File .\assets\build-app-icon.ps1
```

图标生成脚本会调用 Microsoft Edge 的无头模式渲染 SVG，再生成多尺寸 `.ico` 文件。

## 常见问题

### 提示未找到 adb.exe

请安装 Android SDK Platform Tools，并在工具设置中手动选择 `adb.exe`，或把 Platform Tools 目录加入系统 `PATH`。

### 设备状态是 unauthorized

请查看手机屏幕，确认 USB 调试授权弹窗。如果没有弹窗，可以尝试重新插拔数据线、关闭再开启 USB 调试，或执行 `adb kill-server` 后重新连接。

### APK 信息解析失败

请确认已配置 `aapt.exe` 或 `aapt2.exe`。它通常位于 Android SDK 的 `build-tools\版本号` 目录中。

### 安装失败：签名不一致

设备上已安装相同包名但签名不同的应用。可选择“卸载后安装”，或先手动卸载旧版本。

### 安装失败：版本过低

设备上已有更高 `versionCode` 的应用。可卸载旧应用，或提高当前 APK 的版本码后重新安装。

## 注意事项

- 卸载、清除数据、禁用应用和显示参数修改都可能影响设备状态，请确认目标设备和包名后再执行。
- 系统应用卸载仅针对当前用户，但仍可能影响设备功能。
- 录屏能力依赖 Android 系统自带 `screenrecord`，不同系统版本可能存在时长、分辨率或权限限制。
- 无线 ADB 需要电脑和设备网络可达，并且设备端已开启对应连接能力。
