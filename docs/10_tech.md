# 10. 기술 구조

## 목표

1. 웹에서 빠르게 만들어 **핵심 루프의 재미와 밸런스를 검증**한다.
2. 나중에 Unity로 옮길 때 **게임 로직과 데이터를 최대한 그대로 가져간다**.

이를 위해 **시뮬레이션 코어 / 데이터 / 화면**을 처음부터 분리한다.

## 웹 스택

| 영역 | 선택 | 이유 |
|---|---|---|
| 언어 | TypeScript | 타입이 C#과 대응이 쉬움 |
| 빌드 | Vite | 빠른 개발 서버 |
| 관리 화면 | React | 표·목록·폼 위주의 FM식 화면 |
| 하이라이트 컷 | PixiJS (+ spine-pixi는 리깅 도입 시) | 패럴랙스 레이어, 파티클 |
| 상태 관리 | 코어의 `GameState` 하나 + 얇은 React 바인딩 | 로직이 UI 프레임워크에 묶이지 않게 |
| 저장 | IndexedDB (JSON 직렬화, 버전 필드) | 브라우저 로컬 저장 |
| 테스트 | Vitest | 코어 단위 테스트 + 시즌 일괄 시뮬레이션 |

## 폴더 구조 (안)

```
/data                  ← 게임 데이터 (JSON). Unity도 그대로 사용
  races.json
  nations.json
  styles.json
  weapons.json
  armors.json
  skills.json
  traits.json
  facilities.json
  balance.json         ← 공식 계수, 상금, 주급 범위 등 튜닝 값 전부
/src
  /core                ← 순수 로직. DOM·React·Pixi 의존 금지
    rng.ts             ← 시드 고정 난수 (mulberry32 등, C#에서 동일 구현)
    model/             ← GameState, Gladiator, Ludus, Staff, Nation ...
    systems/
      calendar.ts      ← 주 진행, 단계 순서
      challenge.ts     ← 도전 신청·응답·거절 페널티
      combat/          ← 전투 시뮬 (이벤트 로그 출력)
      league.ts        ← RP, 순위, 승강
      cup.ts           ← 판테온 컵
      economy.ts       ← 배당금, 급료, 미납, 대출, 파산
      morale.ts        ← 사기, 반란, 탈주
      training.ts      ← 훈련, 성장, 스킬 습득
      market.ts        ← 노예 시장, 자유민, 이적, 대여
      scouting.ts      ← 정보의 안개, 스카우트 보고서
      youth.ts
      war.ts           ← 국가 관계, 전쟁, 전황
      mercenary.ts
    ai/                ← AI 루두스 의사결정 (도전, 이적, 훈련)
    generators/        ← 검투사·직원·루두스·이름 생성
  /ui                  ← React 화면
  /cutscene            ← PixiJS 하이라이트 컷
/tools
  sim-seasons.ts       ← UI 없이 N시즌 돌려 통계 출력 (밸런스 검증)
/assets                ← 아트 (명명 규칙은 09 아트 디렉션)
```

## 핵심 설계 원칙

### 1. 코어는 이벤트 로그를 출력한다

전투와 주간 진행은 결과만 바꾸지 않고 **이벤트 목록**을 반환한다.

```ts
type CombatEvent =
  | { t: "move"; turn: number; actor: Id; to: Range }
  | { t: "attack"; turn: number; actor: Id; target: Id; skill?: SkillId;
      hit: boolean; blocked: boolean; crit: boolean; damage: number }
  | { t: "status"; turn: number; target: Id; status: StatusId; turns: number }
  | { t: "crowd"; turn: number; delta: number; value: number }
  | { t: "ko"; turn: number; target: Id }
  | { t: "missio"; target: Id; deathChance: number; died: boolean }
  | { t: "highlight"; kind: HighlightKind; actors: Id[] };
```

- 웹 UI는 이 로그로 텍스트 중계를 만들고 `highlight` 이벤트에서 컷을 띄운다.
- Unity도 같은 로그 구조를 쓰면 연출만 다시 만들면 된다.

### 2. 결정론적 시뮬레이션

- 모든 무작위는 `core/rng.ts`의 시드 RNG 하나로만 뽑는다 (`Math.random` 금지).
- 같은 시드와 같은 입력이면 같은 결과가 나온다. 버그 재현과 밸런스 테스트가 쉬워진다.
- Unity 이식 시 **같은 시드로 웹과 C#이 같은 시즌 결과를 내는지** 비교하는 골든 테스트를 만든다.

### 3. 수치는 전부 데이터로

- 공식의 계수(Elo K값, 사망 확률, 주급 범위, 시설 비용 등)는 `balance.json`에 둔다.
- 기획 문서의 "임시값"은 코드에 하드코딩하지 않는다.

### 4. C#으로 옮기기 쉬운 TypeScript

- 클래스 대신 **평범한 데이터 객체 + 시스템 함수** 구조 (`train(state, rng)` 형태).
- union 타입은 C#의 enum + 클래스 계층으로 옮길 수 있는 수준으로만 쓴다.
- ID는 문자열 상수 (`"orc"`, `"murmillo"`, `"skill.power_strike"`)로 통일한다.

## 데이터 스키마 예시

