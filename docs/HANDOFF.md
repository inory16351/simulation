# 인수인계 (2026-11-02)

다음 에이전트가 이어받을 상태. **아직 아무것도 커밋하지 않았다** (마지막 커밋: `fd6705b` 탑뷰 전투 + 동화책 UI).
테스트 27개 통과, `npm run typecheck` 통과, `npm run build` 통과 (작업 중 마지막 확인 기준).

## 1. 이번에 끝낸 것 (커밋 대기)

| 기능 | 핵심 파일 | 문서 |
|---|---|---|
| 장비 품질(`id+숫자`), 창고, 착용 규칙, 사투 노획 | `src/core/gear.ts`, `src/core/data.ts`(getWeapon/getArmor 품질 처리), `src/core/match.ts`(노획) | `docs/14_gear_dungeon.md` |
| 장비 시장 검색·창고 탭, 검투사 장비 칸 | `src/ui/components/GearPanels.tsx`, `MarketScreen.tsx`, `GladiatorScreen.tsx` | 〃 |
| 던전 원정(1~5명, 5:5 진형, 층별 전리품·성장·사망), 나라 토벌 의뢰 | `src/core/dungeon.ts`, `src/ui/screens/DungeonScreen.tsx`, `day.ts`(매일·급료일 훅) | 〃 |
| 던전 원정 중에도 급료는 루두스 부담 | `economy.ts` payday (`deployment.kind === "dungeon"`) | |
| 초상화 두 번째 세트 (종족마다 머리 18종, 파츠 2배) | `tools/gen-race-extra.mjs`, `art/source/portrait2/*/front_*_2.png` | |
| 머리색 필터, 투구 반쯤만 씀, 변형 파츠 고르기 | `src/core/portrait2.ts`, `PaintedAvatar.tsx` | |
| 던전 메뉴 아이콘 | `art/source/sheets/icon_dungeon.png` → `public/assets/icon/menu/dungeon.webp` | |

저장 호환: 새 필드(`armory`, `gearMarket`, `expeditions`, `dungeonQuests`)는 전부 선택값. 예전 저장은 `loadGame`에서 장비 시장만 깔아 준다.

## 2. 진행 중이던 일: 초상화 파츠 위치를 '점 지도'로 맞추기

### 배경
사용자가 "투구가 커서 얼굴과 안 맞는다", "아직 어색한 게 있다"고 했다. 원인은 `tools/lib/anchors.cjs`가 그림 픽셀(눈 어두움, 얼굴 구멍 폭)로 기준점을 재는데 종족마다 틀린다는 것.
예: 사람 머리 1번의 턱을 365로 쟀지만 실제는 391 (갑옷 위 얼굴 다시 그리기가 턱 위에서 잘림).

### 지금 배치 규칙 (`tools/portrait-parts.mjs`)
- 투구: 시트에 그려진 자리 그대로 (`placeFrontSheet`). **잘 맞는다**.
- 머리카락: 시트 크기 + 얼굴 가운데 + 정수리 위 5% (`placeHair`). **잘 맞는다**.
- 수염: 시트 크기 + 콧수염 윗선을 코 밑에 (`placeFront` beard). 미노타우로스는 `NOSE_DROP`으로 더 아래.
- 갑옷: 어깨 폭·목 위치를 재서 (`placeFront` armor).
- **새로 넣은 것**: 기준점 지도 JSON이 있으면 점 맞추기(`placeByDots`)를 먼저 시도하고, 점이 이상하거나 재서 맞춘 자리와 머리 폭의 25% 넘게 다르면 재서 맞춘 자리를 쓴다 (`pickPlacement`). 갑옷은 늘 재서 맞춘다. 머리 턱 점이 있으면 얼굴 다시 그리기 턱 줄로 쓴다.
- 점검: `node tools/render-part-audit.mjs <종족> <출력.png>` (줄 = 파츠, 칸 = 머리 6종). 이걸로 눈으로 확인한다.

### 점 지도 만드는 법
1. ChatGPT(Chrome, 사용자 계정)에 원래 시트를 올리고 "똑같이 다시 그리고 색 점만 찍어라" 프롬프트로 생성.
   - 머리 시트 프롬프트: `tools/gen-anchor-maps.mjs`의 `HEADS` (빨강 정수리, 노랑 미간, 파랑 눈동자 둘, 초록 코끝, 청록 입, 하양 턱, 검정 목 아래).
   - 파츠 시트 프롬프트: 짧은 공통 규칙 + 칸 목록 (머리카락·투구 = 빨강 정수리·노랑 미간, 수염 = 초록 코·청록 입). 칸 목록은 종족 `config.json`의 `front` / `extra[0].front`에서 null이면 empty.
   - Codex(`node tools/gen-anchor-maps.mjs`)로도 같은 걸 돌릴 수 있지만 오늘 15:15까지 사용 한도에 걸려 있었다.
