# Building Font Pocket

## Requirements

- Windows 10/11, Windows PowerShell 5.1, .NET Framework 4.8.
- Windows WinRT metadata in `%WINDIR%\System32\WinMetadata` and framework WinRT reference assemblies.
- Inno Setup 6 for the installer (https://jrsoftware.org/isinfo.php).

The bundled Pretendard SemiBold font is licensed under the SIL Open Font License;
its license is in `outputs/FontPocket/Brand/Fonts/Pretendard-LICENSE.txt`.

## Application

From the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\outputs\FontPocket\build.ps1
```

The output is `outputs/FontPocket/FontPocket.exe`. The build reads its version
from `app.manifest` and generates `VersionInfo.cs`.

## Offline self-tests

```powershell
$results = Join-Path $PWD ('test-results\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $results | Out-Null
$testProcess = Start-Process .\outputs\FontPocket\FontPocket.exe -ArgumentList @('--self-test', ('"' + $results + '"')) -Wait -PassThru
if ($testProcess.ExitCode -ne 0) { throw "Self-tests failed. See $results" }
```

Use a new results directory on every run. These tests use isolated test data.

## Installer

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\outputs\FontPocket-Installer\build-installer.ps1 -CompilerPath 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
```

The app and installer version must match. The script builds an Inno Setup payload,
wraps it in the native Korean/English language selector, and emits
`outputs/FontPocket-Setup-<version>.exe` plus its SHA-256 sidecar.
Temporary wrapper build files go under `work/` and are not committed.

## Code signing

The public v0.1.0 installer is unsigned. See [Code signing policy](CODE_SIGNING.md).
GitHub Actions builds unsigned app and installer artifacts from the reviewed source. SignPath approval and signing integration are pending; there is no active signing job yet.
The intended order is: build app, sign app, build/sign the inner Inno payload,
build/sign the outer language selector, verify all signatures, then generate
the final SHA-256 sidecar. Never publish a checksum generated before signing.
Future CI must build from a reviewed source commit and retain provenance;
each production signing request requires manual maintainer approval.

---

## 한국어

Windows에서 위 명령으로 앱과 설치 파일을 빌드합니다. 앱에는 .NET Framework 4.8,
설치 파일 생성에는 Inno Setup 6이 필요합니다. `--self-test`에는 매번 새로운
결과 폴더를 지정하세요. 현재 공개된 0.1.0은 서명되지 않았으며 SignPath 승인과
자동 빌드·서명 연동을 준비 중입니다.
