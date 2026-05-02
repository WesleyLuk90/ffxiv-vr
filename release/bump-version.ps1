$xml = [xml](Get-Content -Path .\FfxivVr\FfxivVR.csproj)

$projectVersion = [version]$xml.Project.PropertyGroup.Version
$currentVersion = "{0}.{1}.{2}" -f $projectVersion.Major, $projectVersion.Minor, $projectVersion.Build
$nextVersion = "{0}.{1}.{2}" -f $projectVersion.Major, $projectVersion.Minor, ($projectVersion.Build + 1)
$versionString = "v$nextVersion"

$changeLog = git log --pretty=format:"# %s%n%b" "v$currentVersion..HEAD" --invert-grep --grep="Publish Version"
if (!$changeLog) {
    Write-Host "Changelog was empty, exiting"
    Exit 1
}

Write-Host "Bumping version from $currentVersion to $nextVersion"
Write-Host "=== Change Log ==="
Write-Host $changeLog

$xml.Project.PropertyGroup.Version = $nextVersion
$xml.Save(".\FfxivVr\FfxivVR.csproj")

$now = [int](Get-Date -UFormat %s -Millisecond 0)

Remove-TypeData -ErrorAction Ignore System.Array
$repo = Get-Content 'PluginRepo/pluginmaster.json' -raw | ConvertFrom-Json
$repo[0].AssemblyVersion = $nextVersion
$repo[0].LastUpdated = $now
$repo[0].DownloadLinkInstall = "https://github.com/WesleyLuk90/ffxiv-vr/releases/download/$VersionString/FfxivVR.zip"
$repo[0].DownloadLinkTesting = "https://github.com/WesleyLuk90/ffxiv-vr/releases/download/$VersionString/FfxivVR.zip"
$repo[0].DownloadLinkUpdate = "https://github.com/WesleyLuk90/ffxiv-vr/releases/download/$VersionString/FfxivVR.zip"
ConvertTo-Json $repo -depth 32 | set-content 'PluginRepo/pluginmaster.json'

# [IO.File]::WriteAllLines("release/changelog.txt", $changeLog)

# echo "VERSION_STRING=$versionString" >> $env:GITHUB_OUTPUT
