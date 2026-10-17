param([string]$CompilerPath)
$ErrorActionPreference = 'Stop'
if (-not $CompilerPath) {
    $candidates = @(
        (Join-Path $PSScriptRoot '..\..\work\installer-tools\inno\ISCC.exe'),
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
        'C:\Program Files\Inno Setup 6\ISCC.exe'
    )
    $CompilerPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $CompilerPath) { throw 'Inno Setup 6 compiler required. Pass -CompilerPath <ISCC.exe>.' }
$appFile = Join-Path $PSScriptRoot '..\FontPocket\FontPocket.exe'
if (-not (Test-Path -LiteralPath $appFile)) { throw 'Build FontPocket.exe first.' }
$versionLine = Select-String -LiteralPath (Join-Path $PSScriptRoot 'FontPocket.iss') -Pattern '^#define AppVersion "([^"]+)"'
$expectedVersion=[version]$versionLine.Matches[0].Groups[1].Value
$binaryVersion=[Reflection.AssemblyName]::GetAssemblyName((Resolve-Path $appFile)).Version
if($binaryVersion.Major -ne $expectedVersion.Major -or $binaryVersion.Minor -ne $expectedVersion.Minor -or $binaryVersion.Build -ne $expectedVersion.Build) {throw 'App and installer versions differ. Build FontPocket.exe with the matching app.manifest first.'}
& $CompilerPath (Join-Path $PSScriptRoot 'FontPocket.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
$versionLine = Select-String -LiteralPath (Join-Path $PSScriptRoot 'FontPocket.iss') -Pattern '^#define AppVersion "([^"]+)"'
$appVersion = $versionLine.Matches[0].Groups[1].Value
$setupFile=Join-Path $PSScriptRoot "..\FontPocket-Setup-$appVersion.exe"
& (Join-Path $PSScriptRoot 'build-setup-launcher.ps1') -PayloadPath $setupFile -OutputFile $setupFile -Version $appVersion
$hash=(Get-FileHash -LiteralPath $setupFile -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($setupFile+'.sha256',$hash+'  '+[IO.Path]::GetFileName($setupFile)+"`n",[Text.UTF8Encoding]::new($false))
Write-Output $setupFile
