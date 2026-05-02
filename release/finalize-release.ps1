param(
    [string]$VersionString
)

$nextVersion = $VersionString -replace '^v', ''

$now = [int](Get-Date -UFormat %s -Millisecond 0)
$repo = Get-Content 'PluginRepo/pluginmaster.json' -raw | ConvertFrom-Json
$repo[0].LastUpdated = $now
$repo[0].DownloadLinkInstall = "https://github.com/WesleyLuk90/ffxiv-vr/releases/download/$VersionString/FfxivVR.zip"
$repo[0].DownloadLinkTesting = "https://github.com/WesleyLuk90/ffxiv-vr/releases/download/$VersionString/FfxivVR.zip"
$repo[0].DownloadLinkUpdate = "https://github.com/WesleyLuk90/ffxiv-vr/releases/download/$VersionString/FfxivVR.zip"
ConvertTo-Json $repo -depth 32 | set-content 'PluginRepo/pluginmaster.json'

git config --global user.email "1383942+WesleyLuk90@users.noreply.github.com"
git config --global user.name "Release Bot"
git add .
git commit -m "Publish Version $nextVersion"
git tag $VersionString
