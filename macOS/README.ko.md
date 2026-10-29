# Font Pocket 맥 버전 — 0.2.0

[English](README.md) · [윈도우 기능 대응표](FEATURE_PARITY.md)

macOS 13 Ventura 이상용 네이티브 SwiftUI 앱입니다. Apple Silicon과 Intel을 하나의 앱으로 지원합니다. 기존 파란 폴더 로고와 Font Pocket 이름을 유지하고 Apple SF Symbols 아이콘을 사용합니다.

## 열기

성공한 **Build Font Pocket for macOS** 작업의 **Artifacts**에서 **FontPocket-macOS-0.2.0-universal**을 내려받으세요. Finder에서 안쪽 ZIP까지 풀어 **Font Pocket.app**을 여세요. 채팅이나 편집기에서 ZIP 미리보기를 여는 것은 앱 실행과 다릅니다.

현재 개발 빌드는 ad-hoc 코드 서명 상태이며 Apple Developer ID 서명·공증은 아직 받지 않았습니다.

## 기능

- 눈누·Google Fonts·공식 사이트·직접 링크 일괄 다운로드와 파일 선택, ZIP·폴더·일반 폰트·WOFF·WOFF2·SFNT 가져오기
- 형식·고정/가변 선호 설정, 대기열 개별 제거, 문구·크기 즉시 변경, 최대 12개/256MB 비교
- 가변 축 슬라이더, 유니코드 문자표 검색·복사, 지원 문자 범위, 제작사·버전·라이선스와 테이블·체크섬 검사
- 이름·파일명·버전·메모·태그 검색, 형식·상태·언어 범위·굵기·고정/가변·즐겨찾기·모음 필터와 패밀리 순서
- 메모·태그·즐겨찾기, 일반/자동 모음 생성·편집·삭제, 표시 대상 선택 후 모음 일괄 사용/중지
- 설치 전후 버전 보관, SHA-256 구분, 버전 비교·전환·실패 복원, 같은 이름의 기존 폰트 유지
- 중복 검색과 남길 파일 선택, 삭제 전 복구 ZIP, 휴지통 이동과 삭제 폰트 복원
- 내 계정 설치·사용/중지, 기존 사용자 폰트는 원본을 보관한 뒤 관리 대상으로 가져오기
- 선택 폰트 백업과 환경 백업/복원: 사용자 폴더 폰트·비활성 파일·버전·메모·태그·즐겨찾기·모음·사용 상태 포함
- 목록·두 열·격자·패밀리 견본, 여러 크기/전체 문자표 인쇄와 기본 PDF 저장
- 원본 또는 WOFF2와 CSS·수정 가능한 HTML 묶음, WOFF2 단독 내보내기
- 빠른 검색 ⌘K / 전역 ⌘⌥Space, Finder·서체 관리자, 연결 프로그램·드래그 추가, 작업 기록 저장, 중지·작업 중 종료 확인
- 한국어·영어, 선택적 일일 업데이트 확인, 공식 파일·SHA-256·앱 신원 확인 후 업데이트 및 실패 복원

## Liquid Glass

macOS 26 이상에서는 Apple의 실제 Liquid Glass API와 기본 사이드바·도구막대·버튼을 사용합니다. 목록과 미리보기는 읽기 쉬운 배경을 유지합니다. 이전 macOS에서는 기본 컨트롤·재질을 사용하고 투명도/움직임 줄이기 설정을 따릅니다. 새 API를 포함하려면 Xcode 26 이상으로 빌드하세요.

[Apple 공식 문서](https://developer.apple.com/kr/documentation/technologyoverviews/liquid-glass)

## 빌드와 검증

맥의 Xcode Command Line Tools 설치 후 이 폴더에서 아래를 실행합니다.

    swift test
    bash build.sh

산출물은 dist에 생성됩니다. FONTPOCKET_TEST_FONT로 지정한 실제 TTF로 테스트하며 CI는 저장소의 OFL Pretendard를 사용합니다. 격리된 GitHub 맥에서 macOS 14/26 테스트, 양쪽 아키텍처 빌드·서명 검증, 설치·사용 전환·백업·웹폰트 변환·개인 정보·견본 PDF·화면 실행을 확인합니다. 사용자 맥북에 직접 설치한 것은 아닙니다.

## 보관 위치와 범위

활성 파일은 ~/Library/Fonts/FontPocket-<SHA256>.<확장자>, 개인 정보·비활성 파일·원본·복구 ZIP·버전은 ~/Library/Application Support/Font Pocket/에 있습니다. 외부에서 변경된 파일의 관리 작업은 거부하며 시스템·공유 폰트는 보호합니다.

다운로드/ZIP 1GB, 압축 해제 합계 2GB, 최대 10,000개, 일반 폰트 512MB, WOFF2 입력 128MB/변환 결과 150MB입니다. 인쇄는 작업당 300페이지까지이며 초과하면 먼저 안내합니다. ZIP64·암호화·심볼릭 링크·안전하지 않은 경로는 거부합니다. 생성한 백업도 복원 정책을 통과해야 저장됩니다.

Windows 레지스트리·기본 폰트 설정은 Core Text·Finder·서체 관리자로 대응합니다. 환경 ZIP 형식은 플랫폼별이며 Windows 환경 백업과 직접 교환되지 않습니다. 일반 폰트 ZIP은 양쪽에서 가져올 수 있습니다. 인앱 업데이트에는 공식 macOS ZIP과 해당 SHA-256 파일이 함께 게시되어야 합니다.
