# Font Pocket 맥 버전 — 0.1.0 미리보기

macOS 13 Ventura 이상에서 사용하는 SwiftUI 앱입니다. Apple Silicon(M1 이후)과 Intel 맥을 하나의 앱으로 지원합니다. 기존 폴더 모양 로고와 Font Pocket 이름을 유지했습니다.

## Apple Liquid Glass

macOS 26 이상에서는 Apple의 실제 Liquid Glass API를 사용합니다. 기본 사이드바·도구막대, 유리 재질 버튼, 묶음 미리보기 버튼, 작업 중지 영역에 적용했습니다. 폰트 목록과 미리보기 문구에는 읽기 쉬운 콘텐츠 배경을 유지합니다. 이전 macOS에서는 기본 컨트롤과 재질을 사용합니다. Liquid Glass를 포함해 빌드하려면 Xcode 26 이상이 필요합니다.

[Apple 공식 디자인 문서](https://developer.apple.com/kr/documentation/technologyoverviews/liquid-glass)를 기준으로 구현했습니다.

## 사용할 수 있는 기능

- 눈누·Google Fonts·공식 사이트·직접 다운로드 링크를 여러 줄로 입력해 폰트 받기
- 다운로드할 파일을 직접 선택하고, ZIP·TTF·OTF·TTC·OTC·폰트 폴더를 같은 대기열에 추가
- 설치 전 문구·크기 변경, 설치된 폰트 검색, 최대 4개 폰트 비교, 즐겨찾기
- 현재 계정에 설치, 앱이 설치한 폰트의 사용 중지·다시 사용·휴지통 이동
- 선택한 폰트 ZIP 백업, 기존 백업을 유지하는 취소 처리, 작업 중 종료 확인
- 한국어·영어 화면과 설정

## 빌드 및 다운로드

GitHub의 `codex/macos-native` 브랜치에서 **Build Font Pocket for macOS** 작업을 열고, 성공한 실행의 **Artifacts**에 있는 `FontPocket-macOS-0.1.0-universal`을 다운로드할 수 있습니다. 안쪽 ZIP을 풀면 `Font Pocket.app`이 있습니다. 빌드 산출물은 14일간 보관됩니다.

맥에 Xcode Command Line Tools가 있다면 직접 빌드할 수도 있습니다.

```sh
cd macOS
swift test
bash build.sh
```

완성된 앱과 배포 ZIP은 `dist` 폴더에 생성됩니다.

## 폰트 보관 위치

앱이 설치한 폰트는 `~/Library/Fonts/FontPocket-<SHA256>.<확장자>`에 보관됩니다. 설치 내역과 사용 중지한 파일은 `~/Library/Application Support/Font Pocket/`에 있습니다. 앱이 설치하지 않은 폰트와 시스템 폰트는 조회·미리보기만 가능합니다. 삭제는 휴지통으로 이동하며, 실행 중인 다른 앱은 글꼴 목록을 새로고침하거나 재실행해야 변경을 볼 수 있습니다.

백업 ZIP은 폰트 설치 화면에서 다시 추가하고 설치할 수 있습니다. 윈도우 버전의 환경 설정 전체를 옮기는 기능과는 다릅니다.

## 미리보기 범위

현재 앱에는 임시 로컬 코드 서명(ad-hoc)이 들어 있습니다. **Apple Developer ID 서명과 공증을 받은 정식 배포본은 아닙니다.** 정식 배포에는 별도 Apple 개발자 계정과 공증 설정이 필요합니다. 시스템의 Gatekeeper 보호 기능을 끌 필요는 없습니다.

WOFF·WOFF2 변환, 가변 축 조절, 문자표, 폰트 버전 이력, 자동 앱 업데이트는 아직 포함하지 않았습니다. 이 버전에서는 설치용 TTF·OTF 파일을 사용하세요. ZIP64·암호화 ZIP·심볼릭 링크·과도한 압축 해제·안전하지 않은 경로를 포함한 ZIP은 거부합니다.

자세한 구현 및 테스트 범위는 [영문 문서](README.md)를 참고하세요.
