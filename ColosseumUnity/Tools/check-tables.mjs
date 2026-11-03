// Tables/*.csv 검사: 머리 3줄(열 이름·자료형·설명), 자료형(int·float·string·enum:이름), 열거형 값, 묶음 참조, 아이콘 파일.
// 사용 (저장소 루트에서): node ColosseumUnity/Tools/check-tables.mjs
import { copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync } from "node:fs";

const DIR = "ColosseumUnity/Tables";
const ART = "ColosseumUnity/Assets/Resources/Art/icon";
const errors = [];

/** 한 줄을 칸으로: "..." 안의 쉼표·"" 처리 */
function split(line) {
  const out = [];
  let cur = "", q = false;
  for (let i = 0; i < line.length; i++) {
    const ch = line[i];
    if (q) {
      if (ch === '"' && line[i + 1] === '"') { cur += '"'; i++; }
      else if (ch === '"') q = false;
      else cur += ch;
    } else if (ch === '"') q = true;
    else if (ch === ",") { out.push(cur); cur = ""; }
    else cur += ch;
  }
  out.push(cur);
  return out;
}

function load(name) {
  const lines = readFileSync(`${DIR}/${name}.csv`, "utf8").split(/\r?\n/).filter((l) => l !== "");
  const [cols, types] = [split(lines[0]), split(lines[1])];
  if (cols.length !== types.length) errors.push(`${name}: 열 ${cols.length}개, 자료형 ${types.length}개`);
  const rows = lines.slice(3).map((l, i) => {
    const v = split(l);
    if (v.length !== cols.length) errors.push(`${name} ${i + 4}줄: 값 ${v.length}개 (열 ${cols.length}개)`);
    return Object.fromEntries(cols.map((c, k) => [c, v[k]]));
  });
  return { cols, types, rows };
}

const tables = Object.fromEntries(readdirSync(DIR).filter((f) => f.endsWith(".csv")).map((f) => [f.slice(0, -4), load(f.slice(0, -4))]));
// 열거형: Enum.csv(기술, 손으로 씀) + EnumGame.csv(export-tables.mjs가 만듦)
const enums = {};
for (const t of [tables.Enum, tables.EnumGame].filter(Boolean))
  for (const r of t.rows) {
    if ((enums[r.enum] ??= new Set()).has(r.key)) errors.push(`열거형 ${r.enum}.${r.key} 중복`);
    enums[r.enum].add(r.key);
  }

// 다른 테이블의 key를 가리키는 string 열 (열 이름 → 대상 테이블)
const refs = {
  Unique: { base: ["Weapon", "Armor"] }, UniqueEffect: { unique: ["Unique"] }, TraitStat: { trait: ["Trait"] },
  Style: { main: ["Weapon"], off: ["Weapon", "Armor"], armor: ["Armor"], helmet: ["Armor"] },
  StyleStat: { style: ["Style"] }, StyleProf: { style: ["Style"] }, StyleCounter: { style: ["Style"], counter: ["Style"] }, StyleSkill: { style: ["Style"], skill: ["Skill"] },
  RaceData: { race_skill: ["Skill"] }, NationRace: { nation: ["Nation"] }, NationStyle: { nation: ["Nation"], style: ["Style"] },
  NationRelation: { nation: ["Nation"], other: ["Nation"] }, NationRuleText: { nation: ["Nation"] },
  InjuryPenalty: { injury: ["Injury"] }, InjuryPermanent: { injury: ["Injury"] }, EventChoice: { event: ["Event"] }, DungeonFoe: { dungeon: ["Dungeon"] },
};
for (const [name, cols] of Object.entries(refs)) {
  if (!tables[name]) continue;
  for (const [col, targets] of Object.entries(cols))
    for (const [i, r] of tables[name].rows.entries()) {
      if (r[col] === "") continue;
      if (!targets.some((t) => tables[t]?.rows.some((x) => x.key === r[col]))) errors.push(`${name} ${i + 4}줄 ${col}=${r[col]}: ${targets.join("·")}에 없음`);
    }
}
if (tables.EventEffect) {
  const groups = new Set(tables.EventEffect.rows.map((r) => r.group));
  for (const c of tables.EventChoice.rows) if (!groups.has(c.effect_group) && c.effect_group !== "0") errors.push(`EventChoice ${c.event}.${c.seq}: 효과 묶음 ${c.effect_group} 없음`);
}

