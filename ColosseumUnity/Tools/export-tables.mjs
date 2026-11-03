// 게임 데이터(Resources/Data/*.json + C# 코드 안의 상수 표)를 Tables/*.csv로 뽑는다. 기술 테이블(Skill·Effect·SkillLearn·Status·SkillConst·Enum)은 손으로 쓴 원본이라 건드리지 않는다.
// 형식: 1줄 열 이름, 2줄 자료형(int·float·string·enum:이름), 3줄 설명, 4줄부터 데이터. 쉼표·따옴표가 든 값은 "..."로 감싸고 줄바꿈은 \n 글자로 쓴다.
// 사용 (저장소 루트에서): node ColosseumUnity/Tools/export-tables.mjs
import { readFileSync, writeFileSync } from "node:fs";

const DATA = "ColosseumUnity/Assets/Resources/Data";
const OUT = "ColosseumUnity/Tables";
const J = (n) => JSON.parse(readFileSync(`${DATA}/${n}.json`, "utf8"));

const pascal = (s) => String(s).split(/[_\s.-]/).filter(Boolean).map((w) => w[0].toUpperCase() + w.slice(1)).join("");
const cell = (v) => {
  if (v === undefined || v === null) return "";
  if (typeof v === "boolean") return v ? "1" : "0";
  const s = String(v).replace(/\r?\n/g, "\\n");
  return /[",]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
};

// 생성한 열거형 (EnumGame.csv)
const enums = new Map();
function en(name, key, label = "") {
  const k = pascal(key);
  const m = enums.get(name) ?? enums.set(name, new Map()).get(name);
  if (!m.has(k)) m.set(k, label);
  return k;
}

/** cols: [이름, 자료형, 설명][], rows: 값 배열의 배열 */
function table(name, cols, rows) {
  const lines = [cols.map((c) => c[0]), cols.map((c) => c[1]), cols.map((c) => c[2])].map((l) => l.map(cell).join(","));
  for (const r of rows) lines.push(r.map(cell).join(","));
  writeFileSync(`${OUT}/${name}.csv`, lines.join("\n") + "\n");
  console.log(`${name}.csv ${rows.length}줄`);
}
const id = (i, base = 1) => base + i;

// ── 장비 ──
const items = J("items");
table("Weapon", [
  ["id", "int", "고유 번호"], ["key", "string", "코드 키"], ["name", "string", "이름"], ["class", "enum:WeaponClass", "무기 계열"],
  ["damage", "int", "피해"], ["speed", "int", "속도"], ["pierce", "float", "방어 관통"], ["range", "int", "사거리"], ["req_str", "int", "필요 근력"],
  ["crit", "float", "치명타율"], ["two_handed", "int", "양손 (1)"], ["offhand_only", "int", "보조 손 전용 (1)"], ["ammo", "int", "투척물 수"],
  ["block_bypass", "float", "방패 우회"], ["stun", "float", "기절 확률"], ["bound_bonus", "float", "속박 상대 추가 피해"], ["magic_bonus", "float", "주술 강화"],
], items.weapons.map((w, i) => [id(i, 101), w.id, w.name, pascal(w.class), w.damage, w.speed, w.pierce, w.range, w.reqStr, w.crit, w.twoHanded ? 1 : 0, w.offhandOnly ? 1 : 0, w.ammo ?? 0, w.blockBypass ?? 0, w.stun ?? 0, w.boundBonus ?? 0, w.magicBonus ?? 0]));
table("Armor", [
  ["id", "int", "고유 번호"], ["key", "string", "코드 키"], ["name", "string", "이름"], ["slot", "enum:ItemSlot", "칸"], ["weight", "enum:ArmorWeight", "무게"],
  ["defense", "int", "방어"], ["agi_penalty", "float", "민첩 감소"], ["sta_cost", "float", "기력 소모 비율"], ["crowd", "float", "관중 호감 비율"], ["acc_penalty", "int", "정확도 감소"],
  ["block", "float", "막기 (방패)"], ["net_resist", "float", "그물 저항"], ["show_bonus", "int", "쇼맨십 보너스"],
], items.armors.map((a, i) => [id(i, 201), a.id, a.name, en("ItemSlot", a.slot), en("ArmorWeight", a.weight ?? "none"), a.defense ?? 0, a.agiPenalty ?? 0, a.staCost ?? 0, a.crowd ?? 0, a.accPenalty ?? 0, a.block ?? 0, a.netResist ?? 0, a.showBonus ?? 0]));

// ── 고유 장비 ──
const uniques = J("uniques").uniques;
table("Unique", [
  ["id", "int", "고유 번호"], ["key", "string", "코드 키"], ["base", "string", "바탕 장비 key (Weapon·Armor)"], ["grade", "int", "등급 2 명품 3 전설"], ["name", "string", "이름"], ["lore", "string", "사연 (전설)"],
], uniques.map((u, i) => [id(i, 1001), u.id, u.base, u.grade, u.name, u.lore ?? ""]));
const FLAT_FX = new Set(["crowd", "first", "aura", "def"]);
table("UniqueEffect", [
  ["unique", "string", "Unique.key"], ["seq", "int", "순서"], ["effect", "enum:UniqueEffect", "효과"], ["value_type", "enum:ValueType", "값 종류"], ["value", "float", "값"],
], uniques.flatMap((u) => u.effects.map((f, k) => [u.id, k + 1, en("UniqueEffect", f.t), FLAT_FX.has(f.t) ? "Flat" : "Additive", f.v])));

// ── 특성 ──
const traits = J("traits");
table("Trait", [
  ["id", "int", "고유 번호"], ["key", "string", "코드 키"], ["name", "string", "이름"], ["category", "enum:TraitCategory", "분류"], ["desc", "string", "설명"],
  ["spawn", "float", "생성 가중치"], ["negative", "int", "나쁜 특성 (1)"], ["hidden", "int", "숨은 특성 (1)"],
], traits.map((t, i) => [id(i, 1), t.id, t.name, en("TraitCategory", t.cat), t.desc, t.spawn, t.negative ? 1 : 0, t.hidden ? 1 : 0]));
table("TraitStat", [["trait", "string", "Trait.key"], ["stat", "enum:Stat", "능력치"], ["value", "int", "증감"]],
  traits.flatMap((t) => Object.entries(t.attrMods ?? {}).map(([k, v]) => [t.id, pascal(k), v])));

// ── 스타일 ──
const styles = J("styles");
table("Style", [
  ["id", "int", "고유 번호"], ["key", "string", "코드 키"], ["name", "string", "이름"], ["desc", "string", "설명"],
  ["main", "string", "기본 주무기 (Weapon.key)"], ["off", "string", "기본 보조 (Weapon·Armor key)"], ["armor", "string", "기본 갑옷"], ["helmet", "string", "기본 투구"],
  ["preferred_range", "int", "선호 거리"], ["aggression", "float", "공격성"],
], styles.map((s, i) => [id(i, 1), s.id, s.name, s.desc, s.loadout.main ?? "", s.loadout.off ?? "", s.loadout.armor ?? "", s.loadout.helmet ?? "", s.preferredRange, s.aggression]));
table("StyleStat", [["style", "string", "Style.key"], ["stat", "enum:Stat", "능력치"], ["weight", "float", "생성 가중치"]],
  styles.flatMap((s) => Object.entries(s.weights).map(([k, v]) => [s.id, pascal(k), v])));
table("StyleProf", [["style", "string", "Style.key"], ["seq", "int", "순서"], ["weapon", "enum:WeaponClass", "주 숙련 계열"]],
  styles.flatMap((s) => s.profs.map((p, k) => [s.id, k + 1, pascal(p)])));
table("StyleCounter", [["style", "string", "Style.key"], ["counter", "string", "상성상 이기는 Style.key"]],
  styles.flatMap((s) => s.counters.map((c) => [s.id, c])));
table("StyleSkill", [["style", "string", "Style.key"], ["seq", "int", "순서"], ["skill", "string", "시작 기술 (Skill.key)"]],
  styles.flatMap((s) => s.skills.map((k, n) => [s.id, n + 1, k.replace(/^skill\./, "")])));

// ── 종족 ──
const races = J("races");
table("RaceData", [
  ["race", "enum:Race", "종족"], ["name", "string", "이름"], ["color", "string", "색"], ["age_debut", "int", "데뷔 나이"], ["age_growth_end", "int", "성장기 끝"],
  ["age_prime_end", "int", "전성기 끝"], ["age_retire", "int", "은퇴 나이"], ["growth_mult", "float", "성장 배율"], ["wage_mult", "float", "급료 배율"], ["upkeep_mult", "float", "유지비 배율"],
  ["trait_name", "string", "종족 특성 이름"], ["trait_desc", "string", "종족 특성 설명"], ["race_skill", "string", "종족 기술 (Skill.key)"],
], races.map((r) => [pascal(r.id), r.name, r.color, r.age.debut, r.age.growthEnd, r.age.primeEnd, r.age.retire, r.growthMult, r.wageMult, r.upkeepMult, r.racialTrait?.name ?? "", r.racialTrait?.desc ?? "", (r.raceSkill ?? "").replace(/^skill\./, "")]));
table("RaceStat", [["race", "enum:Race", "종족"], ["stat", "enum:Stat", "능력치"], ["value", "int", "증감"]],
  races.flatMap((r) => Object.entries(r.attrMods ?? {}).map(([k, v]) => [pascal(r.id), pascal(k), v])));

// ── 나라 ──
const nations = J("nations");
table("Nation", [
  ["id", "int", "고유 번호"], ["key", "string", "코드 키"], ["name", "string", "이름"], ["short", "string", "짧은 이름"], ["motif", "string", "모티브"], ["color", "string", "색"],
  ["capital", "string", "수도"], ["arena", "string", "대표 경기장"], ["league", "string", "리그 이름"], ["culture", "string", "문화 한 줄"], ["lore", "string", "이야기"],
], nations.map((n, i) => [id(i, 1), n.id, n.name, n.short, n.motif, n.color, n.capital, n.arena, n.league, n.culture, n.lore]));
// 나라 규칙은 나라마다 항목이 달라 한 줄에 하나씩 (참·거짓은 1)
table("NationRule", [["nation", "string", "Nation.key"], ["rule", "enum:NationRule", "규칙"], ["value", "float", "값 (배율·비율·금액, 참이면 1)"]],
  nations.flatMap((n) => Object.entries(n.rules).map(([k, v]) => [n.id, en("NationRule", k), typeof v === "boolean" ? (v ? 1 : 0) : v])));
table("NationRace", [["nation", "string", "Nation.key"], ["race", "enum:Race", "종족"], ["weight", "float", "출신 가중치"]],
  nations.flatMap((n) => Object.entries(n.races).map(([k, v]) => [n.id, pascal(k), v])));
table("NationStyle", [["nation", "string", "Nation.key"], ["seq", "int", "순서"], ["style", "string", "흔한 Style.key"]],
  nations.flatMap((n) => n.styles.map((s, k) => [n.id, k + 1, s])));
table("NationRelation", [["nation", "string", "Nation.key"], ["other", "string", "상대 Nation.key"], ["value", "int", "시작 관계 (-100~100)"]],
  nations.flatMap((n) => Object.entries(n.relations ?? {}).map(([k, v]) => [n.id, k, v])));
table("NationRuleText", [["nation", "string", "Nation.key"], ["seq", "int", "순서"], ["text", "string", "규칙 설명 한 줄"]],
  nations.flatMap((n) => n.ruleText.map((t, k) => [n.id, k + 1, t])));

// ── 부상 ──
const injuries = J("injuries");
table("Injury", [
  ["id", "int", "고유 번호"], ["key", "string", "코드 키"], ["name", "string", "이름"], ["part", "enum:BodyPart", "부위"], ["tier", "enum:InjuryTier", "정도"],
  ["days_min", "int", "회복 최소 (일)"], ["days_max", "int", "회복 최대 (일)"], ["permanent", "float", "후유증 확률"],
], injuries.map((x, i) => [id(i, 1), x.id, x.name, en("BodyPart", x.part), en("InjuryTier", x.tier), x.days[0], x.days[1], x.permanent]));
table("InjuryPenalty", [["injury", "string", "Injury.key"], ["stat", "enum:Stat", "능력치"], ["value", "int", "회복 중 증감"]],
  injuries.flatMap((x) => Object.entries(x.penalty ?? {}).map(([k, v]) => [x.id, pascal(k), v])));
table("InjuryPermanent", [["injury", "string", "Injury.key"], ["kind", "enum:PermanentKind", "후유증 종류"], ["key", "string", "Trait.key 또는 Stat"], ["value", "int", "증감 (Stat)"]],
  injuries.flatMap((x) => !x.perm ? [] : [
    ...(x.perm.trait ? [[x.id, en("PermanentKind", "trait"), x.perm.trait, 0]] : []),
    ...Object.entries(x.perm.attrs ?? {}).map(([k, v]) => [x.id, en("PermanentKind", "stat"), pascal(k), v]),
  ]));

// ── 사건 ──
const events = J("events");
table("Event", [
  ["id", "int", "고유 번호"], ["key", "string", "코드 키"], ["scope", "enum:EventScope", "범위"], ["grade", "enum:EventGrade", "성격"], ["weight", "float", "가중치"],
  ["condition", "enum:EventCondition", "추가 조건"], ["title", "string", "제목"], ["text", "string", "본문"],
], events.map((e, i) => [id(i, 1), e.id.replace(/^event\./, ""), en("EventScope", e.scope), en("EventGrade", e.grade), e.weight, en("EventCondition", e.condition ?? "none"), e.title, e.text]));
let group = 0;
const choiceRows = [], effectRows = [];
for (const e of events) {
  e.choices.forEach((c, k) => {
    const g = c.effects?.length ? ++group : 0;
    choiceRows.push([e.id.replace(/^event\./, ""), k + 1, c.label, c.cost ?? 0, g]);
    (c.effects ?? []).forEach((f, n) => {
      const range = f.min !== undefined;
      const value = range ? f.min : f.value ?? f.mult ?? f.win ?? (f.serious ? 1 : 0);
      const valueMax = range ? f.max : f.lose ?? 0;
      const vt = range ? "Range" : f.type === "goldPercent" ? "PercentCurrent" : f.mult !== undefined ? "Multiplier" : "Flat";
      effectRows.push([g, n + 1, en("EventEffect", f.type), en("EventTarget", f.target ?? "ludus"), vt, value, valueMax, f.days ?? 0, f.chance ?? 1, f.attr ? pascal(f.attr) : f.kind ?? f.cause ?? "", f.desc ?? ""]);
    });
  });
}
table("EventChoice", [["event", "string", "Event.key"], ["seq", "int", "순서"], ["label", "string", "선택지"], ["cost", "int", "비용 (D)"], ["effect_group", "int", "EventEffect.group (0 = 효과 없음)"]], choiceRows);
table("EventEffect", [
  ["group", "int", "효과 묶음"], ["seq", "int", "순서"], ["effect", "enum:EventEffect", "효과"], ["target", "enum:EventTarget", "대상"], ["value_type", "enum:ValueType", "값 종류 (Range면 value~value_max)"],
  ["value", "float", "값 (도박은 이겼을 때)"], ["value_max", "float", "최대값 (도박은 졌을 때)"], ["days", "int", "기간 (일)"], ["chance", "float", "확률"],
  ["key", "string", "능력치·수정자 종류·사망 원인"], ["text", "string", "부상 설명 등"],
], effectRows);

// ── 경기 규칙·밸런스 ──
const bal = J("balance");
table("MatchRule", [
  ["rule", "enum:MatchRule", "경기 규칙"], ["name", "string", "이름"], ["purse_mult", "float", "상금 배율"], ["ticket_mult", "float", "관중 수입 배율"], ["fame_mult", "float", "명성 배율"],
  ["rp_mult", "float", "RP 배율"], ["crowd", "int", "관중 있음 (1)"], ["growth_days", "int", "성장 일수"], ["fatigue_mult", "float", "피로 배율"], ["injury_mult", "float", "부상 배율"],
], Object.entries(bal.rules).map(([k, r]) => [en("MatchRule", k, r.name), r.name, r.purseMult, r.ticketMult, r.fameMult, r.rpMult, r.crowd ? 1 : 0, r.growthDays, r.fatigueMult, r.injuryMult]));
const balRows = [];
(function walk(o, path) {
  for (const [k, v] of Object.entries(o)) {
    const p = path ? `${path}.${k}` : k;
    if (p === "rules") continue;
    if (Array.isArray(v)) v.forEach((x, n) => balRows.push([`${p}.${n}`, x]));
    else if (v && typeof v === "object") walk(v, p);
    else balRows.push([p, typeof v === "boolean" ? (v ? 1 : 0) : v]);
  }
})(bal, "");
table("Balance", [["key", "string", "분류.이름 (배열은 .0 .1)"], ["value", "float", "값"]], balRows);

// ── 이름 ──
const names = J("names");
const nameRows = [];
for (const [kind, v] of Object.entries(names)) {
  if (Array.isArray(v)) v.forEach((t) => nameRows.push([en("NameKind", kind), "Any", t]));
  else for (const [race, list] of Object.entries(v)) list.forEach((t) => nameRows.push([en("NameKind", kind), pascal(race), t]));
}
table("Name", [["kind", "enum:NameKind", "종류"], ["race", "enum:Race", "종족 (Any = 공용)"], ["text", "string", "글"]], nameRows);

// ── C# 코드 안의 상수 표 (Core/Logic, Core/Combat에서 옮김) ──
const WEAPON_CLASS = {
  // 계열: 이름, 동작 상성(베기·찌르기·내려치기), 지대 공격(상·중·하), 지대 방어(상·중·하)  (Exchange.MotionAffinity, Exchange.ZoneProfiles)
  sword: ["한손검", [1.2, 1.0, 0.5], [1.1, 1.0, 0.9], [1.15, 1.0, 0.85]],
  greatsword: ["대검", [1.2, 0.4, 1.1], [1.2, 0.9, 1.1], [1.1, 0.95, 0.8]],
  axe: ["도끼", [0.6, 0.2, 1.4], [1.25, 0.95, 0.8], [0.85, 1.05, 0.9]],
  mace: ["둔기", [0.4, 0.3, 1.5], [1.25, 1.0, 0.75], [0.9, 1.05, 0.85]],
  spear: ["창", [0.3, 1.6, 0.4], [0.85, 1.25, 1.0], [0.85, 1.0, 1.2]],
  trident_net: ["삼지창·그물", [0.2, 1.6, 0.3], [0.8, 1.05, 1.25], [0.9, 1.0, 1.15]],
  dagger: ["단검", [0.6, 1.5, 0.2], [0.85, 1.2, 1.1], [0.8, 1.0, 1.05]],
  throwing: ["투척", [0.5, 1.2, 0.3], [1.0, 1.0, 1.0], [0.9, 0.95, 0.9]],
  shield: ["방패", [0.3, 1.0, 1.0], [0.9, 1.1, 0.9], [1.15, 1.25, 0.75]],
  unarmed: ["맨손", [0.3, 1.3, 0.7], [1.0, 1.2, 0.9], [1.05, 0.9, 0.95]],
  focus: ["주술 촉매", [0.6, 0.8, 0.8], [1.0, 1.0, 1.0], [0.9, 0.9, 0.9]],
};
table("WeaponClassData", [
  ["class", "enum:WeaponClass", "계열"], ["name", "string", "이름"], ["motion_slash", "float", "베기 친화"], ["motion_thrust", "float", "찌르기 친화"], ["motion_smash", "float", "내려치기 친화"],
  ["atk_high", "float", "상단 공격"], ["atk_mid", "float", "중단 공격"], ["atk_low", "float", "하단 공격"], ["def_high", "float", "상단 방어"], ["def_mid", "float", "중단 방어"], ["def_low", "float", "하단 방어"],
], Object.entries(WEAPON_CLASS).map(([k, [n, m, a, d]]) => [pascal(k), n, ...m, ...a, ...d]));

const STANCE = [
  ["assault", "맹공", "쉴 새 없이 몰아칩니다. 방어는 뒷전"], ["balanced", "균형", "상황을 보며 싸웁니다"], ["bulwark", "철벽", "막고 버티며 상대가 지치길 기다립니다. 받는 피해 감소"],
  ["harass", "견제", "거리를 지키며 치고 빠집니다"], ["showman", "쇼맨십", "관중을 홀립니다. 미시오에서 목숨을 건질 보험"], ["blitz", "속전속결", "비싼 기술을 초반에 몰아 씁니다"],
];
table("Stance", [["stance", "enum:Stance", "자세"], ["name", "string", "이름"], ["desc", "string", "설명"]], STANCE.map(([k, n, d]) => [en("Stance", k, n), n, d]));
const AIM = [
  ["spread", "고르게", 0, 0, "효과 없음"], ["head", "머리", -0.15, 0.1, "명중 -15% 치명타 +10% 기절 확률"], ["body", "몸통", 0.1, 0, "명중 +10% 안정적인 피해"],
  ["legs", "다리", -0.05, 0, "명중 -5% 맞히면 30% 확률로 다리 부상"], ["arm", "무기 든 팔", -0.1, 0, "명중 -10% 맞히면 30% 확률로 팔 부상"],
];
table("AimPart", [["aim", "enum:AimPart", "노릴 부위"], ["name", "string", "이름"], ["hit_mod", "float", "명중 가산"], ["crit_mod", "float", "치명타 가산"], ["desc", "string", "설명"]],
  AIM.map(([k, n, h, c, d]) => [en("AimPart", k, n), n, h, c, d]));
const ZONE = [["auto", "자동", "공격은 상대의 약한 방어 지대 / 방어는 상대 무기가 강한 지대"], ["high", "상단", "머리·어깨 높이"], ["mid", "중단", "몸통 높이"], ["low", "하단", "다리 높이"]];
table("Zone", [["zone", "enum:Zone", "높이"], ["name", "string", "이름"], ["desc", "string", "설명"]], ZONE.map(([k, n, d]) => [en("Zone", k, n), n, d]));

const FACILITY = [
  ["barracks", "숙소", "검투사가 먹고 자는 곳"], ["training", "훈련 마당", "모래를 깐 연습장과 허수아비"], ["medical", "의무실", "의원과 약초와 붕대"],
  ["bath", "목욕탕", "뜨거운 물과 마사지"], ["arena", "홈 경기장", "우리 루두스의 작은 투기장"], ["academy", "유소년 학당", "고아와 노예 아이를 검투사로 키우는 학당"], ["guard", "경비 초소", "탈주와 반란을 막는 경비대"],
];
table("Facility", [["facility", "enum:Facility", "시설"], ["name", "string", "이름"], ["desc", "string", "설명"]], FACILITY.map(([k, n, d]) => [en("Facility", k, n), n, d]));
// FacilitiesLogic: Capacity, PaCap, CostBase, CostDays, Upkeep (레벨 0~5)
const CAP = [8, 8, 12, 16, 20, 24], PACAP = [0, 120, 140, 160, 180, 200], COST = [0, 1000, 2500, 6000, 12000, 25000], DAYS = [0, 10, 14, 21, 28, 42], UPKEEP = [0, 10, 20, 40, 70, 110];
table("FacilityLevel", [["level", "int", "레벨"], ["capacity", "int", "숙소 정원"], ["pa_cap", "int", "학당 잠재력 상한"], ["cost", "int", "기본 공사비 (D)"], ["build_days", "int", "공사 일수"], ["upkeep", "int", "주당 유지비 (D)"]],
  CAP.map((_, l) => [l, CAP[l], PACAP[l], COST[l], DAYS[l], UPKEEP[l]]));

const STAFF = [
  ["doctore", "독토레", "수석 교관. 검투사가 훈련과 실전에서 더 빨리 크고 교범도 더 빨리 익힙니다"],
  ["medicus", "메디쿠스", "의원. 부상을 빨리 낫게 하고 사투에서 목숨을 건질 확률을 높입니다"],
  ["scout", "정찰꾼", "정찰꾼. 검투사의 숨은 그릇(잠재력)을 더 정확히 봅니다"], ["agent", "중개인", "중개인. 이적료를 깎아 사고 팔 때는 더 받아 냅니다"],
];
table("StaffRole", [["role", "enum:StaffRole", "역할"], ["name", "string", "이름"], ["desc", "string", "설명"]], STAFF.map(([k, n, d]) => [en("StaffRole", k, n), n, d]));

const TRAIN = [
  ["physical", "체력 단련", "근력·민첩·지구력·강인함·반응·회복력"], ["weapon", "무기술", "주무기 숙련과 정확도"], ["defense", "방어", "회피·막기·방패 숙련"],
  ["grapple", "격투", "제압·맨손 숙련"], ["mental", "정신 수양", "투지·침착·판단력·규율"], ["sense", "실전 감각", "예측력·기만·천재성·적응력·팀워크·타이밍·마무리 본능·위압감 (대련)"],
  ["magic", "주술", "마력·주술 촉매 숙련 (마력이 있어야 효과)"], ["show", "쇼맨십", "쇼맨십"], ["doctrine", "교범 수련", "전투 교범 숙련도"], ["rest", "휴식", "성장 없음. 피로·혹사 회복이 빠릅니다"],
];
table("TrainingFocus", [["focus", "enum:TrainingFocus", "훈련 종류"], ["name", "string", "이름"], ["desc", "string", "오르는 능력치"]], TRAIN.map(([k, n, d]) => [en("TrainingFocus", k, n), n, d]));
const INTENSITY = [["low", "가볍게"], ["normal", "보통"], ["high", "강하게"], ["extreme", "극한"]];
table("TrainingIntensity", [["intensity", "enum:TrainingIntensity", "강도"], ["name", "string", "이름"]], INTENSITY.map(([k, n]) => [en("TrainingIntensity", k, n), n]));

const DUNGEON = [
  ["sewer", "쥐굴 하수도", "도시 밑 하수도. 쥐인간과 도적 떼가 삽니다. 처음 가 보기 좋습니다", 1, 3, 7, 5, 0.04, [60, 32, 8, 0], [["beastkin", "쥐인간"], ["human", "하수도 도적"]], ["beastkin", "쥐왕"]],
  ["mine", "버려진 은광", "무너진 갱도에 오크 약탈자와 광부 망령이 숨어 있습니다", 2, 3, 10, 7, 0.07, [45, 38, 15, 2], [["orc", "오크 약탈자"], ["dwarf", "광부 망령"]], ["orc", "갱도 두목"]],
  ["catacomb", "망자의 지하묘지", "옛 검투사들이 묻힌 묘지. 죽은 자들이 아직 싸웁니다", 3, 4, 14, 9, 0.1, [30, 40, 24, 6], [["human", "해골 검투사"], ["elf", "묘지 사령술사"]], ["human", "무덤의 챔피언"]],
  ["magma", "용암 동굴", "불타는 동굴. 도마뱀 부족과 미노타우로스 광전사가 지킵니다", 4, 4, 18, 11, 0.13, [20, 38, 32, 10], [["lizardfolk", "화염 도마뱀 전사"], ["minotaur", "용암 광전사"]], ["minotaur", "불의 미궁 주인"]],
  ["abyss", "심연의 신전", "가장 깊은 곳. 살아 돌아온 자가 드뭅니다", 5, 5, 21, 13, 0.17, [10, 32, 40, 18], [["elf", "심연의 사제"], ["orc", "타락한 전사"], ["minotaur", "심연의 수문장"]], ["elf", "잊힌 신의 대리인"]],
];
table("Dungeon", [
  ["id", "int", "고유 번호"], ["key", "string", "코드 키"], ["name", "string", "이름"], ["desc", "string", "설명"], ["tier", "int", "등급"], ["floors", "int", "층 수"], ["days", "int", "원정 일수"],
  ["level", "int", "적 수준"], ["death", "float", "층당 사망 위험"], ["loot_common", "float", "보통 전리품 가중치"], ["loot_fine", "float", "정교"], ["loot_master", "float", "명품"], ["loot_legend", "float", "전설"],
], DUNGEON.map(([k, n, d, t, f, dy, lv, de, lo], i) => [id(i, 1), k, n, d, t, f, dy, lv, de, ...lo]));
table("DungeonFoe", [["dungeon", "string", "Dungeon.key"], ["seq", "int", "순서"], ["race", "enum:Race", "종족"], ["name", "string", "이름"], ["boss", "int", "우두머리 (1)"]],
  DUNGEON.flatMap(([k, , , , , , , , , foes, boss]) => [...foes.map(([r, n], s) => [k, s + 1, pascal(r), n, 0]), [k, foes.length + 1, pascal(boss[0]), boss[1], 1]]));
const FORMATION = [
  ["shieldwall", "방패벽", "전열 3·후열 2. 받는 피해가 줄지만 느리게 이깁니다", 0.9, 0.8, 1, "front"], ["wedge", "쐐기", "중앙 돌파. 약한 적부터 몰아칩니다. 받는 피해도 늘어납니다", 1.2, 1.1, 1, "weakest"],
  ["flank", "포위", "양 날개 전진. 수가 많을수록 강합니다", 1, 1, 1, "strongest"], ["spread", "산개", "흩어져 싸웁니다. 투척·창·주술이 강해지고 근접은 약해집니다", 0.95, 1, 1.3, "front"],
];
table("Formation", [["formation", "enum:Formation", "진형 (던전)"], ["name", "string", "이름"], ["desc", "string", "설명"], ["dealt", "float", "주는 피해 배율"], ["taken", "float", "받는 피해 배율"], ["ranged", "float", "원거리 배율"], ["focus", "enum:FormationFocus", "노리는 적"]],
  FORMATION.map(([k, n, d, a, b, r, f]) => [en("Formation", k, n), n, d, a, b, r, en("FormationFocus", f)]));
const CONTRACT = [
  ["escort", "호위", "보급 행렬과 귀족을 지킵니다. 위험이 낮습니다", 1, 2, 14, 21, 40, 400, 0.02], ["front", "전선 파견", "군단과 함께 전선에 섭니다", 3, 5, 28, 42, 60, 900, 0.08],
  ["siege", "공성전", "성벽을 오릅니다. 죽기 쉽지만 보수가 큽니다", 3, 6, 21, 21, 70, 1400, 0.15], ["raid", "정찰·암습", "적진에 숨어들어 지휘관을 노립니다", 1, 2, 14, 14, 55, 1100, 0.1],
  ["duel", "대리 결투", "양국 대표가 1:1 사투로 전황을 가릅니다. 이기면 영웅 지면 거의 죽습니다", 1, 1, 7, 7, 120, 5000, 0], ["levy", "징발령", "조국이 루두스에 검투사를 내놓으라 합니다. 보수는 적고 거부하면 대가가 큽니다", 2, 2, 28, 28, 30, 450, 0.06],
];
table("WarContract", [
  ["contract", "enum:WarContract", "계약"], ["name", "string", "이름"], ["desc", "string", "설명"], ["slots_min", "int", "최소 인원"], ["slots_max", "int", "최대 인원"],
  ["days_min", "int", "최소 일수"], ["days_max", "int", "최대 일수"], ["min_ca", "int", "필요 능력 (CA)"], ["pay", "int", "보수 (D)"], ["risk", "float", "사망 위험"],
], CONTRACT.map(([k, n, ...r]) => [en("WarContract", k, n), n, ...r]));
const MONTHS = ["서리달", "씨앗달", "꽃달", "불꽃달", "수확달", "그림자달"];
table("Month", [["month", "int", "몇째 달 (1부터)"], ["name", "string", "이름"]], MONTHS.map((n, i) => [i + 1, n]));
// 단체 전술 (사용자 결정 2026-11-03, 로직 진행 중)
const ROLE = [
  ["front", "전열", "가장 가까운 적을 막아서며 버팁니다"], ["hunter", "사냥꾼", "빈사·약한 적을 끝내러 갑니다"],
  ["guard", "호위", "지정한 우리 편을 노리는 적을 칩니다"], ["harass", "견제", "거리를 두고 적 하나를 묶어 둡니다"],
];
table("TeamRole", [["role", "enum:TeamRole", "역할"], ["name", "string", "이름"], ["desc", "string", "설명"]], ROLE.map(([k, n, d]) => [en("TeamRole", k, n), n, d]));
const POLICY = [
  ["focus", "집중 공격", "여럿이 한 명씩 달려듭니다. 첫 목표를 지정할 수 있습니다"], ["spread", "분산", "일대일로 흩어져 붙습니다"],
  ["weak", "약한 적 먼저", "HP가 낮은 적부터 노립니다"], ["strong", "강한 적 먼저", "가장 위험한 적부터 묶습니다"],
];
table("TeamPolicy", [["policy", "enum:TeamPolicy", "팀 방침"], ["name", "string", "이름"], ["desc", "string", "설명"]], POLICY.map(([k, n, d]) => [en("TeamPolicy", k, n), n, d]));

// ── 생성한 열거형 ──
const enumRows = [];
for (const [name, m] of enums) [...m].forEach(([k, label], i) => enumRows.push([name, k, i, label]));
table("EnumGame", [["enum", "string", "열거형 이름"], ["key", "string", "키"], ["value", "int", "정수 값"], ["name", "string", "한글 이름"]], enumRows);
