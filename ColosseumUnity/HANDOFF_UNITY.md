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
