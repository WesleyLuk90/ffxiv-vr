param(
    [switch]$DryRun
)

$xml = [xml](Get-Content -Path .\FfxivVr\FfxivVR.csproj)

$currentVersion = [version]$xml.Project.PropertyGroup.Version
$nextVersion = "{0}.{1}.{2}" -f $currentVersion.Major, $currentVersion.Minor, ($currentVersion.Build + 1)
$versionString = "v$nextVersion"

echo "Bumping version from $currentVersion to $nextVersion"

if (-not $DryRun) {
    $xml.Project.PropertyGroup.Version = $nextVersion
    $xml.Save(".\FfxivVr\FfxivVR.csproj")

    Remove-TypeData -ErrorAction Ignore System.Array
    $repo = Get-Content 'PluginRepo/pluginmaster.json' -raw | ConvertFrom-Json
    $repo[0].AssemblyVersion = $nextVersion
    ConvertTo-Json $repo -depth 32 | set-content 'PluginRepo/pluginmaster.json'
}

echo $versionString
