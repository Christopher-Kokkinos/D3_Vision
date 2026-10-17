param([string]$SdkVersion = "", [string]$CertificateThumbprint = "")
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$sdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
if (!$SdkVersion) { $SdkVersion = (Get-ChildItem -LiteralPath $sdkRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'x64\makeappx.exe') } | Sort-Object Name -Descending | Select-Object -First 1).Name }
$makeappx = Join-Path $sdkRoot "$SdkVersion\x64\makeappx.exe"
$signtool = Join-Path $sdkRoot "$SdkVersion\x64\signtool.exe"
if (!(Test-Path -LiteralPath $makeappx)) { throw 'Install the Windows 10/11 SDK with MakeAppx and SignTool.' }
# A fresh staging directory avoids publishing stale files; no existing files are deleted.
$stage = Join-Path $workspace ("artifacts\stage-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
& dotnet publish (Join-Path $workspace 'src\D3Vision.UI') -c Release -r win-x64 --self-contained true -o $stage
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Copy-Item -LiteralPath (Join-Path $workspace 'packaging\AppxManifest.xml') -Destination $stage
Copy-Item -LiteralPath (Join-Path $workspace 'packaging\Assets') -Destination $stage -Recurse
$output = Join-Path $workspace 'artifacts\D3Vision.msix'
& $makeappx pack /d $stage /p $output /o
if ($LASTEXITCODE -ne 0) { throw 'MSIX packaging failed.' }
if ($CertificateThumbprint) {
 & $signtool sign /fd SHA256 /sha1 $CertificateThumbprint $output
 if ($LASTEXITCODE -ne 0) { throw 'Signing failed. Certificate subject must match CN=D3Vision.' }
}
Write-Host "Created $output"
