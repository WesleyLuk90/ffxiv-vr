param(
    [string]$VersionString
)

if ($VersionString -and $VersionString -notmatch '^v') {
    $VersionString = "v$VersionString"
}

$nextVersion = $VersionString -replace '^v', ''
$currentVersion = [version](Select-String -Path ".\FfxivVr\FfxivVR.csproj" -Pattern '<Version>(.*?)</Version>' | ForEach-Object { $_.Matches[0].Groups[1].Value })
$changeLog = git log --pretty=format:"# %s%n%b" $currentVersion..HEAD --invert-grep --grep="Publish Version"
$fixes = $changeLog | Select-String "#\d+" | % { "Closes " + $_.Matches.Value }
$releaseMessage = "Publish Version $nextVersion`n" + [string]::Join("`n", $fixes)

if (!$changeLog) {
    echo "Changelog was empty, exiting"
    Exit 1
}

echo "Deploying $VersionString"
echo "=== Change Log ==="
echo $changeLog
echo "=== Release Message ==="
echo $releaseMessage

[IO.File]::WriteAllLines("release/changelog.txt", $changeLog)

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
git commit -m "$releaseMessage"
git tag $VersionString
