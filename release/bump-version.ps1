$xml = [xml](Get-Content -Path .\FfxivVr\FfxivVR.csproj)

$currentVersion = [version]$xml.Project.PropertyGroup.Version
$nextVersion = "{0}.{1}.{2}" -f $currentVersion.Major, $currentVersion.Minor, ($currentVersion.Build + 1)
$versionString = "v$nextVersion"

$changeLog = git log --pretty=format:"# %s%n%b" "v$currentVersion"..HEAD --invert-grep --grep="Publish Version"
if (!$changeLog) {
    Write-Host "Changelog was empty, exiting"
    Exit 1
}

Write-Host "Bumping version from $currentVersion to $nextVersion"
Write-Host "=== Change Log ==="
Write-Host $changeLog

$xml.Project.PropertyGroup.Version = $nextVersion
$xml.Save(".\FfxivVr\FfxivVR.csproj")

Remove-TypeData -ErrorAction Ignore System.Array
$repo = Get-Content 'PluginRepo/pluginmaster.json' -raw | ConvertFrom-Json
$repo[0].AssemblyVersion = $nextVersion
ConvertTo-Json $repo -depth 32 | set-content 'PluginRepo/pluginmaster.json'

[IO.File]::WriteAllLines("release/changelog.txt", $changeLog)

echo "VERSION_STRING=$versionString" >> $env:GITHUB_OUTPUT