for (const [name, t] of Object.entries(tables)) {
  t.types.forEach((type, k) => {
    const col = t.cols[k];
    if (!/^(int|float|string|enum:\w+)$/.test(type)) errors.push(`${name}.${col}: 쓸 수 없는 자료형 ${type}`);
    for (const [i, r] of t.rows.entries()) {
      const v = r[col];
      const at = `${name} ${i + 4}줄 ${col}=${v}`;
      if (type === "int" && !/^-?\d+$/.test(v)) errors.push(`${at}: int 아님`);
      if (type === "float" && !/^-?\d+(\.\d+)?$/.test(v)) errors.push(`${at}: float 아님`);
      if (type.startsWith("enum:")) {
        const e = enums[type.slice(5)];
        if (!e) errors.push(`${name}.${col}: 없는 열거형 ${type}`);
        else if (!e.has(v)) errors.push(`${at}: ${type.slice(5)}에 없는 값`);
      }
    }
  });
}

// 묶음·키 참조
const effectGroups = new Set(tables.Effect.rows.map((r) => r.group));
const learnGroups = new Set(tables.SkillLearn.rows.map((r) => r.group));
const skillKeys = new Set(tables.Skill.rows.map((r) => r.key));
const ids = new Set();
for (const s of tables.Skill.rows) {
  if (ids.has(s.id)) errors.push(`Skill ${s.key}: id ${s.id} 중복`);
  ids.add(s.id);
  if (!effectGroups.has(s.effect_group)) errors.push(`Skill ${s.key}: 효과 묶음 ${s.effect_group} 없음`);
  if (!learnGroups.has(s.learn_group)) errors.push(`Skill ${s.key}: 습득 묶음 ${s.learn_group} 없음`);
  if ((s.type === "Passive") !== (s.trigger === "Always" || s.proc_rate === "1")) errors.push(`Skill ${s.key}: 상시 기술은 proc_rate 1`);
  if (!existsSync(`${ART}/${s.icon}.png`)) errors.push(`Skill ${s.key}: 아이콘 ${s.icon}.png 없음`);
}
for (const s of tables.Status.rows) {
  if (s.effect_group !== "0" && !effectGroups.has(s.effect_group)) errors.push(`Status ${s.key}: 효과 묶음 ${s.effect_group} 없음`);
  if (!existsSync(`${ART}/${s.icon}.png`)) errors.push(`Status ${s.key}: 아이콘 ${s.icon}.png 없음`);
}
for (const e of tables.Effect.rows) {
  if (e.effect === "ApplyStatus" && e.status === "None") errors.push(`Effect ${e.group}.${e.seq}: ApplyStatus인데 status 없음`);
  if ((e.effect === "StatMod" || e.value_type === "PerStat") && e.stat === "None") errors.push(`Effect ${e.group}.${e.seq}: stat 필요`);
}
const keyEnum = { WeaponProf: "WeaponClass", Attr: "Stat", Race: "Race" };
for (const l of tables.SkillLearn.rows) {
  if (keyEnum[l.cond] && !enums[keyEnum[l.cond]].has(l.key)) errors.push(`SkillLearn ${l.group}.${l.seq}: ${l.cond} 키 ${l.key} 없음`);
  if (l.cond === "HasSkill" && !skillKeys.has(l.key)) errors.push(`SkillLearn ${l.group}.${l.seq}: 기술 ${l.key} 없음`);
}

const counts = Object.entries(tables).map(([n, t]) => `${n} ${t.rows.length}`).join(", ");
console.log(`테이블: ${counts}`);
if (errors.length) {
  console.log(`문제 ${errors.length}개`);
  for (const e of errors) console.log(" - " + e);
  process.exit(1);
}
console.log("문제 없음");

// 게임이 읽는 기술 테이블 사본: Resources/Tables (원본은 Tables/, 문제가 없을 때만 복사)
const RES = "ColosseumUnity/Assets/Resources/Tables";
mkdirSync(RES, { recursive: true });
for (const n of ["Skill", "Effect", "SkillLearn", "SkillConst", "Status", "LudusName", "PersonName"]) copyFileSync(`${DIR}/${n}.csv`, `${RES}/${n}.csv`);
console.log(`기술 테이블 사본 → ${RES}`);
