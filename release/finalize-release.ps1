param(
    [Parameter(Mandatory)]
    [string]$VersionString
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

$nextVersion = $VersionString -replace '^v', ''

$manifest = Get-Content "FfxivVr/bin/x64/Release/FfxivVR/FfxivVR.json" -Raw | ConvertFrom-Json -AsHashtable
if ($manifest.AssemblyVersion -ne "$nextVersion.0") {
    throw "Built manifest version $($manifest.AssemblyVersion) does not match $nextVersion"
}

$downloadLink = "https://github.com/WesleyLuk90/ffxiv-vr/releases/download/$VersionString/FfxivVR.zip"
$manifest.Changelog = (Get-Content "release/changelog.txt" -Raw).TrimEnd()
$manifest.DownloadLinkInstall = $downloadLink
$manifest.DownloadLinkTesting = $downloadLink
$manifest.DownloadLinkUpdate = $downloadLink
$manifest.LastUpdated = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()

ConvertTo-Json -InputObject @($manifest) -Depth 32 | Set-Content "PluginRepo/pluginmaster.json"

git config user.name "github-actions[bot]"
git config user.email "41898283+github-actions[bot]@users.noreply.github.com"
git add FfxivVr/FfxivVr.csproj PluginRepo/pluginmaster.json release/changelog.txt
git commit -m "Publish Version $nextVersion"
git tag -a $VersionString -m "FFXIV VR $VersionString"
