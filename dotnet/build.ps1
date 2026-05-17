# SrunLogin .NET Build Script

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "publish",
    [switch]$Clean,
    [switch]$Run,
    [switch]$Help
)

$ProjectDir = $PSScriptRoot
$ProjectFile = Join-Path $ProjectDir "SrunLogin.GUI\SrunLogin.GUI.csproj"
$OutputPath = Join-Path $ProjectDir $OutputDir

if ($Help) {
    Write-Host @"
SrunLogin Build Script
======================

Usage:
    .\build.ps1                          # Release build
    .\build.ps1 -Configuration Debug      # Debug build
    .\build.ps1 -OutputDir out           # Custom output dir
    .\build.ps1 -Clean                   # Clean before build
    .\build.ps1 -Run                     # Run after build
    .\build.ps1 -Help                    # Show help

"@
    exit 0
}

Write-Host "=== SrunLogin Build Script ===" -ForegroundColor Cyan
Write-Host ""

# Clean
if ($Clean) {
    Write-Host "[Clean] Removing build artifacts..." -ForegroundColor Yellow
    Get-ChildItem -Path $ProjectDir -Include "bin", "obj" -Recurse -Directory | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path $OutputPath -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "[Clean] Done" -ForegroundColor Green
}

# Build
Write-Host "[Build] Building GUI project ($Configuration)..." -ForegroundColor Yellow
dotnet build $ProjectFile -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Host "[Error] Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "[Build] Build succeeded" -ForegroundColor Green

# Publish
Write-Host "[Publish] Publishing..." -ForegroundColor Yellow
Remove-Item -Path $OutputPath -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish $ProjectFile -c $Configuration -o $OutputPath
if ($LASTEXITCODE -ne 0) {
    Write-Host "[Error] Publish failed!" -ForegroundColor Red
    exit 1
}
Write-Host "[Publish] Published to: $OutputPath" -ForegroundColor Green

# Run
if ($Run) {
    Write-Host ""
    Write-Host "[Run] Starting program..." -ForegroundColor Cyan
    $exe = Join-Path $OutputPath "SrunLogin.GUI.exe"
    if (Test-Path $exe) {
        & $exe
    } else {
        & "$OutputPath/SrunLogin.GUI"
    }
}

Write-Host ""
Write-Host "=== Done ===" -ForegroundColor Cyan