Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$winget = Get-Command winget.exe -ErrorAction Stop

& $winget.Source install `
    --id Microsoft.VisualStudio.2022.BuildTools `
    -e `
    --source winget `
    --accept-source-agreements `
    --accept-package-agreements `
    --override "--wait --norestart --passive --add Microsoft.VisualStudio.Workload.VCTools --add Microsoft.VisualStudio.Component.VC.Tools.x86.x64 --add Microsoft.VisualStudio.Component.Windows11SDK.26100 --add Microsoft.VisualStudio.Component.VC.CMake.Project --add Microsoft.VisualStudio.ComponentGroup.NativeDesktop.Core --includeRecommended"

Write-Host "Build Tools installer finished. Run scripts/check-env.ps1 next."

