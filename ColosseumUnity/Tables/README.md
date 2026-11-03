# 게임 테이블

게임 데이터를 한 형식의 CSV 테이블로 모은 곳입니다.

- **기술 테이블** (`Enum`, `Skill`, `Effect`, `SkillLearn`, `Status`, `SkillConst`): 새로 설계한 원본입니다. 손으로 고칩니다.
- **나머지 테이블**: 지금 게임이 읽는 `Assets/Resources/Data/*.json`과 C# 코드 안의 상수 표에서 뽑았습니다.
  `node ColosseumUnity/Tools/export-tables.mjs`로 다시 뽑고, 열거형은 `EnumGame.csv`에 모입니다.
- **아직 게임은 CSV를 읽지 않습니다.** 런타임은 JSON·코드 상수 그대로입니다. CSV를 원본으로 바꾸려면
  CSV → 런타임 데이터 변환(로더)을 붙이고 export 방향을 뒤집어야 합니다.

## 전체 목록

| 분야 | 테이블 |
|---|---|
| 열거형 | `Enum` (기술·공용), `EnumGame` (나머지, 자동 생성) |
| 기술 | `Skill`, `Effect`, `SkillLearn`, `Status`, `SkillConst` |
| 장비 | `Weapon`, `Armor`, `WeaponClassData` (동작 상성·지대 상성), `Unique`, `UniqueEffect` |
| 검투사 | `RaceData`, `RaceStat`, `Trait`, `TraitStat`, `Style`, `StyleStat`, `StyleProf`, `StyleCounter`, `StyleSkill`, `Name` |
| 부상 | `Injury`, `InjuryPenalty`, `InjuryPermanent` |
| 전술 | `Stance`, `AimPart`, `Zone`, `TeamRole`, `TeamPolicy`, `Formation` (던전 진형) |
| 세계 | `Nation`, `NationRule`, `NationRace`, `NationStyle`, `NationRelation`, `NationRuleText`, `Month` |
| 경기·경영 | `MatchRule`, `Balance`, `Facility`, `FacilityLevel`, `StaffRole`, `TrainingFocus`, `TrainingIntensity` |
| 사건 | `Event`, `EventChoice`, `EventEffect` (value_type 사용) |
| 원정·전쟁 | `Dungeon`, `DungeonFoe`, `WarContract` |

다른 테이블을 가리키는 string 열(예: `Style.main` → `Weapon.key`)은 검사 도구가 참조를 확인합니다.

---

# 기술

기술은 특성처럼 **조건을 채우면 익히고**, 전투에서는 **조건이 맞을 때 확률·쿨에 따라 저절로 발동**합니다.
플레이어는 기술을 직접 누르지 않습니다. 교범(우선순위·조건 명령)으로 성향만 정합니다.

## 형식

- CSV (UTF-8), 쉼표 구분. 쉼표·따옴표가 든 값은 `"..."`로 감싸고(`""` = 따옴표), 줄바꿈은 `\n` 글자로 씁니다.
- 1줄 열 이름, 2줄 자료형, 3줄 설명, 4줄부터 데이터입니다.
- 자료형은 `int`, `float`, `string`, `enum:이름`만 씁니다. 열거형 값은 `Enum.csv`에 있습니다.
- 검사: `node ColosseumUnity/Tools/check-tables.mjs` (자료형, 열거형 값, 묶음 참조, 아이콘 파일).

## 테이블

| 파일 | 한 줄 | 키 |
|---|---|---|
| `Enum.csv` | 열거형 값 하나 | enum + key |
| `Skill.csv` | 기술 하나 | id, key |
| `Effect.csv` | 효과 하나 (기술·상태 공용) | group + seq |
| `SkillLearn.csv` | 습득 조건 하나 | group + seq |
| `Status.csv` | 상태 이상 하나 | id, key |
| `SkillConst.csv` | 규칙 상수 하나 | key |

```
Skill.effect_group ──> Effect.group (여러 줄, seq 순서대로)
Skill.learn_group  ──> SkillLearn.group (모두 충족해야 함)
Effect.status      ──> Status.key
Status.effect_group ─> Effect.group (걸려 있는 동안 적용)
```

id 대역: 1000 공격, 2000 제압, 3000 강화, 4000 이동, 5000 쇼·단체, 6000 주술, 7000 종족, 8000 상시, 9000 반응.
상태 효과 묶음은 90000 + 상태 id입니다.

## 습득 (특성처럼)

1. 급료일(7일)마다, 아직 없는 기술 중 `SkillLearn` 조건을 모두 채운 것에서 **하나만** 골라 한 번 굴립니다 (후보가 많아도 빨라지지 않게).
2. 확률 = `learn_rate` × `LearnTierPenalty`^(등급−1) + 독토레 평점/5 × `LearnTrainerBonus`. (`LearnMatchBonus`는 아직 안 씀)
3. 종족 기술은 `learn_rate` 1이라 태어날 때 바로 갖습니다.
4. 칸이 차면 등급, 그다음 우선순위가 높은 것을 남깁니다 (능동 4, 반응 2, 상시 3).

## 발동 (확률·쿨·조건)

- **Active**: 내 차례마다 우선순위가 높은 기술부터 봅니다. 쿨타임이 끝났고, 기력·마나가 있고, `trigger`·`require`가 맞으면
  `proc_rate + 능력치(proc_stat) × proc_per_stat + 교범 숙련도 × FamiliarityProcBonus` (ProcMin~ProcMax)로 굴립니다.
  하나가 발동하면 그 차례는 끝입니다. 아무것도 안 되면 기본 공격입니다.
- **Reaction**: 사건(`OnParry`, `OnBlock`, `OnDodge`, `OnDamaged`, `OnAllyDown` …)이 일어날 때 같은 식으로 굴립니다. 사건당 하나.
- **Passive**: `Always`면 늘 적용, 다른 trigger면 그 조건일 때만 적용합니다. 굴리지 않습니다.
- 교범의 기술 순서·조건 명령("HP 30% 이하 → 기술 최우선")은 우선순위를 덮어씁니다.

## 효과의 value_type

| value_type | 뜻 | 예 |
|---|---|---|
| `Flat` | 그대로 더함 | Crowd Flat 20 = 관중 +20 |
| `Multiplier` | 기준값의 배율 | Damage Multiplier 1.5 = 무기 피해 ×1.5 |
| `Additive` | 확률·비율 가산 | HitMod Additive -0.1 = 명중 −10%p |
| `PercentMax` | 최대치의 비율 | RestoreSta PercentMax 0.3 = 최대 기력 30% 회복 |
| `PercentCurrent` | 현재치의 비율 | |
| `PerStat` | 능력치 1당 | MagicDamage PerStat 1 (stat Mag) = 마력 × 1 |
| `Range` | value~value_max 사이 무작위 (사건) | Gold Range 800~2000 |

`duration` 0은 즉시 또는 이번 공격에만, 1 이상은 그 라운드 동안 유지합니다. `chance`는 그 효과 한 줄의 적용 확률입니다.

## 아이콘

`Resources/Art/icon/skills/<key>.png`(128), `icon/status/<key>.png`(64). 같은 화풍(flat_ref)으로
`ColosseumUnity/Tools/item-icons.mjs`의 skills·status 묶음이 그립니다.
