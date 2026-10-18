# Font Pocket v0.1.4

## 개선 사항

- 사이드바, 버튼, 검색창, 목록을 더 단순하고 일관된 디자인으로 정리했습니다. 기존 로고는 유지하고 직접 제작한 선 아이콘을 추가했습니다.
- 설정·상세 정보·비교·견본·다운로드 선택 창과 확인·오류 메시지에 공통 디자인을 적용했습니다.
- 상세 정보 창에서 미리보기 문구와 크기를 바로 조절할 수 있습니다. 서체를 바꿔도 입력값이 유지되며 긴 문구는 스크롤로 확인할 수 있습니다.
- 폰트 목록의 우클릭 메뉴에 삭제·비교·선택 백업·활성화/비활성화를 추가했습니다. 시스템 폰트 보호와 삭제 전 백업은 유지됩니다.
- 백업 화면에 중단 버튼을 추가했습니다. 취소한 백업은 기존 ZIP을 보존하고 임시 파일을 정리합니다.
- 작업 중 앱을 닫으면 현재 작업을 표시하고 종료 여부를 확인합니다. 종료를 선택하면 중지 요청 후 파일 처리를 정리한 뒤 자동으로 닫힙니다.
- 업데이트 확인을 설정 창으로 옮겼습니다.
- 상세 정보 탭의 선택 표시와 내용 테두리가 어긋나는 문제를 수정했습니다.

## Improvements

- Simplified the sidebar, buttons, search fields and font list. Kept the original logo and added original line icons.
- Applied a consistent design to settings, font details, comparisons, catalogs, download selection, and confirmation/error dialogs.
- Edit preview text and size directly in font details. Values persist when switching fonts, and long previews can be scrolled.
- Added delete, compare, back up selection, and enable/disable actions to the font-list context menu. System-font protection and pre-deletion backups remain in place.
- Added a stop button to the backup screen. Cancellation preserves an existing ZIP and removes temporary files.
- Closing the app during a task now identifies the task and asks for confirmation. If confirmed, the app requests cancellation and closes after cleanup.
- Moved manual update checks into Settings.
- Fixed alignment between the selected details tab and its content border.

## 다운로드 / Download

설치 또는 업데이트하려면 **FontPocket-Setup-0.1.4.exe**를 실행하세요.
Run **FontPocket-Setup-0.1.4.exe** to install or update.

설치된 폰트와 개인 설정은 유지됩니다. Existing fonts and personal settings are preserved.

`.exe.sha256` 파일은 무결성 확인용이며, 일반 사용자가 설치할 때 별도로 받을 필요는 없습니다.
The `.exe.sha256` file is provided for integrity verification and is optional for installation.

Windows 10/11 · .NET Framework 4.8 이상 / or later. 앱 내 가변 미리보기와 WOFF2 가져오기에는 Microsoft Edge WebView2 Runtime이 필요합니다. In-app variable previews and WOFF2 imports require Microsoft Edge WebView2 Runtime.

현재 설치 파일은 코드 서명되지 않았습니다. This installer is currently unsigned.
