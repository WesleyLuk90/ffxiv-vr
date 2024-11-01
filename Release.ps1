$version = $args[0]

if(!($version -match '\d+.\d+.\d+')) {
    echo "Invalid version number"
    exit
}

Remove-TypeData -ErrorAction Ignore System.Array 

$repo = Get-Content 'PluginRepo/pluginmaster.json' -raw | ConvertFrom-Json
$repo[0].AssemblyVersion = $version
ConvertTo-Json $repo -depth 32| set-content 'PluginRepo/pluginmaster.json'

git add .
git commit -m "Release version $version"
git tag v$version
git push origin master
git push origin tag v$version