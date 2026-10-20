$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$assemblyPath = Join-Path $projectRoot 'bin\Release\net48\RevitParameterChecker.dll'
if (-not (Test-Path -LiteralPath $assemblyPath)) {
    throw "先在 Release 配置中生成插件 DLL：$assemblyPath"
}

$addinFolder = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2024'
New-Item -ItemType Directory -Path $addinFolder -Force | Out-Null
$manifestSource = Join-Path $projectRoot 'RevitParameterChecker.addin'
$manifestDestination = Join-Path $addinFolder 'RevitParameterChecker.addin'
$manifestText = [System.IO.File]::ReadAllText($manifestSource)
$manifestText = $manifestText.Replace('__ASSEMBLY_PATH__', $assemblyPath)
[System.IO.File]::WriteAllText($manifestDestination, $manifestText, [System.Text.UTF8Encoding]::new($false))
Write-Host "插件已注册到 Revit 2024：$manifestDestination"
Write-Host '请关闭并重新启动 Revit 后，在“BIM交付检查”选项卡中打开参数检查。'
