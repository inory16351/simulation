# Unity 이식 인수인계 (2026-11-02)

웹판(`src/`, TS+React)을 Unity 6000.3.19f1 프로젝트 `ColosseumUnity/`로 옮기는 중. **최종 목표는 Windows 빌드** (중간 확인용 WebGL).
아무것도 커밋하지 않았다. 커밋은 사용자에게 물어보고 한다.

## 사용자 규칙
- 한국어로 짧게 지시한다. 질문은 선택지(AskUserQuestion)로.
- 씬 오브젝트는 **Unity MCP로 직접 만든다**. 복제해서 쓰는 템플릿·프리팹의 **원본은 하이어라키에 둔다** (비활성 원본을 Instantiate).
  - MCP `execute_code`는 C# 6(CodeDom)이라 긴 코드는 `Assets/Editor/*Builder.cs` 정적 메서드로 두고 `return Colosseum.EditorTools.X.Build();`로 부른다.
- 토큰이 바닥나면 이 문서를 갱신하고 다른 에이전트에게 넘긴다.

## 구조
- `Assets/Scripts/Core/` (asmdef `Colosseum.Core`, 순수 C#, Newtonsoft): 게임 로직. 이식 규칙은 `PORTING.md`.
  - 끝남: Rng, Josa, Data(+Bal, JsMath), Types, StateTypes(GameState 전체), GladiatorRules, Avatar, Generators/GladiatorGen, Combat/Exchange·Sim·Commentary, Portrait2, SaveSystem
  - `Logic/`: 에이전트 셋이 나눠 옮김 (아래 진행 상황)
- `Assets/Scripts/UI/` (asmdef `Colosseum.UI`, TMP): GameController, Shell(+ScreenBase), MenuButton, PlaceholderScreen, NewGameScreen, PortraitView, DuelDemo, Theme
- `Assets/Editor/` (asmdef `Colosseum.Editor`): SceneBuilder(대결 데모, Main.unity), GameSceneBuilder(Game.unity: 상단 바·메뉴·화면 자리·새 게임), UiKit, FontSetup, ArtImporter
- `Assets/Resources/Art/` 그림 PNG, `Resources/Data/` 데이터 JSON, `Resources/Shaders/UIHairTint.shader`, `Resources/Fonts/`(FontSetup이 만듦)
- `Assets/Tests/EditMode/`: GoldenTests(난수·생성기·경기·중계, **통과**), SeasonGoldenTests(새 게임·30일·170일 상태 JSON 비교, 아직 컴파일 전)
- 도구: `tools/golden-dump.ts`, `tools/golden-season.ts`(골든 JSON → Assets/Tests/Golden), `tools/export-unity-assets.mjs`(webp→PNG, data JSON 동기화)

## 진행 상황
1. 로직 이식 (에이전트 3)
   - 경기 묶음 **끝남**: MatchLogic, ChallengeLogic, SeriesLogic, CupLogic, Youth. 가정한 다른 모듈 시그니처는 보고에 있었다:
     `StateUtil.PushMessage(GameState, InboxMessage)` (id·season·day·read를 채움) — 기반 묶음과 모양이 다르면 맞출 것.
   - 기반 묶음 (calendar, career, state, modifiers, nationRules, reputation, condition, injuries, gladiator, tactics, staff, facilities, economy, training, world, gear): 진행 중
   - 진행 묶음 (day, events, transfers, scouting, war, dungeon; Data.cs에 events 추가): 진행 중
2. 다음: Unity 통합 컴파일(MCP `refresh_unity` + `read_console`) → 컴파일 오류 수정 → `run_tests`(Colosseum.Tests) → SeasonGoldenTests 차이 수정 (메시지에 경로별 차이 40개가 나온다)
3. 그다음: `FontSetup.Run()` 두 번(첫 번째는 TMP 기본 리소스 import) → `GameSceneBuilder.Build()` → 플레이 확인
4. 화면 이식: 웹판 `src/ui/screens/*.tsx`를 하나씩 `ScreenBase` 하위로. 목록 줄은 하이어라키의 비활성 원본을 복제.
   순서 제안: inbox → overview → roster → gladiator → training → challenge → match(전투 중계·ArenaStage) → league → cup → market → ...
5. 이미지: PortraitView(초상화)는 만들어 둠. 메뉴 아이콘·문장은 빌더가 붙임. 전투 화면 이펙트(fx/), rig는 아직.

## 알아 둘 것
- 에디터가 포커스를 잃으면 플레이 모드 프레임이 멈춘다 (스크린샷이 첫 프레임에서 안 바뀜). 오류 아님.
- Vite 개발 서버가 ColosseumUnity를 감시하면 패키지 설치가 EPERM으로 실패한다 → `vite.config.ts`에서 제외해 둠.
- 웹판 버그 수정: `src/core/portrait2.ts` 변형 파츠 정규식 `/^d+$/` → `/^\d+$/`.
- 전투 v2 설계: `docs/15_combat_v2.md` (웹판에 1:1 수 싸움 구현됨, 나머지 단계는 미구현).

## 갱신 (2026-11-02 오후)
- 통합 컴파일 오류 0, EditMode 테스트 6개 통과(SeasonGoldenTests 포함), FontSetup·GameSceneBuilder 끝남.
- 플레이 확인: 새 게임 → 서판(InboxScreen/InboxBuilder) 정상 동작.
- 레이아웃 함정: 자식에 `flexW:1`이 있으면 부모 레이아웃 그룹이 flexible을 물려받아 늘어난다. 고정 폭 칸은 `Layout(..., flexW: 0)`을 꼭 준다 (사이드바, 서판 목록 고침).
- 다음: 안뜰(overview) 화면. 웹판 `OverviewScreen.tsx` — 재정·리그·숙소 패널 3개 + 주력 검투사 3명(PortraitView, `G.OpenGladiator`). `GladiatorRules.ComputeCA/CanFight` 사용. `Editor/Screens/OverviewBuilder.cs` + `Scripts/UI/Screens/OverviewScreen.cs`, `GameSceneBuilder.ScreenBuilders`에 등록.
- Unity 에디터가 꺼져 있으면 MCP가 "No Unity Editor instances"를 낸다 → `C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe -projectPath ...`로 띄운다.
- 안뜰(overview) 끝남: `OverviewBuilder` + `OverviewScreen`. 공용 `Widgets`(Stars, SetBar), UiKit `TitledPanel`·`Bar`·`Portrait`(슬롯).
- 초상화 슬롯 (웹판 .portrait): 청동 테두리 > Mask(RectMask2D, #120d0a) > Glow(나라 색 원형 빛, `Resources/Art/ui/portrait_glow.png` 자동 생성) > Portrait(PortraitView). 크기가 잡히기 전에 Show하면 `OnRectTransformDimensionsChange`에서 다시 그린다.
- 막사(roster) 끝남: `RosterBuilder` + `RosterScreen`. 재사용 검투사 표 `GladiatorTable`(런타임) + `GladiatorTableKit.Build(parent, showPa)`(에디터, RosterBuilder.cs 안). 시장·학당·리그 화면에서 그대로 쓴다.
- 검투사 상세(gladiator) 끝남: `GladiatorBuilder` + `GladiatorScreen` (신상·컨디션·경기력·특성·능력치·무기 숙련·기술). `GladiatorRules.PerformanceParts` 추가(Performance는 이걸 더한다, 결과 같음). 아직 안 옮긴 패널: 전술(DoctrinePanel), 장비(EquipPanel), 경력(CareerPanel), 영입(AcquirePanel).
- 연무장(training) 끝남: `TrainingBuilder` + `TrainingScreen`. UiKit `Dropdown`(TMP_DefaultControls로 만들고 색·폰트만 바꿈, 화살표는 ▼ 글자 — ▾는 폰트에 없음). 드롭다운이 있는 화면은 펼침 목록이 RectMask2D에 잘릴 수 있어 스크롤 영역을 안 썼다.
- 확인 요령: 같은 프레임에 활성화한 TMP_Dropdown에 Show()를 부르면 Start 전이라 NRE가 난다(실제 클릭에선 안 남). 에디터가 포커스를 잃으면 스크린샷이 멈춘 프레임이라, 펼침 목록 같은 건 코드로 확인한다.
- 도전장(challenge) 끝남: `ChallengeBuilder` + `ChallengeScreen`. 보내기는 `ChallengeLogic.TryCreateChallenge`의 error를 그대로 보여 준다. 플레이로 확인: 다른 리그 오류 표시, 보내기 → 답 기다림 줄 → 다음 날 잡힌 경기(오늘·모래 마당으로). 받은 도전장 줄은 아직 실제로 받아 보지 못했다(코드만).
- 모래 마당(match) 끝남: `MatchBuilder` + `MatchScreen` (준비 화면 SetupRoot + 중계 화면 PlayRoot). 공식 1:1 경기(미리 계산한 draft를 중계 뒤 `GameController.Replace`), 연습 시합(끝나면 ApplyMatchResult), 단체전 출전 순서·판 넘김(SeriesLives), 컷 글자·붉은 번쩍임, 속도 3단. 플레이로 확인: 공식 1:1 → 결과 반영, 연습 미시오 → 결과 반영. 단체전은 코드만(아직 안 해 봄).
  - 아직 안 옮김: 탑뷰 전투 장면(웹판 `ArenaStage.tsx`), 효과음·전투 음악, 피 튀김 장식.
  - 주의: 플레이 중에 스크립트를 고치면 Unity가 다시 컴파일해 `GameController.I`가 null이 된다. 고친 뒤엔 Play를 껐다 켠다.
- 서열표(league) 끝남: `LeagueBuilder` + `LeagueScreen` (1부·2부, 나라 고르기, 승강 구역 띠).
- 전투 장면(ArenaStage) 끝남: 런타임 `UI/ArenaStage.cs`(웹판 derive·Place·말 자세 애니메이션·이펙트 키프레임 그대로) + 에디터 `Screens/ArenaStageKit.cs`(바닥·원·고리·삼각형 PNG를 `Resources/Art/ui/stage_*.png`로 한 번 그림, 말 둘·핏자국·이펙트 원본을 하이어라키에). 400×160 세계를 칸에 맞춰 늘림(slice). MatchScreen이 문장마다 그 이벤트까지 `stage.Show`.
  - 확인: 결과만 속도로 정지 화면(바닥·관중·말·체력 고리·무기 표식·환호 고리·베기·피 튀김). 움직임(이동 보간·들이받기·흔들림·이펙트 사라짐)은 에디터 포커스가 없어 프레임이 멈춰 눈으로는 못 봤다.
  - 웹판과 다른 점: 무기 표식은 그림 대신 한 글자, 관중 들썩임·열광 빛 고리·모래 결 노이즈는 생략.
- 판테온 컵(cup) 끝남: `CupBuilder` + `CupScreen` (오늘의 컵 경기·출전 순서, 나라 계수, 우리 경기, 조별 순위 4조, 토너먼트). 중계는 `MatchScreen.RunSeries(lives, commit, speed, "cup")`로 모래 마당 화면에서 하고 끝나면 컵으로 돌아온다. 플레이 확인: 조·계수·토너먼트 표시. 우리 루두스가 2부라 컵 경기·중계는 아직 못 돌려 봤다(코드만).
- 다음: market (이적 시장, GearPanels 포함, 252줄).
- 이적 시장(market) 끝남: `MarketBuilder` + `MarketScreen` (탭 6개: 선수 찾기·관심 명단·협상·제안·우리 검투사·장비 시장·창고). 검색 조건 드롭다운은 스크롤 밖, 결과 표만 스크롤. 방출은 두 번 눌러 확인(웹판 confirm). `GameController.Try(fn)`: 복사본에서 먼저 돌려 오류면 반영 안 함(웹판 structuredClone 검증).
- 검투사 상세에 영입 패널: `UI/AcquirePanel.cs` + `GladiatorBuilder.BuildAcquire` (정찰·관심 명단·무소속 주급 흥정·이적료 흥정). 스크롤 안이라 계약 기간은 드롭다운 대신 버튼.
- **Unity판 전용 기능 (웹판에 없음, 사용자 요청 2026-11-02)**:
  - 흥정 `Logic/Haggling.cs`: FM처럼 여러 번 제안 가능, 마지노선(floor, 상대만 앎)보다 낮으면 인내심이 깎이고 역제안이 온다. 바닥나면 7일간 결렬. `GameState.haggles` (키 gear:/fee:/deal:/wage:/staff:).
  - 적용: `GearLogic.HaggleListing`(현금 + 창고 장비 맞교환, 기준가 50% × 중개인), `Transfers.HaggleFee`/`HaggleTransferWage`, `Economy.HaggleFreeAgent`, `StaffLogic.HaggleStaff`(식솔 화면이 아직 없어 UI 미연결). 기존 함수(Bid, CompleteTransfer, SignFreeAgent, HireStaff)는 웹판 동치로 그대로 둠.
  - 위탁 판매: `GearLogic.Consign/Withdraw/DailyConsignments`, `GameState.consignments`. 직접(수수료 없음, 느림) 또는 중개인(수수료 10%, 빠름, 중개인 실력만큼 더 빠름). 싸게 내놓을수록 빨리 팔린다. Day.AdvanceDay에서 호출, 내놓은 게 없으면 난수 안 씀 → 골든 테스트 영향 없음(8개 통과).
  - 등급 색: `Widgets.QualityColors/ItemLabel/GradeTag/GradeTint` (보통·정교 초록·명품 파랑·전설 주황), 장비 표에 등급 칸과 줄 바탕.
- 배경 그림 적용: `UiKit.CoverBackground` (css cover + 덮개 그라디언트, 그라디언트 텍스처는 `Resources/Art/ui/gen/`에 에셋으로). bg_main(전체), topbar, sidebar, arena_provincial(모래 마당 머리).
- 폰트에 없는 글자: ☑ ▾. ◆◇■□★☆는 있다.
- 다음: dungeon → youth → staff(HaggleStaff 연결) → facilities → finance → world → hall. 전투 음악(battle.mp3)은 아직 안 옮김.
- (2026-11-02 저녁) 남은 화면 모두 옮김. 메뉴 16개 중 비석의 언덕(hall, 웹판에도 없음)만 자리 표시.
  - 식솔 `StaffBuilder/StaffScreen` (고용은 `StaffLogic.HaggleStaff` 흥정), 증축 `FacilitiesBuilder/FacilitiesScreen`, 금고 `FinanceBuilder/FinanceScreen`
  - 유소년 `YouthBuilder/YouthScreen`, 던전 `DungeonBuilder/DungeonScreen`, 대륙 정세 `WorldBuilder/WorldScreen` (나라 탭 + 전쟁·용병 탭)
  - 검투사 상세: 전투 교범 `DoctrinePanel`, 장비 `EquipPanel`, 경력 `CareerPanel` (빌더는 `GladiatorBuilder.BuildDoctrine/BuildEquip/BuildCareer`). 스크롤 안이라 드롭다운 없이 버튼으로 펼친다.
  - 레이아웃 함정 하나 더: 고정 높이 줄(요약 패널, 자리 패널)을 담은 칸에는 `Layout(flexH: 0)`을 준다. 안 주면 자식 TitledPanel이 남는 높이를 다 먹는다.
- 소리: `UI/GameAudio.cs` (효과음 12종을 웹판 Web Audio 레시피대로 처음 한 번 합성, 전투 음악은 `Resources/Audio/battle.mp3`), 상단 바 `AudioControl` (음소거, 효과음·음악 크기, PlayerPrefs). MatchScreen 중계 화면이 떠 있는 동안 음악, 문장마다 `PlaySound`.
- 다음: 플레이 전체 흐름 점검(시즌 한 바퀴), 단체전·컵 중계 실제 확인, 던전 원정 실제 확인, Windows 빌드.
- (2026-11-02 저녁) 말투: FM 한국어판처럼 합니다체로 통일. 화면 글·데이터(data/*.json)·서판 소식·경기 중계 전부. 버튼은 "~기"/짧은 명사, 따옴표 안 인물 대사는 그대로. 직역투도 함께 고침.
  - Gemini 검수: Gemini CLI는 계정 문제로 막힘 → Antigravity CLI `agy --model gemini-4-argon-high --mode plan -p "..."`로 부른다. 지침·도구는 세션 스크래치(gem/style.md, textkit.mjs, jsonkit.cjs, gem2/kit.mjs: TS↔C# 문자열 짝 맞추기)에 있었다.
  - 문장을 바꾸면 웹판·C# 둘 다 같은 글자로 바꾸고 `npx tsx tools/golden-dump.ts` + `tools/golden-season.ts`로 골든을 다시 뽑는다. 던전 전사 판정은 "일어나지 못했습니다"를 찾는다.

## 갱신 (2026-11-02 밤): 웹판 동결, Unity 파일만 수정, 에디터는 켜지 않음
- 사용자 지시: 웹판(`src/`, `data/`)은 더 바꾸지 않는다. Unity판이 본 게임. Unity 에디터는 켜지 말고 파일만 다룬다.
- **에디터 없이 컴파일·테스트**: `dotnet build Colosseum.Core.csproj`(UI·Editor·Tests도 같음)로 컴파일 확인, 테스트는 `sh Tools/TestRunner/run.sh` (리플렉션으로 [Test] 실행, 8개).
- **전투 좌표 (Unity판 전용)**: `Combat/Sim.cs`의 `Arena` (밀려남·벽에 몰림·도망·가운데 잡기·라운드마다 원 그리며 밀어붙이기·피하면 비켜서기). 사건마다 `CombatEvent.pos`. 골든 테스트는 `Arena.Enabled = false`로 웹판과 같은 결과를 비교하고, `ArenaTests`가 켠 상태를 본다. 모래 타원은 탑뷰 그림(`Resources/Art/ui/arena_crowd.png`)에 맞춤: 가운데 (197,77), 설 수 있는 반지름 146×46.
- **전투 장면**: `ArenaStage`가 시뮬레이션 좌표대로 바둑알을 움직인다(말 크기 `TokenScale` 0.5 ≈ 경기장 폭 8%, 경기장 전체가 보이게 맞춤, 무기 휘두르기 호 이펙트). 바닥은 실행 중 `Resources.Load("Art/ui/arena_crowd|arena_empty")`, 없으면 코드로 그린 바닥.
- **새 초상화**: 7종족 다크 그래픽 노블 그림체. `Resources/Art/portrait2/**` PNG 386장 + `Resources/Data/portrait2.json`을 웹판 결과에서 옮김. `Portrait2.cs`에 흉터·문신(`mark_*`), 수염 아래 조각(`_lo`), 닫힌 투구 아래 머리 자르기(`clipTop`, PortraitView가 처리) 추가. 옮길 때 `tools/export-unity-assets.mjs`는 쓰지 말 것 — 데이터 JSON 전체를 웹판 것으로 덮어 Unity 말투 수정이 사라진다.
- **말투 검수 반영**: 첫 서판 소식·경력 기록 합니다체, 영입 이유는 따옴표 대사, 이적료 거절 대사 분리, "~하십시오" 10곳을 "~해야 합니다 / ~할 수 있습니다"로. 골든 JSON(world_day*.json, golden.json)의 같은 문장도 함께 바꿈.
- 아직 확인 못 함(에디터를 안 켜서): 새 초상화·경기장이 화면에서 어떻게 보이는지, 바둑알 움직임. Unity를 켤 수 있을 때 `GameSceneBuilder.Build()` 한 번 다시 돌리고 경기를 한 판 보면 된다.

## 다음 작업 (2026-11-02 밤, 사용자 요청 — 새 세션이 이어받음)
사용자가 정한 것 (AskUserQuestion으로 확인): 단체전 = **동시 난전**, 검투사 클릭 = **팝업 카드**, 연습 시합 = **없앰**.

0. **끝남: 연습 시합 제거.** `MatchScreen`은 수락된 경기(도전장·컵)만, 없으면 `noMatch` 안내. `MatchBuilder`에서 연습 패널을 뺐다. dotnet 컴파일 통과. **씬은 아직 다시 빌드 안 함** → Unity에서 `GameSceneBuilder.Build()` 필요.
1. **검투사 팝업 카드** (`GladiatorPopup`): 캔버스 위 오버레이를 `GameSceneBuilder`에서 하이어라키에 만든다(비활성 원본 규칙). 내용: 큰 초상화, 이름·별명, 종족·스타일(아래 3번 호버), 나이, 현재·잠재(남의 검투사는 `Scouting.CaRange/PaRange`), 전적, 소속, **장착 장비 칸 4개**(주무기·보조·갑옷·투구, `Widgets.ItemLabel` + `GearLogic`의 수치 글), 특성. 버튼: 자세히(`G.OpenGladiator`), 닫기. `GameController.ShowGladiator(id)` 추가.
2. **어떤 검투사든 클릭하면 팝업**: 목록·표의 `G.OpenGladiator` 호출을 `ShowGladiator`로. 지금 클릭이 안 되는 곳(도전장 줄의 상대 검투사, 모래 마당 공식 경기 줄·중계 머리 칸, 컵, 시장, 학당, 던전, 대륙 정세, 서열표는 루두스라 제외)에 초상화·이름 클릭을 붙인다. 검투사 상세의 "← 막사"는 이전 화면으로 돌아가게(`GameController`가 이전 화면 기억).
3. **종족·스타일 호버 설명**: 툴팁 오버레이(하이어라키) + TMP `<link="race:human">`·`<link="style:x">`를 읽는 컴포넌트(`TMP_TextUtilities.FindIntersectingLink`, 그 TMP는 raycastTarget 켜기). 종족: 종족 특성 이름·설명, 능력치 보정(`Attrs.Names`), 데뷔·전성기·은퇴 나이, 주급 배율. 스타일: 설명, 유리한 상대(`counters`), 불리한 상대(나를 counters로 가진 스타일). 검투사 표·상세·팝업·시장 표에 적용. 웹판 `RaceTag/StyleTag`(`src/ui/components/common.tsx`)가 같은 내용.
4. **단체전 동시 난전**: 지금 3:3·5:5 도전장과 컵(3명)은 `SeriesLogic`의 "이긴 사람이 남아 다음 상대와"(태그 매치). 이것을 양쪽 전원이 한 경기장에서 동시에 싸우는 방식으로. 경기장 좌표 시뮬(`Sim.cs`의 `Arena`)을 N:N으로 넓히거나 새 `TeamSim`: 각자 목표(가까운 적·약한 적·교범), 사람마다 좌표, 쓰러진 자는 빠지고, 한쪽이 전멸하거나 항복하면 끝. 1:1 경로는 그대로(골든 테스트가 `Arena.Enabled=false`로 웹판과 비교). 결과는 기존 정산(`ChallengeLogic.TeamSettle`, `CupLogic.PlayTie`)이 쓰도록 맞추고, 중계는 `ArenaStage`가 바둑알 최대 10개를 그리게. 생사 규칙(미시오 판정 등)을 쓰러진 사람마다 어떻게 할지는 사용자에게 선택지로 물어볼 것.
5. **이적 시장 관심도 검색 (FM처럼)**: 선수 찾기에 관심도 조건과 칸 추가. 관심도는 `Reputation.DemandFor`의 standing − reputation 차이로 낮음·보통·높음·관심 없음 등. 무소속은 `Economy.FreeAgentDemand`, 다른 루두스 소속은 이적 의사(같은 계산 + 소속 루두스 명성 차이)로.

### 진행 (2026-11-02 밤, 이어받은 세션 — 사용자가 MCP를 켜도 된다고 함)
- **1·2·3 끝남**: `GladiatorPopup`(+`Editor/Screens/PopupBuilder.cs`), `TipLayer`(마우스 아래 TMP 링크 `race:`/`style:` 툴팁, `g:` 링크와 초상화를 누르면 팝업), `Tips`(Race/Style/Name 링크 글). `GameSceneBuilder`가 캔버스 맨 끝에 팝업·툴팁을 붙인다. 목록·표의 클릭은 모두 `ShowGladiator`. 버튼 안(던전 원정대 고르기 줄 등)에서는 팝업을 열지 않는다. 플레이로 팝업 확인함, 툴팁은 글 내용만 확인(마우스 호버는 눈으로 못 봄).
- **이벤트 빈도 낮춤** (사용자 요청 "이벤트가 너무 자주 터짐", 쉬는 기간 없이 확률만): balance.json `ludusPerDay 0.04`, `gladiatorPerDay 0.002`, `worldPerDay 0.008` (전부 1/3). `SeasonGoldenTests`는 SetUp에서 웹판 값으로 되돌려 비교.
- **5 끝남: 이적 관심도**: `Reputation.InterestIn(state, g, myRep)` 0~4 (관심 없음·낮음·보통·높음·매우 높음). 0은 `DemandFor`가 실제로 거절하는 경우(격−명성 > 35). 다른 루두스 소속이면 그 루두스 명성이 우리보다 높은 만큼 ×0.5를 더한다. 선수 찾기에 "이적 관심도 ≥" 필터, 정렬(능력·잠재·관심도·주급·나이), 결과 표에 관심도 칸. 플레이로 확인.
- 남은 것: **4 단체전 동시 난전** (생사 규칙은 사용자에게 선택지로 물어볼 것).
- 주의: Unity를 안 켠 채 새 .cs를 만들면 `.csproj`에 안 들어가 dotnet 빌드가 그 파일을 못 본다. 에디터를 한 번 켜면 다시 만들어진다.

### 진행 (2026-11-02 밤 늦게)
- **4 끝남: 단체전 동시 난전.** `Sim.RunTeam(side0, side1, opt)` — 같은 `Match` 클래스를 N명으로 넓혔다(`team` 플래그, `ctxA/ctxB` = 지금 맞붙은 둘, `dist`는 1:1이면 저장값·난전이면 좌표에서, `Other(i)`/`Opp(f)`가 맞상대). 1:1 경로는 난수 순서 그대로(골든 통과). 상대 고르기 `PickTarget`(가까운·약한 적, 몰림 분산), 쓰러지면 그 자리에서 `Verdict`(1:1과 같은 사망 판정), 혼자 남은 자만 항복, `Separate()`로 겹침 방지. 결과 `MatchResult.sides/winnerSide/downs/deaths`.
  - `SeriesLogic.Melee = true`면 `RunSeries`가 단체전·컵 모두 난전 한 판(`Series.melee/meleeSides`, score = 쓰러뜨린 수). 정산 `MatchLogic.ApplyTeamResult`(사람마다 전적·인기·성장·부상·죽음). 골든 테스트는 `Melee=false`.
  - 중계: `Commentary`가 난전 시작·KO(누가 쓰러뜨렸나)·끝(이긴 편) 문장. `ArenaStage`는 말 N개(tokenB 복제), 우리 편 금색·상대 붉은색 테두리, 각자 마지막 맞상대를 본다. `MatchScreen.TeamBox`: 편 명단(쓰러지면 취소선, 죽으면 붉게) + 서 있는 수 + 편 체력. 플레이로 2:3 난전 중계 확인함.
  - `ArenaTests.TeamMeleeEndsCleanly` 추가 (9개 통과).
- **역대 기록 (사용자 요청 "팀 우승 기록도 역사에 FM처럼")**: `Logic/Honours.cs` — 시즌 종료(`Day.EndSeason`, 승강 전)에 나라·부마다 우승·준우승(`GameState.honours`), 모든 루두스 시즌 줄(`Ludus.seasons`: 부·순위·전적·RP·우승/승격/강등·우승컵). 컵 결승에서 `RecordCup`. 우리가 리그 우승하면 서판 소식. 골든은 `Honours.Enabled=false`.
  - 화면: 메뉴 '비석의 언덕'(hall)을 `HallBuilder/HallScreen`으로: 연혁(루두스 고르기·우승컵 진열장·시즌 표), 역대 우승(대회 고르기), 비석(우리 전사자).
- **하이퍼링크 (FM처럼)**: `Tips.Nation/Ludus/League/Cup/Item` 링크(`nation:` `ludus:` `comp:cup` `comp:league:나라:부` `item:`), 올리면 짧은 툴팁, 누르면 `InfoPopup`(카드 안 링크로 계속 이동, 뒤로 버튼, 아래 이동 버튼). 내용은 `UI/InfoCards.cs`. 검투사(`g:`)는 기존 팝업. 적용: 검투사 팝업·상세, 장비 칸, 시장(소속·장비), 서열표, 컵, 대륙 정세, 도전장, 던전 전리품, 비석의 언덕. 139개 링크 카드 전부 만들어지는지 확인.
- **장비 확장 (사용자 선택: 팔·다리 추가, 명품·전설 고유, 그림은 Codex, 초상화와 그림체 통일)**
  - 칸 6개 `Equipment.Slots` = main·off·helmet·armor·arms·legs (`Equipment.Get/Set/Items`). 팔·다리 기본 장비 7종(items.json 끝). 맞은 부위가 팔·어깨면 팔 보호구, 허벅지·다리면 다리 보호구 방어가 더해진다. 새로 만드는 검투사는 갑옷 무게대로 팔·다리를 갖춘다(GladiatorGen.FixLoadout, 난수 없음).
  - 고유 장비 `Resources/Data/uniques.json` + `Logic/Uniques.cs`: 명품 39(효과 1), 전설 18(효과 2 + 사연). id는 `gladius+3~senate_edict`(`Data.SplitItemId`/`UniqueOf`). 시장·던전에서 명품·전설이 나오면 `Uniques.Assign`이 고유를 붙인다(그 장비 고유가 없으면 같은 계열). 효과 16종은 `Sim`의 `Fighter.fx`로 계산(명중·치명·관통·피해·출혈·기절·막기·회피·방어·관중·위압·회복·빈사 피해·되갚기 `thorns` 사건·기력·선공).
  - `Uniques.Enabled=false`면 팔·다리·고유가 꺼진다(시장·전리품 목록에서 빠지고 난수도 안 씀): GoldenTests·SeasonGoldenTests가 끈다. `ArenaTests.UniqueGearWorksInCombat` 추가(10개 통과).
  - 화면: 장비 패널·검투사 팝업 6칸 + 아이콘, 장비 카드에 고유 효과·사연·큰 아이콘, 툴팁에 효과.
  - 아이콘: `ColosseumUnity/Tools/item-icons.mjs manifest` → `node tools/gen-images.mjs --manifest ColosseumUnity/Tools/item-icons.json --jobs 3`(Codex, 기준 그림 art/source/style/dark_ref.png 첨부, 3x3 마젠타 시트 11장 → art/source/item_icons/) → `item-icons.mjs slice` → `Assets/Resources/Art/icon/items/<기본 id 또는 고유 id>.png`(256으로 들여옴). `Widgets.ItemIcon/SetIcon`.
  - 끝남: 시트 11장 → 아이콘 97개 생성·자름(자르기 전에 칸 경계에 마젠타 띠를 칠해 옆 칸 조각을 끊고, 작은 조각 덩어리를 지운다). 플레이로 팝업 6칸 아이콘·전설 카드 확인. 아이콘을 다시 그리려면 시트 png를 지우고 gen-images를 다시 돌린 뒤 slice.
  - 아직: 시장 장비 표·창고 줄에는 아이콘을 안 붙였다(이름 링크·툴팁만). 기존 저장의 검투사는 팔·다리가 비어 있다(새로 생기는 검투사부터 갖춤).

작업 방식: 파일 편집 후 `dotnet build Colosseum.*.csproj`로 컴파일, `sh Tools/TestRunner/run.sh`로 테스트(에디터가 켜져 있으면 에디터 Test Runner). Unity 에디터는 사용자가 허락했을 때만 켠다(지금은 이미지 세션이 켜 둔 상태일 수 있음). 화면 글은 합니다체.

### 진행 (2026-11-03) 아이템 슬롯 + 그림체 통일
- **검수 결과**: 초상화(플랫 붓그림, 어두운 명도)와 달리 장비 아이콘은 녹·광택 디테일이 과하고, 메뉴 아이콘·나라 문장은 예전 밝은 동화풍, 전투 이펙트는 사실풍 페인팅이었다. rig/는 게임에서 안 쓴다(그대로 둠). 배경(ui/)은 어둡게 깔려 크게 튀지 않아 그대로 둠.
- **화풍 기준**: `art/source/style/flat_ref.png` (dark_ref + 실제 초상화 투구·갑옷 파츠). 사용자가 "이게 내가 원하는 아트 스타일"이라고 확인함. `ColosseumUnity/Tools/item-icons.mjs ref`로 다시 만든다.
- **아이콘 도구 확장**: `item-icons.mjs`가 묶음 items(97)·menu(16)·crest(6)·fx(9)를 한 매니페스트로 (`art/source/icon_sheets/<묶음>_NN.png`, 15장). `slice [묶음]`은 묶음별 폴더·크기(512/64/128/256)로 자른다. fx는 흩어진 방울이 본체라 조각 지우기를 끈다. 옛 시트는 `art/source/item_icons/`에 남아 있다.
- **아이템 슬롯**: `Tools/ui-slot.mjs` → `Resources/Art/ui/item_slot.png`(붓 청동 틀) + `item_slot_rim.png`(등급 색 테두리). 에디터 `UiKit.ItemSlot(name, parent, size)` (틀 > Rim > Icon), 런타임 `Widgets.SetSlot(slot, id, slotKind)` (등급 정교 이상이면 Rim에 등급 색, 비었으면 부위 기본 장비를 흐리게).
  - 적용: 검투사 상세 장비 패널(왼쪽 72px 슬롯, 글은 `Info/` 아래로 옮김), 검투사 팝업 6칸(68px), 장비 정보 카드(116px), 시장 장비 표·창고·내놓은 장비 표(40px, 줄 높이 48).
  - **씬 다시 조립 필요**: 빌더를 고쳤으니 Unity에서 `GameSceneBuilder.Build()`를 다시 돌려야 화면에 나온다 (아직 안 돌림 — 에디터는 사용자 허락 후).
- 그림 다시 그림 끝남: 15장 생성·자름 → 장비 97·메뉴 16·문장 6·이펙트 9 교체 (Codex 1차 시험은 너무 밝은 모바일풍이라 "어두운 명도·큰 그림자 덩어리·마른 붓결" 지시를 더해 다시 그림). 검수표: `node ColosseumUnity/Tools/art-audit.mjs <출력.png>`.
- 남은 것: Unity에서 `GameSceneBuilder.Build()` 다시 돌리고 플레이로 슬롯 확인. 장비 바꾸기 목록·흥정 맞교환 버튼·던전 전리품 줄엔 아직 슬롯이 없다(글만).
- 슬롯 플레이 확인 끝남 (상세 장비 6칸·등급 테두리 금/파랑/초록·빈 칸 자리 그림). 등급 테두리는 청동 틀과 구분되게 굵게 + 안쪽 빛. 아이콘 자르기는 고정 3x3 칸 대신 **덩어리 단위**(중심이 있는 칸에 통째로)로 바꿔 잘림 없앰.
- 스페이스 키 = 다음 날로 (`Shell.Update`, 글 입력·드롭다운 펼침 중엔 무시). 코드만, 키 입력은 눈으로 확인 못 함.

### 전략(전술) 설계 — 사용자 결정 (2026-11-03, 선택지로 받음)
- 왼쪽 메뉴에 새 화면 **'전략'** (막사 아래): 탭 3개 개인 전술 / 3:3 / 5:5. 메뉴 아이콘도 같은 화풍으로.
- **개인 전술** = 기존 교범 전부(자세·거리·노릴 부위·기술 순서·조건 규칙·항복 허용, 바꾸면 숙련도 하락 유지). 검투사 상세의 교범 패널(이미 있음)을 재사용.
  + 새로: **노릴 높이** atkZone(auto=상대 약한 방어 지대 / high / mid / low), **막을 높이** guardZone(auto=상대 무기가 강한 지대 / high / mid / low).
- **무기 계열별 지대 상성** `Exchange.ZoneProfiles[class] = (atk[상중하], def[상중하])`. 방패를 들면 방어는 shield 프로필. 맞붙은 방향의 (공격 강함 − 방어 강함)이 명중에 더해진다. 툴팁으로 계열마다 "상단 공격 강함·하단 방어 약함" 같은 힌트만 보여 준다(숫자 대신).
- **단체 전술** (3:3·5:5 각각 기본 하나, 경기 준비 화면에서 출전자에 맞게 손봄): 역할 4종 전열(가장 가까운 적을 막아서며 버팀)·사냥꾼(빈사·약한 적 마무리)·호위(지정한 우리 편을 노리는 적을 침)·견제(거리 두고 적 하나를 묶음) + 팀 방침 4종 집중/분산/약한 적 먼저/강한 적 먼저, 집중이면 첫 목표를 상대 명단에서 지정.
- 개인 교범 + 역할 덧씌우기: 자세·부위 등은 개인 교범, 누구를 노릴지·어디 설지는 역할·방침.
- AI 루두스도 감독 성향(출전자 능력·스타일)대로 단체 전술을 자동으로 짠다.
- 골든 테스트 보호: `Exchange.Zones=false`면 지대 상성·높이 지시를 끄고 난수 순서도 그대로 (GoldenTests·SeasonGoldenTests SetUp에서 끈다). 단체 전술은 난전(`SeriesLogic.Melee`) 경로에만.

### 진행 (2026-11-03 오전)
- **지대 상성 끝남**: `Exchange.ZoneProfiles`(계열별 atk/def 상·중·하), `AttackBias/GuardBias`(교범 `atkZone/guardZone`, null=자동), `ZoneHit`(명중 보정), `ZoneHint`(툴팁 문장). `Sim.RunExchange`가 높이 지시를 넘기고 `ResolveHit`이 ZoneHit를 더한다. `Exchange.Zones=false`면 난수 순서까지 웹판과 같음 → 골든 두 파일 SetUp에서 끔. EditMode 10개 통과.
  - 교범 새 필드는 null 기본(저장 JSON에 안 나옴 → 골든 비교 통과).
  - **아직 UI 없음**: 교범 패널에 "노릴 높이/막을 높이" 버튼 줄과 `ZoneHint` 툴팁을 붙여야 한다(전략 화면 작업 때).
- **장비 고르기 팝업 끝남**: `UI/ItemPicker.cs` + `PopupBuilder.BuildItemPicker`(캔버스, 검투사 팝업 다음). 검투사 상세 장비 칸의 슬롯(버튼)이나 '바꾸기'를 누르면 창고 후보를 아이템 슬롯 격자로 보여 주고, 누르면 `GearLogic.EquipItem`. 옛 인라인 후보 목록(Picker)은 지웠다. 플레이로 장착·창고 반환 확인.
- 주의: GladiatorBuilder 편집 중 `var msg = Text("Msg", pad,`가 교범 패널에도 있어 문자열 찾기가 엉뚱한 곳을 잡은 적이 있다 → 고쳤음. 스크립트 치환 땐 유일한 문자열로.

### 남은 일 (순서)
1. 단체 전술 로직: `TeamTactic { policy, focusTarget, roles{gid→front|hunter|guard|harass}, guardOf{gid→gid} }`, `Ludus.teamTactics["3"/"5"]`(null 기본), `MatchOptions.teamTactics[2]`, `Sim.PickTarget`에 역할·방침 반영(난전 경로만), AI는 `Tactics.AutoTeam(lineup)`.
2. 전략 화면: 메뉴 '전략'(막사 아래, 아이콘은 item-icons.mjs MENU에 strategy 추가 → 시트 1장), 탭 개인 전술(검투사 목록 + DoctrinePanel 재사용 + 높이 버튼) / 3:3 / 5:5(AutoLineup 명단 + 역할 드롭다운 + 방침 + 집중 대상). 경기 준비 화면(MatchScreen 단체전)에서 손보기.
3. UI 패널·버튼 그림: 사용자 요청 "ui 패널이나 버튼들 좀 이미지 뽑아서 깔끔하게". UiKit.Panel/Button/TitledPanel에 9-slice 스프라이트(붓 청동 테두리, flat_ref 화풍). ui-slot.mjs처럼 코드로 그리거나 Codex로.

### 진행 (2026-11-03 낮) 테이블
- **`ColosseumUnity/Tables/`** CSV 51개 + README. 형식: 1줄 열 이름, 2줄 자료형(int·float·string·enum:이름만), 3줄 설명, 4줄부터 데이터. 쉼표 든 값은 "..." 따옴표, 줄바꿈은 `\n` 글자.
  - 기술 설계(손으로 쓴 원본): `Enum`, `Skill`(60: 기존 33 + 무기 숙련·상시·반응·단체 기술), `Effect`(value_type, 기술·상태 공용, 상태는 group 90000+id), `SkillLearn`(특성처럼 조건 충족 → 주마다 learn_rate), `Status`(21), `SkillConst`. 습득·발동 규칙은 README.
  - 나머지(장비·고유·특성·스타일·종족·나라·부상·사건·밸런스·이름·시설·던전·전쟁 계약·훈련·직원·자세·노릴 부위·지대·단체 역할/방침 등)는 `Tools/export-tables.mjs`가 JSON + C# 상수 표에서 뽑는다. 열거형은 `EnumGame.csv`(자동).
  - 검사: `node ColosseumUnity/Tools/check-tables.mjs` (자료형·열거형·참조·아이콘) → 지금 문제 없음.
  - **런타임은 아직 CSV를 안 읽는다** (JSON·코드 상수 그대로). 기술 테이블은 설계만 있고 Sim·습득 로직에 아직 안 붙었다. 다음 단계 후보: CSV → 런타임 로더, 기술 습득(주간)·발동(Sim) 구현.
- 아이콘: `Resources/Art/icon/skills/<key>.png`(60), `icon/status/<key>.png`(21), `icon/menu/strategy.png`. item-icons.mjs 묶음 skills·status·menux (Skill.csv·Status.csv의 key를 읽는다).
- 단체 전술 로직·전략 화면·UI 패널 그림은 **아직 시작 전** (위 "남은 일 (순서)" 그대로).

### 진행 (2026-11-03 오후) 단체 전술·전략 화면
- **단체 전술 로직 끝남**: `TeamTactic`(Types.cs: policy·focusTarget·roles·guardOf), `Ludus.teamTactics["3"/"5"]`(null 기본), `MatchOptions.teamTactics`. `Sim.PickByTactic` — 방침(집중: 몰릴수록·첫 목표 우선 / 분산 / 약한 적 / 강한 적)과 역할(전열 가까운 적 / 사냥꾼 빈사 / 호위 지킬 동료를 노리는 적 / 견제 아무도 안 붙은 강한 적). 난수 안 씀. `Tactics.AutoRole/AutoTeam/TeamFor/SavedTeam`, AI는 aiPersonality로 방침(aggressive→weak, grudge→strong, developer→focus, 그 외 spread), 5명이면 호위 하나. `SeriesLogic.RunMelee`가 양편 전술을 넘긴다. 테스트 `ArenaTests.TeamTacticsSteerTargets` 추가 (EditMode 11개 통과).
- **전략 화면 끝남**: 메뉴 '전략'(막사 아래, 아이콘 icon/menu/strategy), `StrategyBuilder` + `StrategyScreen`. 탭 개인 전술(검투사 목록 + 교범 패널 재사용) / 3:3 / 5:5(팀 방침 버튼, 출전자 줄 역할 버튼 넷, 호위면 '지킬 동료' 버튼으로 순환, 자동으로 되돌리기). 출전자는 `SeriesLogic.AutoLineup`. 플레이로 확인.
- **교범 패널에 높이 줄**: 노릴 높이·막을 높이(자동/상단/중단/하단, null=자동) + 주무기 계열 힌트(`Exchange.ZoneHint`).
- **검투사 상세 탭 분리** (사용자 요청 "개인 전략이랑 아이템 장착 분리"): 신상 아래 탭 정보(능력치·숙련·기술·경력) / 장비 / 전술. `GladiatorScreen.SetTab(i)`.
- 아직: 경기 준비 화면(MatchScreen 단체전)에서 집중 대상 지정·그 경기만 손보기는 없음(지금은 전략 화면 기본값 + 집중 첫 목표는 상대 최강자 자동). 기술 테이블 런타임 연결, UI 패널·버튼 그림도 남음.

### 진행 (2026-11-03 오후 늦게) UI 그림·경기 전 전술·기술 테이블 연결
- **UI 패널·버튼 그림**: `Tools/ui-brush.mjs` → `ui/brush_frame·brush_fill·brush_head.png` (밝은 회색 붓 그림, 9-slice 경계는 ArtImporter). `UiKit.Brush()`로 Panel·Button·TitledPanel 제목 줄에 입힘. 색은 기존 Image.color 그대로 칠해져 Seg·primary 색 로직은 안 바뀜. Fill은 테두리 띠만큼 4px 들여 놓음. `Theme.PanelFrame` 추가.
- **경기 준비 전술 줄**: 모래 마당 단체전 줄 Info/Tactic — 팀 방침 드롭다운, 집중이면 첫 목표 드롭다운(상대 AutoLineup 명단), '역할 정하기'(전략 화면). 고르면 그 인원 기본 전술에 바로 저장. 플레이로 화면은 아직 못 봄(단체전 일정이 있어야 보임) — 컴파일·씬 조립만 확인.
- `Tactics.SavedTeam`이 처음 만들 때 자동 방침에서 시작 (역할만 바꿔도 방침이 집중으로 바뀌던 문제).
- **기술 테이블 연결** `Core/Logic/SkillBook.cs`:
  - 읽기: 원본 `Tables/*.csv`, 게임은 `Resources/Tables/` 사본 (`check-tables.mjs`가 검사 통과 시 복사). `Data.Ensure`가 불러옴. 테스트는 `SkillBook.LoadFrom(n => File.ReadAllText($"Tables/{n}.csv"))`.
  - 능동: skills.json에 없는 표 기술 → `SkillDef`로 변환해 `Data.AddSkill` (기존 33개는 JSON 정의 그대로, `Data.SkillFromJson`). 전투 후보에 넣기 전 `SkillBook.ProcGate`(trigger + proc_rate + 능력치 보정) — 표 기술만 난수를 씀. 회복(Heal)은 기력 회복으로, StatMod는 adapt 상태로 근사.
  - 상시·반응: `SkillBook.PassiveFx` → `Fighter.fx`(hit crit dmg block evade pierce crowd regen + 새 키 taken·parryCounter). Sim이 taken(받는 피해)·parryCounter(흘린 뒤 되받아치기 확률)를 쓴다. 반응 기술 중 OnParry·OnDamaged만 연결, OnBlock·OnDodge·OnAllyDown·OnAllyHpBelow(맞받아 밀치기·비켜서기·결속의 외침·엄호)는 **아직 효과 없음**. 조건부 상시 기술은 반만 상시 적용으로 근사.
  - 습득: 급료일 `Day` → `SkillBook.WeeklyLearn` (조건 충족 시 learn_rate × 등급 배율 + 독토레 평점/5 × 0.04, 주당 하나, 칸 차면 낮은 등급 밀어냄, 우리 검투사면 서판 소식). 능동 기술은 교범 기술 순서 끝에 붙는다.
  - `SkillBook.Enabled=false`(골든 두 파일) → 습득·표 기술 확률·보정치 모두 끔. `ArenaTests.SkillTablesLearnAndFight` 추가, EditMode 12개 통과.
  - 플레이 확인: 런타임 로드 OK, 8주 판정에 전 세계 검투사 257개 습득 → **빠른 편**. learn_rate나 SkillConst로 낮출 것.
- **반응 기술 연결 끝남**: `Sim.React(f, trigger, other, ally)` — 막음(OnBlock), 피함(OnDodge), 동료 쓰러짐(OnAllyDown, CheckDowns), 동료 위험(OnAllyHpBelow, 단체전 피격 뒤). 우선순위대로 하나만, 쿨·기력·방패 조건, 확률 굴림, 효과 테이블(Damage=되받아치기 ResolveHit, ApplyStatus, Momentum(AllAllies 포함), Disengage) 적용. `Fighter.reactions = SkillBook.Reactions(g)`(반응 기술 없으면 난수 안 씀). 해설 이벤트 `react` ("X이/가 곧바로 '비켜서기'!"). 흘림·피격 반응은 여전히 PassiveFx(parryCounter·thorns).
- **습득 속도 조정**: 주마다 후보 하나만 골라 한 번 굴림(종족 기술은 바로). 24주에 582명 중 256개(1인 0.44, 조건 충족자는 시즌 2~3개). README 습득 규칙 갱신.
- 테스트: `SkillTablesLearnAndFight`에 비켜서기 발동 확인 추가. EditMode 12개 통과.
- 남은 근사: 재기(회복)→기력 회복, 집중 자세(능력치 증감)→임기응변 상태, 조건부 상시 기술(살육 본능 등)→반만 상시. 전투에 회복·임시 능력치 효과를 넣으면 정확히 바꿀 수 있다.

### 진행 (2026-11-03 오전~) HP 버그·기술 아이콘·하이퍼링크 — 여기까지 끝, 컴파일 오류 0, EditMode 12개 통과
- **HP가 갑자기 차오르던 버그 (판별: 버그 맞음)**: ① ArenaStage가 중독·출혈 틱에서 HP 막대를 `hpAfter/(hpAfter+damage)`로 계산 → 막대가 튀어 오름(300판 중 67번). 이제 maxHp 기준, poison 이벤트에 maxHp를 넣음. ② 장비 고유 '재생'은 진짜 HP 회복인데 이벤트가 없어 화면이 몰랐음 → `regen` 이벤트 추가(ArenaStage·MatchScreen이 막대 갱신). ③ 상시 기술 '강철 폐'(기력 회복)를 HP 재생에 잘못 연결했던 것 → fx `staRegen`(기력)으로. 수정 뒤 300판 막대 상승 0번.
- **기술 아이콘 표시**: 검투사 상세 정보 탭 '기술' 패널이 아이콘+이름 칸 격자(`skillTemplate`), 아래 줄에 빈 칸·습득 조건 충족 수. `Tips.SkillIcon(id)`.
- **하이퍼링크 (사용자 원칙: FM처럼 웬만한 이름은 다 링크 → 툴팁 + 눌러서 정보 카드)**:
  - 새 링크: `Tips.Skill(id)`, `Tips.Trait(id, color)`, `Tips.Dungeon(id)`, `Tips.Status(key)` + `Tips.For`(툴팁) + `InfoCards.For`(카드: skill·trait·status·dungeon). `InfoCard.iconSprite`(기술·상태 그림을 카드 슬롯에).
  - 기술 카드: 설명·발동 시점·효과(상태 이름도 링크)·쿨타임·비용·발동 확률·필요 무기/종족·**습득 조건**·우리 검투사 중 익힌 자/조건 충족자.
  - SkillBook이 `Status.csv`도 읽음(`Statuses`). check-tables가 Status.csv도 Resources/Tables로 복사.
  - 화면 적용: 교범 기술 순서, 검투사 상세·팝업 특성, 영입 패널 소속 루두스, 금고 급료 밀린 검투사, 시장 제안·협상 루두스, 단체전 편 명단, 던전 의뢰·원정 줄, **중계 피드의 출전자 이름**(MatchScreen.LinkNames).
  - 아직 링크 안 된 곳(다음에): 서판 메시지 본문(상태에 저장된 글), 식솔(직원 링크 종류 없음), 버튼 안 글자(누르기와 겹쳐 일부러 안 함), 도전장 화면 설명 줄의 상대 루두스.
- 표현: '익히는 조건'→**습득 조건**, '쿨'→**쿨타임 N라운드** (Skill.csv 설명 줄, README 포함).

### 다음 일: 아웃게임 로비 (사용자 결정, 시작 전)
- **게임 제목: 아레나 도미누스** (Arena Dominus). 로비 큰 제목.
- 로비에 넣을 것(사용자 선택): **이어하기·새 게임·종료**, **설정**, **저장 칸 3개**. (명예의 전당은 안 함)
- 설계 제안:
  - `GameController.Start`가 바로 Load하지 말고 로비를 띄운다. `Shell`의 gameRoot/newGameRoot 옆에 lobbyRoot. 빌더 `Editor/Screens/LobbyBuilder.cs`(캔버스 자식, 배경 `ui/bg_main`, 큰 제목 serif, 메뉴 버튼 세로), 런타임 `UI/LobbyScreen.cs`.
  - 저장 칸: `GameController.Slot`(1~3), 저장 폴더 `persistentDataPath/slot{n}`(SaveSystem.Save/Load/Clear는 dir을 받으니 그대로). 기존 저장(persistentDataPath 바로 아래 `colosseum-league.save.v9.json`)은 처음 실행 때 slot1로 옮긴다. 칸 카드: 루두스 이름·나라·시즌·날짜·금고, [불러오기]/[새 게임]/[지우기(확인)]. 이어하기 = 마지막으로 쓴 칸(PlayerPrefs).
  - 새 게임: 칸 고른 뒤 기존 NewGameScreen으로. NewGameScreen에 '로비로' 뒤로 버튼.
  - 설정 패널: `GameAudio.MusicVolume/SfxVolume/Muted`(PlayerPrefs 이미 있음) 슬라이더·토글, 화면 모드 `Screen.fullScreenMode`(창/전체화면) + 해상도 몇 개. 게임 안 상단 바의 소리 버튼도 같은 값.
  - 게임 안 사이드바 맨 아래 '새 루두스 세우기'(restartButton)를 **'로비로'**로 바꾸기 (저장 후 로비).
  - 종료: `Application.Quit()` (에디터에서는 play 멈춤).
- **제목 폰트: 마루 부리(MaruBuri)** 로 결정됨. 이 PC에 파일 없음 → 네이버 한글 폰트 배포처에서 받아야 하는데 **다운로드는 사용자 허락이 필요**(파일 이름·출처·크기를 말하고 물을 것). 받으면 `Assets/Fonts/`에 두고 `Editor/FontSetup.cs`의 Serif를 마루 부리로(SerifPath SDF 다시 생성), 본문은 Noto Sans KR 유지.

### 진행 (2026-11-03 낮) 로비 끝
- **로비(아웃게임)**: 켜면 로비부터. `LobbyBuilder`(캔버스, 새 게임 화면 다음) + `UI/LobbyScreen.cs`. 로고(영어 ARENA DOMINUS, Codex `art/source/ui/logo.png` → `ui/logo.png`), 이어하기(마지막 칸 요약), 새 게임/불러오기(저장 칸 패널: 칸 카드, 덮어쓰기·지우기는 두 번 눌러 확인), 설정(음악·효과음 슬라이더, 소리 끄기, 창/전체 화면, 해상도 3종), 종료.
- **저장 칸 3개**: `GameController.Slot`, 폴더 `persistentDataPath/slotN`, `LoadSlot/BeginNewGame/DeleteSlot/ToLobby/Peek/LastSlot`(PlayerPrefs `colosseum.lastSlot`). 옛 단일 저장은 처음 실행 때 slot1로 옮김(`MigrateOldSave`). 사이드바 맨 아래 '새 루두스 세우기' → **'로비로'**(저장 후 로비). 새 게임 화면 취소는 게임이 없으면 로비로.
- **UI 그림 교체**: Codex `art/source/ui/ui_frames.png`(패널·버튼·제목 줄) → `Tools/ui-frames.mjs`가 명암만 남긴 회색 9-slice(`brush_frame` 192x128/18, `brush_fill` 128/8, `brush_button` 256x48/16, `brush_head` 320x36/12)로. 색은 기존 Image.color가 칠함. 버튼은 brush_button. Fill 들여쓰기 패널 9 / 버튼 6, TitledPanel 패딩 9. `Theme.Line` 0x56442f로 조금 밝힘.
- **비네트·그림자**: `Tools/ui-fx.mjs` → `ui/vignette.png`(로비 전체), `ui/logo_shadow.png`(로고 뒤 번진 그림자), `ui/soft_shadow.png`(9-slice 24, 로비 메뉴 버튼·오른쪽 패널 뒤). 로비 메뉴 버튼은 Holder(그림자+버튼), 오른쪽 패널도 Holder째 켜고 끔.
- 로비 OnEnable에서 `Data.Ensure()` (GameController.Awake보다 먼저 켜질 때 Nation NRE 났음).
- 남음: **마루 부리 폰트**(다운로드 허락 필요), 게임 안 패널에도 그림자를 줄지(지금은 로비만).

### 진행 (2026-11-03 오후) 로비 로고 교체
- 로고를 **글자형(시안 1번: ARENA DOMINUS + 월계수 가로줄)**으로 바꿈. 원본 `art/source/ui/logo_a.png`(사용자 시안 시트 왼쪽을 잘라 마젠타 제거), Unity `ui/logo.png` 1200x583. `node ColosseumUnity/Tools/ui-fx.mjs`로 `logo_shadow.png` 다시 만듦.
- `LobbyBuilder` LogoHolder 560x328 → 560x272(새 비율), 그림자 Stretch(-28,-18,-28,-42), 알파 0.95.
- 확인 필요: Unity에서 씬 빌더 다시 돌려 로비 보기 (원본이 489px 폭이라 크게 띄우면 약간 흐릴 수 있음).

### 진행 (2026-11-03 오후) 기술 습득 속도, ESC, 이어하기
- **기술 습득이 너무 잦던 버그**: 급료일(7일, 시즌당 24번)마다 굴렸고, 교관 평점(1~20)을 1~5로 보고 `/5` 해서 교관 보너스가 최대 +16%였음. → `SkillBook.WeeklyLearn`이 `LearnCheckDays`(28일, 달마다 = 시즌 6번)에만 일반 기술을 굴림(종족 기술 learn_rate 1은 급료일마다 그대로). 교관 보너스는 평점/20 × `LearnTrainerBonus`(0.06, 최대 +6%). `SkillConst.csv` 두 곳(Tables/, Resources/Tables/) 같이 고침. 안내 글 '급료일마다' → '달마다'(InfoCards, GladiatorScreen).
- **ESC = 저장하고 로비로**: `Shell.LateUpdate`. 팝업(InfoPopup·GladiatorPopup·ItemPicker)은 ESC로 먼저 닫히고 `Shell.EscUsedFrame`을 남겨 같은 ESC로 로비까지 가지 않음. 로비에서는 ESC가 열린 칸/설정 패널을 닫음.
- **이어하기 = 불러올 칸 고르기**(load 모드 패널 열기, 마지막 칸에 '(마지막)' 표시). 바로 들어가지 않음.
- `dotnet build Colosseum.UI.csproj` 오류 0. Unity 실행 확인은 안 함.
- **메인 메뉴 확인 창**: 새 `UI/ConfirmPopup.cs`(예/아니오, ESC=아니오, Enter=예) + `PopupBuilder.BuildConfirmPopup`(GameSceneBuilder에서 ItemPicker 다음). ESC와 사이드바 '로비로' 둘 다 "메인 메뉴로 이동하시겠습니까?"를 묻고 예일 때만 저장 후 로비. 씬을 다시 빌드하기 전엔 확인 창이 없어 바로 간다(`Ask`의 대체 동작). `Colosseum.UI.csproj`에 파일 추가함.
- **종족 기술은 태어날 때 가짐**: `SkillBook.GiveRaceSkills(g)`(learn_rate 1 = 종족 조건 기술, 서판 알림 없음)를 `GladiatorGen` 생성 직후 호출. 옛 저장 대비로 `WeeklyLearn` 시작 때 살아 있는 모두에게 조용히 챙김. 달마다 굴림에서는 종족 기술을 뺌.
- 헤드리스 테스트 12/12 통과.
- (2026-11-03) 사용자 요청:
  - 급료일 금액: `Economy.NextPayBill` (주급·밀린 급료·식비·시설·식솔, Payday와 같은 계산). 상단 바 "급료일에 낼 돈"(모자라면 붉게), 금고 화면. `Calendar.DaysUntilPayday`(1~7, 급료일 당일은 이미 냈으므로 7).
  - 이적 시장 기간: `Transfers.WindowState/WindowLine`. 상단 바·이적 시장 머리·영입 패널에 표시. 툴팁 링크 `info:pay`, `info:window` (Tips.InfoTip).
  - 협상 답장 지연 + 영입 경쟁: `Logic/Negotiations.cs`. 이적료·개인 계약·무소속 주급 제안은 `GameState.bids`에 쌓였다가 1~3일 뒤 서판으로 답이 온다. 처음 제안할 때 같은 나라 AI 루두스가 경쟁자로 붙을 수 있다(`GameState.rivals`). 경쟁자가 마지노선 이상·우리보다 높은 이적료를 부르면 그쪽에 팔리고, 개인 계약은 명성을 감안한 조건 비교로 검투사가 고른다. 보낸 제안이 없으면 난수를 안 쓴다 → 골든 테스트 영향 없음. 장비·식솔 흥정은 그대로 즉시.
  - 이적료 합의와 개인 계약은 별개 (합의 뒤 검투사가 "무명 루두스엔 안 가"로 거절할 수 있음 — 사용자: 실제로도 그러니 그대로 둔다).
  - 툴팁: 특성 줄 맨 앞 종족 특성(`racial:`), 단체전 역할 버튼(`role:`), 식솔 후보 자리(`staff:`)에 링크 추가.
  - Windows 빌드: `Builds/ColosseumWindows/Colosseum.exe` (`Builds/Windows`는 RatWar 빌드라 건드리지 않음).

### 진행 (2026-11-03 밤) FM식 규칙 묶음 — `FmRules.Enabled` (골든 두 파일은 끈다)
- **Unity Hub 등록**: `%APPDATA%/UnityHub/hub.db`의 projects 표에 `D:\pro\simulation\ColosseumUnity` 추가 (백업은 세션 스크래치). 첫 임포트·csproj 생성은 배치 모드로 함.
- **4부 리그**: 모든 나라 1~4부 × 10개 (240 루두스, 검투사 약 1,940명, 하루 처리 약 25ms). `FmRules.RpBase/RosterTarget/ReputationBase/TierValueMult`. 시즌 끝 승강은 나라·부마다 위 2 / 아래 2 (`Day.EndSeason` FM 분기), 역대 기록·루두스 연혁도 4부까지. 새 게임에서 시작 리그(1~4부 = 난이도) 고름 (`World.NewGame(..., startTier)`).
- **이름 테이블**: `Tools/gen-names.mjs` → `Tables/LudusName.csv`(912, 나라 분위기별) · `Tables/PersonName.csv`(1,149, 종족·성별 이름·성·별명). `Logic/NameBook.cs`. check-tables가 Resources/Tables로 복사.
- **몸값·주급 요구** `Logic/Valuation.cs`: 격 = 능력 32%·명성 42%·승률·킬. 몸값 = 능력²×승률 + 명성 + 잠재 + 킬, × 나이 × 뛰는 리그 × 계약 만료 할인. CA는 화면에서 숨김 → `Widgets.CaStars`(우리 = 독토레 '능력 판단'만큼 흐린 평가, 남 = 정찰 범위).
- **계약 조항** `Logic/Contracts.cs` + `GladiatorContract`의 새 칸: 계약금·승리/킬 수당·승격 보너스·승격 시 연장·인상·강등 시 삭감·최소 이적료. 검투사는 조항을 '주급 환산 값'으로 따져(성격·나이·승격 가능성) 흥정. 재계약(renew), FM식은 재계약 안 하면 시즌 끝에 무소속으로 떠남(120·150일 경고).
- **결정 전엔 못 넘어감**: `GameState.decisions`(상대 역제안), 받은 이적 제안(open). `Day.Blockers`에 들어간다. 다음 날 버튼·스페이스는 **읽지 않은 소식부터 차례로** 열고, 그다음 막힌 일이면 협상 창을 연다.
- **받은 제안 역제안·이적 명단·최소 이적료**: `Transfers.CounterOffer/ListForTransfer/Unlist/DailyOffersFm` (AI는 최대 3번 값을 올리거나 거둬들임).
- **식솔 세부 능력치 14개** (`Staff.attrs`, `StaffLogic.AttrKeys`): 훈련 종류별 지도, 판단(별점 정확도·잠재력), 치료·몸 관리, 동기 부여·규율·인간 관리, 협상·인맥. 평점(rating)은 역할 가중 평균.
- **전투 FM식 보정** (`Sim.DayForm/FmHit/FmDamage/StanceEdge`): 그날 컨디션(일관성·큰 무대), 민첩·반응 회피, 공격성×지구력·침착(무모함), 판단·예측, 스타일 상성 강화, 자세 가위바위보. 측정 `sh Tools/BalanceSim/run.sh combat`: CA 8~14 차이 강자 평균 65%(쌍마다 50~83%, 40쌍 중 10쌍은 약자 우세), 상성 자세 +8%p.
- **경제**: FM식 전용 표 `purseFm/ticketIncomeFm/dividendBaseFm`(웹판 값은 그대로). `sh Tools/BalanceSim/run.sh econ 3` — 모든 시작 리그에서 3시즌 흑자.
- **도전장**: 받는 쪽이 낼 사람이 없으면(부상·경기 예정·휴식) 못 보냄(`ChallengeLogic.CanReceive`), AI→우리 빈도 ⅜, 우리에게 답할 도전장 2통까지. 도전장 화면엔 보낼 수 있는 루두스만.
- **UI**: `NegotiationPopup`(+`PopupBuilder.BuildNegotiationPopup`), 서판 결정 카드, 영입 패널(우리 검투사 = 재계약·이적 명단), 시장(협상·이적 명단 버튼), 서열표 1~4부 탭·지난 시즌 칸, 새 게임 시작 리그 버튼, 식솔 링크(`person:`)·능력치 카드. 서판 본문은 `Tips.AutoLink`로 이름에 링크.
- **씬 다시 조립 필요**: 빌더를 고쳤다 (LeagueBuilder·GameSceneBuilder 새 게임·MarketBuilder·PopupBuilder). Unity에서 `GameSceneBuilder.Build()`.
- 테스트 러너 `Tools/TestRunner/runner.csproj`는 *.csproj가 gitignore라 저장소에 없다 (net10.0 콘솔 프로젝트로 다시 만들면 된다).

### 진행 (2026-11-03 밤) MCP로 씬 적용 + 전쟁 규칙
- **MCP 연결**: Unity의 MCP for Unity는 기본이 HTTP(127.0.0.1:8080, 에디터 창에서 서버 시작)라 `.mcp.json`도 `{"type":"http","url":"http://127.0.0.1:8080/mcp"}`로 바꿨다. stdio(uvx)로 띄우면 에디터와 만나지 못한다.
- **씬 적용**: MCP `execute_code`로 `GameSceneBuilder.Build()` → `Game.unity` 저장. 확인한 새 객체: NegotiationPopup, 서열표 Tier3·Tier4 탭과 Last 칸, 새 게임 Tier1~4 버튼, 시장 List 버튼.
- **플레이 확인** (스크린샷 `Assets/Screenshots/`, gitignore): 새 게임 3부 선택·설명, 다음 날 버튼이 '새 소식 읽기 (N)'로 먼저 소식을 열고 날짜는 그대로, 서열표 4부(승격 구역)·그롬마르 2부(승강 구역·부족풍 이름), 우리 검투사 '재계약 협상'·'이적 명단' 버튼 → 협상 창, 계약 판에서 주급을 깎으면 반응이 '긍정적' → '모욕적'으로 바로 바뀜, 식솔 이름 → 세부 능력치 카드. 협상 창 높이 780에 조항 10줄이 넘치지 않음, 새 게임 하단 줄 폭 정상.
- **고친 것**: ① FM식 시작 자유민에게 계약이 없어 재계약 버튼이 안 나옴 → 생성 때 시즌 2까지 계약. ② 재계약 요구가 영입용 명성 격차 배율(최대 ×2)을 그대로 써서 64 D → 102 D로 뜀 → 재계약은 격차 1점당 1%, 최대 +40%. ③ 식솔 후보 줄에 핵심 능력치 글이 넘침 → 표에는 빼고(이름 툴팁·카드로), 우리 식솔 카드엔 가장 높은 2개만.
- **전쟁 규칙 (사용자 요청)**: 전쟁 중인 나라의 루두스와는 이적 거래 불가 (`Transfers.WarBlock`, 이적료 제안·답장·개인 계약 모두), 판테온 컵에서 전쟁 중인 나라 루두스끼리 만나면 사투 (`CupLogic.TieRule`, 컵 화면·중계 제목에 표시). `WarLogic.AtWar`. 테스트 `FmRulesTests.WarBlocksTransfersAndMakesCupSudden`. EditMode 18개 통과.

### 진행 (2026-11-04) 리그별 돈 계단
- 부가 올라갈수록 수입이 커지게 다시 나눔 (FM식 전용 표, 웹판 값은 그대로): 상금 `purseFm` 90/150/285/360, 관중 `ticketIncomeFm` 160/280/520/700, 시즌 배당 `dividendBaseFm` 3,000/5,500/10,500/14,000 (4부→1부 순). 시작 금고 `World.StartTreasury` 3,500/5,500/9,000/16,000 + 첫 배당, AI 금고·부수입도 같은 비율.
- `sh Tools/BalanceSim/run.sh econ 3`: 시즌당 대략 4부 +2~4천(우승 +1만), 3부 +3~5천, 2부 +4~6천, 1부 +2.5만~4.5만 D. 4부도 바닥나지 않음.
- 경력 표 '능력' 칸: CA 숫자 대신 별점 + ▲▼ (GladiatorBuilder 칸 폭 104로, 씬 다시 조립 필요).

### 진행 (2026-11-04) 시즌 흐름 개편: 정규 리그 → 국내 컵 → 판테온 컵, 올림피카 컵
사용자 결정: 판테온은 전부 5:5 미시오 단판, 올림피카 대표는 "성적이 좋을 때만 감독"(명성 나라 1위면 제안, 아니면 자동), 올림피카는 정규 리그 사이 대표 주간, 동률의 "킬 수"는 쓰러뜨린 상대 수.
- **달력** (`FmRules.RegularEnd=105`, `ChallengeClose=98`): 정규 리그 15~105일. 106일에 순위 동결(`Day.LeagueEnd`: 최소 경기 페널티, 이후 `FmRules.Frozen` → RP 변동 없음. 승강·판테온 출전은 이 순위). AI끼리 리그 경기 확률 ×1.4, AI가 강한 상대를 피하는 정도 절반(최소 30% 수락).
- **시즌 배당** 순위 비례: 뛴 부의 `dividendBaseFm` × `FmRules.DividendCoef`(1위 ×1.6 ~ 10위 ×0.6). 승강과 무관.
- **국내 컵** `Logic/NatCupLogic.cs` (`GameState.natCups`, 나라별 40팀): 추첨 106일, 40강 108·20강 111·10강 114, 5강 리그 117~129일(5라운드). `Fixtures.Table`: 승수 → 쓰러뜨린 상대 수 → 승자승 → RP. 다른 나라 40·20강은 `QuickTie`(전력 공식, 16ms×100판 방지). 인원이 모자라면 있는 인원, 0명이면 기권. 우리는 `Forfeit`(불참 버튼). 탈락 메시지에 던전 안내. RP 없음.
- **판테온 컵** FM식 (`Cup.fm`, `CupLogic.DailyCupFm`): 추첨 131일. 플레이오프 6팀(국내 컵 우승, 그 팀이 1부 1·2위면 1부 3위) 3팀 2개 조 133~139일 → 조 상위 2팀 + 나라별 1부 1·2위 12팀 = 16팀 4개 조 142~148일 → 조 상위 2팀 8강 151 · 4강 154 · 결승 157일. 전쟁 중 사투 규칙은 그대로. 옛 저장의 진행 중 컵은 옛 일정으로 끝난다.
- **올림피카 컵** `Logic/OlympicaLogic.cs` (`GameState.olympica`): 시즌 % 4 == 0, 5일에 대표 7명 선발·감독 제안(결정 전 진행 불가), 대표 주간 20~92일 8일 간격 10라운드(그날은 리그 경기 없음). 나라는 `g.nation` 기준. 5:5 미시오, 홈·원정, 많이 이긴 나라 우승. 감독이면 대표 편집(첫 경기 전)·출전 5명 지정·직접 중계, 우승 포상 1.5만 D.
- **화면**: 메뉴 '국내 컵'·'올림피카 컵' (`NatCupScreen`, `OlympicaScreen`, `Editor/Screens/CompBuilder.cs`), 판테온 화면에 트로피·일정·플레이오프 조·5인 출전, 서판 감독 제안 카드, 비석의 언덕 대회 목록(1~4부, 국내 컵, 올림피카)과 트로피 진열, 대회 정보 카드(`comp:natcup:나라`, `comp:olympica`) + 트로피 그림.
- **그림**: `Tools/item-icons.mjs` 묶음 `trophy`(12개, `Art/icon/trophy/`) · `menuy`(메뉴 2개). `node tools/gen-images.mjs --manifest ColosseumUnity/Tools/item-icons.json --only trophy` 뒤 `item-icons.mjs slice trophy`.
- 측정 도구 `Tools/BalanceSim`: `combat`, `econ N`, `melee`(5:5 비용), `chal`(도전장 사유).
- **씬 적용·플레이 확인** (MCP `execute_code`로 `GameSceneBuilder.Build()`, 콘솔 오류 0): 국내 컵 40강 중계·복귀, 5강 표, 판테온 플레이오프 조, 올림피카 감독 제안·1라운드 중계·순위표·대표 명단, 비석의 언덕 트로피 진열을 플레이로 확인. 메뉴 20칸이라 줄 높이 42→36. 스크린샷 `Assets/Screenshots/`(git 제외).
- 플레이로 못 본 것: 판테온 8강~결승 중계(로직은 테스트), 여러 시즌 연속 실제 플레이, Windows 빌드는 이 변경 전 것.
- 세션에 UnityMCP 도구가 안 잡혀도 서버가 떠 있으면 8080에 JSON-RPC(`initialize` → `tools/call`, `mcp-session-id` 헤더)로 직접 부를 수 있다.

### 진행 (2026-11-04) 일정 탭 · 날짜 지정 도전/연습전 · 대회 수상 · 경력 탭
- **일정 탭** (메뉴 '일정', `UI/Screens/ScheduleScreen.cs` + `Editor/Screens/ScheduleBuilder.cs`): 한 달 28일 = 7×4 달력(칸 원본 `CellTemplate`, 그리드 안 비활성). 칸마다 우리 경기·답 대기·컵 일정·대표 주간·이적 시장·급료일 표시, 밝은 칸 = 정규 리그 기간. 날을 누르면 오른쪽에 그날 일정 + 보내기 양식.
- **날짜 지정 도전** (`ChallengeLogic.TryCreateChallengeOn/CanChallengeOn/DateBlock`, `Challenge.wantAt`): 오늘+3일부터 시즌 끝까지. 정규 리그 기간(15~105일)이면 리그 도전장(대표 주간 제외), 그 밖이면 **연습전**(`Challenge.friendly`): 훈련전 고정, 명성·상금·RP·리그 경기 수 없음(성장만), 거절해도 불이익 없음, AI는 80% 수락. 상대는 **컵 일정이 끝난 팀만**(`InCup` = `InNatCup` || `InPantheon`, 판테온 추첨 전엔 1부 1~3위·국내 컵 우승 팀을 살아 있는 것으로 봄). 우리 컵 경기 날은 못 고름(`CupOn`). 받으면 그 날짜로 잡힘(`Schedule`), AI가 그날 낼 사람이 없으면 거절.
- **대회 수상** (`Logic/Awards.cs`, `GameState.compStats/awards/ceremonySeason`): 경기마다 대회별 개인 기록(출전·승·쓰러뜨린 상대·죽음)을 쌓음 — `ApplyOptions.comp`, `RunOptions.comp`, `ApplyTeamResult(..., comp)`. 리그(106일, 나라×부), 국내 컵·판테온·올림피카(끝날 때)마다 최우수 검투사 · 최다 처치 · 최우수 신인(종족별 **성장기** 나이) · 올해의 라니스타(리그: 전력 순위보다 가장 높이 오른 팀, 컵: 멀리 간 정도 + 아래 부 가산, 올림피카: 우승국 대표팀 감독). 시즌 끝(`EndSeason` 처음)에 나라별 올해의 검투사 + 세계 올해의 검투사. 보상: 명성(+4~12), 소정의 상금(리그 1부 2,000 ~ 4부 500, 세계 8,000), 경력 기록. `Awards.Enabled = Honours.Enabled && FmRules.Enabled`(골든 테스트는 꺼짐). 다른 나라 국내 컵 앞 라운드는 `QuickTie`라 개인 기록이 없음.
- **수상 화면**: 서판 알림(우리 나라 대회만), 시즌이 끝나면 **시상식 카드**(`InfoCards` `awards:season:N`, Shell이 한 번 띄움), 검투사 카드·상세의 '수상 N회' 링크(`awards:g:ID`), 비석의 언덕 **'수상' 탭**(대회 고르기, 시즌 시상 포함).
- **경력 탭**: 검투사 상세 탭이 정보 / **경력** / 장비 / 전술. 경력 = 시즌별 기록 표(수상 칸 추가) + 수상·우승(받은 상, 뛰던 루두스가 그 시즌 든 우승컵) + 일대기(이름 링크, 25개씩 '전부 보기').
- 그림: Codex 묶음 `extra`(`Tools/item-icons.mjs`) → `icon/menu/schedule.png`, `icon/award.png`.
- 테스트: `FmRulesTests.DatedChallengeAndFriendly`, `AwardsAfterFullSeason` 추가, EditMode 26/26 통과. 사이드바 메뉴 21칸이라 줄 높이 35.
- 플레이 확인: 일정 화면(9번 시험 칸, 사용자 저장 칸은 안 건드림) — 20일 리그 도전·6일 연습전이 답을 받아 잡힘. 수상·경력 탭은 씬 재조립 뒤 확인 필요(아래).
- **(같은 날 이어서) 바뀐 점**
  - 휴식기 경기 이름은 **연습전**(규칙은 훈련전 고정). `ChallengeLogic.RuleLabel`.
  - 시상 카드 줄: `GameState.ceremonies`(링크 목록). 대회가 끝날 때마다 `awards:comp:<대회 키>:<시즌>` 카드(우리 리그·우리 국내 컵·판테온·올림피카), 시즌 끝 `awards:season:N`은 **세계·나라별 올해의 검투사만**. Shell이 갱신마다 하나씩 띄움.
  - **평점 매니저** `Logic/Ratings.cs`: 모든 경기(1:1·난전) 전투 기록 → 출전자별 `FighterStats`(입힌·받은 피해, 명중, 막기, 피하기, 치명타, 쓰러뜨림, 죽음)와 평점 3.0~10.0(6.0 기준). `MatchResult.stats`. 공식 경기만 `SeasonRecord.ratingSum/rated`, `CompStat.ratingSum`에 쌓고, 수상 점수는 평균 평점을 가장 크게 반영. 경력 표 '평점' 칸.
  - **경기 결과창** (`MatchBuilder` Result, `MatchScreen.ShowResult`): 경기가 끝나면 경기장 위에 출전자 표 + 경기 최우수 검투사. '결과창/중계 기록' 전환, '한 판 더' 없앰 → '다음으로'(오늘 경기가 더 없으면 서판으로) / 단체전 '다음 판'.
  - **명성과 주급** (`Reputation.DemandFor`, FM식만): 명문일수록 완만하게 덜 요구(격차 −30이면 ×0.79, 최소 ×0.75). 우리보다 격이 12 넘게 높은 검투사는 계약금·최소 이적료가 없으면 아쉬워함(최대 −12%, 협상 창 역제안 한마디).
  - **불만·인상 요구** `Logic/Grievances.cs` (`Gladiator.grievance/grievanceUntil/raiseSeason/raiseBy`): 우리·AI 검투사 같은 규칙 — ① 소속보다 이름난(+8) 루두스의 제안이 거절됨(우리가 `RejectOffer`, AI가 우리 제안에 '결렬'), 사기 최대 −12, 6주 ② 인상 요구(받을 만한 주급의 1/1.35 미만, 급료일, 시즌 1번): 우리는 14일 안에 재계약 안 하면 −8·4주, AI는 금고·성향으로 수락 아니면 불만 ③ 주전급(루두스 상위 5)이 리그 중 35일 넘게 결장 −5·4주 ④ AI끼리 영입 시도: 소속보다 격이 8 넘게 높은 AI 검투사에게 급료일마다 2.5% 확률로 같은 나라 더 큰 루두스가 제안했다가 거절(→①). 불만이면 매일 사기 −0.2(30까지), **몸값 ×0.85**. 재계약(`Contracts.Apply`)이면 풀림. 측정(한 시즌, 1,940명, 사용자 목표 10~15%): 불만 207~229명(10.7~11.8%), 동시 최대 112~119명.
  - **몸값과 계약 기간** (`Valuation.MarketValue`): 남은 계약 0시즌 ×0.85→×0.65(시즌 말로 갈수록), 1시즌 ×0.92, 2시즌 ×1.0, 3시즌 이상 ×1.05.
  - 테스트: `RejectingBiggerClubUpsetsGladiator` 추가.
- **(같은 날 이어서) 불만은 원인별로 풀림 · 계약 만료**
  - `Gladiator.grievanceKind`(raise | transfer | bench), `grievanceLudus`, `grievanceAt`. 풀리는 조건(`Grievances.Resolved`): 기한(시간) / 다른 루두스로 옮김(모두) / bench는 그 뒤 경기에 나가면 / transfer는 이적 명단에 올리면. **재계약(`Contracts.Apply` → `OnNewContract`)은 raise만 푼다.** 화면 한 줄·서판에 푸는 방법(`Grievances.Remedy`)을 보여 줌, 경기·이적 명단으로 풀리면 우리 검투사는 서판 알림.
  - **계약 만료**: 우리 검투사는 재계약하지 않으면 시즌 끝에 무소속(전부터 있던 규칙)인데, `RefreshFreeAgents`가 바로 지워 버리던 버그 → 만료로 나간 검투사는 명단을 새로 만든 **뒤에** `freeAgents`에 넣는다(`Day.EndSeason`의 `released`). **AI도 만료 처리**(`Day.AiContracts`): 능력 상위 3(또는 쇠퇴기 아닌 상위 6)은 대개 재계약(바라는 기간, 받을 만한 주급), 루두스보다 격이 12 넘게 높으면 45%·불만이면 +30%·충성으로 덜 떠남, 나머지는 무소속. 계약서 없는 AI 자유민은 시즌 끝에 1~3시즌 계약을 받음. 새 게임 AI 자유민 계약은 로스터 순서로 1~4시즌(우리는 그대로 2). 시즌 끝 서판 '계약 만료로 풀린 검투사 N명'(우리 나라 눈에 띄는 5명).
  - AI는 무소속을 영입하지 않는다(ReplenishAi가 새로 뽑음). 풀린 검투사는 `WeeklyFreeAgents`로 조금씩 사라진다 — 필요하면 AI 무소속 영입을 다음에.
  - 측정(한 시즌): 불만 231명/1,984(11.6%), 동시 최대 99, 시즌 끝 AI 만료 무소속 42명. 테스트 `ExpiredContractsBecomeFreeAgents` 추가, 28/28.
- **(같은 날 이어서) 등록 로스터 · AI 무소속 영입 · 인원 · 이름 · 스크롤**
  - **등록 로스터** `Logic/Registration.cs` (`Ludus.registered/regSeen`): 공식 경기(리그·컵)는 등록 15명만. 미등록은 훈련·연습전·던전만. `ChallengeLogic.Availability(..., official)`(기본 true) + `FreeOn/CanReceive/PickFighter/SeriesLogic.AutoLineup`의 `official` 인자 — 연습전 경로만 false. 해제는 이적 시장 기간(`Transfers.InWindow`)에만, 빈자리는 언제든. `Registration.Daily`(하루 처리 처음): 떠난 사람 정리, 새로 온 사람은 자리가 있으면 자동 등록, 처음엔 강한 15명. AI는 1일·78일에 강한 15명으로 다시, 빈자리는 늘 채움. UI: 검투사 상세 계약 패널 '등록하기/등록 해제 (n/15)'(`AcquirePanel.regButton`, 빌더 `Reg`), 막사 머리글 등록 수, 표 상태 '미등록', 시장 머리글.
  - **막사 인원 제한 없앰** (FM식 `Economy.RosterCapacity` = 무제한, 숙소 효과 글에서 정원 뺌). 보유는 주급으로만 제한.
  - **AI 무소속 영입** `Logic/FreeAgency.cs` (급료일, `Economy.WeeklyFreeAgents` 뒤): 인원이 기본보다 적으면 채우고(60%), 등록 자리가 남고 금고가 (주급+새 주급)×12주 이상이면 능력 5위+2보다 나은 무소속을 데려옴(15%). 올 마음(명성 격차) 확인, 우리가 협상 중인 검투사 제외, 한 주 최대 3명, 무소속 명단 12명 이하면 멈춤.
  - **인원**: AI 기본 인원 `FmRules.AiRoster` 11(웹판 8), 플레이어 시작 8명(웹판 6), 새 게임 때 나라마다 무소속 4명(1~4부 수준) 추가. 대륙 약 2,700명.
  - **이름**: `Tools/gen-names.mjs` 음절 조합 종족당 남 120·여 70 → PersonName 1,990줄(전 1,149). `node ColosseumUnity/Tools/check-tables.mjs`로 Resources에 복사.
  - **스크롤**: `UiKit.ScrollView`에 세로 스크롤바(넘칠 때만, AutoHideAndExpandViewport). 스크롤이 없던 도전장·서열표·안뜰·연무장·유소년 학당·일정 화면을 화면 전체 스크롤로 감쌈(`page`). 씬 재조립함.
- **(같은 날 이어서) 측정 · 신분 구분 없앰 · 유소년 계약**
  - 측정(에디터, 1.5시즌): AI 무소속 영입 시즌 1에 63명, 무소속 명단 48→12(바닥)→시즌 끝 131, AI 평균 10.3~11명(최대 12, 등록 15 찬 AI 없음), AI 금고 중앙값 증가·적자 1~3곳(−1,650까지). 성능: 등록 정리 10→1.9ms/일, 불만 급료일 38→15.6ms(능력 정렬을 필요할 때만). dotnet BalanceSim 하루 20~26ms(예전 기록 25ms와 비슷, 에디터는 ~100ms).
  - **노예·자유민 구분 없앰** (FM식): 새 검투사 모두 계약(`GladiatorGen` status), 시작 로스터도 계약(우리 2~4시즌, AI 1~4시즌으로 나눔), 시작 주급은 모두 `Valuation.FairWage`(리그 수준), 옛 저장의 노예는 하루 처리 처음에 계약 검투사로 바꿈(주급 받을 만한 값의 80%). 검투사 상세 '신분' 칸 → '계약'(시즌 N까지). 나라 소개 글(nations.json·Nation.csv)과 학당 설명에서 노예 문구 뺌. 사건 '노예선 난파'는 웹판 비교 테스트 때문에 JSON은 그대로 두고 `Events.Unity()`로 FM식에서만 '상선 난파'로 보여 줌.
  - BalanceSim(자동 플레이어, 2시즌): 1부 +78k, 2부 +25k, 3부 +2k, **4부 −4.7k**(자동 플레이어가 모든 도전을 받아 11명 전사, 로스터 붕괴). 앞선 측정에서 4부 반란 게임 오버는 시작 주급이 높았던 탓(급료 밀림 → 반란)이라 FairWage로 고친 뒤 사라짐. 자동 플레이어는 인상 요구에 재계약하도록 고침.
  - **유소년**: 모든 루두스에 학당 1단계 기본(옛 저장도). 우리 루두스는 서리달 1일 **입단 테스트 후보**(학당 단계×2+1명, 잠재력 상한은 단계별 `AcademyPaCap`)를 받고, 28일 안에 골라 **유소년 계약**(흥정 없음, 주급 2~5 D, 정원 단계×3+3). 고르지 않은 후보는 마감에 돌아감(`Youth.DailyTrial`). AI는 예전처럼 자동 입학. 1군 승격 시 정식 계약(바라는 기간, 받을 만한 주급의 60%). **입단 예고**(140일): 학당장이 다음 세대 수준(평범 60%·괜찮음 25%·뛰어남 10%·황금 세대 5%, 잠재력 +0/8/15/20~40)을 알림. 입단 날 후보 명단 팝업(`InfoCards` `youth:trial`, `GameState.ceremonies`). 유소년 화면에 후보 표(`YouthBuilder` Trial, `YouthScreen.trial*`).
  - 이적 제안이 우리 검투사(이제 모두 계약 선수)에게도 들어와 `ContractCounterBlocksUntilAnswered` 테스트 루프에서 제안을 거절하게 고침. EditMode 28/28.
  - 초상화 파츠 추가는 별도 세션 "Generate more portrait parts with Codex"에서 진행 중(오크 끝, 미노타우로스·도마뱀·수인 남음).

### 진행 (2026-11-04 오후) 초상화 7종족 추가분 넣기 · 4부 경제 (주급 요구·AI 주급 예산·아래 부 고정비)
- **초상화**: Codex로 만든 7종족 새 시트(사람 `front_*_4`, 나머지 `front_*_2`)와 점 지도를 `portrait-more.mjs anchors` → `build`로 Unity에 넣음(종족마다 머리 +9). 견본 `node ColosseumUnity/Tools/portrait-more.mjs sample art/tmp/portrait_unity/sample.png`로 확인, 웹판 data/·public/은 안 바뀜. 엘프 새 머리 3번은 턱 점이 없음(목 점 빠짐은 기존 세트도 같음).
- **주급 수준** `Valuation.WageTierMult` 1.35/1.0/0.72/0.52 (몸값 배율 `TierValueMult`와 따로, 3·4부를 더 낮춤). `FairWageAt(g, tier)` = 옮겨 갈 루두스 부 기준.
- **영입 요구**: 무소속 바탕 주급 `Economy.FreeAgentBaseWage(g, tier)` = 우리 부 기준 FairWage(전: 무소속 자기 주급, 2~3부 사이 기준). 이적 `TransferBaseWage(s, g, tier)`도 옮겨 갈 부 기준. `Reputation.DemandFor` FM식 웃돈: 격차 12까지 0, 그 위 1점당 +2.5%(전 +3.3%, 4부는 명성이 늘 격보다 10쯤 낮아 늘 웃돈이 붙었음). 재계약 `RenewDemand`도 격차 12까지 0, 최대 +30%. 측정(`run.sh wages`): 무소속 요구 4부 91→39, 3부 112→66, 2부 111→94, 1부 98→132(1부는 올라감).
- **AI 주급 예산** `Economy.ExpectedWeeklyIncome/WageBudget/Affords`: 기대 수입(부수입 + 주 2경기 몫 + 배당/24, aifin으로 잰 실제 수입에 맞춤)의 85% − 식비(플레이어는 시설·식솔도) + 금고 여유(석 달치 수입 넘는 돈/48, 적자면 빼기). 예산을 넘으면 AI는 포기: 무소속 영입(`FreeAgency.BestFor`), 시즌 끝 재계약(`Day.AiContracts` — 인상 없이 지금 주급으로만 붙잡고, 지금 주급도 버거우면 주전 셋 말고 내보냄), 인상 요구(`Grievances`, 수락 5%), 우리 검투사 이적 제안(`Transfers.Buyers`), 협상 경쟁자(`Negotiations.MaybeRival`). `ReplenishAi`로 새로 뽑힌 검투사 주급도 FairWage. 웹판 규칙이면 `Affords`는 늘 참.
- 측정(`run.sh aifin 2`): 4부 AI 적자 2시즌 뒤 10곳→0곳, 금고 중앙값 6.6천→1만, 주급 합 375→527(전)·290→348(후). 예상 수입 4부 419/실제 443, 3부 734/750.
- **아래 부 고정비** `FmRules.CostTierMult` 3부 ×0.8 · 4부 ×0.6: 시설 유지비(`FacilitiesLogic.WeeklyUpkeep`), 식솔 주급(`StaffLogic.MakeStaff(..., tier)` — 후보 시장은 우리 부, 시작 독토레는 그 루두스 부). 옛 저장의 식솔 주급은 그대로.
- **상단 바**: '급료일에 낼 돈' 한 줄 → `주급 N · 유지비 N D` + `급료일 합계 N D` 두 줄(유지비 = 식비 + 시설 + 식솔, 툴팁은 항목별). 씬 재조립 필요 없음(글자만), 플레이로 줄이 넘치지 않는지 확인 필요.
- BalanceSim: `wages`(부별 명성·격·주급·무소속 요구), `aifin [시즌]`(AI 금고·예산·실제 수입) 모드 추가, `ECON_VERBOSE=1`이면 '기타' 지출을 항목별로.
- **남은 문제**: 플레이어 4부(자동 플레이어, 매 시즌 꼴찌 4승 13패·3승 17패)는 3시즌째 반란. 주당 수입 ~261(상금·관중 + 배당) vs 지출 ~383(주급 212 · 식비 52 · 유지비 39 · 식솔 31 · 사건 40). AI는 4부 부수입 110/주를 받지만 플레이어는 없음 — 후원 수입을 줄지, 자동 플레이어가 너무 약한지(4부 AI 인원 11 vs 우리 7~8) 사용자 결정 대기.
- EditMode 테스트는 Unity를 안 켜서 못 돌림(Core는 dotnet 빌드 통과). 다음에 Unity를 켜면 먼저 테스트 돌릴 것.
- **(이어서) 하부 리그 지원금** (사용자 결정): `FmRules.LowerLeagueGrant` 4부 120 · 3부 70 D/주(세금 전), 플레이어만, 급료일에 다른 돈을 내기 전에 먼저 들어옴(`Economy.Payday`, 장부 'N부 하부 리그 지원금'). `PayBill.grant`(세금 뺀 값)·`Net` 추가 — 상단 바 붉은 표시, 재정 화면 '모자랍니다', 급료일 툴팁(지원금 줄)이 지원금을 셈에 넣음. `ExpectedWeeklyIncome`에도 반영.
  - 측정(`run.sh econ 3`, 자동 플레이어): 4부 매 시즌 꼴찌(4승 13패·3승 17패·5승 11패)여도 3시즌 버팀 — 금고 6,500 → 6,759 → 5,965 → 3,032 (3시즌째 전사 보충 영입으로 주급 187 → 279). 3부 11,000 → 24,340, 2부 19,500 → 37,524, 1부 30,000 → 111,988.
- **(이어서) Windows 빌드 교체**: 열려 있던 에디터에서 MCP로 강제 새로 고침·컴파일 → EditMode 28/28 통과 → `BuildWindows.Build()` 성공(오류·경고 0) → `Builds/ColosseumWindows/` 덮어씀, `Builds/ColosseumWindows.zip` 새로 만듦(172 MB). 12초 실행해 Player.log에 오류 없음. 화면(상단 바 두 줄 등)은 직접 플레이로 아직 안 봄.

### 진행 (2026-11-04 오후) 유소년 입단 시기 · 유소년 계약 협상 (사용자 결정: 매 시즌 중반, 모든 조항, 경쟁 없음)
- **시기**: 우리 루두스 입단 테스트 `Youth.TrialDay` = 85일(불꽃달 1일), 예고 `PreviewDay` = 57일(꽃달 1일). 새 게임 첫날·시즌 시작(`Youth.Intake`)에는 우리 루두스(FM식)는 받지 않고 `Day.AdvanceDay`에서 그날 `Youth.Trial`. 첫 시즌도 85일에 처음. AI는 그대로 시즌 시작 자동 입학. 마감은 그대로 28일(수확달 1일).
- **협상**: kind `youth`가 다른 계약과 같은 길로 감 — `Youth.HaggleTrial` → `Negotiations.Send`(경쟁자 없음) → 하루이틀 뒤 `ReplyYouth` → `Youth.ResolveTrial`(`Contracts.Resolve`, 키 `youth:<id>`). 받아들이면 계약금 내고 `Contracts.Apply` + 조항 `youth`, 역제안이면 결정할 일(`youth`, 다음 날 막음), 결렬이면 아이는 돌아감(세계에서 지움). 마감에 남은 후보의 답 기다림·역제안·흥정도 지움(`Youth.Forget`).
- 바라는 주급 `Youth.TrialDemand`: (3 + max(0, 잠재력−70)/10) × 종족 주급 배율, 보통 3~15 D. 명성 격차는 안 따짐(보장 조항 요구 없음).
- UI: 학당 후보 줄 버튼 '계약 협상'(역제안이 오면 '역제안 보기', 답 기다리면 '답 기다리는 중') → `NegotiationPopup.OpenContract("youth", id)` (모든 조항, 유소년은 주급 ±1 D·바라는 주급 1 D 단위, 정보 줄: 마감·학당 정원·데뷔까지). 입단 카드·학당 빈 화면 문구 바꿈(`YouthBuilder`, 씬 재조립함).
- 테스트 `FmRulesTests.YouthTrialMidSeasonWithNegotiation` 추가, EditMode 29/29. 플레이 확인(9번 시험 칸, 4부 새 게임): 1일 후보 없음 → 85일 입단 카드 → 협상 창 → 제안 → 87일 서명·학당 입학, 남은 후보 2명 표에 남음.
- Windows 빌드 다시 뽑음(11:26, 오류·경고 0), zip 교체(172 MB), 12초 실행 Player.log 오류 없음.

### 진행 (2026-11-04 낮) 새 수염이 얼굴에 붙던 문제
- 증상: 새 세트 수염(사람 beard_full_3·short_5·short_6, 드워프 full_2~4·short_2·3, 오크 full_2·short_2·3, 미노타우로스 short·short_2)이 코·입 위로 올라가거나 콧수염·수염 자국이 사라짐.
- 원인 ①: 새 수염에 점이 없어 자르기 도구가 '가운데 줄 첫 픽셀을 코 밑에' 놓음 — 입·엄니 자리를 비운 수염은 첫 픽셀이 턱수염이라 코까지 올라감. ②: 자르기 도구의 부스러기 지우기(`anchors.cjs` load)가 가장 큰 덩어리에서 떨어진 조각을 지워 염소수염 위 콧수염·수염 자국이 없어지고, 남은 덩어리가 입 줄에서 안 나뉘어 엉뚱한 데 붙음.
- 고침(웹판 tools/는 그대로, `Tools/portrait-more.mjs`만): `anchors`가 새 수염 점으로 같은 칸 첫 세트 머리의 코·입·턱 점을 씀(새 파츠는 front_heads.png 같은 칸 머리에 맞춰 그림). `build`가 `placeNewBeards`로 새 수염을 다시 놓음 — 칸 그림을 그대로(가장자리에 닿은 작은 덩어리만 지움) 쓰고, 그 머리의 입 줄에서 위는 코 점·아래는 입~턱 점에 머리마다 맞춤(splitBeard와 같은 셈). 입 위가 비면 통째로 입~턱(오크 short_2, 미노타우로스 short·short_2, 그때 `_lo` 파일 지움).
- 점검표(`portrait-more.mjs audit <종족>`)로 사람·드워프·오크·미노타우로스 수염, 엘프·도마뱀·수인 머리카락·투구 확인. Windows 빌드·zip 다시 뽑음(11:49).

### 진행 (2026-11-04 저녁) 경력 이적별 분리 · 출전 불만 · 던전 순환과 전투 보기 · 모래 시험 · 수상 링크
- **씬 재빌드 필요** (`GameSceneBuilder.Build()`, 이번 세션은 UnityMCP 연결 실패로 못 함): 내놓은 장비 표 스크롤(`consignedScroll`), 학당 화면에 박힌 안내 글. 재빌드 전에는 내놓은 장비를 앞 3줄만 보여 준다. 나머지 변경은 재빌드 없이 동작하게 짰다.
- 화면 깨짐: 코드로 찾은 원인은 '내놓은 장비' 표에 스크롤이 없어 많이 내놓으면 창고 표를 밀어내던 것. 3줄 높이로 묶고 스크롤. **에디터에서 재현·확인은 못 함.**
- 경력: `Career.SeasonRecord`가 소속이 바뀌면 새 줄을 연다. `Career.Joined(s, g, fee, via)`로 이적료 기록(0 = 자유, via = "학당"). `SeasonRecord.ludusId/fee/via` 추가. 경력 탭 '수익' 칸 → '이적료' (칸 이름은 Earnings 그대로). 수상은 받을 때 뛰던 루두스 줄에.
- 출전 불만: 21일(`BenchGapDays`)을 넘기지 않고 3경기(`BenchClearApps`) 출전해야 풀림. 우리 검투사는 시간으로 안 풀리고(이적하면 풀림), AI는 시간으로도 풀림.
- 던전: 14종, `s.dungeonRotation`에 등급별 하나씩 급료일마다 새로 뽑음(`WeeklyRotation`). 층마다 `Expedition.battles`에 싸우기 전 사본과 대결 시드를 남기고 `DungeonLogic.Replay`로 다시 돌려 `MatchScreen.WatchFight`(링크 `fight:원정:층`)로 중계. 던전 화면·서판 소식(`InboxMessage.replay`)에 링크. 다시 보기 중 '그만 보기'로 나감.
- 유소년 문구: 입단 테스트 → 모래 시험, 유소년 계약 → 학당 서약, 황금 세대 → 전설의 씨앗. 코드 id(`youth`, `youthTrial`)는 그대로.
- 수상: 상 이름 링크(`InfoCards.AwardKindLink`) → 수상자 카드 → '역대 수상자 보기'(`G.OpenHallAwards`) → 비석의 언덕 수상 탭의 그 대회. 대회 카드 끝에 최근 수상자.
- 확인: `sh Tools/TestRunner/run.sh` 32개 통과(새 테스트 3개). 게임 화면은 못 봄 → 재빌드 뒤 창고, 던전 '전투 보기', 수상 링크를 눌러 볼 것.
- (같은 날) MCP로 `Colosseum/Build Game Scene` 재빌드 완료. 씬에 `consignedScroll` 연결, 새 문구 확인, 플레이 모드 진입 시 콘솔 오류 없음. 저장 칸을 건드리지 않으려고 게임 안을 눌러 보지는 않았다 → 창고, 던전 '전투 보기', 수상 링크는 직접 눌러 볼 것.
