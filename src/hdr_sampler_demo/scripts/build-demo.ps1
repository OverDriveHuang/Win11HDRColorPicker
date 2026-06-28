Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$demoRoot = Resolve-Path -LiteralPath "$PSScriptRoot\.."
$vswhereDefault = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"

if (-not (Test-Path -LiteralPath $vswhereDefault)) {
    throw "vswhere.exe was not found. Install Visual Studio Build Tools first."
}

$vsInstall = & $vswhereDefault -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vsInstall) {
    throw "No Visual Studio / Build Tools installation with MSVC x64 tools was found."
}

$vsDevCmd = Join-Path $vsInstall "Common7\Tools\VsDevCmd.bat"
if (-not (Test-Path -LiteralPath $vsDevCmd)) {
    throw "VsDevCmd.bat was not found under $vsInstall."
}

$command = "`"$vsDevCmd`" -arch=x64 -host_arch=x64 && msbuild `"$demoRoot\HdrSamplerDemo.sln`" /m /p:Configuration=Release /p:Platform=x64"
cmd.exe /c $command
if ($LASTEXITCODE -ne 0) {
    throw "msbuild failed with exit code $LASTEXITCODE."
}

Write-Host "Built HDR sampler solution. Main binaries:"
Write-Host "$demoRoot\x64\Release\HdrSamplerDemo.exe"
Write-Host "$demoRoot\x64\Release\HdrColorTests.exe"
Write-Host "$demoRoot\x64\Release\HdrSamplerNative.dll"
