param(
    [string]$Version = "1.0.0"
)

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$OriginalDir = Get-Location
Set-Location $ScriptDir

$CONFIG = "Release"
$OUTPUT_DIR = "publish"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SrunLogin Build Script v$Version" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Clean old files
Write-Host "Cleaning old files..." -ForegroundColor Yellow

$exePath = Join-Path $OUTPUT_DIR "SrunLogin.Wpf.exe"
if (Test-Path $exePath) {
    $process = Get-Process -Name "SrunLogin.Wpf" -ErrorAction SilentlyContinue
    if ($process) {
        Stop-Process -Name "SrunLogin.Wpf" -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
    }
}

# Retry deletion
$retryCount = 3
for ($i = 0; $i -lt $retryCount; $i++) {
    try {
        if (Test-Path $OUTPUT_DIR) {
            Remove-Item -Recurse -Force $OUTPUT_DIR -ErrorAction Stop
        }
        break
    }
    catch {
        if ($i -lt ($retryCount - 1)) {
            Start-Sleep -Milliseconds 500
        }
        else {
            Write-Host "[ERROR] Cannot clean output directory. Is the exe file still running?" -ForegroundColor Red
            exit 1
        }
    }
}

# Build single file exe (framework-dependent)
Write-Host ""
Write-Host "Building WPF single file exe..." -ForegroundColor Yellow
dotnet publish SrunLogin.Wpf/SrunLogin.Wpf.csproj `
    -c $CONFIG `
    -p:SelfContained=false `
    -p:PublishSingleFile=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -p:Version=$Version `
    -p:AssemblyVersion=$Version `
    -p:FileVersion=$Version `
    -o ./$OUTPUT_DIR

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "[ERROR] Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "Output: ./$OUTPUT_DIR/" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

Write-Host ""
Write-Host "Files in output directory:" -ForegroundColor Yellow
Get-ChildItem $OUTPUT_DIR | ForEach-Object { Write-Host "  $($_.Name)" }

# 恢复原来目录
Set-Location $OriginalDir
