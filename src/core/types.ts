import type { Doctrine } from "./tactics";

export type Id = string;

export const PHYSICAL = ["str", "agi", "end", "tou", "rea", "rec"] as const;
export const TECHNICAL = ["acc", "eva", "blk", "grp", "mag"] as const;
export const MENTAL = ["grit", "comp", "dec", "show", "agg", "disc", "lead"] as const;
export const ATTRS = [...PHYSICAL, ...TECHNICAL, ...MENTAL] as const;
export type AttrId = (typeof ATTRS)[number];

export const ATTR_NAMES: Record<AttrId, string> = {
  str: "근력", agi: "민첩", end: "지구력", tou: "강인함", rea: "반응", rec: "회복력",
  acc: "정확도", eva: "회피", blk: "막기", grp: "제압", mag: "마력",
  grit: "투지", comp: "침착", dec: "판단력", show: "쇼맨십", agg: "공격성", disc: "규율", lead: "리더십",
};

export const WEAPON_CLASSES = [
  "sword", "greatsword", "axe", "mace", "spear", "trident_net", "dagger", "throwing", "shield", "unarmed", "focus",
] as const;
export type WeaponClassId = (typeof WEAPON_CLASSES)[number];

export const WEAPON_CLASS_NAMES: Record<WeaponClassId, string> = {
  sword: "한손검", greatsword: "대검", axe: "도끼", mace: "둔기", spear: "창", trident_net: "삼지창·그물",
  dagger: "단검", throwing: "투척", shield: "방패", unarmed: "맨손", focus: "주술 촉매",
};

export type RaceId = "human" | "orc" | "minotaur" | "elf" | "dwarf" | "lizardfolk" | "beastkin";
export type NationId = "aurelia" | "grommar" | "sylvaren" | "dolgar" | "karsea" | "kemer";
export type DeathRule = "training" | "missio" | "sudden";
export type Status = "slave" | "free" | "freedman";

export interface Hidden {
  pa: number;
  consistency: number;
  bigStage: number;
  injuryProne: number;
  loyalty: number;
  ambition: number;
  greed: number;
}

export interface Injury {
  severity: "light" | "serious";
  daysLeft: number;
  desc: string;
}

export interface Gladiator {
  id: Id;
  name: string;
  nickname?: string;
  gender: "m" | "f";
  race: RaceId;
  nation: NationId;
  age: number;
  status: Status;
  style: string;
  attrs: Record<AttrId, number>;
  prof: Record<WeaponClassId, number>;
  hidden: Hidden;
  traits: string[];
  skills: { active: string[]; passive: string[] };
  equipment: { main: string; off: string | null; armor: string; helmet: string | null };
  wage: number;
  /** 밀린 급료일 횟수 */
  unpaidWeeks: number;
  morale: number;
  popularity: number;
  fatigue: number;
  injury?: Injury;
  record: { wins: number; losses: number; kills: number };
  alive: boolean;
  diedAt?: { season: number; day: number; cause: string };
  doctrine: Doctrine;
  portraitSeed: number;
  /** 초상화 파츠 선택 (없으면 portraitSeed로 자동 조합) */
  portrait?: PortraitParts;
  training: TrainingPlan;
  /** 능력치별 성장 누적치. 1이 되면 능력치 +1 (음수면 -1) */
  growth: Partial<Record<string, number>>;
  /** 마지막 경기 날짜 (절대 일수) */
  lastFightAt?: number;
  /** 경기감각 0~100. 경기를 뛰면 오르고 쉬면 떨어진다 */
  sharpness: number;
  /** 혹사도 0~100. 체력(피로)과 별개로 경기 간격이 짧을수록 쌓인다 */
  wear: number;
  /** 시즌별 기록 */
  history: SeasonRecord[];
  /** 일대기 (주요 사건) */
  career: CareerEvent[];
}

export interface SeasonRecord {
  season: number;
  ludus: string;
  tier: number;
  apps: number;
  wins: number;
  losses: number;
  kills: number;
  caStart: number;
  caEnd: number;
  popularity: number;
  injuries: number;
  earnings: number;
}

export interface CareerEvent {
  season: number;
  day: number;
  text: string;
  kind: "join" | "match" | "milestone" | "injury" | "death" | "other";
}

