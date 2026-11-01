// 나라별 리그 특징 (data/nations.json의 rules). 값이 없으면 기본값을 쓴다.
import { BALANCE, getNation, type NationRules } from "./data";
import type { GameState, NationId } from "./types";

export type ResolvedRules = Required<NationRules>;

const DEFAULTS: ResolvedRules = {
  ticketMult: 1, fameMult: 1, taxRate: BALANCE.economy.taxRate, growthMult: 1, surrenderShame: false,
  marketMult: 1, purseMult: 1, suddenPurseMult: 1, aiSuddenMult: 1, templeSubsidy: 0,
  suddenBanned: false, missioDeathMult: 1, magicFameMult: 1,
};

export function rulesOfNation(id: NationId): ResolvedRules {
  return { ...DEFAULTS, ...getNation(id).rules };
}

/** 플레이어가 속한 나라 리그의 규칙 */
export const leagueRules = (state: GameState) => rulesOfNation(state.nation);
