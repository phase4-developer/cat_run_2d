# vendor 출처 (D-50 사용자 승인 범위)

받은 날 2026-11-09. 새 빈 폴더에 받아 형식·SHA256을 확인한 뒤 옮겼다. 하네스(`src/harness.js` `VENDOR`)가 실행마다 SHA256을 다시 확인한다.
글꼴 TTF는 크기(합계 약 33 MB) 때문에 Git에 넣지 않는다(`.gitignore`). 없으면 아래 URL에서 같은 파일을 받아 `vendor/fonts/`에 두고 SHA256을 맞춘다.

| 파일 | 출처 URL | 버전 | SHA256 | 라이선스 | Git |
|---|---|---|---|---|---|
| `three.min.js` | https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js (정본 L729와 같은 URL) | r128 | `9274bbcec8d96168626c732b5d31c775aa8cfb7eaa0599bec0c175908a2c1ce2` | MIT (파일 머리 주석) | 넣음 |
| `fonts/BlackHanSans-Regular.ttf` | https://raw.githubusercontent.com/google/fonts/main/ofl/blackhansans/BlackHanSans-Regular.ttf | google/fonts main | `31960809284026681774a8e52dc19ebcad26cf69b0ad9d560f288296fbb52739` | OFL 1.1 (`fonts/OFL-BlackHanSans.txt`) | 제외 |
| `fonts/GothicA1-Regular.ttf` | https://raw.githubusercontent.com/google/fonts/main/ofl/gothica1/GothicA1-Regular.ttf | google/fonts main | `211151bea98098c579610ab1114bb1bfe909057207db561f357b896d076c21ac` | OFL 1.1 (`fonts/OFL-GothicA1.txt`) | 제외 |
| `fonts/GothicA1-Bold.ttf` | 같은 폴더 `GothicA1-Bold.ttf` | google/fonts main | `2e883fa0ae548000996258b4101f2bb732a28b197264f49621a2cb16d6e9f126` | OFL 1.1 | 제외 |
| `fonts/GothicA1-ExtraBold.ttf` | 같은 폴더 `GothicA1-ExtraBold.ttf` | google/fonts main | `37a65353a646fea5a1ac90585d1bb8f9b997ecae0b79c4f1f187566f910e3f9d` | OFL 1.1 | 제외 |
| `fonts/NotoColorEmoji-Regular.ttf` | https://raw.githubusercontent.com/google/fonts/main/ofl/notocoloremoji/NotoColorEmoji-Regular.ttf | google/fonts main (COLRv1) | `4d82a18d8d95f60ba883ce242bbadbf84a576e987745dd7ba38d71c67cff2d73` | OFL 1.1 (`fonts/OFL-NotoColorEmoji.txt`) | 제외 |
| `fonts/OFL-BlackHanSans.txt` | https://raw.githubusercontent.com/google/fonts/main/ofl/blackhansans/OFL.txt | — | `c324192c8b3a988bae45a9a7e4ac1258b684739003bda114e0523423440457c9` | — | 넣음 |
| `fonts/OFL-GothicA1.txt` | https://raw.githubusercontent.com/google/fonts/main/ofl/gothica1/OFL.txt | — | `ed95c33f80ccca002e3a360b683c43368f9c5eb024e5b992abb51af3c10b59bc` | — | 넣음 |
| `fonts/OFL-NotoColorEmoji.txt` | https://raw.githubusercontent.com/google/fonts/main/ofl/notocoloremoji/OFL.txt | — | `ac564676d10054a8445923dfc2dfb13c042d97888bd27c1b6ec6dfe89a9d8d62` | — | 넣음 |

npm 쪽: `playwright` **1.65.0**(정확 고정, `package-lock.json`). Chromium은 `npx playwright install chromium`(빌드 chromium-1254, chromium_headless_shell-1254)이 `%LOCALAPPDATA%\ms-playwright`에 둔다.

## 글꼴 공급 방식
정본은 Google Fonts css2(L6)를 읽는다. 하네스는 그 요청을 가로채 위 TTF로 만든 `@font-face` CSS를 돌려준다(`src/harness.js` `fontCss`). Google이 실제로 주는 woff2 분할본이 아니라 저장소 원본 TTF이며, Unity(TMP)도 같은 TTF를 쓰므로 대조에는 이쪽이 맞다.
이모지는 정본 글꼴 스택(`--f-display`, `--f-body`, L19~20)을 바꾸지 않고, 같은 글꼴 이름에 `unicode-range`가 이모지 영역인 Noto Color Emoji 면을 낮은 우선순위로 덧붙인다. 시스템 이모지 글꼴(Segoe UI Emoji 등)에 따른 차이를 없앤다.
