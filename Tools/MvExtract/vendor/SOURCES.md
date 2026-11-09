# vendor 출처

사용자 승인: D-50 「전부 승인」(2026-11-09). 내려받은 파일은 빈 임시 폴더에서 받아 형식·라이선스를 확인한 뒤 옮겼다. 실행 중 외부 네트워크 접속은 0이다(`lib/canon.mjs`가 cdnjs·Google Fonts 요청을 이 파일들로 돌리고 나머지는 막는다).

| 파일 | 출처 URL | 버전 | SHA256 | 라이선스 |
|---|---|---|---|---|
| `three.min.js` | https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js | r128 (정본 L729와 같은 URL) | `9274bbcec8d96168626c732b5d31c775aa8cfb7eaa0599bec0c175908a2c1ce2` | MIT (`licenses/LICENSE_threejs.txt`, https://raw.githubusercontent.com/mrdoob/three.js/r128/LICENSE) |
| `fonts/BlackHanSans-Regular.ttf` | https://github.com/google/fonts/raw/main/ofl/blackhansans/BlackHanSans-Regular.ttf | google/fonts main (2026-11-09 받음) | `31960809284026681774a8e52dc19ebcad26cf69b0ad9d560f288296fbb52739` | OFL 1.1 (`licenses/OFL_blackhansans.txt`) |
| `fonts/GothicA1-Regular.ttf` | https://github.com/google/fonts/raw/main/ofl/gothica1/GothicA1-Regular.ttf | 〃 | `211151bea98098c579610ab1114bb1bfe909057207db561f357b896d076c21ac` | OFL 1.1 (`licenses/OFL_gothica1.txt`) |
| `fonts/GothicA1-Bold.ttf` | https://github.com/google/fonts/raw/main/ofl/gothica1/GothicA1-Bold.ttf | 〃 | `2e883fa0ae548000996258b4101f2bb732a28b197264f49621a2cb16d6e9f126` | OFL 1.1 |
| `fonts/GothicA1-ExtraBold.ttf` | https://github.com/google/fonts/raw/main/ofl/gothica1/GothicA1-ExtraBold.ttf | 〃 | `37a65353a646fea5a1ac90585d1bb8f9b997ecae0b79c4f1f187566f910e3f9d` | OFL 1.1 |
| `fonts/GothicA1-Black.ttf` | https://github.com/google/fonts/raw/main/ofl/gothica1/GothicA1-Black.ttf | 〃 | `6398ff5c6923c74cb453390892dcb982ec0a6c43b35da8590a8f32702d8c079d` | OFL 1.1 |
| `fonts/NotoColorEmoji-Regular.ttf` | https://github.com/google/fonts/raw/main/ofl/notocoloremoji/NotoColorEmoji-Regular.ttf | google/fonts main, COLRv1 (2026-11-09 받음) | `4d82a18d8d95f60ba883ce242bbadbf84a576e987745dd7ba38d71c67cff2d73` | OFL 1.1 (`licenses/OFL_notocoloremoji.txt`) |
| `licenses/OFL_notocoloremoji.txt` | https://github.com/google/fonts/raw/main/ofl/notocoloremoji/OFL.txt | | `ac564676d10054a8445923dfc2dfb13c042d97888bd27c1b6ec6dfe89a9d8d62` | |
| `licenses/OFL_blackhansans.txt` | https://github.com/google/fonts/raw/main/ofl/blackhansans/OFL.txt | | `c324192c8b3a988bae45a9a7e4ac1258b684739003bda114e0523423440457c9` | |
| `licenses/OFL_gothica1.txt` | https://github.com/google/fonts/raw/main/ofl/gothica1/OFL.txt | | `ed95c33f80ccca002e3a360b683c43368f9c5eb024e5b992abb51af3c10b59bc` | |
| `licenses/LICENSE_threejs.txt` | https://raw.githubusercontent.com/mrdoob/three.js/r128/LICENSE | r128 | `7dddf7c5b8fd10ee654db8857d75d104b5557889aa5a91fc4ca545ea7c07062f` | |

npm: `playwright` 1.65.0(`package-lock.json` 고정, Apache-2.0). 브라우저: `npx playwright install chromium`이 받는 Playwright 1.65.0 고정 빌드(chromium-1254, `%LOCALAPPDATA%/ms-playwright`, 저장소 밖).

참고: Google Fonts CSS(`fonts.googleapis.com/css2`)는 유니코드 범위별 woff2 조각을 주지만, 여기서는 같은 글꼴의 google/fonts 저장소 전체 TTF를 쓴다(같은 글리프). Gothic A1 900(Black)은 정본 CSS가 요청하지 않지만 정본 캔버스 코드가 `900` 굵기를 쓰므로(03 §3.7.2) 함께 둔다 — 정본 브라우저에서는 800으로 대체되므로 `lib/canon.mjs`는 기본으로 정본 CSS가 요청한 400/700/800만 등록한다.
