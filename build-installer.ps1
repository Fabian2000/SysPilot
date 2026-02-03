# SysPilot Installer Build Script

$ErrorActionPreference = "Stop"
Write-Host "=== SysPilot Installer Build ===" -ForegroundColor Cyan

Get-Process -Name "SysPilotSetup" -ErrorAction SilentlyContinue | Stop-Process -Force
$vsPath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer"
if (Test-Path $vsPath) { $env:PATH = "$vsPath;$env:PATH" }

$root = $PSScriptRoot
$payloadDir = "$root\SysPilot.Installer\_payload"
$resourcesDir = "$root\SysPilot.Installer\Resources"

# Cleanup
Remove-Item -Recurse -Force $payloadDir, "$resourcesDir\Payload.zip", "$resourcesDir\Uninstall.exe" -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $resourcesDir -Force | Out-Null

# 1. Build SysPilot (first - before everything else)
Write-Host "`n1. Building SysPilot..." -ForegroundColor Yellow
dotnet publish "$root\SysPilot.csproj" -c Release -r win-x64 --self-contained -o $payloadDir -p:DebugType=none -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw "SysPilot build failed" }
Get-ChildItem $payloadDir -Filter "*.pdb" -Recurse | Remove-Item -Force

# 2. Build Uninstaller
Write-Host "`n2. Building Uninstaller..." -ForegroundColor Yellow
dotnet publish "$root\SysPilot.Uninstaller" -c Release -r win-x64
if ($LASTEXITCODE -ne 0) { throw "Uninstaller build failed" }
Copy-Item "$root\SysPilot.Uninstaller\bin\Release\net10.0-windows\win-x64\publish\Uninstall.exe" $payloadDir -Force
$uSize = [math]::Round((Get-Item "$payloadDir\Uninstall.exe").Length / 1MB, 1)
Write-Host "   Uninstaller: $uSize MB" -ForegroundColor Gray

# 3. Create Payload ZIP
Write-Host "`n3. Creating Payload.zip..." -ForegroundColor Yellow
Compress-Archive -Path "$payloadDir\*" -DestinationPath "$resourcesDir\Payload.zip" -CompressionLevel Optimal -Force
Remove-Item -Recurse -Force $payloadDir
$pSize = [math]::Round((Get-Item "$resourcesDir\Payload.zip").Length / 1MB, 1)
Write-Host "   Payload: $pSize MB" -ForegroundColor Gray

# 4. Build Installer
Write-Host "`n4. Building Installer..." -ForegroundColor Yellow
dotnet publish "$root\SysPilot.Installer" -c Release -r win-x64
if ($LASTEXITCODE -ne 0) { throw "Installer build failed" }

$exe = "$root\SysPilot.Installer\bin\Release\net10.0-windows\win-x64\publish\SysPilotSetup.exe"
$size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "`n=== Build Complete ===" -ForegroundColor Green
Write-Host "Output: $exe"
Write-Host "Size:   $size MB" -ForegroundColor Cyan