export type PortraitParts = Partial<Record<"skin" | "hair" | "hairColor" | "eyes" | "mouth" | "beard" | "helmet" | "cloth" | "variant", string | null>>;

export type TrainingFocus = "physical" | "weapon" | "defense" | "grapple" | "mental" | "magic" | "show" | "doctrine" | "rest";
export type TrainingIntensity = "low" | "normal" | "high" | "extreme";

export interface TrainingPlan {
  focus: TrainingFocus;
  intensity: TrainingIntensity;
}

export interface Ludus {
  id: Id;
  name: string;
  nation: NationId;
  tier: 1 | 2;
  rp: number;
  treasury: number;
  gladiators: Id[];
  isPlayer: boolean;
  aiPersonality?: "aggressive" | "stable" | "grudge" | "merchant" | "developer";
  record: { wins: number; losses: number };
  /** 이번 시즌 치른 공식 경기 수 */
  matchesThisSeason: number;
  /** 이번 시즌 쓴 거절 횟수 */
  refusalsUsed: number;
  facilities: Facilities;
  /** 진행 중인 공사 */
  construction?: { facility: FacilityId; toLevel: number; doneAt: number };
  staff: Id[];
  /** 금고가 마이너스가 된 날 (절대 일수) */
  bankruptSince?: number;
}

export interface Facilities {
  barracks: number;
  training: number;
  medical: number;
  bath: number;
  arena: number;
  guard: number;
}

export type FacilityId = keyof Facilities;

export type StaffRole = "doctore" | "medicus" | "scout" | "agent";

export interface Staff {
  id: Id;
  name: string;
  race: RaceId;
  gender: "m" | "f";
  age: number;
  role: StaffRole;
  /** 능력 1~20 */
  rating: number;
  wage: number;
  portraitSeed: number;
  /** 은퇴한 검투사 출신이면 그 검투사 id */
  formerGladiator?: Id;
}

export interface Challenge {
  id: Id;
  from: Id;
  to: Id;
  /** 신청한 쪽 출전 검투사 */
  fromGladiator: Id;
  /** 지명한 상대 검투사 (없으면 상대가 고른다) */
  toGladiator?: Id;
  rule: DeathRule;
  stake: number;
  createdAt: number;
  /** 응답 기한 (절대 일수) */
  respondBy: number;
  status: "pending" | "scheduled" | "declined" | "done" | "cancelled";
  /** 경기 날짜 (절대 일수) */
  matchAt?: number;
}

export interface LedgerEntry {
  at: number;
  amount: number;
  desc: string;
}

export interface MarketListing {
  gladiatorId: Id;
  price: number;
}

export interface Modifier {
  kind: "ticket" | "food" | "market";
  mult: number;
  /** 이 날(절대 일수)까지 유효 */
  until: number;
}

export interface PendingEvent {
  id: Id;
  eventId: string;
  targetId?: Id;
  title: string;
  text: string;
}

export interface InboxMessage {
  id: Id;
  season: number;
  day: number;
  kind: "info" | "match" | "event" | "warning" | "death";
  title: string;
  body: string;
  read: boolean;
}

export interface GameState {
  version: number;
  seed: number;
  rngState: number;
  season: number;
  /** 시즌 안의 날짜 1~168 */
  day: number;
  nation: NationId;
  playerLudusId: Id;
  ludi: Record<Id, Ludus>;
  gladiators: Record<Id, Gladiator>;
  inbox: InboxMessage[];
  /** 플레이어 루두스에서 죽은 검투사 (명예의 전당·로스터 표시용) */
  fallen: Id[];
  challenges: Challenge[];
  staff: Record<Id, Staff>;
  /** 고용할 수 있는 식솔 후보 */
  staffMarket: Id[];
  market: MarketListing[];
  ledger: LedgerEntry[];
  /** 플레이어가 골라야 하는 이벤트 선택지 */
  pendingEvents: PendingEvent[];
  /** 마지막 '재앙' 이벤트 (절대 일수) */
  lastDisasterAt: number;
  /** 운 보정: 연속 악재면 양수, 연속 행운이면 음수 */
  luckStreak: number;
  /** 세계 이벤트가 건 일시 효과 */
  modifiers: Modifier[];
  gameOver?: string;
  nextId: number;
}
