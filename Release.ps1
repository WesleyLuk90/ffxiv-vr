$version = $args[0]

if(!($version -match '\d+.\d+.\d+')) {
    echo "Invalid version number"
    exit
}

$xml = [xml](Get-Content -Path .\FfxivVr\FfxivVR.csproj)
$xml.Project.PropertyGroup.Version = $version
$xml.Save(".\FfxivVr\FfxivVR.csproj")

Remove-TypeData -ErrorAction Ignore System.Array 
$now = [int](Get-Date -UFormat %s -Millisecond 0)
$repo = Get-Content 'PluginRepo/next.json' -raw | ConvertFrom-Json
$repo[0].AssemblyVersion = $version
$repo[0].LastUpdated = $now
$repo[0].DownloadLinkInstall = "https://github.com/WesleyLuk90/ffxiv-vr/releases/download/v$version/FfxivVR.zip"
$repo[0].DownloadLinkTesting = "https://github.com/WesleyLuk90/ffxiv-vr/releases/download/v$version/FfxivVR.zip"
$repo[0].DownloadLinkUpdate = "https://github.com/WesleyLuk90/ffxiv-vr/releases/download/v$version/FfxivVR.zip"
ConvertTo-Json $repo -depth 32| set-content 'PluginRepo/next.json'

git add .
git commit -m "Release version $version"
git tag v$version
git push origin master
git push origin tag v$version
