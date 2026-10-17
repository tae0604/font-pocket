# Code signing policy

## Current status

Font Pocket is preparing an application to the SignPath Foundation program.
It has not been accepted, and the public v0.1.0 installer is unsigned.
Do not treat this policy as evidence that any current binary has been signed.

If approved, the required credit will be:
"Free code signing provided by [SignPath.io](https://signpath.io/), certificate by [SignPath Foundation](https://signpath.org/)".

## Roles

- Author, committer and reviewer: [tae0604](https://github.com/tae0604).
- Release and signing approver: [tae0604](https://github.com/tae0604).
- Signing requires manual approval for each release and multi-factor authentication
  on the maintainer's GitHub and SignPath accounts before signing is enabled.
- Pull requests from external contributors must be reviewed before merging.

## Artifact rules

Only Font Pocket binaries built from the source in this repository may be submitted.
Production signing must use a verifiable automated build tied to the reviewed commit.
Product names and versions must match the release. Third-party components keep
their own licenses and may not be re-signed as project-owned code.
Signing credentials must never be committed to this repository.

## Privacy

See [Privacy policy](PRIVACY.md), including opt-out details for automatic update checks.

---

## 한국어

현재 SignPath 신청 준비 단계이며 아직 승인되거나 서명된 상태가 아닙니다.
공개 버전 0.1.0은 서명되지 않았습니다. 승인 후 검증 가능한 자동 빌드에서
생성한 앱과 설치 파일만 서명 요청하고, 매 릴리스는 관리자가 직접 승인합니다.
관리자 계정에는 다중 인증을 사용해야 합니다.
