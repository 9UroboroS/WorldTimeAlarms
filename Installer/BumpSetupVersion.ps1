$ErrorActionPreference = 'Stop'

$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$setupFile = Join-Path $root 'Setup.iss'
$projectFile = Join-Path $root 'WorldTimeAlarms\WorldTimeAlarms.csproj'

if (-not (Test-Path $setupFile)) {
	throw "Setup.iss not found: $setupFile"
}

if (-not (Test-Path $projectFile)) {
	throw "Project file not found: $projectFile"
}

$setupContent = Get-Content $setupFile -Raw -Encoding UTF8
$setupPattern = '#define MyAppVersion "(\d+)\.(\d+)\.(\d+)"'
$setupMatch = [System.Text.RegularExpressions.Regex]::Match($setupContent, $setupPattern)

if (-not $setupMatch.Success) {
	throw 'Could not find #define MyAppVersion "x.y.z" in Setup.iss'
}

$major = [int]$setupMatch.Groups[1].Value
$minor = [int]$setupMatch.Groups[2].Value
$patch = [int]$setupMatch.Groups[3].Value + 1
$newVersion = "$major.$minor.$patch"
$assemblyVersion = "$newVersion.0"

$setupContent = [System.Text.RegularExpressions.Regex]::Replace(
	$setupContent,
	$setupPattern,
	"#define MyAppVersion `"$newVersion`"",
	1)
Set-Content -Path $setupFile -Value $setupContent -Encoding UTF8

[xml]$projectXml = Get-Content $projectFile -Encoding UTF8
$propertyGroup = $projectXml.Project.PropertyGroup | Select-Object -First 1
if (-not $propertyGroup) {
	throw 'No PropertyGroup found in project file.'
}

foreach ($name in 'Version','AssemblyVersion','FileVersion','InformationalVersion') {
	if (-not $propertyGroup.$name) {
		$node = $projectXml.CreateElement($name, $projectXml.Project.NamespaceURI)
		[void]$propertyGroup.AppendChild($node)
	}
}

$propertyGroup.Version = $newVersion
$propertyGroup.AssemblyVersion = $assemblyVersion
$propertyGroup.FileVersion = $assemblyVersion
$propertyGroup.InformationalVersion = $newVersion
$projectXml.Save($projectFile)

Write-Output "Version synchronized to $newVersion"