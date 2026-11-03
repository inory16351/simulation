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
