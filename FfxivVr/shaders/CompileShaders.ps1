$ErrorActionPreference = "Stop"

$kitsRoot = (Get-ItemProperty "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows Kits\Installed Roots" -Name KitsRoot10 -ErrorAction SilentlyContinue).KitsRoot10
if (-not $kitsRoot) {
    $kitsRoot = (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots" -Name KitsRoot10 -ErrorAction SilentlyContinue).KitsRoot10
}
if (-not $kitsRoot) {
    $kitsRoot = "C:\Program Files (x86)\Windows Kits\10\"
}

$fxc = Get-ChildItem -Path (Join-Path $kitsRoot "bin") -Filter "fxc.exe" -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.DirectoryName -match '\\x86$' } |
    Sort-Object { [version]$_.Directory.Parent.Name } -Descending |
    Select-Object -First 1

if (-not $fxc) {
    throw "Could not find fxc.exe under $kitsRoot"
}

& $fxc.FullName /Fo shaders\VertexShader.cso /T vs_5_0 .\shaders\VertexShader.hlsl
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $fxc.FullName /Fo shaders\PixelShader.cso /T ps_5_0 .\shaders\PixelShader.hlsl
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
