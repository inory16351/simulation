// 장비: 루두스 창고, 장비 시장 매물, 착용 규칙, 사투 노획.
// 장비 id 뒤의 '+숫자'가 품질이다 (data.ts의 splitItemId). 시장엔 명품까지, 전설은 던전에서만 나온다.
import { absDay } from "./calendar";
import { ARMORS, NATIONS, QUALITY_NAMES, WEAPONS, getArmor, getNation, getWeapon, splitItemId } from "./data";
import { addMoney } from "./economy";
import { josa } from "./josa";
import type { Rng } from "./rng";
import { pushMessage } from "./state";
import type { GameState, Gladiator, Id, NationId } from "./types";

export type Slot = keyof Gladiator["equipment"];
export const SLOT_NAMES: Record<Slot, string> = { main: "주무기", off: "보조", armor: "갑옷", helmet: "투구" };

/** 버려도 아깝지 않은 기본 장비: 벗기면 창고에 남기지 않는다 */
const BASIC = new Set(["loincloth", "cestus"]);
/** 빈손일 때 대신 드는 것 */
const FALLBACK: Partial<Record<Slot, string>> = { main: "cestus", armor: "loincloth" };

export interface GearListing {
  id: Id;
  item: string;
  price: number;
  nation: NationId;
  seller: string;
}

export type ItemKind = "weapon" | "armor" | "helmet" | "shield";
export const ITEM_KIND_NAMES: Record<ItemKind, string> = { weapon: "무기", armor: "갑옷", helmet: "투구", shield: "방패" };

export function itemKind(id: string): ItemKind | null {
  if (getWeapon(id)) return "weapon";
  const a = getArmor(id);
  return a ? a.slot : null;
}

export const itemName = (id: string) => getWeapon(id)?.name ?? getArmor(id)?.name ?? id;
export const qualityOf = (id: string) => splitItemId(id)[1];
export const QUALITY_CLASS = ["q0", "q1", "q2", "q3"];

/** 장비 기준가: 성능에 비례하고 품질이 오를수록 가파르게 비싸진다 */
export function itemPrice(id: string): number {
  const [b, q] = splitItemId(id);
  const w = getWeapon(b);
  const a = getArmor(b);
  let base = 40;
  if (w) base = 60 + w.damage * 14 + w.pierce * 300 + (w.magicBonus ?? 0) * 1600 + (w.stun ?? 0) * 900 + (w.crit ?? 0) * 1200 + (w.offhandOnly ? 80 : 0);
  else if (a?.slot === "armor") base = 50 + (a.defense ?? 0) * 70 + (a.crowd ?? 0) * 600;
  else if (a?.slot === "helmet") base = 70 + (a.defense ?? 0) * 70 + (a.showBonus ?? 0) * 60 + (a.netResist ?? 0) * 300;
  else if (a?.slot === "shield") base = 80 + (a.block ?? 0) * 900;
  return Math.round((base * [1, 2.2, 5, 12][q]) / 10) * 10;
}

/** 장비 한 줄 요약 (표에 쓴다) */
export function itemStats(id: string): string {
  const w = getWeapon(id);
  if (w) return [`피해 ${w.damage}`, `속도 ${w.speed}`, w.range ? `사거리 ${w.range}` : "", w.twoHanded ? "양손" : "", w.offhandOnly ? "보조 전용" : "", `근력 ${w.reqStr}`].filter(Boolean).join(" · ");
  const a = getArmor(id);
  if (!a) return "";
  if (a.slot === "shield") return `막기 ${Math.round((a.block ?? 0) * 100)}%`;
  return [`방어 ${a.defense ?? 0}`, a.weight ? { light: "경량", medium: "중량", heavy: "중장" }[a.weight] : "", a.agiPenalty ? `민첩 -${a.agiPenalty}` : "", a.showBonus ? `쇼맨십 +${a.showBonus}` : ""].filter(Boolean).join(" · ");
}

export const armoryOf = (s: GameState) => (s.armory ??= []);

/** 이 장비를 이 칸에 낄 수 있는가 (안 되면 이유) */
export function cannotEquip(g: Gladiator, slot: Slot, id: string): string | null {
  const w = getWeapon(id);
  const a = getArmor(id);
  switch (slot) {
    case "main":
      if (!w) return "무기가 아닙니다";
      if (w.offhandOnly) return "보조 무기로만 쓸 수 있습니다";
      return null;
    case "off": {
      if (getWeapon(g.equipment.main)?.twoHanded) return "양손 무기를 들고 있습니다";
      if (a?.slot === "shield") return null;
      if (w && (w.offhandOnly || w.class === "dagger")) return null;
      return "방패나 보조 무기만 됩니다";
    }
    case "armor":
      if (a?.slot !== "armor") return "갑옷이 아닙니다";
      if (g.race === "minotaur" && a.weight === "light") return "미노타우로스 몸에 맞지 않습니다";
      return null;
    case "helmet":
      return a?.slot === "helmet" ? null : "투구가 아닙니다";
  }
}

