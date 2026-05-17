# SrunLogin .NET 编译脚本

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "publish",
    [switch]$Clean,
    [switch]$Run,
    [switch]$Help
)

$ProjectDir = $PSScriptRoot
$ProjectFile = Join-Path $ProjectDir "dotnet\SrunLogin.csproj"
$OutputPath = Join-Path $ProjectDir $OutputDir

if ($Help) {
    Write-Host @"
SrunLogin 编译脚本
==================

用法:
    .\build.ps1                    # Release 编译
    .\build.ps1 -Configuration Debug  # Debug 编译
    .\build.ps1 -OutputDir out        # 自定义输出目录
    .\build.ps1 -Clean               # 编译前清理
    .\build.ps1 -Run                 # 编译后运行
    .\build.ps1 -Help                # 显示帮助

参数:
    -Configuration  编译模式 (Release|Debug), 默认: Release
    -OutputDir      输出目录, 默认: publish
    -Clean          编译前清理 bin/obj
    -Run            编译后运行示例
    -Help           显示帮助

"@
    exit 0
}

Write-Host "=== SrunLogin 编译脚本 ===" -ForegroundColor Cyan
Write-Host ""

# 清理
if ($Clean) {
    Write-Host "[清理] 正在清理项目..." -ForegroundColor Yellow
    Remove-Item -Path (Join-Path $ProjectDir "dotnet\bin") -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path (Join-Path $ProjectDir "dotnet\obj") -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path $OutputPath -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "[清理] 完成" -ForegroundColor Green
}

# 编译
Write-Host "[编译] 开始编译 ($Configuration)..." -ForegroundColor Yellow
dotnet build $ProjectFile -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Host "[错误] 编译失败!" -ForegroundColor Red
    exit 1
}
Write-Host "[编译] 编译成功" -ForegroundColor Green

# 发布
Write-Host "[发布] 开始发布..." -ForegroundColor Yellow
Remove-Item -Path $OutputPath -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish $ProjectFile -c $Configuration -o $OutputPath
if ($LASTEXITCODE -ne 0) {
    Write-Host "[错误] 发布失败!" -ForegroundColor Red
    exit 1
}
Write-Host "[发布] 发布成功: $OutputPath" -ForegroundColor Green

# 运行
if ($Run) {
    Write-Host ""
    Write-Host "[运行] 启动程序..." -ForegroundColor Cyan
    $exe = Join-Path $OutputPath "SrunLogin.exe"
    if (Test-Path $exe) {
        & $exe --help
    } else {
        & "$OutputPath/SrunLogin" --help
    }
}

Write-Host ""
Write-Host "=== 完成 ===" -ForegroundColor Cyan