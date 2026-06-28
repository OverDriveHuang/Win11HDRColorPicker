Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$demoRoot = Resolve-Path -LiteralPath "$PSScriptRoot\.."
$projectRoot = Resolve-Path -LiteralPath "$demoRoot\..\.."
$nuget = Resolve-Path -LiteralPath "$projectRoot\.agent\cache\tools\nuget.exe" -ErrorAction SilentlyContinue

if (-not $nuget) {
    New-Item -ItemType Directory -Force -Path "$projectRoot\.agent\cache\tools" | Out-Null
    Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile "$projectRoot\.agent\cache\tools\nuget.exe"
    $nuget = Resolve-Path -LiteralPath "$projectRoot\.agent\cache\tools\nuget.exe"
}

& $nuget.Path restore "$demoRoot\HdrSamplerDemo.sln" -PackagesDirectory "$demoRoot\packages" -NonInteractive
