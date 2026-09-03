$ErrorActionPreference = 'Stop'

$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$setupFile = Join-Path $root 'Setup.iss'
$projectFile = Join-Path $root 'WorldTimeAlarms\WorldTimeAlarms.csproj'
$bumpScript = Join-Path $PSScriptRoot 'BumpSetupVersion.ps1'
$runtimeFile = Join-Path $PSScriptRoot 'Redist\windowsdesktop-runtime-10.0.11-win-x64.exe'
$outputDir = Join-Path $root 'InstallerOutput'
$updateManifestFile = Join-Path $root 'update.json'
$innoCandidates = @(
	'C:\Program Files\Inno Setup 7\ISCC.exe',
	'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
)
$inno = $innoCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not (Test-Path $projectFile)) {
	throw "Project file not found: $projectFile"
}

if (-not (Test-Path $setupFile)) {
	throw "Setup.iss not found: $setupFile"
}

if (-not (Test-Path $bumpScript)) {
	throw "Version bump script not found: $bumpScript"
}

if (-not (Test-Path $runtimeFile)) {
	throw "Offline .NET runtime not found: $runtimeFile"
}

if (-not $inno) {
	throw 'ISCC.exe was not found. Install Inno Setup 7 or adjust the script path.'
}

& powershell -ExecutionPolicy Bypass -File $bumpScript
if ($LASTEXITCODE -ne 0) { throw 'Version bump failed.' }

& dotnet publish $projectFile -c Release -r win-x64 --self-contained false
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

& $inno $setupFile
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }

$setupContent = Get-Content $setupFile -Raw -Encoding UTF8
$versionMatch = [System.Text.RegularExpressions.Regex]::Match($setupContent, '#define MyAppVersion "([0-9]+\.[0-9]+\.[0-9]+)"')
if (-not $versionMatch.Success) {
	throw 'Could not determine MyAppVersion from Setup.iss after compile.'
}

$version = $versionMatch.Groups[1].Value
$installerFileName = "WorldTimeAlarms-Setup-$version.exe"
$installerPath = Join-Path $outputDir $installerFileName

if (-not (Test-Path $installerPath)) {
	throw "Installer output not found: $installerPath"
}

$hash = Get-FileHash -Path $installerPath -Algorithm SHA256
$timestamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
$hashFilePath = Join-Path $outputDir ("WorldTimeAlarms-Setup-$version.sha256.txt")

@(
	"File: $installerFileName"
	"Version: $version"
	"BuiltAt: $timestamp"
	"SHA256: $($hash.Hash)"
) | Set-Content -Path $hashFilePath -Encoding UTF8

if (Test-Path $updateManifestFile) {
	try {
		$manifest = Get-Content $updateManifestFile -Raw -Encoding UTF8 | ConvertFrom-Json
		$manifest.version = $version
		$manifest.installerUrl = "https://github.com/9UroboroS/WorldTimeAlarms/releases/download/v$version/WorldTimeAlarms-Setup-$version.exe"
		$manifest.notes = "Update generated on $timestamp"
		$manifest | ConvertTo-Json -Depth 5 | Set-Content -Path $updateManifestFile -Encoding UTF8
	}
	catch {
		Write-Warning "Could not update update.json automatically: $($_.Exception.Message)"
	}
}

Write-Output "Installer build completed successfully: $installerPath"
Write-Output "SHA256 file generated: $hashFilePath"
