param(
    [string]$Configuration = 'Release',
    [string]$Version = '1.1.0'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "==> 编译 EduKit ($Configuration)" -ForegroundColor Cyan
dotnet build (Join-Path $root 'EduKit.sln') -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) {
    throw '编译失败'
}

$bin = Join-Path $root "src\EduKit\bin\$Configuration"
$distRoot = Join-Path $root 'dist'
$outDir = Join-Path $distRoot "EduKit-v$Version"
$zipPath = Join-Path $distRoot "EduKit-v$Version.zip"

Write-Host "==> 准备发布目录 $outDir" -ForegroundColor Cyan
if (Test-Path $outDir) {
    Remove-Item $outDir -Recurse -Force
}

New-Item -ItemType Directory -Path $outDir -Force | Out-Null
Copy-Item (Join-Path $bin '*') $outDir -Recurse -Force

# 清理开发文件
Get-ChildItem $outDir -Filter '*.pdb' -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force
Get-ChildItem $outDir -Filter '*.xml' -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force
$runtimeData = Join-Path $outDir 'EduKitData'
if (Test-Path $runtimeData) {
    Remove-Item $runtimeData -Recurse -Force
}

# 附带说明文件
Copy-Item (Join-Path $root 'README.md') $outDir -Force
Copy-Item (Join-Path $root 'LICENSE') $outDir -Force

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Write-Host "==> 打包 $zipPath" -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $outDir '*') -DestinationPath $zipPath -Force

$size = [math]::Round((Get-Item $zipPath).Length / 1KB, 1)
Write-Host "==> 完成：$zipPath（$size KB）" -ForegroundColor Green
