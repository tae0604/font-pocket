# Bundled dependencies

- Microsoft.Web.WebView2 1.0.3650.58, from the official NuGet package. The build copies the .NET Framework assemblies and x86/x64/ARM64 loader DLLs next to the app. WebView2 Evergreen Runtime must be installed on the user PC; an external browser is not launched for font previews.
- woff2-encoder 2.0.0, from https://github.com/itskyedo/woff2-encoder via npm. The ES module includes Google WOFF2 WebAssembly and runs locally inside WebView2. No font data is sent to an external server.

Upstream licenses are included beside dependency files and copied to the application Licenses directory by build.ps1. The installer includes assemblies, loader directories, WebAssets and notices. Distribute the complete application folder, not only FontPocket.exe.