```ts
interface Gladiator {
  id: Id;
  name: string;
  nickname?: string;
  race: RaceId;
  nation: NationId;
  age: number;
  status: "slave" | "free" | "freedman";
  style: StyleId;
  subStyle?: StyleId;
  attrs: Record<AttrId, number>;          // 1~20, 신체·기술·정신
  weaponProf: Record<WeaponClassId, number>;
  hidden: { pa: number; consistency: number; bigStage: number;
            injuryProne: number; loyalty: number; ambition: number; greed: number };
  traits: TraitId[];                       // 최대 5
  skills: { active: SkillId[]; passive: SkillId[]; learning?: { id: SkillId; progress: number } };
  equipment: { main?: ItemId; off?: ItemId; armor?: ItemId; helmet?: ItemId };
  wage: number;                            // 주급
  unpaidWeeks: number;
  contract?: { untilSeason: number; clauses: ContractClause[] };
  morale: number; popularity: number; fatigue: number;
  injury?: { severity: "light" | "serious"; weeksLeft: number };
  record: { wins: number; losses: number; kills: number };
  alive: boolean;
  portraitParts: Record<PortraitLayer, string>;
}

interface Ludus {
  id: Id;
  name: string;
  nation: NationId;
  tier: 1 | 2;
  rp: number;
  treasury: number;
  facilities: Record<FacilityId, number>;  // 레벨
  gladiators: Id[];
  staff: Id[];
  youth: Id[];
  aiPersonality?: "aggressive" | "stable" | "grudge" | "merchant" | "developer";
  reputation: { fame: number; coward: number; traitor: number };
  nationFavor: Record<NationId, number>;   // -100 ~ 100
  refusalsUsed: number;
  loans: Loan[];
}
```

## 일일 처리 순서

```
1. 아침        : 랜덤 이벤트·전쟁 판정, 시장 갱신(해당 날짜)
2. 급료일이면   : 우선순위대로 지급, 미납 누적, 사기 반영
3. 플레이어 입력 : 도전 응답·신청, 훈련 배정, 교범 수정, 거래, 파견
4. AI 입력      : AI 루두스 도전·응답·거래
5. 경기일이면   : 오늘 잡힌 경기 시뮬레이션 → 결과 반영 (RP, 상금, 부상, 사망)
6. 훈련        : 하루치 성장, 스킬·교범 수련, 피로
7. 부상·회복    : 하루치 회복
8. 파견        : 용병 판정(7일마다), 전황 갱신
9. 사기 정산    : 하루치 사기 변동
10. 반란·탈주   : 급료일 다음 날에만 판정
11. 하루 끝     : 파산 체크(급료일 기준), 다음 날로
```

[다음 날로]는 하루, [일정까지 진행]은 플레이어 결정이 필요한 일(경기, 선택지 이벤트, 도전 도착, 급료 부족)이 생길 때까지 여러 날을 진행한다.

## MVP 범위와 로드맵

| 단계 | 내용 | 완료 기준 |
|---|---|---|
| **M0 골격** | 프로젝트 세팅, 데이터 JSON 초안, RNG, 모델, 생성기 | 무작위 검투사 100명 생성·출력 |
| **M1 전투** | 1:1 전투 시뮬 + 텍스트 중계, 스킬 일부(10개) | 콘솔에서 경기 1회 중계 확인 |
| **M2 운영 루프** | 주 진행, 급료·미납·사기·반란, 노예 시장, 훈련 | 1개 루두스로 24주 굴리기 |
| **M3 리그** | 1개 국가 20팀, RP, 도전 시스템, AI 루두스, 승강전 | 10시즌 자동 시뮬에서 승강이 정상 작동 |
| **M4 정보와 사람** | 직원, 시설, 스카우팅 안개, 이적·자유민·대여 | 타 루두스 검투사가 범위로 보임 |
| **M5 세계** | 6개 국가(타국은 추상 시뮬), 국가 계수, 판테온 컵, 유소년, 전쟁·용병 | 전체 시즌 캘린더 동작 |
| **M6 아트** | 초상화 파츠 합성, 패럴랙스 하이라이트 컷 | 경기 중 컷 표시 |
| **M7 다듬기** | 밸런스, 세이브/로드, 튜토리얼 | 외부 플레이테스트 |
| **Unity 이식** | 코어 C# 포팅 → 골든 테스트 → UI·연출 재구현 | 같은 시드 결과 일치 |

### MVP에서 타국 처리

- M3~M4에서는 국가 1개만 실제로 시뮬레이션한다.
- 나머지 5개국 루두스는 **팀 전력 수치만 가진 추상 팀**으로 두고, 판테온 컵과 국가 계수 계산에만 쓴다.
- M5 이후 성능이 허락하면 전체 120팀을 실제 시뮬레이션한다 (주당 경기 수 수백 건 수준이라 브라우저에서도 충분히 가능할 것으로 예상, 측정 필요).

## 밸런스 검증 도구

`tools/sim-seasons.ts`로 UI 없이 수십 시즌을 돌려 다음을 확인한다.

- 시즌당 평균 사망자 수 (너무 많으면 로스터가 붕괴, 너무 적으면 긴장감 없음)
- 승강 팀 분포 (같은 팀이 계속 오르내리지 않는가)
- AI 루두스 파산율, 반란 발생률
- 스타일·종족별 승률 (특정 조합이 지배하지 않는가)
- RP 분포와 시즌 회귀 효과