/** 장비 착용: 창고에서 꺼내 끼고, 원래 것은 창고로. item이 null이면 벗기만 한다 */
export function equipItem(s: GameState, gId: Id, slot: Slot, item: string | null): string | null {
  const g = s.gladiators[gId];
  if (!g?.alive) return "검투사가 없습니다";
  if (g.deployment) return "파견 중이라 장비를 바꿀 수 없습니다";
  const armory = armoryOf(s);
  if (item !== null) {
    const why = cannotEquip(g, slot, item);
    if (why) return why;
    const at = armory.indexOf(item);
    if (at < 0) return "창고에 없는 장비입니다";
    armory.splice(at, 1);
  }
  const stash = (old: string | null) => { if (old && !BASIC.has(old)) armory.push(old); };
  const old = g.equipment[slot];
  if (slot === "main" || slot === "armor") g.equipment[slot] = item ?? FALLBACK[slot]!;
  else g.equipment[slot] = item;
  stash(old);
  // 양손 무기를 들면 보조 손은 비운다
  if (slot === "main" && getWeapon(g.equipment.main)?.twoHanded && g.equipment.off) {
    stash(g.equipment.off);
    g.equipment.off = null;
  }
  return null;
}

/** 창고 장비 팔기: 기준가의 35% */
export function sellItem(s: GameState, item: string): string | null {
  const armory = armoryOf(s);
  const at = armory.indexOf(item);
  if (at < 0) return "창고에 없습니다";
  armory.splice(at, 1);
  addMoney(s, s.ludi[s.playerLudusId], Math.round(itemPrice(item) * 0.35), `${itemName(item)} 판매`);
  return null;
}

export const gearMarketOf = (s: GameState) => (s.gearMarket ??= []);

export function buyListing(s: GameState, listingId: Id): string | null {
  const market = gearMarketOf(s);
  const l = market.find((x) => x.id === listingId);
  if (!l) return "이미 팔린 매물입니다";
  const me = s.ludi[s.playerLudusId];
  if (me.treasury < l.price) return "금고에 돈이 모자랍니다";
  addMoney(s, me, -l.price, `${itemName(l.item)} 구입 (${l.seller})`);
  armoryOf(s).push(l.item);
  s.gearMarket = market.filter((x) => x !== l);
  return null;
}

/** 나라마다 잘 나오는 장비 (그 나라 상인 매물에 가중치) */
const NATION_TASTE: Record<NationId, string[]> = {
  aurelia: ["gladius", "scutum", "muscle_cuirass", "crested_helm", "brimmed_helm", "manica"],
  dolgar: ["hasta", "aspis", "plate", "open_helm", "warhammer", "greatsword"],
  karsea: ["trident", "net", "sica", "parmula", "scale", "javelin"],
  kemer: ["staff", "rune_charm", "sica", "scale", "hunting_spear", "pugio"],
  grommar: ["greataxe", "battleaxe", "mace", "chainmail", "sealed_helm", "knuckles"],
  sylvaren: ["hunting_spear", "rune_charm", "staff", "leather", "pugio", "longsword"],
};
const SELLERS = ["떠돌이 대장장이", "군수 상인", "전리품 상인", "명장 공방", "항구 밀수꾼", "노병의 유품 가게"];
const ALL_ITEMS = [...WEAPONS.map((w) => w.id), ...ARMORS.map((a) => a.id)].filter((id) => !BASIC.has(id));

function makeListing(s: GameState, rng: Rng, nation: NationId): GearListing {
  const taste = NATION_TASTE[nation];
  const base = rng.chance(0.55) ? rng.pick(taste) : rng.pick(ALL_ITEMS);
  // 시장엔 명품까지만. 전설은 던전에서만 나온다
  const q = rng.weighted({ "0": 70, "1": 24, "2": 6 });
  const item = q === "0" ? base : `${base}+${q}`;
  return {
    id: `G${s.nextId++}`, item, nation,
    price: Math.round((itemPrice(item) * rng.range(0.85, 1.3)) / 10) * 10,
    seller: `${getNation(nation).short} ${rng.pick(SELLERS)}`,
  };
}

/** 장비 시장 매물: 처음엔 새로 깔고, 급료일마다 3분의 1쯤 바뀐다 */
export function refreshGearMarket(s: GameState, rng: Rng, full = false): void {
  const market = gearMarketOf(s);
  const keep = full ? [] : market.filter(() => rng.chance(0.65));
  const want = 48;
  while (keep.length < want) keep.push(makeListing(s, rng, rng.chance(0.4) ? s.nation : rng.pick(NATIONS).id));
  s.gearMarket = keep;
}

/** 사투에서 우리 검투사가 적을 죽이면 그 장비를 거둔다 */
export function lootFallen(s: GameState, killer: Gladiator, dead: Gladiator): string[] {
  const got = Object.values(dead.equipment).filter((x): x is string => !!x && !BASIC.has(x));
  if (!got.length) return got;
  armoryOf(s).push(...got);
  pushMessage(s, {
    kind: "info",
    title: `전리품: ${dead.name}의 장비`,
    body: `${josa(killer.name, "이/가")} 쓰러뜨린 ${dead.name}의 장비를 거뒀습니다.\n${got.map(itemName).join(", ")}\n\n막사 → 검투사 화면에서 착용하거나, 이적 시장 → 창고에서 팔 수 있습니다.`,
  });
  return got;
}

/** 오늘 절대 일수 */
export const today = (s: GameState) => absDay(s.season, s.day);
export { QUALITY_NAMES };
