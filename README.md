# ADB 工具

这是一个面向 Windows 的 Android ADB 图形化工具。它把常用的 `adb`、`aapt/aapt2` 操作整理成可视化界面，适合 Android 开发、测试、调试和批量设备维护场景使用。

工具支持同时选择多台设备执行操作，并提供设备列表刷新、无线 ADB 连接、运行日志、配置记忆等能力。

## 主要功能

### 设备连接与管理

- 自动刷新并显示 `adb devices -l` 检测到的设备，主窗口处于焦点时可按 `F5` 刷新目标设备列表。
- 支持勾选一台或多台 `device` 状态设备作为操作目标。
- 支持无线 ADB 连接和断连，可输入 `IP:端口`，省略端口时默认使用 `5555`。
- 连接设备弹窗会记忆上次使用的设备地址，关闭应用后再次打开仍会自动填入。
- 提供运行日志区域，便于查看每一步 ADB 执行结果。

### APK 安装

- 选择本地 APK 文件并读取应用名称、包名、版本号、版本码和启动 Activity。
- 支持安装/覆盖安装。
- 支持卸载后安装。
- 支持仅卸载、清空数据、启动应用。
- 支持安装完成后自动启动应用；每次打开程序时，“安装后启动”默认勾选，可手动取消。在“安装/覆盖”和“卸载后安装”之间切换时保留当前勾选状态；切换到“仅卸载”“清空数据”或“启动应用”时取消勾选并禁用，返回任一安装模式时自动恢复勾选。
- 支持对多台设备批量执行，并可在执行过程中中止。
- “最近 APK 文件夹”最多保留 20 条记录；左键点击可选择文件夹内最新的 APK，右键可删除记录或在资源管理器中打开该文件夹。

### 软件管理

- 自动识别当前前台界面的包名和 Activity，并可自动填充目标包名。
- 支持启动应用、强制停止、查看软件信息。
- 支持跳转到指定应用的系统应用详情页面。
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

### 文本互传

- 在“文本互传”页直接读写手机剪贴板，支持中文、英文、Emoji、多行文本及特殊符号。
- 使用已有 USB 或无线 ADB 连接，无需手机安装 App、无需投屏窗口或互联网；一次只连接一台 `device` 状态设备。
- 手动收发：“电脑 → 手机”输入文本，或用 `Ctrl+V` / 右键粘贴后点击“发送到手机剪贴板”，再在手机目标应用中长按粘贴；“手机 → 电脑”先在手机复制，点击“读取手机剪贴板”，在右侧选中文本后用 `Ctrl+C` / 右键复制。发送会读回核对，不自动向手机当前应用注入粘贴。
- 两侧分别提供“清空”按钮，只清空对应文本框，不修改手机剪贴板；接收文本不会覆盖发送草稿，右侧预览只读但可选中复制。
- 连接期间约每 0.7 秒检查设备选择和连接状态，不定时读取或传输剪贴板内容；只有点击发送或读取按钮才执行文本传输。
- 只传纯文本，不传图片、文件或富文本格式。
- 单次发送上限为 **262130 个 UTF-8 字节**（约 256 KB，中文、Emoji 会占多个字节），超限会提示分段发送。手机返回内容接近协议截断边界时拒绝交付可能不完整的文本。
- 设备选择变化或连接断开时停止文本连接，需重新连接；重新连接会清空旧接收预览，保留发送草稿。
- 文本仅在当前会话内存中处理，不保存到配置、历史记录或运行日志。日志仅记录方向、长度和结果。
- 内置并校验固定版本的 scrcpy 4.0 服务，运行时临时推送至手机，退出时清理本功能的连接和临时文件。不会关闭其他 ADB/scrcpy 会话。

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
6. 点击“刷新”或在主窗口处于焦点时按 `F5`，在目标设备列表中勾选需要操作的设备。
7. 切换到对应功能页执行 APK 安装、文件传输、文本互传、截屏录屏、日志录制或设置页跳转等操作。使用文本互传时，先勾选一台设备，再点击“连接文本服务”。

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
├── TextTransfer.cs            # 文本互传页面、手动收发与连接状态检查
├── ScrcpyClipboardSession.cs  # 固定版本的剪贴板通信与会话清理
├── AssemblyInfo.cs            # 程序程序集信息
├── ADB工具.exe                # 已编译的可执行文件
├── app.ico                    # 程序图标
├── assets\
│   ├── app-icon.svg           # 图标 SVG 源文件
│   ├── build-app-icon.ps1     # 生成 app.ico 的脚本
│   └── scrcpy\                # 内嵌服务、上游许可证与版本说明
├── tests\                    # 文本协议与可选实机回归测试
├── adb-tool.config.json       # 本地运行配置示例/旧版配置
└── install-apk.config.json    # 旧版安装工具配置
```

## 从源码编译

项目是 .NET Framework WinForms 程序，没有依赖 NuGet 包。文本服务在编译时嵌入 EXE，运行不依赖 `bin\scrcpy` 文件夹。可在 Visual Studio Developer PowerShell 或已配置 .NET Framework 编译器的命令行中执行：

```powershell
csc /target:winexe /platform:anycpu /out:"ADB工具.exe" /win32icon:app.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /resource:assets\scrcpy\scrcpy-server-v4.0,AdbTool.scrcpy-server-v4.0 AdbTool.cs TextTransfer.cs ScrcpyClipboardSession.cs AssemblyInfo.cs
```

如需重新生成图标：

```powershell
powershell -ExecutionPolicy Bypass -File .\assets\build-app-icon.ps1
```

图标生成脚本会调用 Microsoft Edge 的无头模式渲染 SVG，再生成多尺寸 `.ico` 文件。

文本传输测试（临时测试程序运行后自动删除）：

```powershell
# 本地协议回归：分片收包、Unicode、读回核对、异常长度、超限和断开连接等
.\tests\run-text-transfer-tests.ps1

# 可选实机回归：先在手机复制一段普通文本，测试会临时改写并在结束时恢复手机剪贴板
.\tests\run-text-transfer-tests.ps1 -AdbPath "C:\Android\platform-tools\adb.exe" -Serial "设备序列号"

# 加测界面事件：手动收发、清空和连接状态检查，不覆盖电脑实际剪贴板
.\tests\run-text-transfer-tests.ps1 -AdbPath "C:\Android\platform-tools\adb.exe" -Serial "设备序列号" -UiTests
```

scrcpy 服务采用 Apache-2.0 许可证，原始许可证和固定版本说明见 `assets\scrcpy\LICENSE`、`assets\scrcpy\NOTICE.md`。升级时必须同时核对协议、握手版本和 SHA-256，不应单独替换服务文件。

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

- 文本互传依赖手机系统的剪贴板兼容性。若无法读取，请先在手机复制纯文本后重试；若写入后核对失败，可能是厂商系统限制或另一端同时修改了剪贴板，界面会显示失败，不会仅凭请求确认提示成功。

- 卸载、清除数据、禁用应用和显示参数修改都可能影响设备状态，请确认目标设备和包名后再执行。
- 系统应用卸载仅针对当前用户，但仍可能影响设备功能。
- 录屏能力依赖 Android 系统自带 `screenrecord`，不同系统版本可能存在时长、分辨率或权限限制。
- 无线 ADB 需要电脑和设备网络可达，并且设备端已开启对应连接能力。
