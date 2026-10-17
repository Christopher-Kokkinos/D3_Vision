param(
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '1.1.1',
    [string]$RuntimeVersion = '',
    [string]$CompilerPath = '',
    [switch]$Verify
)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path $PSScriptRoot -Parent
if (!$CompilerPath) { $CompilerPath = Join-Path $workspace 'artifacts\tools\nsis-3.12\Bin\makensis.exe' }
if (!(Test-Path $CompilerPath)) {
    $toolsDir = Join-Path $workspace 'artifacts\tools'
    New-Item $toolsDir -ItemType Directory -Force | Out-Null
    $archive = Join-Path $toolsDir 'nsis-3.12.zip'
    Invoke-WebRequest 'https://downloads.sourceforge.net/project/nsis/NSIS%203/3.12/nsis-3.12.zip' -OutFile $archive -UseBasicParsing
    if ((Get-FileHash $archive -Algorithm SHA256).Hash -ne '56581F90DB321581C5381193D796FFFCF2D24B2F8FED2160A6C6A3BAA67F2C4F') { throw 'NSIS download checksum mismatch.' }
    Expand-Archive $archive $toolsDir -Force
}
if (!$RuntimeVersion) {
    $metadata = Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/8.0/releases.json'
    $RuntimeVersion = $metadata.'latest-runtime'
}
if ($RuntimeVersion -notmatch '^8\.0\.\d+$') { throw 'Expected a .NET 8 runtime version.' }
$buildRoot = Join-Path $workspace ('artifacts\installer-' + [guid]::NewGuid().ToString('N'))
$publish = Join-Path $buildRoot 'publish'
$outputDir = Join-Path $workspace 'artifacts\installers'
New-Item $outputDir -ItemType Directory -Force | Out-Null
$output = Join-Path $outputDir "D3Vision-Setup-$Version-win-x64.exe"
& dotnet publish (Join-Path $workspace 'src\D3Vision.UI\D3Vision.UI.csproj') -c Release -r win-x64 --self-contained true --artifacts-path (Join-Path $buildRoot 'build') -o $publish "-p:InstallerRuntimeVersion=$RuntimeVersion" "-p:CustomAfterMicrosoftCommonTargets=$workspace\packaging\Installer.Runtime.targets" "-p:Version=$Version" -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Copy-Item (Join-Path $workspace 'packaging\INSTALL.txt') $publish
$files = @(Get-ChildItem $publish -Recurse -File)
$manifest = @($files | ForEach-Object {
    $relative = $_.FullName.Substring($publish.Length + 1)
    if ($_.Name -match '^(Turbo|ilspy|csharpier|settings\.json|current-session\.json)') { throw "Unexpected payload file: $relative" }
    [ordered]@{ Path = $relative; Bytes = $_.Length; SHA256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash }
})
[ordered]@{ Version = $Version; Runtime = $RuntimeVersion; Architecture = 'win-x64'; Files = $manifest } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $outputDir "D3Vision-$Version-payload.json") -Encoding UTF8
$uninstall = Join-Path $buildRoot 'uninstall-files.nsh'
$lines = @($manifest | ForEach-Object { 'Delete "$INSTDIR\{0}"' -f $_.Path })
$lines += @(Get-ChildItem $publish -Recurse -Directory | Sort-Object { $_.FullName.Length } -Descending | ForEach-Object { 'RMDir "$INSTDIR\{0}"' -f $_.FullName.Substring($publish.Length + 1) })
$lines | Set-Content $uninstall -Encoding UTF8
$sizeKB = [int][Math]::Ceiling(($files | Measure-Object Length -Sum).Sum / 1KB)
$defines = @("/DAPP_VERSION=$Version", "/DPUBLISH_DIR=$publish", "/DPACKAGE_DIR=$workspace\packaging", "/DUNINSTALL_INCLUDE=$uninstall", "/DINSTALL_SIZE_KB=$sizeKB")
$script = Join-Path $workspace 'packaging\D3Vision.nsi'
& $CompilerPath /V2 @defines "/DOUTPUT_FILE=$output" $script
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
if ($Verify) {
    $validationExe = Join-Path $buildRoot 'validation-setup.exe'
    & $CompilerPath /V2 @defines /DVALIDATION_BUILD "/DOUTPUT_FILE=$validationExe" $script
    if ($LASTEXITCODE -ne 0) { throw 'Validation installer compilation failed.' }
    $target = Join-Path $buildRoot 'validation-install'
    $process = Start-Process $validationExe -ArgumentList @('/S', "/D=$target") -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Validation install failed: $($process.ExitCode)" }
    foreach ($file in $manifest) {
        if ((Get-FileHash (Join-Path $target $file.Path) -Algorithm SHA256).Hash -ne $file.SHA256) { throw "Installed payload mismatch: $($file.Path)" }
    }
    $sentinel = Join-Path $target 'user-added-file.txt'
    'Preserve user files.' | Set-Content $sentinel
    $process = Start-Process (Join-Path $target 'Uninstall.exe') -ArgumentList @('/S', "_?=$target") -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Validation uninstall failed: $($process.ExitCode)" }
    foreach ($file in $manifest) { if (Test-Path (Join-Path $target $file.Path)) { throw "Uninstall left payload: $($file.Path)" } }
    if (!(Test-Path $sentinel)) { throw 'Uninstall removed user-added files.' }
    Write-Host "Verified $($manifest.Count) installed file hashes, uninstall, and user-file retention. Validation build omits shortcuts, registry changes, and running-app checks."
}
((Get-FileHash $output -Algorithm SHA256).Hash + '  ' + (Split-Path $output -Leaf)) | Set-Content "$output.sha256" -Encoding ASCII
$zip = Join-Path $outputDir "D3Vision-$Version-win-x64.zip"
$zipFiles = @($output, "$output.sha256", (Join-Path $workspace 'packaging\INSTALL.txt'))
$releaseNotes = Join-Path $workspace "docs\releases\v$Version.md"
if (Test-Path $releaseNotes) { $zipFiles += $releaseNotes }
Compress-Archive -LiteralPath $zipFiles -DestinationPath $zip -CompressionLevel Optimal -Force
((Get-FileHash $zip -Algorithm SHA256).Hash + '  ' + (Split-Path $zip -Leaf)) | Set-Content "$zip.sha256" -Encoding ASCII
Write-Host "Installer: $output"
Write-Host "ZIP: $zip"
Write-Host "Size: $([Math]::Round((Get-Item $output).Length / 1MB, 1)) MB; bundled runtime: $RuntimeVersion"
