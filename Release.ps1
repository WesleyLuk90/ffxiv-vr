
$xml = [xml](Get-Content -Path .\FfxivVr\FfxivVR.csproj)

$currentVersion = [version]$xml.Project.PropertyGroup.Version
$nextVersion = "{0}.{1}.{2}" -f $currentVersion.Major, $currentVersion.Minor, ($currentVersion.Build + 1)

$changeLog = git log --pretty=format:%s v$currentVersion..HEAD --invert-grep --grep="Publish Version" | Select-String "^\[" | % { $_.Line }
$fixes = git log --pretty=format:%s%n%b v$currentVersion..HEAD --invert-grep --grep="Publish Version" | Select-String "#\d+" | % { "Closes " + $_.Matches.Value }
$releaseMessage = "Publish Version $nextVersion`n" + [string]::Join("`n", $fixes)

echo "=== Change Log ==="
echo $changeLog
echo "=== Release Message ==="
echo $releaseMessage

echo "Do you want to deploy version $($nextVersion) (y/n)?";
$key = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown');

if ( $key.Character -ne "y" ) {
    echo "Release cancelled"
    Exit 1
}

echo "Deploying $($nextVersion)"

[IO.File]::WriteAllLines("changelog.txt", $changeLog)
[IO.File]::WriteAllLines("release-message.txt", $releaseMessage)

$xml.Project.PropertyGroup.Version = $nextVersion
$xml.Save(".\FfxivVr\FfxivVR.csproj")

Remove-TypeData -ErrorAction Ignore System.Array 
$now = [int](Get-Date -UFormat %s -Millisecond 0)
$repo = Get-Content 'PluginRepo/next.json' -raw | ConvertFrom-Json
$repo[0].AssemblyVersion = $nextVersion
$repo[0].LastUpdated = $now
$repo[0].DownloadLinkInstall = "https://github.com/WesleyLuk90/ffxiv-vr/releases/download/v$nextVersion/FfxivVR.zip"
$repo[0].DownloadLinkTesting = "https://github.com/WesleyLuk90/ffxiv-vr/releases/download/v$nextVersion/FfxivVR.zip"
$repo[0].DownloadLinkUpdate = "https://github.com/WesleyLuk90/ffxiv-vr/releases/download/v$nextVersion/FfxivVR.zip"
ConvertTo-Json $repo -depth 32| set-content 'PluginRepo/next.json'

git add .
git commit -m "Release version $nextVersion"
git tag v$nextVersion
git push origin master
git push origin tag v$nextVersion
