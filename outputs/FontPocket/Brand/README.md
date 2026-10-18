# Font Pocket 로고

`FontPocket.svg`는 사용자가 제공한 최신 `Font Poket Icon.svg`의 원본 사본입니다.
경로·색·비율을 바꾸지 않고 정사각형 투명 캔버스에 배치했습니다.

- `../FontPocket.ico`: 16, 20, 24, 32, 40, 48, 64, 128, 256px 프레임
- `../FontPocket-logo.png`: 앱 사이드바용 투명 이미지
- `../FontPocket-installer.bmp`: 설치 마법사 헤더용 흰색 배경 이미지

Node.js와 sharp가 준비되어 있다면 `build-assets.cjs`로 재생성할 수 있습니다.
sharp가 기본 모듈 경로에 없으면 `FONTPOCKET_SHARP_PATH`로 해당 모듈의 절대 경로를 지정합니다.
앱 빌드는 이미 생성된 ICO·PNG를 사용하므로 일반적인 재빌드에는 Node.js가 필요하지 않습니다.

제목 글꼴은 공식 Pretendard Bold TTF 원본을 앱에 포함해 비공개 메모리 폰트로 사용합니다.
Windows에 설치하지 않으며 `Fonts/Pretendard-LICENSE.txt`를 설치 패키지의 `Licenses` 폴더에 동봉합니다.
[공식 Pretendard 저장소](https://github.com/orioncactus/pretendard)
