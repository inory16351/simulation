# TS → C# 이식 규칙 (게임 로직)

웹판 `src/core/*.ts`를 `ColosseumUnity/Assets/Scripts/Core/` C#으로 옮긴다. **결과가 웹판과 비트 단위로 같아야 한다.**
골든 테스트(`Assets/Tests/EditMode/*GoldenTests.cs`)가 같은 시드로 170일을 진행한 상태 JSON 전체를 웹판과 비교한다.

## 이미 옮긴 것 (참고해서 같은 스타일로)
- `Rng.cs` (rng.ts), `Josa.cs`, `Data.cs` (data.ts, `Data.*`, `Bal.*`, `JsMath`), `Types.cs` + `StateTypes.cs` (types.ts와 모듈 상태 타입 전부)
- `GladiatorRules.cs`: gladiator.ts의 `effectiveAttr/computeCA/stars/ageStage/canFight`, condition.ts의 `performance`, injuries.ts의 `areaOf/makeInjury`, tactics.ts의 `defaultDoctrine`
- `Avatar.cs` (makeAvatar), `Generators/GladiatorGen.cs` (generators/gladiator.ts, `ComputeWage` 포함)
- `Combat/Exchange.cs`, `Combat/Sim.cs` (`Sim.Run(a, b, MatchOptions)` = simulateMatch), `Combat/Commentary.cs` (`Commentary.Build`)

## 이름 규칙
- TS 파일 하나 = `public static class` 하나, 파일 `Assets/Scripts/Core/Logic/<클래스>.cs`, 네임스페이스 `Colosseum.Core`.
- 클래스 이름 = 파일 이름 PascalCase. 타입과 겹치면 `Logic`을 붙인다:
  calendar→`Calendar`, career→`Career`, state→`StateUtil`, modifiers→`Modifiers`, nationRules→`NationRules`, reputation→`Reputation`,
  condition→`Condition`(performance는 GladiatorRules에 있음), injuries→`Injuries`, gladiator→`GladiatorRules`에 추가, tactics→`Tactics`,
  staff→`StaffLogic`, facilities→`FacilitiesLogic`, economy→`Economy`, training→`Training`, generators/world→`World`, gear→`GearLogic`,
  match→`MatchLogic`, challenge→`ChallengeLogic`, series→`SeriesLogic`, cup→`CupLogic`, youth→`Youth`,
  day→`Day`, events→`Events`, transfers→`Transfers`, scouting→`Scouting`, war→`WarLogic`, dungeon→`DungeonLogic`.
- 함수 이름 = TS 이름 PascalCase (`advanceDay` → `AdvanceDay`). 내부(비공개) 함수도 같은 규칙, `public static`으로 둬도 된다.
- 상수 = PascalCase (`CUP` → `Cup`가 타입과 겹치면 `CupConst`). 모듈 안 타입(MatchSummary, PlayedMatch, Series, EventDef …)은 그 파일에 `public class`로, **필드는 TS와 같은 소문자 이름**.
- 다른 모듈 함수는 위 규칙대로 부른다 (아직 없어도 규칙대로 쓰면 된다). 시그니처는 그 TS 파일을 보고 맞춘다.
- 선택 인자는 C# 기본값으로 (`stake = 0`). 유니온 반환(`Challenge | string`)은 `object`를 쓰지 말고 TS가 실제로 쓰는 쪽에 맞춰 클래스 하나 + `string error` 필드, 또는 `out string error`. 호출부가 하는 일을 보고 정한다.

## 결과가 같게 나오도록 지킬 것
- 난수: `Rng` 호출 순서·횟수를 TS와 똑같이. 단락 평가(`a && rng.chance()`)도 그대로.
- 숫자: 계산은 `double`. 정수 나눗셈 금지 (`x / 2` → `x / 2.0`). `Math.round` → `JsMath.Round`. `Math.floor/ceil/min/max/abs/pow/sqrt` → `Math.*`.
  덧셈 묶음 순서도 TS 그대로 (`a + (b + c)`와 `a + b + c`는 다를 수 있다).
- 정렬: JS `sort`는 안정 정렬 → `OrderBy/OrderByDescending/ThenBy` (List.Sort 금지). 비교 함수가 `b - a`면 OrderByDescending.
  문자열 `localeCompare` → `string.CompareOrdinal`은 다르다. TS가 쓰면 그 자리에 주석을 남기고 `StringComparer.Ordinal`로.
- 순서: `Object.values/keys/entries`는 넣은 순서 → `Dictionary` 순회 순서와 같다 (지우고 다시 넣으면 달라지므로 `delete` 후 재삽입이 있으면 주의).
- `Partial<Record<...>>` 가중치를 `rng.weighted`에 넘길 때는 넣은 순서가 유지되는 `Dictionary` 또는 `List<KeyValuePair<string,double>>`.
- 선택 필드(`?`)는 `null` 가능 타입. 웹판이 `x ??= []`로 만들면 C#도 그 시점에 만든다 (상태 JSON 비교에서 빈 배열 유무는 무시된다).
- `BALANCE.x.y` → `Bal.Combat["y"]` 같은 JObject 접근 (`(double)Bal.Economy["foodPerWeek"]`). 필요하면 `Bal`에 JObject 필드를 더한다 (Data.cs는 events 담당만 수정).
- 한국어 문자열(메시지, 일대기)은 **글자 하나 다르지 않게** 복사한다. 조사는 `Josa.J(word, "이/가")`.
- 숫자를 문자열로 넣을 때 JS와 같게: 정수는 `"12"`, `toLocaleString()`은 `N0`(천 단위 콤마), `toFixed(n)`은 `ToString("F" + n, CultureInfo.InvariantCulture)`.

## 하지 말 것
- UnityEngine API를 로직에 쓰지 않는다 (Data.Ensure 외). 순수 C#.
- 맡은 파일 밖은 고치지 않는다. 다른 모듈에 필요한 게 있으면 보고에 적는다.
- Unity를 실행하거나 MCP로 컴파일하지 않는다 (여러 명이 동시에 쓰는 중이라 반쯤 된 코드가 서로 깨진다). 통합 컴파일은 메인 세션이 한다.
