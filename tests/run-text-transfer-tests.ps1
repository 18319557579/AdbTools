param([string]$AdbPath = "", [string]$Serial = "", [switch]$UiTests)
$ErrorActionPreference = "Stop"
$projectDir = Split-Path -Parent $PSScriptRoot
$testOutput = Join-Path ([IO.Path]::GetTempPath()) ("adb-tool-text-tests-" + [Guid]::NewGuid().ToString("N") + ".exe")
$compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
Push-Location $projectDir
try {
    & $compiler /nologo /target:exe /main:TextTransferTests "/out:$testOutput" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /resource:assets\scrcpy\scrcpy-server-v4.0,AdbTool.scrcpy-server-v4.0 AdbTool.cs TextTransfer.cs ScrcpyClipboardSession.cs tests\TextTransferTests.cs tests\TextTransferUiTests.cs
    if ($LASTEXITCODE -ne 0) { throw "Test compilation failed." }
    if ($UiTests -and (!$AdbPath -or !$Serial)) { throw "UI tests require AdbPath and Serial." }
    if ($UiTests) { & $testOutput $AdbPath $Serial --ui }
    elseif ($AdbPath -and $Serial) { & $testOutput $AdbPath $Serial }
    elseif ($AdbPath -or $Serial) { throw "Provide both AdbPath and Serial for device tests." }
    else { & $testOutput }
    if ($LASTEXITCODE -ne 0) { throw "Text transfer tests failed." }
}
finally {
    Pop-Location
    Remove-Item -LiteralPath $testOutput -ErrorAction SilentlyContinue
}
