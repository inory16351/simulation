// 하루 진행 (docs/10_tech.md 일일 처리 순서)
import { absDay, DAYS_PER_SEASON, isPayday } from "./calendar";
import { closeSeason, logCareer, seasonRecord } from "./career";
import { dailyChallenges, inRegularPeriod, playAiScheduled, todaysPlayerMatches } from "./challenge";
import { dailyCondition } from "./condition";
import { BALANCE } from "./data";
import { payday, payDividend, refreshMarket, tierRanking } from "./economy";
import { rollDailyEvents } from "./events";
import { dailyConstruction } from "./facilities";
import { onHealed } from "./injuries";
import { coachMult, healMult, ratingOf, refreshStaffMarket } from "./staff";
import { rulesOfNation } from "./nationRules";
import { generateGladiator } from "./generators/gladiator";
import { computeCA } from "./gladiator";
import { josa } from "./josa";
import { Rng } from "./rng";
import { pushMessage } from "./state";
import { dailyTraining } from "./training";
import type { GameState, Gladiator, Ludus } from "./types";

/** 다음 날로 넘어가기 전에 플레이어가 처리해야 할 일 */
export function blockers(state: GameState): string[] {
  const out: string[] = [];
  if (state.gameOver) out.push(state.gameOver);
  if (state.pendingEvents.length) out.push(`결정할 사건 ${state.pendingEvents.length}건`);
  const matches = todaysPlayerMatches(state);
  if (matches.length) out.push(`오늘 치를 경기 ${matches.length}건`);
  return out;
}

/** 하루를 진행한다. 막힌 일이 있으면 진행하지 않고 그 이유를 돌려준다 */
export function advanceDay(state: GameState): string[] {
  const blocked = blockers(state);
  if (blocked.length) return blocked;
  const rng = new Rng(state.rngState);

  if (state.day >= DAYS_PER_SEASON) endSeason(state, rng);
  else state.day++;
  const at = absDay(state.season, state.day);
  const player = state.ludi[state.playerLudusId];

  // 1. 아침: 이벤트, 시장
  rollDailyEvents(state, rng);
  dailyConstruction(state);
  if ((BALANCE.economy.marketRefreshDays as number[]).includes(state.day)) {
    refreshMarket(state, rng);
    refreshStaffMarket(state, rng);
    replenishAi(state, rng);
    pushMessage(state, { kind: "info", title: "노예 경매장에 새 매물", body: "경매장에 새 노예들이 들어왔다. 노예 경매 화면에서 볼 수 있다." });
  }
  if (state.day === 15) pushMessage(state, { kind: "info", title: "정규 기간 시작", body: "오늘부터 도전장을 주고받을 수 있다. 시즌 동안 최소 8경기는 치러야 한다." });

  // 2. 급료일
  if (isPayday(state.day)) for (const l of Object.values(state.ludi)) payday(state, l);

  // 3. 훈련과 회복 (모든 루두스)
  for (const l of Object.values(state.ludi)) {
    for (const id of l.gladiators) {
      const g = state.gladiators[id];
      if (!g.alive) continue;
      if (!l.isPlayer) aiTrainingPlan(g);
      const injured = dailyTraining(g, rng, l.facilities.training, rulesOfNation(l.nation).growthMult * coachMult(ratingOf(state, l, "doctore")));
      const healed = dailyCondition(g, at, l.facilities.medical, l.facilities.bath, healMult(ratingOf(state, l, "medicus")));
      if (healed) {
        g.injury = healed;
        onHealed(state, g, rng, l.facilities.medical, ratingOf(state, l, "medicus"), l.isPlayer);
        g.injury = undefined;
        if (l.isPlayer && healed.severity === "serious") pushMessage(state, { kind: "info", title: `${g.name} 복귀`, body: `${healed.desc}에서 회복했다. 다시 싸울 수 있다.` });
      }
      if (injured && l.isPlayer) {
        seasonRecord(state, g).injuries++;
        logCareer(state, g, `훈련 중 다쳤다: ${injured}`, "injury");
        pushMessage(state, { kind: "warning", title: `훈련 중 부상: ${g.name}`, body: `${injured}. ${g.injury?.daysLeft}일 정도 쉬어야 한다.` });
      }
    }
  }

  // 4. 도전과 경기
  dailyChallenges(state, rng);
  playAiScheduled(state, rng);

  // 5. 급료일 다음 날: 탈주와 반란
  if (state.day % 7 === 1 && state.day > 1) unrest(state, rng, player);

  // 6. 파산
  if (player.treasury < 0) {
    player.bankruptSince ??= at;
    const days = at - player.bankruptSince;
    if (days >= BALANCE.economy.bankruptDays) state.gameOver = "파산: 채권자들이 루두스를 압류했다.";
    else if (days === 0) pushMessage(state, { kind: "warning", title: "금고가 바닥났다", body: `${BALANCE.economy.bankruptDays}일 안에 빚을 갚지 못하면 루두스를 압류당한다. 검투사를 팔거나 경기로 돈을 벌어라.` });
  } else player.bankruptSince = undefined;

  state.rngState = rng.state;
  return [];
}

