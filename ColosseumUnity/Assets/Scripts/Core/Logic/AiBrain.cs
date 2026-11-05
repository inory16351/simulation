// FM 세계 단계 7 (Unity판 전용, FM식): AI 루두스가 유저와 같은 운영을 한다.
// 480팀을 매일 다 돌리지 않고 id 해시로 요일을 나눠 루두스당 주 1회 차례 (GearLogic.TurnDay). 성격(aiPersonality)으로 우선순위를 바꾼다.
// 차례: 재정 점검 → 식솔 → 시설 → 장비 → 유소년 → 던전(요약 판정) → 훈련.
// 로스터(무소속·이적 영입, 재계약)는 FreeAgency.Weekly · Negotiations.DailyAiMarket · Day.AiContracts가 맡는다.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Colosseum.Core
{
    public static class AiBrain
    {
        public static bool Enabled => FmRules.Enabled;

        /// <summary>측정용: 루두스마다 한 일 (dungeon, gear, staff, facility, sell, buy, release)</summary>
        public static readonly Dictionary<string, HashSet<string>> Did = new();
        static void Mark(string what, Ludus l) { if (!Did.TryGetValue(what, out var set)) Did[what] = set = new HashSet<string>(); set.Add(l.id); }

        static IEnumerable<Gladiator> Alive(GameState s, Ludus l) =>
            l.gladiators.Select(id => s.gladiators.TryGetValue(id, out var g) ? g : null).Where(g => g != null && g.alive);

        static bool Crisis(GameState s, Ludus l) => l.treasury < Economy.WeeklyWages(s, l) * 4;

        /// <summary>매일: 원정 귀환은 매일, 운영 차례는 그 요일 루두스만</summary>
        public static void Daily(GameState s, Rng rng)
        {
            if (!Enabled) return;
            foreach (var l in s.ludi.Values)
            {
                if (l.isPlayer) continue;
                if (l.aiExpedition != null) DungeonReturn(s, rng, l);
                if (!GearLogic.TurnDay(s, l)) continue;
                Turn(s, rng, l);
            }
        }

        public static void Turn(GameState s, Rng rng, Ludus l)
        {
            Finance(s, l);
            Staffing(s, l);
            Facilities(s, l);
            Gear(s, l);
            YouthStep(s, l);
            Dungeon(s, rng, l);
            foreach (var g in Alive(s, l)) TrainingPlan(g);
        }

        // ── 1. 재정 점검: 적자면 긴축 (식솔 해고 → 내놓은 장비 값 내리기 → 비싼 벤치 방출) ──

        static void Finance(GameState s, Ludus l)
        {
            if (!Crisis(s, l)) return;
            // 가장 못하는 식솔부터 하나 내보낸다 (독토레는 남긴다)
            var fire = l.staff.Select(id => s.staff.TryGetValue(id, out var st) ? st : null).Where(st => st != null && st.role != "doctore")
                .OrderBy(st => st.rating).FirstOrDefault();
            if (fire != null) { l.staff.Remove(fire.id); StateUtil.RemoveOrdered(s.staff, fire.id); }
            foreach (var c in GearLogic.ConsignmentsOf(s).Where(c => c.seller == l.id)) c.price = JsMath.Round(c.price * 0.9 / 10) * 10;
            // 금고가 바닥났으면 주급이 가장 비싼 벤치 하나를 풀어 준다
            if (l.treasury < 0)
            {
                var starters = new HashSet<string>(Alive(s, l).OrderByDescending(GladiatorRules.ComputeCA).Take(5).Select(g => g.id));
                var cut = Alive(s, l).Where(g => !starters.Contains(g.id)).OrderByDescending(g => g.wage).FirstOrDefault();
                if (cut != null && Alive(s, l).Count() > FmRules.AiRoster(l.tier) - 2)
                {
                    l.gladiators.Remove(cut.id);
                    l.registered?.Remove(cut.id);
                    cut.contract = null;
                    s.freeAgents.Add(cut.id);
                    Economy.MarkFree(s, cut);
                    Career.LogCareer(s, cut, $"{l.name}의 재정난으로 풀려났습니다", "other");
                    Mark("release", l);
                }
            }
        }

        // ── 3. 식솔: 여유가 있으면 모자란 역할부터, 성격대로 ──

        static string[] StaffPriority(Ludus l) => l.aiPersonality switch
        {
            "developer" => new[] { "doctore", "youthHead", "skillCoach", "weaponCoach", "fitnessCoach", "medicus", "scout", "agent" },
            "aggressive" => new[] { "doctore", "weaponCoach", "fitnessCoach", "skillCoach", "medicus", "scout", "agent", "youthHead" },
            "merchant" => new[] { "doctore", "agent", "scout", "medicus", "weaponCoach", "fitnessCoach", "skillCoach", "youthHead" },
            "grudge" => new[] { "doctore", "fitnessCoach", "weaponCoach", "medicus", "skillCoach", "scout", "agent", "youthHead" },
            _ => new[] { "doctore", "medicus", "weaponCoach", "fitnessCoach", "skillCoach", "scout", "agent", "youthHead" },
        };

        static void Staffing(GameState s, Ludus l)
        {
            if (Crisis(s, l) || s.staffMarket == null || s.staffMarket.Count == 0) return;
            double rep = Reputation.LudusReputation(s, l);
            // 주급 예산(고정비·식솔 급료를 뺀 값, 금고가 넉넉하면 더)에서 지금 주급을 뺀 여유
            double fixedRoom = Economy.WageBudget(s, l) - Economy.WeeklyWages(s, l);
            // 금고가 넉넉하면(넉 달 수입 넘게) 그 여유를 두 시즌에 나눠 식솔 급료로 쓸 수 있다
            fixedRoom = Math.Max(fixedRoom, (l.treasury - Economy.ExpectedWeeklyIncome(s, l) * 16) / 48);
            foreach (var role in StaffPriority(l))
            {
                int have = StaffLogic.StaffsOf(s, l, role).Count;
                if (have >= StaffLogic.RoleCap(l, role)) continue;
                // 우리가 고를 후보는 남긴다
                if (s.staffMarket.Count(id => s.staff.TryGetValue(id, out var st) && st.role == role) <= StaffLeftForPlayer) continue;
                // 둘째부터는 금고가 넉넉할 때만
                if (have > 0 && l.treasury < Economy.ExpectedWeeklyIncome(s, l) * 16) continue;
                Staff best = null;
                double bestWage = 0;
                foreach (var id in s.staffMarket)
                {
                    if (!s.staff.TryGetValue(id, out var c) || c.role != role) continue;
                    var d = Reputation.DemandFor(Reputation.StaffStanding(c), c.wage, rep);
                    if (d.refuse || d.wage > fixedRoom) continue;
                    if (best == null || c.rating > best.rating) { best = c; bestWage = d.wage; }
                }
                if (best == null) continue;
                best.wage = bestWage;
                s.staffMarket.Remove(best.id);
                l.staff.Add(best.id);
                Mark("staff", l);
                return; // 한 차례에 한 명
            }
        }

        // ── 4. 시설: 유저와 같은 비용·기간, 성격대로 ──

        static string[] FacilityPriority(Ludus l) => l.aiPersonality switch
        {
            "developer" => new[] { "academy", "training", "medical", "barracks" },
            "aggressive" => new[] { "training", "medical", "arena", "bath" },
            "merchant" => new[] { "arena", "barracks", "training", "medical" },
            "grudge" => new[] { "training", "guard", "medical", "bath" },
            _ => new[] { "medical", "training", "barracks", "bath" },
        };

        static void Facilities(GameState s, Ludus l)
        {
            if (l.construction != null || Crisis(s, l)) return;
            double reserve = Economy.ExpectedWeeklyIncome(s, l) * 12 + Economy.WeeklyWages(s, l) * 8;
            foreach (var id in FacilityPriority(l))
            {
                int lvl = FacilitiesLogic.Level(l.facilities, id);
                if (lvl >= FacilitiesLogic.MaxLevel) continue;
                var (cost, _) = FacilitiesLogic.UpgradeCost(id, lvl + 1);
                if (l.treasury - cost < reserve) return; // 가장 바라는 것을 살 돈이 모일 때까지 기다린다
                if (FacilitiesLogic.StartUpgrade(s, l, id) == null) Mark("facility", l);
                return;
            }
        }

        // ── 5. 장비: 창고 것을 주전에게, 남는 것은 위탁, 같은 나라 상인 매물을 산다 ──

        static void Gear(GameState s, Ludus l)
        {
            var armory = GearLogic.ArmoryOf(s, l);
            // 창고 장비를 가장 나아지는 주전에게 (CannotEquip을 통과하는 것만 — BestUse가 확인한다)
            foreach (var item in armory.ToList())
            {
                var (who, gain) = GearLogic.BestUse(s, l, item);
                if (who != null && gain > 0.05 && GearLogic.EquipItem(s, who.id, GearLogic.SlotFor(item), item) == null) Mark("gear", l);
            }
            // 아무에게도 안 쓰이는 것은 정가의 80%로 내놓는다 (다른 루두스가 산다). 내 매물은 4개까지, 넘치면 싼 것부터 상인에게
            int listed = GearLogic.GearMarketOf(s).Count(x => x.sellerLudus == l.id);
            var startersNow = GearLogic.StartersOf(s, l);
            foreach (var item in armory.ToList())
            {
                if (GearLogic.BestUse(s, l, item, startersNow).who != null) continue;
                armory.Remove(item);
                if (listed >= MaxListings || GearLogic.ItemPrice(item) < 60)
                {
                    Economy.AddMoney(s, l, JsMath.Round(GearLogic.ItemPrice(item) * 0.35), $"{GearLogic.ItemName(item)} 상인에게 넘김");
                    continue;
                }
                listed++;
                GearLogic.ListForSale(s, l, item, JsMath.Round(GearLogic.ItemPrice(item) * 0.8 / 10) * 10);
                Mark("sell", l);
            }
            // 안 팔린 내 매물은 차례마다 15%씩 내리고, 정가 35% 아래로 가면 상인에게 넘긴다 (트레이드인 값, 매물이 쌓이지 않게)
            var mine = GearLogic.GearMarketOf(s).Where(x => x.sellerLudus == l.id).ToList();
            foreach (var x in mine)
            {
                double floor = GearLogic.ItemPrice(x.item) * 0.35;
                if (x.price * 0.85 < floor)
                {
                    GearLogic.GearMarketOf(s).Remove(x);
                    Economy.AddMoney(s, l, JsMath.Round(floor), $"{GearLogic.ItemName(x.item)} 상인에게 넘김");
                }
                else x.price = JsMath.Round(x.price * 0.85 / 10) * 10;
            }
            // 같은 나라 장비 시장(상인·다른 루두스 매물): 지불 의사가 값 이상인 것 중 가장 이득인 하나 (상인 구매는 금고에서 돈이 빠져나가는 통로)
            var market = GearLogic.GearMarketOf(s);
            var starters = GearLogic.StartersOf(s, l);
            double wages = Economy.WeeklyWages(s, l);
            var pick = market.Where(x => x.nation == l.nation && x.sellerLudus != l.id).Select(x => (x, pay: GearLogic.GearDemand(s, l, x.item, 0, starters, wages))).Where(t => t.pay >= t.x.price)
                .OrderByDescending(t => t.pay - t.x.price).FirstOrDefault();
            if (pick.x != null) { GearLogic.AiBuyListing(s, l, pick.x); Mark("buy", l); Mark("gear", l); }
        }

        // ── 6. 유소년: 데뷔 나이가 되면 쓸 만한 아이는 올리고, 잠재력이 낮은 아이는 내보낸다 ──

        static void YouthStep(GameState s, Ludus l)
        {
            if (l.youth == null || l.youth.Count == 0) return;
            var starters = Alive(s, l).Select(GladiatorRules.ComputeCA).OrderByDescending(x => x).ToList();
            double bench = starters.Count > 5 ? starters.Skip(5).Average() : 0;
            foreach (var id in l.youth.ToList())
            {
                if (!s.gladiators.TryGetValue(id, out var g) || !g.alive) continue;
                if (GladiatorRules.AgeStage(g) == "youth") continue;
                if (g.hidden.pa >= bench || l.gladiators.Count < FmRules.AiRoster(l.tier)) Youth.PromoteYouth(s, l, id);
                else Youth.ReleaseYouth(s, l, id);
            }
        }

        // ── 7. 던전: 요약 판정 (Sim을 돌리지 않는다). 정규 리그·컵 일정과 겹치면 가지 않는다 ──

        public const int AiParty = 5;
        /// <summary>한 루두스가 장비 시장에 동시에 내놓는 매물 수</summary>
        public const int MaxListings = 4;
        /// <summary>식솔 시장은 우리 몫으로 역할마다 이만큼은 남긴다</summary>
        public const int StaffLeftForPlayer = 4;

        static void Dungeon(GameState s, Rng rng, Ludus l)
        {
            if (l.aiExpedition != null || Crisis(s, l)) return;
            // 정규 리그가 끝나고 컵 일정이 없는 팀만 (공식 경기와 겹치지 않게)
            if (!FmRules.Frozen(s) || ChallengeLogic.InCup(s, l.id)) return;
            if (s.day > Calendar.DaysPerSeason - 8) return;
            var party = Alive(s, l).Where(g => g.deployment == null && g.injury == null && GladiatorRules.AgeStage(g) != "youth")
                .OrderByDescending(GladiatorRules.ComputeCA).Skip(l.aiPersonality == "aggressive" ? 0 : 2).Take(AiParty).ToList();
            if (party.Count < 3) return;
            double avg = party.Average(GladiatorRules.ComputeCA);
            // 적정 난이도: 적보다 30쯤 강한 곳 중 가장 어려운 곳 (성격이 공격적이면 한 단계 더)
            var fit = DungeonLogic.Dungeons.Where(d => DungeonLogic.FoeCa(d) + 30 <= avg + (l.aiPersonality == "aggressive" ? 12 : 0) && d.days <= Calendar.DaysPerSeason - s.day)
                .OrderByDescending(DungeonLogic.FoeCa).FirstOrDefault();
            if (fit == null) return;
            double until = Calendar.AbsDay(s.season, s.day) + fit.days;
            l.aiExpedition = new AiExpedition { dungeon = fit.id, party = party.Select(g => g.id).ToList(), until = until };
            foreach (var g in party) g.deployment = new Deployment { contractId = "ai:" + l.id, until = until, kind = "dungeon" };
            Mark("dungeon", l);
        }

        /// <summary>원정 귀환: 층마다 파티 평균 CA와 적 수준으로 완주를 판정하고, 쓰러진 사람마다 유저와 같은 사망 확률, 전리품은 같은 표에서</summary>
        static void DungeonReturn(GameState s, Rng rng, Ludus l)
        {
            var ex = l.aiExpedition;
            if (Calendar.AbsDay(s.season, s.day) < ex.until) return;
            l.aiExpedition = null;
            var d = DungeonLogic.GetDungeon(ex.dungeon);
            var party = ex.party.Select(id => s.gladiators.TryGetValue(id, out var g) ? g : null).Where(g => g != null && g.alive).ToList();
            foreach (var g in party) if (g.deployment?.contractId == "ai:" + l.id) g.deployment = null;
            double foe = DungeonLogic.FoeCa(d);
            var loot = new List<string>();
            for (int floor = 1; floor <= d.floors && party.Count > 0; floor++)
            {
                double avg = party.Average(GladiatorRules.ComputeCA);
                double win = 1 / (1 + Math.Exp(-(avg - foe - 15 - floor * 2) / 12));
                // 층마다 몇 명은 쓰러지고, 쓰러지면 유저와 같은 사망 확률
                foreach (var g in party.ToList())
                {
                    if (!rng.Chance(0.25 + (1 - win) * 0.35)) continue;
                    if (!rng.Chance(DungeonLogic.DeathChance(GladiatorRules.ComputeCA(g), foe, false))) continue;
                    g.alive = false;
                    g.injury = null;
                    g.deployment = null;
                    g.diedAt = new DiedAt { season = s.season, day = s.day, cause = $"{d.name}에서 전사" };
                    l.gladiators.Remove(g.id);
                    l.registered?.Remove(g.id);
                    Career.LogCareer(s, g, $"{d.name} 깊은 곳에서 쓰러져 돌아오지 못했습니다", "death");
                    party.Remove(g);
                }
                if (!rng.Chance(win) || party.Count == 0) break;
                loot.AddRange(DungeonLogic.RollLoot(rng, d, floor == d.floors));
            }
            GearLogic.ArmoryOf(s, l).AddRange(loot);
            foreach (var g in party) g.fatigue = Math.Min(100, g.fatigue + 25);
        }

        // ── 8. 훈련: 지치면 쉬고, 아니면 스타일 기준 가장 약한 분야 ──

        static readonly string[] Focuses = { "physical", "weapon", "defense", "mental", "sense" };

        public static void TrainingPlan(Gladiator g)
        {
            if (g.fatigue > 60 || g.wear > 60) { g.training = new TrainingPlan { focus = "rest", intensity = "normal" }; return; }
            string best = "weapon";
            double low = double.MaxValue;
            foreach (var f in Focuses)
            {
                var t = Training.FocusTargets(g, f);
                double sum = 0, w = 0;
                foreach (var kv in t)
                {
                    double v = kv.Key.StartsWith("prof:") ? (g.prof.TryGetValue(kv.Key.Substring(5), out var p) ? p : 0) : g.attrs.TryGetValue(kv.Key, out var a) ? a : 0;
                    sum += v * kv.Value; w += kv.Value;
                }
                double avg = w > 0 ? sum / w : 20;
                // 방어는 방패를 들거나 회피형일 때만 우선한다
                if (f == "defense" && g.equipment.off == null) avg += 2;
                if (avg < low) { low = avg; best = f; }
            }
            g.training = new TrainingPlan { focus = best, intensity = g.fatigue > 35 ? "low" : "normal" };
        }
    }
}
