# SrunLogin .NET Build Script

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "publish",
    [switch]$Clean,
    [switch]$Run,
    [switch]$Help
)

$ProjectDir = $PSScriptRoot
$ProjectFile = Join-Path $ProjectDir "SrunLogin.csproj"
$OutputPath = Join-Path $ProjectDir $OutputDir

if ($Help) {
    Write-Host @"
SrunLogin Build Script
======================

Usage:
    .\build.ps1                     # Release build
    .\build.ps1 -Configuration Debug  # Debug build
    .\build.ps1 -OutputDir out        # Custom output dir
    .\build.ps1 -Clean               # Clean before build
    .\build.ps1 -Run                 # Run after build
    .\build.ps1 -Help                # Show help

Options:
    -Configuration  Build mode (Release|Debug), default: Release
    -OutputDir      Output directory, default: publish
    -Clean          Clean bin/obj before build
    -Run            Run after build
    -Help           Show this help

"@
    exit 0
}

Write-Host "=== SrunLogin Build Script ===" -ForegroundColor Cyan
Write-Host ""

# Clean
if ($Clean) {
    Write-Host "[Clean] Removing build artifacts..." -ForegroundColor Yellow
    Remove-Item -Path (Join-Path $ProjectDir "bin") -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path (Join-Path $ProjectDir "obj") -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path $OutputPath -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "[Clean] Done" -ForegroundColor Green
}

# Build
Write-Host "[Build] Building project ($Configuration)..." -ForegroundColor Yellow
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
    $exe = Join-Path $OutputPath "SrunLogin.exe"
    if (Test-Path $exe) {
        & $exe --help
    } else {
        & "$OutputPath/SrunLogin" --help
    }
}

Write-Host ""
Write-Host "=== Done ===" -ForegroundColor Cyan