/** AI 검투사의 훈련 계획: 지치면 쉬고 아니면 스타일에 맞게 */
function aiTrainingPlan(g: Gladiator): void {
  if (g.fatigue > 60 || g.wear > 60) g.training = { focus: "rest", intensity: "normal" };
  else g.training = { focus: g.age % 2 ? "weapon" : "physical", intensity: "normal" };
}

/** AI 루두스 인원 보충 (시장 갱신 때) */
function replenishAi(state: GameState, rng: Rng): void {
  const used = new Set(Object.values(state.gladiators).map((g) => g.name));
  for (const l of Object.values(state.ludi)) {
    if (l.isPlayer) continue;
    while (l.gladiators.length < BALANCE.economy.aiRoster) {
      const g = generateGladiator(rng, { id: `G${state.nextId++}`, nation: l.nation, target: (l.tier === 1 ? 10.5 : 7.5) + rng.range(-1.5, 1.5), usedNames: used });
      state.gladiators[g.id] = g;
      l.gladiators.push(g.id);
    }
  }
}

/** 탈주와 반란 (docs/05_economy.md) */
function unrest(state: GameState, rng: Rng, player: Ludus): void {
  const U = BALANCE.unrest;
  const roster = player.gladiators.map((id) => state.gladiators[id]).filter((g) => g.alive);
  if (!roster.length) return;

  for (const g of roster) {
    if (g.unpaidWeeks < 4 || g.traits.includes("loyal_hound")) continue;
    const p = (g.unpaidWeeks - 3) * U.desertBase * (1 - g.hidden.loyalty / 25) * (1 - player.facilities.guard * 0.1);
    if (rng.chance(p)) {
      player.gladiators = player.gladiators.filter((x) => x !== g.id);
      logCareer(state, g, `급료가 ${g.unpaidWeeks}번 밀리자 ${player.name}에서 달아났다`, "other");
      pushMessage(state, { kind: "warning", title: `${josa(g.name, "이/가")} 달아났다`, body: `급료가 ${g.unpaidWeeks}번 밀리자 밤사이 담을 넘었다.` });
    }
  }

  const left = player.gladiators.map((id) => state.gladiators[id]).filter((g) => g.alive);
  const maxUnpaid = Math.max(0, ...left.map((g) => g.unpaidWeeks));
  if (!left.length || maxUnpaid === 0) return;
  const avgMorale = left.reduce((s, g) => s + g.morale, 0) / left.length;
  let chance = (100 - avgMorale) * maxUnpaid * U.revoltPerMoralePerWeek;
  chance += left.filter((g) => g.traits.includes("agitator") && g.morale <= 30).length * U.agitatorBonus;
  chance -= player.facilities.guard * U.guardReduction;
  chance -= left.filter((g) => g.traits.includes("loyal_hound") && g.morale >= 70).length * U.loyalReduction;
  if (!rng.chance(Math.max(0, chance))) return;

  // 반란: 사기 낮은 자들과 나머지+경비대가 맞붙는다
  const rebels = left.filter((g) => g.morale <= 30 && !g.traits.includes("loyal_hound"));
  if (!rebels.length) return;
  const loyal = left.filter((g) => !rebels.includes(g));
  const power = (gs: Gladiator[]) => gs.reduce((s, g) => s + computeCA(g), 0);
  const guardPower = player.facilities.guard * 2 * 60;
  const rebelPower = power(rebels) * rng.range(0.8, 1.2);
  const loyalPower = (power(loyal) + guardPower) * rng.range(0.8, 1.2);
  if (rebelPower > loyalPower) {
    state.gameOver = `반란: ${rebels.map((g) => g.name).join(", ")} 일당이 루두스를 장악했다.`;
    pushMessage(state, { kind: "death", title: "반란 — 루두스를 빼앗겼다", body: state.gameOver });
    return;
  }
  // 진압 성공: 반란자 중 일부가 죽고 금고가 털린다
  const dead: string[] = [];
  for (const g of rebels) {
    if (rng.chance(0.3)) {
      g.alive = false;
      g.diedAt = { season: state.season, day: state.day, cause: "반란 진압 중 사망" };
      player.gladiators = player.gladiators.filter((x) => x !== g.id);
      state.fallen.push(g.id);
      logCareer(state, g, "반란을 일으켰다가 진압 중 죽었다", "death");
      dead.push(g.name);
    } else {
      g.morale = Math.max(0, g.morale - 10);
      logCareer(state, g, "반란에 가담했다가 진압당했다", "other");
    }
  }
  const lost = Math.round(Math.max(0, player.treasury) * 0.1);
  player.treasury -= lost;
  pushMessage(state, {
    kind: "death",
    title: "반란을 진압했다",
    body: `${rebels.map((g) => g.name).join(", ")}이(가) 들고일어났지만 진압됐다.${dead.length ? `\n죽은 자: ${dead.join(", ")}` : ""}\n금고 ${lost} D를 잃었다.`,
  });
}

