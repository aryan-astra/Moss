# Moss local/CI build: restore, build, test, publish, package.
# Same steps run inside GitHub Actions; keep them in sync.
[CmdletBinding()]
param(
	[string]$Configuration = 'Release',
	[string]$Runtime = 'win-x64'
)
$ErrorActionPreference = 'Stop'
$root = (Get-Item $PSScriptRoot).Parent.FullName
Set-Location $root

[xml]$props = Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props')
$version = $props.SelectSingleNode('/Project/PropertyGroup/Version').InnerText.Trim()
Write-Host "Moss $version ($Configuration, $Runtime)"

dotnet restore Moss.sln
if ($LASTEXITCODE -ne 0) { throw 'restore failed' }
dotnet build Moss.sln -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw 'build failed' }
dotnet run --project tests/Moss.Tests -c $Configuration --no-build
if ($LASTEXITCODE -ne 0) { throw 'tests failed' }

$artifacts = Join-Path $root 'artifacts'
$portable = Join-Path $artifacts 'Moss-win-x64'
$single = Join-Path $artifacts 'Moss-single'
$dist = Join-Path $artifacts 'Moss-dist'
foreach ($directory in @($artifacts, $portable, $single, $dist)) {
	if (-not (Test-Path -LiteralPath $directory)) {
		New-Item -ItemType Directory -Path $directory | Out-Null
	}
}

dotnet publish src/Moss.Windows -c $Configuration -r $Runtime --self-contained true -p:DebugType=None -o $portable
if ($LASTEXITCODE -ne 0) { throw 'portable publish failed' }
dotnet publish src/Moss.Windows -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=true -p:DebugType=None -o $single
if ($LASTEXITCODE -ne 0) { throw 'single-file publish failed' }

# Portable distribution: published runtime + content + docs (verification
# media under docs/evidence stays in the repository only).
Copy-Item (Join-Path $portable '*') $dist -Recurse -Force
foreach ($item in @('characters', 'README.md', 'LICENSE', 'licenses', 'uninstall-portable.ps1')) {
	Copy-Item (Join-Path $root $item) $dist -Recurse -Force
}
$docsDist = Join-Path $dist 'docs'
New-Item -ItemType Directory -Path $docsDist -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $root 'docs') | Where-Object { $_.Name -ne 'evidence' } | ForEach-Object { Copy-Item $_.FullName $docsDist -Recurse -Force }

$zip = Join-Path $artifacts "Moss-$version-windows-x64.zip"
Compress-Archive (Join-Path $dist '*') $zip -Force
$exe = Join-Path $artifacts "Moss-$version-windows-x64.exe"
Copy-Item (Join-Path $single 'Moss.exe') $exe -Force

$sums = Join-Path $artifacts 'SHA256SUMS.txt'
$lines = foreach ($file in @($zip, $exe)) {
	(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash + '  ' + (Split-Path $file -Leaf)
}
$lines | Out-File $sums -Encoding ascii
Write-Host "Artifacts:"
Write-Host "  $zip"
Write-Host "  $exe"
Write-Host "  $sums"
