param([string]$OutputFile = (Join-Path $PSScriptRoot 'FontPocket.exe'))
$ErrorActionPreference = 'Stop'
$manifest=[xml](Get-Content -LiteralPath (Join-Path $PSScriptRoot 'app.manifest') -Raw)
$assemblyVersion=$manifest.assembly.assemblyIdentity.version
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'VersionInfo.cs'),"using System.Reflection;`r`n[assembly: AssemblyVersion(""$assemblyVersion"")]`r`n[assembly: AssemblyFileVersion(""$assemblyVersion"")]`r`n[assembly: System.Runtime.Versioning.TargetFramework("".NETFramework,Version=v4.8"")]`r`n",[Text.UTF8Encoding]::new($true))
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
$gac = Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL'
$compression = Get-ChildItem -LiteralPath (Join-Path $gac 'System.IO.Compression') -Recurse -Filter System.IO.Compression.dll | Select-Object -First 1 -ExpandProperty FullName
$zip = Get-ChildItem -LiteralPath (Join-Path $gac 'System.IO.Compression.FileSystem') -Recurse -Filter System.IO.Compression.FileSystem.dll | Select-Object -First 1 -ExpandProperty FullName
if (-not $compression -or -not $zip) { throw '.NET Framework 4.8 is required.' }
$runtime = Get-ChildItem -LiteralPath (Join-Path $gac 'System.Runtime') -Recurse -Filter System.Runtime.dll | Select-Object -First 1 -ExpandProperty FullName
$winMetadata = Join-Path $env:WINDIR 'System32\WinMetadata'
$frameworkDirectory = [IO.Path]::GetDirectoryName($compiler)
$nativeRefs = @("/reference:$runtime")
foreach ($assembly in @('System.Runtime.WindowsRuntime','System.Runtime.WindowsRuntime.UI.Xaml','System.Runtime.InteropServices.WindowsRuntime')) {
    $nativeRefs += "/reference:$frameworkDirectory\$assembly.dll"
}
foreach ($metadata in @('Windows.Foundation','Windows.Graphics','Windows.UI','Windows.UI.Xaml','Windows.Storage')) {
    $nativeRefs += "/reference:$winMetadata\$metadata.winmd"
}
$webView=Join-Path $PSScriptRoot 'Dependencies\WebView2'
foreach($library in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll')) {
    $reference=Join-Path $webView ('lib\net462\'+$library)
    if(-not(Test-Path -LiteralPath $reference)){throw 'Missing bundled WebView2 SDK dependency.'}
    $nativeRefs+='/reference:'+$reference
}
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ "/out:$OutputFile" "/win32manifest:$PSScriptRoot\app.manifest" "/win32icon:$PSScriptRoot\FontPocket.ico" "/resource:$PSScriptRoot\FontPocket.ico,FontPocket.Icon" "/resource:$PSScriptRoot\FontPocket-logo.png,FontPocket.Logo" "/resource:$PSScriptRoot\English.json,FontPocket.English" "/resource:$PSScriptRoot\Brand\Fonts\Pretendard-SemiBold.ttf,FontPocket.PretendardSemiBold" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "/reference:$compression" "/reference:$zip" @nativeRefs "$PSScriptRoot\*.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$outputDirectory=[IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($OutputFile))
foreach($library in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll')){Copy-Item -LiteralPath (Join-Path $webView ('lib\net462\'+$library)) -Destination (Join-Path $outputDirectory $library) -Force}
Copy-Item -LiteralPath (Join-Path $webView 'runtimes') -Destination $outputDirectory -Recurse -Force
$assets=Join-Path $outputDirectory 'WebAssets'
New-Item -ItemType Directory -Path $assets -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Dependencies\Woff2\woff2-encoder.js') -Destination $assets -Force
$licenses=Join-Path $outputDirectory 'Licenses'
New-Item -ItemType Directory -Path $licenses -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $webView 'LICENSE.txt') -Destination (Join-Path $licenses 'WebView2.txt') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Dependencies\Woff2\LICENSE.txt') -Destination (Join-Path $licenses 'woff2-encoder.txt') -Force
foreach($notice in @('Google-WOFF2.txt','Brotli.txt')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('Dependencies\Woff2\'+$notice)) -Destination (Join-Path $licenses $notice) -Force}
Write-Output 'FontPocket.exe built successfully.'