/** 시즌 종료: 승강, 페널티, 나이, RP 회귀, 배당금 */
function endSeason(state: GameState, rng: Rng): void {
  const L = BALANCE.league;
  const player = state.ludi[state.playerLudusId];
  for (const g of Object.values(state.gladiators)) if (g.alive) closeSeason(state, g);

  // 최소 경기 수 페널티
  for (const l of Object.values(state.ludi)) {
    const missing = Math.max(0, L.minMatches - l.matchesThisSeason);
    if (missing) l.rp -= missing * L.minMatchPenalty;
  }

  const t1 = tierRanking(state, 1);
  // 신전 후원 (케메르)
  const subsidy = rulesOfNation(state.nation).templeSubsidy;
  if (subsidy) {
    for (const l of t1.slice(0, 3)) {
      l.treasury += subsidy;
      if (l.isPlayer) {
        state.ledger.unshift({ at: absDay(state.season, state.day), amount: subsidy, desc: "신전 후원금" });
        pushMessage(state, { kind: "info", title: "신전이 금을 내렸다", body: `1부 상위 루두스에 신전 후원금 ${subsidy.toLocaleString()} D가 내려왔다.` });
      }
    }
  }
  const t2 = tierRanking(state, 2);
  const promoted = t2.slice(0, L.promote);
  const relegated = t1.slice(-L.relegate);
  const finalRank = new Map<string, number>([...t1.map((l, i) => [l.id, i + 1] as const), ...t2.map((l, i) => [l.id, i + 1] as const)]);
  promoted.forEach((l) => { l.tier = 1; l.rp = Math.max(l.rp, L.rpTier1 - 80); });
  relegated.forEach((l) => { l.tier = 2; l.rp = Math.min(l.rp, L.rpTier2 + 80); });

  const playerRank = finalRank.get(player.id)!;
  const wasPromoted = promoted.includes(player);
  const wasRelegated = relegated.includes(player);
  pushMessage(state, {
    kind: "info",
    title: `시즌 ${state.season} 종료 — ${wasPromoted ? "승격!" : wasRelegated ? "강등" : `${playerRank}위`}`,
    body: `${player.name}은(는) ${wasPromoted ? "2부" : wasRelegated ? "1부" : `${player.tier}부`} ${playerRank}위로 시즌을 마쳤다.` +
      `\n승격: ${promoted.map((l) => l.name).join(", ")}\n강등: ${relegated.map((l) => l.name).join(", ")}`,
  });

  state.season++;
  state.day = 1;
  for (const l of Object.values(state.ludi)) {
    const base = l.tier === 1 ? L.rpTier1 : L.rpTier2;
    l.rp = Math.round(l.rp - (l.rp - base) * L.seasonRegression);
    l.matchesThisSeason = 0;
    l.refusalsUsed = 0;
    const rank = finalRank.get(l.id)!;
    const coef = promoted.includes(l) ? BALANCE.economy.promotedDividendCoef
      : relegated.includes(l) ? BALANCE.economy.relegatedDividendCoef
      : 1.5 - rank / L.teamsPerTier;
    payDividend(state, l, coef);
  }
  for (const g of Object.values(state.gladiators)) {
    if (!g.alive) continue;
    g.age++;
    seasonRecord(state, g);
  }
  state.challenges = state.challenges.filter((c) => c.status === "pending" || c.status === "scheduled");
  for (const c of state.challenges) c.status = "cancelled";
  state.challenges = [];
  refreshMarket(state, rng);
  replenishAi(state, rng);
}

export { inRegularPeriod };
