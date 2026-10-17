param(
    [Parameter(Mandatory=$true)][string]$PayloadPath,
    [Parameter(Mandatory=$true)][string]$OutputFile,
    [Parameter(Mandatory=$true)][string]$Version
)
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(-not(Test-Path -LiteralPath $compiler)){$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'}
$work=Join-Path $PSScriptRoot ('..\..\work\setup-launcher\'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work -Force | Out-Null
$payload=Join-Path $work 'FontPocket-Install.exe'
Copy-Item -LiteralPath $PayloadPath -Destination $payload
$versionInfo=Join-Path $work 'VersionInfo.cs'
$parsed=[version]$Version
$fourPart='{0}.{1}.{2}.{3}' -f $parsed.Major,$parsed.Minor,$parsed.Build,[Math]::Max(0,$parsed.Revision)
[IO.File]::WriteAllText($versionInfo,"using System.Reflection;`r`n[assembly:AssemblyVersion(""$fourPart"")]`r`n[assembly:AssemblyFileVersion(""$fourPart"")]`r`n[assembly:AssemblyProduct(""Font Pocket"")]`r`n[assembly:AssemblyTitle(""Font Pocket Setup"")]`r`n",[Text.UTF8Encoding]::new($true))
$icon=Join-Path $PSScriptRoot '..\FontPocket\FontPocket.ico'
$manifest=Join-Path $PSScriptRoot '..\FontPocket\app.manifest'
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ "/out:$OutputFile" "/win32manifest:$manifest" "/win32icon:$icon" "/resource:$icon,FontPocket.Icon" "/resource:$payload,FontPocket.Setup" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'SetupLauncher.cs') $versionInfo
if($LASTEXITCODE -ne 0){throw 'Setup language launcher build failed.'}
