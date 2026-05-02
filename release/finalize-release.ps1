param(
    [string]$VersionString
)

$nextVersion = $VersionString -replace '^v', ''

git config --global user.email "1383942+WesleyLuk90@users.noreply.github.com"
git config --global user.name "Release Bot"
git add .
git commit -m "Publish Version $nextVersion"
git tag $VersionString