2. 생성된 그림은 다운로드·localhost 전송이 막혀 있다 (chatgpt.com CSP). 대신 페이지에서 `tools/chatgpt-dots.js` 코드를 실행해 점 좌표만 읽는다 (`window.__HEADS = true/false` 먼저 설정, 결과는 `window.__out`, 1,000자 넘으면 잘리니 나눠 읽기).
3. `node tools/save-anchors.mjs <종족>/<시트> "<칸1>|...|<칸9>"` → `art/source/portrait2/<종족>/anchors/<시트>.json`.
4. `node tools/portrait-parts.mjs`로 다시 빌드, `render-part-audit.mjs`로 확인.

### 점 지도 진행 상황
- 끝남: `human/front_heads`, `human/front_heads_2`, `human/front_parts`, `human/front_parts_2`.
  - 머리 점은 아주 정확하다 (칸마다 9개씩 정확히 검출, 원본과 위치 차이 수 px).
  - 파츠 점은 그림 모델의 짐작이라 덜 믿을 만하다 (갑옷 목 점이 빗나감, 투구 점이 시트 자리보다 70px 위를 가리킴). 그래서 sanity check와 25% 규칙을 넣었다.
- **머리 시트는 7종족 모두 끝남** (2026-11-02): `dwarf, elf, orc, minotaur, lizardfolk, beastkin`의 `front_heads`, `front_heads_2`. 다시 빌드하고 `render-part-audit.mjs`로 7종족 확인했다. 눈에 띄게 어긋난 파츠 없음.
  - 목(검정) 점은 머리카락·눈가 같은 어두운 부분이 잘못 잡히는 일이 잦다 (orc_2, minotaur_2). 가운데선에서 90px 넘게 벗어나거나 턱보다 위에 있는 점은 버리고 `-`로 저장했다. 읽을 때 `chatgpt-dots.js`에 이 필터를 넣으면 손이 덜 간다 (`Math.abs(x-256)<90`, `neck.y > chin.y`).
  - 수인 정수리(빨강) 점은 귀 때문에 칸마다 높이가 들쭉날쭉하다 (20~153). 25% 규칙이 걸러 준다.
- 남은 것 (선택, 효과 작음): 파츠 시트 `dwarf 1·2, elf 1·2, orc 1·2, minotaur 1·2, lizardfolk 2, beastkin 2` (도마뱀·수인 1세트는 갑옷뿐이라 생략).

## 3. 알려진 남은 문제
- 엘프 `helmet_visor_2`는 어깨 갑옷까지 같이 그려져 있다 (그림 자체). 거슬리면 다시 생성.
- 엘프 `helmet_crest` 오른쪽 위에 작은 갈색 부스러기가 남아 있다.
- 어두운 머리색 필터가 렌더러에 따라 푸르스름하게 보일 수 있다 (`HAIR_TINTS`).
- `optimize-images.mjs`가 시트 원본까지 webp로 만들어 `public/assets/portrait2/*/front_*.webp`, `portrait_v1`, `rig`, `sheets`, `style` 아래에 **쓰지 않는 미추적 파일**이 있다. 커밋에서 빼고, 사용자 허락을 받고 지울 것 (삭제는 권한 거절됐었다).
- 5:5 던전 전투(`skirmish()`)는 임시 규칙. 사용자가 전투 메커니즘 md를 줄 예정이니 그에 맞춰 `FORMATIONS`/`skirmish()`를 바꾼다.
- `src/ui/components/RigFighter.tsx`는 게임에서 안 쓰지만 `tools/render-poses.tsx` 등이 아직 부른다.

## 4. 사용자 선호 (대화에서 확인)
- 한국어로 짧게 지시한다. 그림은 Codex(`tools/gen-images.mjs`) 또는 Chrome의 ChatGPT로 생성해도 된다고 했다.
- 그림체: 동화책 페인터리, 붓 터치 + 플랫 (`docs/09_art_direction.md`). 이집트 신 같은 신 형상은 빼 달라고 했다.
- 커밋은 물어보고 한다.

## 5. 커밋할 때
```
git add -u
git add src/core/gear.ts src/core/dungeon.ts src/ui/components/GearPanels.tsx src/ui/screens/DungeonScreen.tsx \
  tools/gen-race-extra.mjs tools/gen-anchor-maps.mjs tools/save-anchors.mjs tools/chatgpt-dots.js tools/render-part-audit.mjs \
  docs/14_gear_dungeon.md docs/HANDOFF.md \
  art/source/portrait2/*/front_*_2.png art/source/portrait2/*/anchors art/source/portrait2/human/config.json \
  art/source/sheets/icon_dungeon.png art/source/icon/menu/dungeon.png public/assets/icon/menu/dungeon.webp \
  public/assets/portrait2/*/front/
```
`public/assets/portrait2/*/front_*.webp`(시트 원본 webp)는 넣지 말 것.
