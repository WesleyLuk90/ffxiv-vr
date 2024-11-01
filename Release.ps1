$version = $args[0]

if(!($version -match '\d+.\d+.\d+')) {
    echo "Invalid version number"
    exit
}

$xml = [xml](Get-Content -Path .\FfxivVr\FfxivVR.csproj)
$xml.Project.PropertyGroup.Version = $version
$xml.Save(".\FfxivVr\FfxivVR.csproj")

git add .
git commit -m "Release version $version"
git tag v$version
git push origin master
git push origin tag v$version

Remove-TypeData -ErrorAction Ignore System.Array 
$now = [int](Get-Date -UFormat %s -Millisecond 0)
$repo = Get-Content 'PluginRepo/pluginmaster.json' -raw | ConvertFrom-Json
$repo[0].AssemblyVersion = $version
$repo[0].LastUpdated = $now
ConvertTo-Json $repo -depth 32| set-content 'PluginRepo/pluginmaster.json'

git add .
git commit -m "Publish version $version"

echo "Run `git push origin master` once release build is complete"