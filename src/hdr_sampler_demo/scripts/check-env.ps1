Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Find-CommandPath {
    param([string]$Name)
    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $cmd) {
        return $null
    }

    return $cmd.Source
}

$vswhereDefault = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vswhere = Find-CommandPath "vswhere.exe"
if (-not $vswhere -and (Test-Path -LiteralPath $vswhereDefault)) {
    $vswhere = $vswhereDefault
}

$result = [ordered]@{
    winget = Find-CommandPath "winget.exe"
    dotnet = Find-CommandPath "dotnet.exe"
    nuget = Find-CommandPath "nuget.exe"
    localNuget = (Resolve-Path -LiteralPath "$PSScriptRoot\..\..\..\.agent\cache\tools\nuget.exe" -ErrorAction SilentlyContinue).Path
    msbuild = Find-CommandPath "msbuild.exe"
    cl = Find-CommandPath "cl.exe"
    vswhere = $vswhere
    windowsSdkInclude26100 = Test-Path -LiteralPath "${env:ProgramFiles(x86)}\Windows Kits\10\Include\10.0.26100.0"
    restoredPackages = Test-Path -LiteralPath "$PSScriptRoot\..\packages\Microsoft.Windows.SDK.CPP.10.0.26100.1"
}

if ($vswhere) {
    $instances = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    $result.visualStudioWithVCTools = $instances
} else {
    $result.visualStudioWithVCTools = $null
}

$result.GetEnumerator() | ForEach-Object {
    "{0}: {1}" -f $_.Key, $_.Value
}

