// FM식 규칙 (Unity판 전용): 4부 리그와 승강, 계약 협상 역제안과 '결정 전엔 못 넘어감', 받은 제안 역제안, 이적 명단, 도전장 제한.
using System.IO;
using System.Linq;
using Colosseum.Core;
using NUnit.Framework;

namespace Colosseum.Tests
{
    public class FmRulesTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            Data.LoadFrom(n => File.ReadAllText($"Assets/Resources/Data/{n}.json"));
            SkillBook.LoadFrom(n => File.ReadAllText($"Tables/{n}.csv"));
            NameBook.LoadFrom(n => File.ReadAllText($"Tables/{n}.csv"));
            FmRules.Enabled = true;
        }

        [Test]
        public void FourTiersEverywhereAndPromotion()
        {
            var s = World.NewGame(77, "aurelia", "시험", 4);
            Assert.AreEqual(4, s.ludi[s.playerLudusId].tier);
            foreach (var n in Data.Nations)
                for (int t = 1; t <= 4; t++) Assert.AreEqual(FmRules.TeamsPerTier, s.ludi.Values.Count(l => l.nation == n.id && l.tier == t), $"{n.id} {t}부");
            Assert.AreEqual(s.ludi.Count, s.ludi.Values.Select(l => l.name).Distinct().Count(), "루두스 이름이 겹치지 않는다");
            // 시즌을 끝내면 부마다 20개가 그대로 (3 올라가고 3 내려온다)
            var before = s.ludi.Values.ToDictionary(l => l.id, l => l.tier);
            s.day = Calendar.DaysPerSeason;
            Day.EndSeason(s, new Rng(5));
            foreach (var n in Data.Nations)
                for (int t = 1; t <= 4; t++) Assert.AreEqual(FmRules.TeamsPerTier, s.ludi.Values.Count(l => l.nation == n.id && l.tier == t));
            foreach (var n in Data.Nations)
                for (int t = 1; t < 4; t++)
                {
                    Assert.AreEqual(3, s.ludi.Values.Count(l => l.nation == n.id && before[l.id] == t + 1 && l.tier == t), $"{n.id} {t + 1}부에서 3팀 승격");
                    Assert.AreEqual(3, s.ludi.Values.Count(l => l.nation == n.id && before[l.id] == t && l.tier == t + 1), $"{n.id} {t}부에서 3팀 강등");
                }
            Assert.IsTrue(s.ludi.Values.All(l => l.seasons != null && l.seasons.Count == 1), "모든 루두스에 시즌 연혁");
        }

        /// <summary>FM 세계 단계 1: 부별 수준 차(1 > 2 > 3 > 4, 1부 최약 > 2부 평균), 부별 로스터, 정규 기간 같은 부끼리만 도전·같은 상대 제한, 휴식기 연습전은 다른 부도</summary>
        [Test]
        public void TwentyTeamWorldTierStrengthAndSameTierChallenges()
        {
            var s = World.NewGame(4242, "aurelia", "시험", 3);
            double Top5(Ludus l) => l.gladiators.Select(id => (double)GladiatorRules.ComputeCA(s.gladiators[id])).OrderByDescending(x => x).Take(5).Average();
            foreach (var n in Data.Nations)
            {
                var avg = new double[5];
                for (int t = 1; t <= 4; t++)
                {
                    var ls = s.ludi.Values.Where(l => l.nation == n.id && l.tier == t && !l.isPlayer).ToList();
                    Assert.IsTrue(ls.All(l => l.gladiators.Count == FmRules.AiRoster(t)), $"{n.id} {t}부 로스터 {FmRules.AiRoster(t)}");
                    avg[t] = ls.Average(Top5);
                }
                for (int t = 1; t < 4; t++) Assert.Greater(avg[t], avg[t + 1], $"{n.id} {t}부 주전이 {t + 1}부보다 강하다");
                double weakest1 = s.ludi.Values.Where(l => l.nation == n.id && l.tier == 1).Min(Top5);
                Assert.Greater(weakest1, avg[2], $"{n.id} 1부 최약 팀 > 2부 평균");
            }
            Assert.IsTrue(s.ludi.Values.All(l => l.crest == null || l.crest.StartsWith(l.nation + "_")), "문장은 자기 나라 것");

            var me = s.ludi[s.playerLudusId];
            s.day = 20;
            var same = s.ludi.Values.First(l => l.nation == me.nation && l.tier == me.tier && !l.isPlayer);
            var upper = s.ludi.Values.First(l => l.nation == me.nation && l.tier == me.tier - 1);
            Assert.AreEqual(ChallengeLogic.OtherTierMessage, ChallengeLogic.CanChallenge(s, me, upper));
            Assert.AreEqual(ChallengeLogic.OtherTierMessage, ChallengeLogic.CanChallengeOn(s, me, upper, 30));
            Assert.AreNotEqual(ChallengeLogic.OtherTierMessage, ChallengeLogic.CanChallengeOn(s, me, same, 30) ?? "");
            // 같은 상대에게는 시즌당 2번까지
            for (int i = 0; i < FmRules.SameOpponentPerSeason; i++)
                s.challenges.Add(new Challenge { id = $"CX{i}", from = me.id, to = same.id, status = "done", createdAt = Calendar.AbsDay(s.season, 15 + i) });
            StringAssert.Contains("2번까지", ChallengeLogic.CanChallenge(s, me, same));
            // 휴식기(정규 리그 뒤) 연습전은 부 제한 없음
            s.day = 160;
            Assert.AreNotEqual(ChallengeLogic.OtherTierMessage, ChallengeLogic.CanChallengeOn(s, me, upper, 165) ?? "");
        }

        /// <summary>FM 세계 단계 2: 같은 선수의 별이 우리 팀 수준에 따라 달라진다 (기준 = 우리 상위 5명 평균 CA = ★3.5, CA 20마다 ★1)</summary>
        [Test]
        public void StarsAreRelativeToOurTeam()
        {
            var low = World.NewGame(5151, "aurelia", "시험", 4);
            var high = World.NewGame(5151, "aurelia", "시험", 1);
            double refLow = StarScale.Reference(low), refHigh = StarScale.Reference(high);
            Assert.Greater(refHigh, refLow + 40, "1부 우리 팀이 4부 우리 팀보다 훨씬 강하다");
            Assert.AreEqual(3.5, StarScale.ForTeam(low, refLow), 0.5);
            Assert.AreEqual(4.5, StarScale.ForTeam(low, refLow + 20), 0.5);
            // 1부 주전급 선수(CA 165): 4부 팀에서는 ★5, 1부 팀에서는 그보다 낮다
            Assert.AreEqual(5, StarScale.ForTeam(low, 165));
            Assert.Less(StarScale.ForTeam(high, 165), 5);
            Assert.AreEqual(0.5, StarScale.ForTeam(high, 20), "범위 아래 끝");
            // 절대 별은 그대로 (웹판 비교)
            Assert.AreEqual(4, GladiatorRules.Stars(160));
            FmRules.Enabled = false;
            try { Assert.AreEqual(GladiatorRules.Stars(165), StarScale.ForTeam(low, 165)); }
            finally { FmRules.Enabled = true; }
        }

        /// <summary>FM 세계 단계 4: 학당 1단계에서도 큰 표본에 PA 180 이상이 나오고, 레벨이 오를수록 잦아진다</summary>
        [Test]
        public void YouthPotentialHasNoCapButBetterAcademiesHelp()
        {
            var rng = new Rng(31337);
            var n160 = new int[6]; var n180 = new int[6];
            const int N = 20000;
            for (int lvl = 1; lvl <= 5; lvl++)
                for (int i = 0; i < N; i++)
                {
                    int pa = Youth.RollPa(rng, lvl, 25);
                    Assert.LessOrEqual(pa, 200);
                    Assert.GreaterOrEqual(pa, 35);
                    if (pa >= 160) n160[lvl]++;
                    if (pa >= 180) n180[lvl]++;
                }
            Assert.Greater(n180[1], 0, "레벨 1에서도 PA 180 이상이 나온다");
            for (int lvl = 1; lvl < 5; lvl++)
            {
                Assert.Greater(n160[lvl + 1], n160[lvl], $"레벨 {lvl + 1}이 {lvl}보다 PA 160+가 잦다");
                Assert.Greater(n180[lvl + 1], n180[lvl], $"레벨 {lvl + 1}이 {lvl}보다 PA 180+가 잦다");
            }
            System.Console.WriteLine($"MEASURE youth PA160+ {string.Join(" / ", Enumerable.Range(1, 5).Select(l => $"{100.0 * n160[l] / N:0.0}%"))} · PA180+ {string.Join(" / ", Enumerable.Range(1, 5).Select(l => $"{100.0 * n180[l] / N:0.00}%"))}");
            // 실제 모래 시험 후보도 상한 없이 (학당 1단계라도 160 이상이 나올 수 있다)
            var s = World.NewGame(6060, "aurelia", "시험", 3);
            var me = s.ludi[s.playerLudusId];
            int best = 0;
            for (int i = 0; i < 300; i++) best = System.Math.Max(best, Youth.Trial(s, rng, me).Max(g => g.hidden.pa));
            Assert.Greater(best, FacilitiesLogic.AcademyPaCap(1), "옛 상한(120)을 넘는 후보가 나온다");
        }

        /// <summary>FM 세계 단계 3: 리그 수준을 한참 넘는 영입은 드물게만, 복합 상황이면 올라가고, 같은 CA라도 상황에 따라 요구 주급이 다르다</summary>
        [Test]
        public void SituationDrivesDemandsAndLowLeagueInterest()
        {
            var s = World.NewGame(7070, "aurelia", "시험", 4);
            var me = s.ludi[s.playerLudusId];
            // 상황 없음 기준: 다른 나라 1부 주전급, 쇠퇴기·불만 아님
            var stars = s.ludi.Values.Where(l => l.tier == 1 && l.nation == "dolgar").SelectMany(l => l.gladiators).Select(id => s.gladiators[id])
                .Where(g => SelfValue.ProperTier(g) == 1 && GladiatorRules.AgeStage(g) != "decline" && g.grievance == null).ToList();
            Assert.Greater(stars.Count, 50, "1부 주전급 표본");
            // 1,000회 표본: 선수·이적 기간을 바꿔 가며 굴린다 (판정은 열쇠마다 고정이라 같은 열쇠를 다시 굴리지 않는다)
            int ok = 0, okHard = 0, n = 0;
            for (int w = 0; n < 1000; w++)
                foreach (var g in stars)
                {
                    if (n >= 1000) break;
                    s.season = 1 + w;
                    s.leagueRefusals = null;
                    if (SelfValue.Interested(s, g, me)) ok++;
                    n++;
                }
            double plain = ok / 1000.0;
            Assert.Less(plain, 0.03, $"4부 팀의 1부 주전 영입 관심 {plain:P1} (상황 없음 기준 3% 미만)");
            // 복합 상황: 쇠퇴기 + 무소속 석 달 넘게
            var old = stars[0];
            old.age = Data.Race(old.race).age.primeEnd + 2;
            var owner = Career.LudusOf(s, old.id);
            owner.gladiators.Remove(old.id);
            s.freeAgents.Add(old.id);
            old.freeSince = Calendar.AbsDay(s.season, s.day) - 100;
            Assert.Greater(SelfValue.InterestChance(s, old, me), 0.05, "쇠퇴기 + 무소속 장기면 확률이 뚜렷이 오른다");
            for (int i = 0; i < 1000; i++) { s.season = 100 + i; s.leagueRefusals = null; if (SelfValue.Interested(s, old, me)) okHard++; }
            Assert.Greater(okHard / 1000.0, plain + 0.03, $"복합 상황 {okHard / 1000.0:P1} > 기준 {plain:P1}");
            // 같은 판정은 다시 굴려도 같고, 거절하면 그 기간엔 다시 못 묻는다
            s.season = 1; s.leagueRefusals = null;
            var x = stars[1];
            bool first = SelfValue.Interested(s, x, me);
            Assert.AreEqual(first, SelfValue.Interested(s, x, me));
            if (!first) Assert.IsFalse(SelfValue.Interested(s, x, me, 3), "거절 뒤엔 값을 올려도 그 기간엔 안 된다");
            // 같은 선수라도 상황에 따라 요구 주급이 다르다 (승률·불만·무소속)
            var y = stars[2];
            var buyer = s.ludi.Values.First(l => l.tier == 1 && l.nation == "aurelia" && !l.gladiators.Contains(y.id));
            y.record.wins = 2; y.record.losses = 2; y.grievance = null;
            double baseM = SelfValue.WageMult(s, y, buyer).mult;
            y.record.wins = 20; y.record.losses = 2;
            Assert.Greater(SelfValue.WageMult(s, y, buyer).mult, baseM, "승률이 높으면 더 바란다");
            y.record.wins = 2; y.record.losses = 20;
            Assert.Less(SelfValue.WageMult(s, y, buyer).mult, baseM, "승률이 낮으면 덜 바란다");
            y.record.wins = 2; y.record.losses = 2; y.grievance = "x";
            Assert.Less(SelfValue.WageMult(s, y, buyer).mult, baseM, "불만이면 이적 때 덜 바란다");
            double renewAngry = SelfValue.WageMult(s, y, Career.LudusOf(s, y.id), renew: true).mult;
            y.grievance = null;
            Assert.Greater(renewAngry, SelfValue.WageMult(s, y, Career.LudusOf(s, y.id), renew: true).mult, "불만이면 재계약 때 더 바란다");
            var m = SelfValue.WageMult(s, old, me).mult;
            Assert.LessOrEqual(m, SelfValue.MaxMult);
            Assert.GreaterOrEqual(m, SelfValue.MinMult);
        }

        /// <summary>FM 세계 단계 5: 같은 역할 여러 명 (상한까지), 효과는 1·½·¼로 합치고, 해고하면 돌아온다. 교관이 모자라면 효율이 떨어진다</summary>
        [Test]
        public void StaffStackByRoleAndFiringRestores()
        {
            var s = World.NewGame(8080, "aurelia", "시험", 1);
            var me = s.ludi[s.playerLudusId];
            var rng = new Rng(3);
            me.treasury = 1_000_000;
            Assert.GreaterOrEqual(StaffLogic.RoleCap(me, "weaponCoach"), 2, "1부는 교관을 여럿 둘 수 있다");
            var a = StaffLogic.MakeStaff(s, rng, "weaponCoach", 14, 1);
            var b = StaffLogic.MakeStaff(s, rng, "weaponCoach", 10, 1);
            s.staffMarket.Add(a.id); s.staffMarket.Add(b.id);
            double before = StaffLogic.CoachMultFor(s, me, "weapon");
            Assert.IsNull(StaffLogic.HireStaff(s, a.id, 10000, 0), "첫째 고용");
            double one = StaffLogic.CoachMultFor(s, me, "weapon");
            Assert.IsNull(StaffLogic.HireStaff(s, b.id, 10000, 0), "같은 역할 둘째 고용");
            double two = StaffLogic.CoachMultFor(s, me, "weapon");
            Assert.Greater(one, before);
            Assert.Greater(two, one, "둘째도 효과가 있다");
            Assert.Less(two - one, one - before + 1e-9, "둘째는 반만큼 (첫째보다 덜)");
            Assert.AreEqual(a.rating + b.rating * 0.5, StaffLogic.RatingOf(s, me, "weaponCoach"), 1e-9, "평점 합산 1·½");
            Assert.AreEqual(StaffLogic.CoachMultFor(s, me, "physical"), StaffLogic.CoachMultFor(s, me, "physical"), "다른 분야 그대로");
            StaffLogic.FireStaff(s, b.id);
            Assert.AreEqual(one, StaffLogic.CoachMultFor(s, me, "weapon"), 1e-9, "해고하면 효과가 돌아온다");
            // 상한
            int cap = StaffLogic.RoleCap(me, "youthHead");
            Assert.AreEqual(1, cap);
            var y1 = StaffLogic.MakeStaff(s, rng, "youthHead", 12, 1); var y2 = StaffLogic.MakeStaff(s, rng, "youthHead", 12, 1);
            s.staffMarket.Add(y1.id); s.staffMarket.Add(y2.id);
            Assert.IsNull(StaffLogic.HireStaff(s, y1.id, 10000, 0));
            Assert.IsNotNull(StaffLogic.HireStaff(s, y2.id, 10000, 0), "유소년 책임자는 한 명");
            Assert.Greater(Youth.YouthHeadBonus(s, me), 0);
            // 선수가 교관 수보다 너무 많으면 효율이 떨어진다
            double full = StaffLogic.CoachMultFor(s, me, "weapon");
            for (int i = 0; i < 40; i++) me.gladiators.Add(me.gladiators[0]);
            Assert.Less(StaffLogic.CoachMultFor(s, me, "weapon"), full);
        }

        /// <summary>FM 세계 단계 6: 위탁품은 실제 루두스가 사야 팔린다. 팔리면 늘 어느 루두스 창고나 장착 칸에 있고, 너무 비싸면 안 팔리고, 중개인이 있으면 더 빨리 팔린다</summary>
        [Test]
        public void ConsignedGearGoesToARealBuyer()
        {
            int Count(GameState st, string item) =>
                st.ludi.Values.Sum(l => (l.armory?.Count(x => x == item) ?? 0) + l.gladiators.Count(id => st.gladiators.TryGetValue(id, out var g) && g.alive && Equipment.Slots.Any(sl => g.equipment.Get(sl) == item)))
                + (st.consignments?.Count(c => c.item == item) ?? 0);
            // 시장 최고급 투구 하나를 직접 위탁: 적당한 값이면 몇 주 안에 누군가 산다
            var s = World.NewGame(9090, "aurelia", "시험", 2);
            var me = s.ludi[s.playerLudusId];
            string item = "crested_helm+2";
            GearLogic.ArmoryOf(s).Add(item);
            int before = Count(s, item);
            Assert.IsNull(GearLogic.Consign(s, item, GearLogic.ItemPrice(item) * 0.6, false));
            Assert.AreEqual(before, Count(s, item), "내놓아도 사라지지 않는다");
            double money = me.treasury;
            int days = 0;
            for (; days < 28 && s.consignments.Count > 0; days++) { s.day = 20 + days; GearLogic.DailyConsignments(s, new Rng(1)); }
            Assert.AreEqual(0, s.consignments.Count, "적당한 값이면 4주 안에 팔린다");
            Assert.AreEqual(before, Count(s, item), "판 장비는 산 루두스 창고나 장착 칸에 있다");
            Assert.IsTrue(s.ludi.Values.Any(l => !l.isPlayer && ((l.armory?.Contains(item) ?? false) || l.gladiators.Any(id => s.gladiators[id].equipment.helmet == item))), "AI 루두스가 가졌다");
            Assert.Greater(me.treasury, money, "돈은 우리에게");
            Assert.IsNotNull(GearLogic.SellItem(s, item), "시스템 즉시 판매는 없다");
            // 너무 비싸면 아무도 안 산다
            GearLogic.ArmoryOf(s).Add(item);
            GearLogic.Consign(s, item, GearLogic.ItemPrice(item) * 5, false);
            for (int d = 0; d < 28; d++) { s.day = 60 + d; GearLogic.DailyConsignments(s, new Rng(1)); }
            Assert.AreEqual(1, s.consignments.Count, "사려는 곳이 없는 값이면 안 팔린다");
            // 중개인: 직접보다 빨리 팔린다 (같은 값)
            int DaysToSell(bool agent)
            {
                var t = World.NewGame(9191, "aurelia", "시험", 2);
                var m = t.ludi[t.playerLudusId];
                if (agent)
                {
                    var a = StaffLogic.MakeStaff(t, new Rng(2), "agent", 16, 2);
                    m.staff.Add(a.id);
                }
                GearLogic.ArmoryOf(t).Add(item);
                GearLogic.Consign(t, item, GearLogic.ItemPrice(item) * 0.7, agent);
                for (int d = 0; d < 56; d++) { t.day = 20 + d; GearLogic.DailyConsignments(t, new Rng(1)); if (t.consignments.Count == 0) return d + 1; }
                return 99;
            }
            int direct = DaysToSell(false), viaAgent = DaysToSell(true);
            System.Console.WriteLine($"MEASURE gear sale days direct {direct} agent {viaAgent}");
            Assert.LessOrEqual(viaAgent, direct, "중개인이 있으면 더 빨리 (또는 같이) 팔린다");
        }

        /// <summary>FM 세계 단계 7: AI 차례에 식솔 고용·장비 착용(불법 장비 없음)·던전 요약 원정(귀환하면 장비 창고로, 원정 중엔 출전 불가)</summary>
        [Test]
        public void AiTurnHiresEquipsAndRunsDungeons()
        {
            var s = World.NewGame(1212, "aurelia", "시험", 2);
            var rng = new Rng(4);
            var l = s.ludi.Values.First(x => !x.isPlayer && x.tier == 1);
            l.treasury = 500000;
            int staffBefore = l.staff.Count;
            AiBrain.Turn(s, rng, l);
            Assert.Greater(l.staff.Count, staffBefore, "돈이 넉넉하면 식솔을 들인다");
            // 창고의 좋은 장비는 주전에게
            GearLogic.ArmoryOf(s, l).Add("crested_helm+2");
            AiBrain.Turn(s, rng, l);
            Assert.IsTrue(l.gladiators.Any(id => s.gladiators[id].equipment.helmet == "crested_helm+2"), "주전이 입는다");
            foreach (var id in l.gladiators)
            {
                var g = s.gladiators[id];
                foreach (var sl in Equipment.Slots)
                {
                    var it = g.equipment.Get(sl);
                    // 쌍검 스타일의 기본 보조 무기는 스타일 규칙 (생성기가 준 것)
                    if (sl == "off" && it == Data.Style(g.style).loadout.off) continue;
                    if (!string.IsNullOrEmpty(it) && it != "cestus" && it != "loincloth") Assert.IsNull(GearLogic.CannotEquip(g, sl, it), $"{g.name} {sl} {it}");
                }
            }
            // 던전: 시즌 중엔 상위 7명을 남기고 벤치만 보낸다. 리그가 끝나면 주전도 간다. 정해진 날 돌아온다
            s.day = 50;
            AiBrain.Turn(s, rng, l);
            if (l.aiExpedition != null)
            {
                var top = l.gladiators.Select(id => s.gladiators[id]).Where(g => g.alive && GladiatorRules.AgeStage(g) != "youth").OrderByDescending(GladiatorRules.ComputeCA).Take(7).Select(g => g.id).ToList();
                Assert.IsFalse(l.aiExpedition.party.Any(top.Contains), "정규 리그 중엔 주전·교체는 남긴다");
                Assert.AreEqual(l.id, DungeonLogic.Sites(s).First(x => x.id == l.aiExpedition.dungeon).claimedBy, "AI도 자리를 차지한다");
            }
            var low = s.ludi.Values.First(x => !x.isPlayer && x.tier == 4 && x.nation == "aurelia");
            low.treasury = 500000;
            s.day = 140;
            AiBrain.Turn(s, rng, low);
            Assert.IsNotNull(low.aiExpedition, "휴식기엔 원정");
            var party = low.aiExpedition.party.ToList();
            Assert.IsTrue(party.All(id => s.gladiators[id].deployment != null), "원정 중엔 출전 불가");
            s.day = 140 + DungeonLogic.GetDungeon(low.aiExpedition.dungeon).days;
            AiBrain.Daily(s, rng);
            Assert.IsNull(low.aiExpedition, "돌아왔다");
            Assert.IsTrue(party.All(id => s.gladiators[id].deployment == null), "돌아오면 다시 뛸 수 있다");
            // 훈련 계획은 늘 아는 분야
            foreach (var id in low.gladiators) { AiBrain.TrainingPlan(s.gladiators[id]); Assert.IsTrue(Training.FocusNames.ContainsKey(s.gladiators[id].training.focus)); }
        }

        /// <summary>FM 세계 단계 8: 시즌 시작에 모든 루두스 목표·스폰서, 우리는 기한까지 안 고르면 첫 제안, 시즌 끝 판정(달성 보너스·미달 누적)</summary>
        [Test]
        public void PatronGoalsAndSponsors()
        {
            var s = World.NewGame(1313, "aurelia", "시험", 3);
            var me = s.ludi[s.playerLudusId];
            Assert.IsTrue(s.ludi.Values.All(l => l.boardGoal != null && l.boardGoal.season == 1), "모두 목표");
            Assert.IsTrue(s.ludi.Values.Where(l => !l.isPlayer).All(l => l.sponsor != null), "AI는 스폰서를 바로 맺는다");
            Assert.IsNull(me.sponsor);
            Assert.AreEqual(3, me.sponsorOffers.Count);
            s.day = Patrons.SponsorPickUntil + 1;
            Patrons.Daily(s);
            Assert.IsNotNull(me.sponsor, "기한이 지나면 첫 제안");
            double before = me.treasury;
            Economy.Payday(s, me);
            Assert.IsTrue(s.ledger.Any(e => e.desc.Contains("스폰서")), "급료일에 스폰서 돈");
            // 판정: 목표 순위 안이면 달성, 아니면 미달 누적
            var ranks = s.ludi.Values.ToDictionary(l => l.id, l => l == me ? 1 : 20);
            Patrons.SeasonEnd(s, ranks);
            Assert.AreEqual(true, me.boardGoal.met);
            Assert.AreEqual(0, me.patronFails ?? 0);
            var ai = s.ludi.Values.First(l => !l.isPlayer && l.boardGoal.kind != "survive");
            Assert.AreEqual(false, ai.boardGoal.met);
            Assert.AreEqual(1, ai.patronFails);
            Assert.Less(Patrons.SupportMult(ai), 1, "미달이면 다음 시즌 지원이 준다");
        }

        [Test]
        public void ContractCounterBlocksUntilAnswered()
        {
            var s = World.NewGame(78, "aurelia", "시험", 2);
            var rng = new Rng(9);
            s.ludi[s.playerLudusId].treasury = 50000;
            var g = s.freeAgents.Select(id => s.gladiators[id]).First(x => !Economy.FreeAgentDemand(s, x).refuse);
            var d = Economy.FreeAgentDemand(s, g);
            // 바라는 것의 80%만 주급으로: 역제안이 와야 한다
            var res = Economy.HaggleFreeAgentTerms(s, rng, g.id, new ContractTerms { wage = System.Math.Round(d.wage * 0.8), seasons = Contracts.PreferredSeasons(g) });
            Assert.AreEqual("sent", res.status);
            for (int i = 0; i < 3 && s.decisions == null && s.freeAgents.Contains(g.id); i++) { foreach (var o in Transfers.OpenOffers(s)) Transfers.RejectOffer(s, o.id); Day.AdvanceDay(s); }
            if (!s.freeAgents.Contains(g.id)) Assert.Inconclusive("경쟁 루두스가 먼저 데려갔다");
            Assert.IsNotNull(s.decisions, "역제안이 결정할 일로 남는다");
            var dec = s.decisions[0];
            Assert.IsNotNull(dec.counterTerms);
            Assert.Greater(dec.counterTerms.wage, d.wage * 0.8);
            Assert.IsTrue(Day.Blockers(s).Any(b => b.Contains("협상")));
            int day = s.day;
            Day.AdvanceDay(s);
            Assert.AreEqual(day, s.day, "결정 전에는 다음 날로 못 간다");
            // 상대 조건 그대로 다시 내면 서명한다 (승리 수당까지 얹어서)
            var t = dec.counterTerms.Clone();
            t.winBonus = 20;
            Economy.HaggleFreeAgentTerms(s, rng, g.id, t);
            Assert.IsNull(s.decisions);
            for (int i = 0; i < 3 && s.freeAgents.Contains(g.id); i++) { foreach (var o in Transfers.OpenOffers(s)) Transfers.RejectOffer(s, o.id); Day.AdvanceDay(s); }
            Assert.IsTrue(s.ludi[s.playerLudusId].gladiators.Contains(g.id));
            Assert.AreEqual(20, g.contract.winBonus);
        }

        [Test]
        public void OfferCounterAndTransferList()
        {
            var s = World.NewGame(79, "aurelia", "시험", 2);
            var rng = new Rng(3);
            var me = s.ludi[s.playerLudusId];
            var g = s.gladiators[me.gladiators[0]];
            Assert.IsNull(Transfers.ListForTransfer(s, g.id, 1000));
            Assert.IsTrue(Transfers.Listed(s, g.id));
            // 제안을 하나 만들어 역제안: 상대 최대보다 낮으면 받아들인다
            var buyer = s.ludi.Values.First(l => !l.isPlayer && l.nation == s.nation);
            buyer.treasury = 100000;
            var o = new Offer { id = "O1", fromLudus = buyer.id, gladiatorId = g.id, fee = 500, maxFee = 900, status = "open", expiresAt = 9999 };
            Transfers.TransferOf(s).offers.Add(o);
            Assert.AreEqual(1, Transfers.OpenOffers(s).Count);
            Assert.IsTrue(Day.Blockers(s).Any(b => b.Contains("이적 제안")));
            Assert.IsNull(Transfers.CounterOffer(s, "O1", 800));
            Assert.AreEqual(0, Transfers.OpenOffers(s).Count);
            Day.AdvanceDay(s);
            Assert.IsFalse(me.gladiators.Contains(g.id), "800 ≤ 900이면 팔린다");
            Assert.IsTrue(buyer.gladiators.Contains(g.id));
            Assert.IsFalse(Transfers.Listed(s, g.id));
        }

        [Test]
        public void ChallengeNeedsSomeoneToReceive()
        {
            var s = World.NewGame(80, "aurelia", "시험", 2);
            s.day = 20;
            var me = s.ludi[s.playerLudusId];
            var target = s.ludi.Values.First(l => !l.isPlayer && l.nation == s.nation && l.tier == me.tier);
            Assert.IsNull(ChallengeLogic.CanChallenge(s, me, target));
            // 상대 검투사를 모두 다치게 하면 보낼 수 없다
            foreach (var id in target.gladiators) s.gladiators[id].injury = new Injury { severity = "serious", daysLeft = 10, desc = "시험" };
            Assert.IsNotNull(ChallengeLogic.CanChallenge(s, me, target));
        }

        [Test]
        public void WarBlocksTransfersAndMakesCupSudden()
        {
            var s = World.NewGame(82, "aurelia", "시험", 1);
            s.day = 1;
            var enemy = s.ludi.Values.First(l => l.nation == "grommar" && l.tier == 1);
            var g = s.gladiators[enemy.gladiators[0]];
            s.ludi[s.playerLudusId].treasury = 100000;
            Assert.IsNull(Transfers.WarBlock(s, enemy));
            s.wars.Add(new War { id = "W1", a = "aurelia", b = "grommar", startedAt = 0, endsAt = 9999 });
            Assert.IsTrue(WarLogic.AtWar(s, "grommar", "aurelia"));
            var res = Transfers.HaggleFee(s, new Rng(1), g.id, 1000);
            Assert.AreEqual("error", res.status);
            StringAssert.Contains("전쟁", res.line);
            // 컵: 전쟁 중인 두 나라 루두스가 만나면 사투
            var mine = s.ludi[s.playerLudusId];
            var tie = new CupTie { id = "T1", stage = "group", home = mine.id, away = enemy.id, day = s.day };
            Assert.AreEqual("sudden", CupLogic.TieRule(s, tie));
            var other = s.ludi.Values.First(l => l.nation == "sylvaren");
            Assert.AreEqual("missio", CupLogic.TieRule(s, new CupTie { id = "T2", home = mine.id, away = other.id }));
        }

        [Test]
        public void DatedChallengeAndFriendly()
        {
            var s = World.NewGame(83, "aurelia", "시험", 2);
            var rng = new Rng(5);
            var me = s.ludi[s.playerLudusId];
            var g = s.gladiators[me.gladiators[0]];
            // 휴식기(시즌 초)는 연습전: 훈련전으로 바뀐다
            var opp = s.ludi.Values.First(l => !l.isPlayer && l.nation == s.nation && ChallengeLogic.CanChallengeOn(s, me, l, 6) == null);
            var f = ChallengeLogic.TryCreateChallengeOn(s, me, opp, g.id, "missio", 6, out var err);
            Assert.IsNull(err);
            Assert.AreEqual(true, f.friendly);
            Assert.AreEqual("training", f.rule);
            Assert.IsNotNull(ChallengeLogic.DateBlock(s, 2), "너무 가까운 날은 못 고른다");
            // 리그 기간 날짜는 리그 도전장
            var g2 = s.gladiators[me.gladiators[1]];
            var opp2 = s.ludi.Values.First(l => !l.isPlayer && l.nation == s.nation && l.id != opp.id && ChallengeLogic.CanChallengeOn(s, me, l, 20) == null);
            var lc = ChallengeLogic.TryCreateChallengeOn(s, me, opp2, g2.id, "missio", 20, out err);
            Assert.IsNull(err);
            Assert.IsNull(lc.friendly);
            double rp = me.rp, fame = g.popularity;
            double played = me.matchesThisSeason;
            RunTo(s, rng, 1, 7);
            if (f.status == "declined") Assert.Inconclusive("상대가 연습전을 거절했다");
            Assert.AreEqual("done", f.status);
            Assert.AreEqual(6, f.matchAt);
            Assert.AreEqual(rp, me.rp, "연습전은 RP가 오가지 않는다");
            Assert.AreEqual(played, me.matchesThisSeason);
            Assert.AreEqual(fame, g.popularity, "연습전은 명성이 없다");
            if (lc.status == "scheduled") Assert.AreEqual(20, lc.matchAt);
        }

        [Test]
        public void RejectingBiggerClubUpsetsGladiator()
        {
            var s = World.NewGame(85, "aurelia", "시험", 4);
            var me = s.ludi[s.playerLudusId];
            var g = s.gladiators[me.gladiators[0]];
            g.hidden.ambition = 18;
            g.hidden.loyalty = 2;
            g.morale = 70;
            var big = s.ludi.Values.First(l => l.nation == s.nation && l.tier == 1);
            Transfers.TransferOf(s).offers.Add(new Offer { id = "O9", fromLudus = big.id, gladiatorId = g.id, fee = 500, maxFee = 900, status = "open", expiresAt = 9999 });
            double value = Valuation.MarketValue(s, g);
            Transfers.RejectOffer(s, "O9");
            Assert.Less(Valuation.MarketValue(s, g), value, "불만이면 몸값이 내려간다");
            // 계약이 짧게 남을수록 싸다
            var keep = g.contract;
            g.contract = new GladiatorContract { untilSeason = s.season + 3 };
            double longV = Valuation.MarketValue(s, g);
            g.contract = new GladiatorContract { untilSeason = s.season };
            Assert.Less(Valuation.MarketValue(s, g), longV);
            g.contract = keep;
            Assert.Less(g.morale, 70, "1부 루두스 제안을 막으면 실망한다");
            Assert.IsNotNull(g.grievance);
            Assert.LessOrEqual(70 - g.morale, 12, "과하지 않게");
            Assert.AreEqual("transfer", g.grievanceKind);
            Contracts.Apply(s, g, Contracts.TermsOf(g) ?? new ContractTerms { wage = g.wage });
            Assert.IsNotNull(g.grievance, "이적 불만은 재계약으로 풀리지 않는다");
            Assert.IsNull(Transfers.ListForTransfer(s, g.id, 1000));
            Grievances.Daily(s, new Rng(1));
            Assert.IsNull(g.grievance, "이적 명단에 올리면 풀린다");
            // 명문일수록 덜 요구한다
            Assert.Less(Reputation.DemandFor(40, 100, 70).wage, Reputation.DemandFor(40, 100, 45).wage);
        }

        /// <summary>이적 시장 첫날, 돈 넉넉한 상태에서 우리 나라 다른 루두스 검투사 하나</summary>
        static (GameState s, Ludus me, Gladiator g, Ludus owner) TransferSetup(double seed)
        {
            var s = World.NewGame(seed, "aurelia", "시험", 2);
            s.day = 2;
            foreach (var l in s.ludi.Values) l.treasury = 5000000;
            var me = s.ludi[s.playerLudusId];
            var owner = s.ludi.Values.First(l => !l.isPlayer && l.nation == s.nation && l.tier == 2);
            var g = owner.gladiators.Select(id => s.gladiators[id]).First(x => x.alive);
            return (s, me, g, owner);
        }

        /// <summary>보낸 제안에 답이 올 때까지 날짜를 넘긴다. noRival이면 경쟁자를 치운다</summary>
        static void AwaitReply(GameState s, Rng rng, bool noRival = true)
        {
            for (int i = 0; i < 5 && s.bids != null && s.bids.Count > 0; i++)
            {
                if (noRival) s.suitors = null;
                s.day++;
                Negotiations.Daily(s, rng);
            }
        }

        [Test]
        public void BidAtAskingPriceAlwaysReachesNegotiation()
        {
            var (s, me, g, owner) = TransferSetup(90);
            var rng = new Rng(3);
            // 낮게 불러 협상이 깨져 있어도
            Haggling.Open(s, rng, Transfers.FeeKey(g.id), 1000, 0.04, 0.1);
            Haggling.Get(s, Transfers.FeeKey(g.id)).lockedUntil = Calendar.AbsDay(s.season, s.day) + 7;
            double asking = Transfers.AskingFor(s, g, owner);
            Assert.AreEqual("locked", Transfers.HaggleFee(s, rng, g.id, asking * 0.5).status, "낮은 값은 듣지 않는다");
            Assert.AreEqual("sent", Transfers.HaggleFee(s, rng, g.id, asking).status, "부른 값이면 다시 듣는다");
            // 경쟁자가 더 높게 불러도 우리 값이 부른 값 이상이면 협상으로 간다
            var other = s.ludi.Values.First(l => !l.isPlayer && l != owner && l.nation == s.nation);
            Suitor(s, g, other, asking * 3, 50);
            AwaitReply(s, rng, false);
            Assert.AreEqual(owner, Career.LudusOf(s, g.id), "아직 안 옮겼다");
            Assert.IsTrue(Transfers.TransferOf(s).deals.Any(d => d.gladiatorId == g.id), "이적료 합의가 생긴다");
        }

        /// <summary>AI 루두스가 이 검투사를 노리게 한다</summary>
        static void Suitor(GameState s, Gladiator g, Ludus club, double fee, double wage)
        {
            s.suitors ??= new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<RivalBid>>();
            if (!s.suitors.TryGetValue(g.id, out var l)) s.suitors[g.id] = l = new System.Collections.Generic.List<RivalBid>();
            l.Add(new RivalBid { ludusId = club.id, fee = fee, wage = wage, until = Calendar.AbsDay(s.season, s.day) + 20 });
        }

        [Test]
        public void AiClubsPickTargetsThemselvesAndCompeteOnlyWhenOverlapping()
        {
            var (s, me, g, owner) = TransferSetup(94);
            var rng = new Rng(6);
            Transfers.HaggleFee(s, rng, g.id, Transfers.AskingFor(s, g, owner));
            Assert.IsNull(s.suitors, "우리가 제안한다고 경쟁이 붙지는 않는다");

            // AI는 이적 시장 동안 스스로 대상을 골라 실제로 데려간다
            var (s2, me2, _, _) = TransferSetup(95);
            var rng2 = new Rng(7);
            var homeOf = s2.gladiators.Values.ToDictionary(x => x.id, x => Career.LudusOf(s2, x.id)?.id);
            bool picked = false;
            for (int day = 1; day <= 14; day++)
            {
                s2.day = day;
                Transfers.DailyTransfers(s2, rng2);
                picked |= s2.suitors != null;
            }
            for (int day = 15; day <= 24; day++) { s2.day = day; Transfers.DailyTransfers(s2, rng2); }
            Assert.IsTrue(picked, "AI가 영입 대상을 고른다");
            var moved = s2.gladiators.Values.Where(x => x.alive && Career.LudusOf(s2, x.id) != null && homeOf.TryGetValue(x.id, out var h) && h != null && h != Career.LudusOf(s2, x.id).id).ToList();
            Assert.Greater(moved.Count, 0, "AI끼리 실제로 이적한다");
            Assert.IsTrue(moved.All(x => !Career.LudusOf(s2, x.id).isPlayer && homeOf[x.id] != me2.id), "우리 검투사는 받은 제안으로만 떠난다");
            Assert.IsTrue(moved.All(x => x.history[x.history.Count - 1].fee > 0), "경력에 이적료가 남는다");
            Assert.IsTrue(moved.All(x => s2.suitors == null || !s2.suitors.ContainsKey(x.id)));

            // 겹치면: 우리가 협상 중이어도 AI가 먼저 데려갈 수 있다
            var (s3, me3, g3, owner3, deal3, rng3) = AgreedFee(96);
            var rival3 = s3.ludi.Values.First(l => !l.isPlayer && l != owner3 && l.nation == s3.nation);
            Suitor(s3, g3, rival3, deal3.fee, 100);
            s3.suitors[g3.id][0].until = Calendar.AbsDay(s3.season, s3.day);
            Negotiations.DailyAiMarket(s3, rng3);
            Assert.AreEqual(rival3, Career.LudusOf(s3, g3.id));
            Assert.IsEmpty(Transfers.TransferOf(s3).deals.Where(d => d.gladiatorId == g3.id), "우리 합의는 사라진다");
        }

        /// <summary>이적료 합의까지 끝낸 상태</summary>
        static (GameState s, Ludus me, Gladiator g, Ludus owner, Deal deal, Rng rng) AgreedFee(double seed)
        {
            var (s, me, g, owner) = TransferSetup(seed);
            var rng = new Rng((uint)seed);
            Transfers.HaggleFee(s, rng, g.id, Transfers.AskingFor(s, g, owner));
            AwaitReply(s, rng);
            return (s, me, g, owner, Transfers.TransferOf(s).deals.First(d => d.gladiatorId == g.id), rng);
        }

        [Test]
        public void TermsNegotiateOnTheSpotUntilPatienceRunsOut()
        {
            var (s, me, g, owner, deal, rng) = AgreedFee(91);
            double asking = deal.fee;
            var first = Transfers.HaggleTransferTerms(s, rng, deal.id, new ContractTerms { wage = System.Math.Round(Transfers.TransferDemand(s, g).wage * 0.7), seasons = 2 });
            Assert.AreEqual("countered", first.status, "그 자리에서 역제안이 온다");
            Assert.IsNotNull(first.terms);
            Assert.IsNull(s.bids, "답을 기다리지 않는다");
            Assert.IsNull(s.decisions?.FirstOrDefault(d => d.gladiatorId == g.id), "결정할 일로 막지 않는다");
            Assert.Less(Haggling.Get(s, Transfers.DealKey(deal.id)).patience, 100, "인내심이 준다");
            HaggleResult last = first;
            for (int i = 0; i < 20 && last.status == "countered"; i++) last = Transfers.HaggleTransferTerms(s, rng, deal.id, new ContractTerms { wage = 1, seasons = 1 });
            Assert.AreEqual("walked", last.status, "인내심이 바닥나면 끝난다");
            Assert.AreEqual(owner, Career.LudusOf(s, g.id), "영입 제안이 없으면 지금 팀에 남는다");
            Assert.IsEmpty(Transfers.TransferOf(s).deals.Where(d => d.gladiatorId == g.id));
            Assert.IsFalse(Transfers.MovedThisWindow(s, g), "옮기지 않았으니 이적 횟수는 그대로");
            Assert.AreEqual("error", Transfers.HaggleFee(s, rng, g.id, asking * 2).status, "한동안 우리와 이야기하지 않는다");
            s.day += Haggling.LockDays + 1;
            Assert.AreEqual("sent", Transfers.HaggleFee(s, rng, g.id, Transfers.AskingFor(s, g, owner)).status, "기한이 지나면 다시 된다");

            // 노리던 루두스가 여럿이면 그중 끌리는 곳으로 간다
            var (s2, me2, g2, owner2, deal2, rng2) = AgreedFee(97);
            var clubs = s2.ludi.Values.Where(l => !l.isPlayer && l != owner2 && l.nation == s2.nation).Take(2).ToList();
            var w2 = Transfers.TransferBaseWage(s2, g2, clubs[1].tier);
            Suitor(s2, g2, clubs[0], deal2.fee, 1);
            Suitor(s2, g2, clubs[1], deal2.fee, w2 * 20);
            last = new HaggleResult { status = "countered" };
            for (int i = 0; i < 20 && last.status == "countered"; i++) last = Transfers.HaggleTransferTerms(s2, rng2, deal2.id, new ContractTerms { wage = 1, seasons = 1 });
            Assert.AreEqual("walked", last.status);
            Assert.AreEqual(clubs[1], Career.LudusOf(s2, g2.id), "조건이 좋은 쪽으로 간다");
            Assert.AreEqual(deal2.fee, g2.history[g2.history.Count - 1].fee, "경력에 이적료가 남는다");
            Assert.IsTrue(Transfers.MovedThisWindow(s2, g2));
            Assert.AreEqual("error", Transfers.HaggleFee(s2, rng2, g2.id, 999999).status, "한 기간에 한 번만 옮긴다");
            s2.day = 80;
            Assert.IsFalse(Transfers.MovedThisWindow(s2, g2), "다음 이적 시장에는 다시 된다");
        }

        [Test]
        public void AgreedTermsWaitThenGladiatorPicksClub()
        {
            // 경쟁자가 없으면 고민한 뒤 우리에게 온다
            var (s, me, g, owner, deal, rng) = AgreedFee(92);
            var d0 = Transfers.TransferDemand(s, g);
            Assert.IsFalse(d0.refuse, "이적료가 합의되면 협상 자체는 거부하지 않는다");
            var ok = new ContractTerms { wage = System.Math.Round(d0.wage * 1.3), seasons = Contracts.PreferredSeasons(g) };
            Assert.AreEqual("sent", Transfers.HaggleTransferTerms(s, rng, deal.id, ok).status);
            Assert.AreEqual(owner, Career.LudusOf(s, g.id), "받아들여도 바로 오지 않고 고민한다");
            Assert.AreEqual(true, Negotiations.Pending(s, "terms", deal.id).agreed);
            AwaitReply(s, rng);
            Assert.AreEqual(me, Career.LudusOf(s, g.id));
            Assert.IsTrue(Transfers.MovedThisWindow(s, g), "우리에게 와도 이번 기간에는 다시 못 판다");

            // 더 좋은 조건의 루두스가 있으면 그쪽을 고른다
            var (s2, me2, g2, owner2, deal2, rng2) = AgreedFee(93);
            var d2 = Transfers.TransferDemand(s2, g2);
            Assert.AreEqual("sent", Transfers.HaggleTransferTerms(s2, rng2, deal2.id, new ContractTerms { wage = System.Math.Round(d2.wage * 1.05), seasons = Contracts.PreferredSeasons(g2) }).status);
            var other = s2.ludi.Values.First(l => !l.isPlayer && l != owner2 && l.nation == s2.nation);
            var weak = s2.ludi.Values.First(l => !l.isPlayer && l != owner2 && l != other && l.nation == s2.nation);
            Suitor(s2, g2, weak, deal2.fee, 1);
            Suitor(s2, g2, other, deal2.fee, d2.wage * 50);
            AwaitReply(s2, rng2, false);
            Assert.AreEqual(other, Career.LudusOf(s2, g2.id), "원하는 루두스를 골라 간다");
        }

        [Test]
        public void AttributeChangesShowTrendForFourWeeks()
        {
            var s = World.NewGame(98, "aurelia", "시험", 4);
            var g = s.gladiators[s.ludi[s.playerLudusId].gladiators[0]];
            g.attrs["str"] = 10;
            g.attrs["agi"] = 10;
            Training.Today = 100;
            Training.Bump(g, "str", 1);
            Training.Bump(g, "agi", -1);
            Assert.AreEqual(1, Training.TrendOf(g, "str", 100), "오르면 ▲");
            Assert.AreEqual(-1, Training.TrendOf(g, "agi", 110), "내리면 ▼");
            Assert.AreEqual(0, Training.TrendOf(g, "end", 100), "안 움직였으면 없음");
            Assert.AreEqual(0, Training.TrendOf(g, "str", 100 + Training.TrendDays + 1), "4주가 지나면 사라진다");
            Assert.AreEqual(1, Training.TrendOf(SaveSystem.FromJson(SaveSystem.ToJson(s)).gladiators[g.id], "str", 100), "저장해도 남는다");
        }

        /// <summary>던전 수준에 target만큼 더한 원정대 다섯을 보내 (원정마다 죽은 비율, 완주 비율)</summary>
        static (double death, double clear) RunParties(string did, double plus, int runs) => RunParties(did, plus, runs, null);

        static (double death, double clear) RunParties(string did, double plus, int runs, System.Collections.Generic.List<double> predicted)
        {
            int died = 0, sent = 0, cleared = 0;
            var d = DungeonLogic.GetDungeon(did);
            for (int i = 0; i < runs; i++)
            {
                var s = World.NewGame(300 + i, "aurelia", "시험", 2);
                var me = s.ludi[s.playerLudusId];
                s.dungeonRotation = DungeonLogic.Dungeons.Select(x => x.id).ToList();
                s.dungeonSites = new System.Collections.Generic.List<DungeonSite> { new DungeonSite { id = did } };
                var rng = new Rng((uint)(11 + i));
                var party = new System.Collections.Generic.List<string>();
                for (int k = 0; k < 5; k++)
                {
                    var g = GladiatorGen.Generate(rng, new GenOptions { id = $"P{i}_{k}", nation = "aurelia", race = "human", status = "free", target = d.level * DungeonLogic.FoeLevelMult + plus, age = 25 });
                    s.gladiators[g.id] = g;
                    me.gladiators.Add(g.id);
                    party.Add(g.id);
                }
                sent += party.Count;
                if (predicted != null)
                {
                    var o = DungeonLogic.Estimate(d, party.Select(id => s.gladiators[id]).ToList());
                    predicted[0] += 1 - o.survive; predicted[1] += o.clear;
                }
                Assert.IsNull(DungeonLogic.StartExpedition(s, did, party.ToList(), "focus"));
                s.day += d.days;
                DungeonLogic.DailyDungeons(s, rng);
                if (DungeonLogic.ExpeditionsOf(s)[0].status == "done") cleared++;
                died += party.Count(id => !s.gladiators[id].alive);
            }
            return ((double)died / sent, (double)cleared / runs);
        }

        /// <summary>던전 자리(위험도)마다 실제 전투 결과와 화면 어림(Estimate)을 견준다</summary>
        [Test]
        public void DungeonOddsMatchRealRuns()
        {
            double errDeath = 0, errClear = 0;
            int n = 0;
            foreach (var site in new[] { "sewer@2", "catacomb@5", "labyrinth@7", "abyss@9" })
            {
                var d = DungeonLogic.GetDungeon(site);
                double foeTarget = DungeonLogic.FloorFoeCa(d, 0) / 200 * 19 + 1;
                foreach (double plus in new[] { 1.0, 3, 5, 7 })
                {
                    var pred = new System.Collections.Generic.List<double> { 0, 0 };
                    int runs = 30;
                    var r = RunParties(site, foeTarget - d.level * DungeonLogic.FoeLevelMult + plus, runs, pred);
                    double pd = pred[0] / runs, pc = pred[1] / runs;
                    System.Console.WriteLine($"MEASURE {site} gap+{plus * 10.5:0}: death real {r.death:P0} est {pd:P0} | clear real {r.clear:P0} est {pc:P0}");
                    errDeath += System.Math.Abs(r.death - pd); errClear += System.Math.Abs(r.clear - pc); n++;
                }
            }
            System.Console.WriteLine($"MEASURE odds error: death {errDeath / n:P1} clear {errClear / n:P1}");
            Assert.Less(errDeath / n, 0.05, "죽을 확률 어림이 실제와 크게 다르지 않다");
            Assert.Less(errClear / n, 0.12, "완주 어림이 실제와 크게 다르지 않다");
        }

        [Test]
        public void DungeonSitesOpenWeeklyAndClaimsBlockOthers()
        {
            var s = World.NewGame(87, "aurelia", "시험", 2);
            var me = s.ludi[s.playerLudusId];
            var rng = new Rng(5);
            s.day += Calendar.PaydayInterval;
            DungeonLogic.WeeklyRotation(s, rng);
            var sites = DungeonLogic.Sites(s);
            Assert.That(sites.Count, Is.InRange(22, 28), "급료일마다 22~28곳");
            var grades = sites.Select(x => DungeonLogic.GetDungeon(x.id).grade).Distinct().Count();
            Assert.AreEqual(DungeonLogic.MaxGrade, grades, "위험도 1~10이 모두 열린다");
            Assert.Greater(sites.Select(x => DungeonLogic.GetDungeon(x.id).type).Distinct().Count(), 20, "종류가 고루 섞인다");
            // 우리가 들어간 자리는 막힌다
            var party = me.gladiators.Take(3).ToList();
            var site = sites[3].id;
            Assert.IsNull(DungeonLogic.StartExpedition(s, site, party.ToList(), "focus"));
            Assert.AreEqual(me.id, DungeonLogic.SiteOf(s, site).claimedBy);
            Assert.IsNotNull(DungeonLogic.StartExpedition(s, site, me.gladiators.Skip(3).Take(2).ToList(), "focus"), "같은 자리에 두 번 못 간다");
            // AI는 하루에 조금씩, 한 주에 60%까지만
            var ai = s.ludi.Values.First(l => !l.isPlayer);
            int claimed = 0;
            for (int day = 0; day < 7; day++)
            {
                while (DungeonLogic.AiMayClaim(s, me.id) && DungeonLogic.OpenSites(s).Any())
                { Assert.IsNull(DungeonLogic.ClaimSite(s, DungeonLogic.OpenSites(s).First().id, ai.id)); claimed++; }
                s.day++;
            }
            Assert.That(claimed, Is.InRange((int)(sites.Count * 0.5), (int)System.Math.Ceiling(sites.Count * 0.6)), "AI 몫은 주에 60%까지");
            Assert.Greater(DungeonLogic.OpenSites(s).Count(), 0, "주말에도 우리 몫이 남는다");
            // 다른 루두스가 들어간 자리는 못 간다
            var taken = DungeonLogic.Sites(s).First(x => x.claimedBy == ai.id).id;
            Assert.AreEqual("다른 루두스 원정대가 먼저 들어갔습니다", DungeonLogic.StartExpedition(s, taken, me.gladiators.Skip(3).Take(2).ToList(), "focus"));
            // 저장해도 남는다
            Assert.AreEqual(DungeonLogic.Sites(s).Count, SaveSystem.FromJson(SaveSystem.ToJson(s)).dungeonSites.Count);
        }

        [Test]
        public void DungeonDeathFitsPartyStrength()
        {
            double fair = 0, weak = 0, strong = 0;
            var ids = new[] { "sewer", "mine", "catacomb", "magma", "abyss" };
            foreach (var did in ids)
            {
                double fairPlus = 2 + DungeonLogic.GetDungeon(did).tier * 0.6; var f = RunParties(did, fairPlus, 60);
                var w = RunParties(did, fairPlus - 2.5, 60);
                var o = RunParties(did, fairPlus + 3, 60);
                System.Console.WriteLine($"MEASURE {did}: fair death {f.death:P1} clear {f.clear:P0} | weak death {w.death:P1} clear {w.clear:P0} | strong death {o.death:P1} clear {o.clear:P0}");
                fair += f.death; weak += w.death; strong += o.death;
            }
            fair /= ids.Length; weak /= ids.Length; strong /= ids.Length;
            System.Console.WriteLine($"MEASURE avg: fair {fair:P1} weak {weak:P1} strong {strong:P1}");
            Assert.That(fair, Is.InRange(0.05, 0.10), "적정 능력이면 원정에서 5~10% 죽는다");
            Assert.Greater(weak, fair, "약하면 더 잘 죽는다");
            Assert.Less(strong, fair, "강하면 덜 죽는다");
        }

        [Test]
        public void BenchGrievanceNeedsSteadyAppearances()
        {
            var s = World.NewGame(86, "aurelia", "시험", 4);
            var me = s.ludi[s.playerLudusId];
            var g = s.gladiators[me.gladiators[0]];
            s.day = 40;
            int start = Calendar.AbsDay(s.season, s.day);
            g.grievance = "출전 기회가 없다"; g.grievanceKind = "bench"; g.grievanceLudus = me.id; g.grievanceAt = start; g.grievanceUntil = start + 1;
            void Play(int day, bool fought)
            {
                s.day = day;
                if (fought) g.lastFightAt = Calendar.AbsDay(s.season, day);
                Grievances.Daily(s, new Rng(1));
            }
            Play(45, true);
            Assert.IsNotNull(g.grievance, "한 경기로는 풀리지 않는다");
            Assert.AreEqual(1, g.benchApps);
            Play(90, false);
            Assert.IsNotNull(g.grievance, "우리 검투사는 시간이 지나도 풀리지 않는다");
            Assert.AreEqual(0, g.benchApps, "오래 쉬면 처음부터 다시 센다");
            Play(95, true);
            Play(105, true);
            Assert.IsNotNull(g.grievance);
            Assert.AreEqual(2, g.benchApps);
            Play(115, true);
            Assert.IsNull(g.grievance, "꾸준히 나가면 풀린다");
            Assert.IsNull(g.benchApps);
        }

        [Test]
        public void CareerSplitsOnTransferAndKeepsFee()
        {
            var s = World.NewGame(87, "aurelia", "시험", 4);
            var me = s.ludi[s.playerLudusId];
            var buyer = s.ludi.Values.First(l => !l.isPlayer);
            var g = s.gladiators[me.gladiators[0]];
            Career.SeasonRecord(s, g).apps = 4;
            int before = g.history.Count;
            me.gladiators.Remove(g.id);
            buyer.gladiators.Add(g.id);
            Career.Joined(s, g, 1200);
            Assert.AreEqual(before + 1, g.history.Count, "시즌 중 이적하면 줄이 나뉜다");
            var now = g.history[g.history.Count - 1];
            Assert.AreEqual(buyer.id, now.ludusId);
            Assert.AreEqual(1200, now.fee);
            Assert.AreEqual(0, now.apps);
            Assert.AreEqual(4, g.history[g.history.Count - 2].apps, "이적 전 기록은 그대로");
            Assert.AreSame(now, Career.SeasonRecord(s, g), "같은 루두스면 그 줄을 이어 쓴다");
        }

        [Test]
        public void DungeonRotatesOnPaydayAndFightsCanBeReplayed()
        {
            var s = World.NewGame(88, "aurelia", "시험", 4);
            var me = s.ludi[s.playerLudusId];
            var first = DungeonLogic.Sites(s).Select(x => x.id).ToList();
            var rng = new Rng(5);
            s.day += Calendar.PaydayInterval;
            DungeonLogic.WeeklyRotation(s, rng);
            var now = DungeonLogic.Sites(s).Select(x => x.id).ToList();
            Assert.IsFalse(now.Intersect(first).Any(), "급료일마다 새 자리가 열린다");
            var party = me.gladiators.Take(3).ToList();
            Assert.IsNotNull(DungeonLogic.StartExpedition(s, first[0], party.ToList(), "shieldwall"), "지난주 자리는 못 간다");
            Assert.IsNotNull(DungeonLogic.StartExpedition(s, "sewer", party.ToList(), "shieldwall"), "열리지 않은 던전은 못 간다");

            string open = now[0];
            Assert.IsNull(DungeonLogic.StartExpedition(s, open, party.ToList(), "shieldwall"));
            var ex = DungeonLogic.ExpeditionsOf(s)[0];
            s.day += DungeonLogic.GetDungeon(open).days;
            DungeonLogic.DailyDungeons(s, new Rng(9));
            Assert.Greater(ex.battles.Count, 0, "층마다 전투 기록이 남는다");
            var fb = ex.battles[0];
            Assert.AreEqual(5, fb.foes.Count, "적은 늘 다섯");
            var res = DungeonLogic.Replay(fb);
            Assert.IsTrue(res.IsTeam, "한 판짜리 난전이다");
            Assert.AreEqual(party.Count, res.sides[0].Count);
            Assert.AreEqual(5, res.sides[1].Count);
            Assert.AreEqual(fb.won, res.winnerSide == 0, "다시 봐도 결과가 같다");
            Assert.IsTrue(res.events.Any(e => e.t == "verdict" && e.rule == "missio"), "쓰러지면 미시오 판정");
            Assert.IsFalse(res.events.Any(e => e.t == "crowd"), "던전에는 관중이 없다");
            // 저장했다 불러와도 같은 싸움이 나온다
            var loaded = SaveSystem.FromJson(SaveSystem.ToJson(s));
            var again = DungeonLogic.Replay(DungeonLogic.ExpeditionsOf(loaded)[0].battles[0]);
            Assert.AreEqual(res.winnerSide, again.winnerSide);
            CollectionAssert.AreEqual(res.deaths, again.deaths);
        }

        [Test]
        public void ExpiredContractsBecomeFreeAgents()
        {
            var s = World.NewGame(86, "aurelia", "시험", 2);
            var me = s.ludi[s.playerLudusId];
            var g = s.gladiators[me.gladiators[0]];
            g.status = "free";
            g.contract = new GladiatorContract { untilSeason = 1, clauses = new System.Collections.Generic.List<string>() };
            var ai = s.gladiators.Values.Where(x => x.contract?.untilSeason == 1 && !me.gladiators.Contains(x.id)).Select(x => x.id).ToList();
            Assert.Greater(ai.Count, 0, "AI 계약도 시즌 1에 끝나는 검투사가 있다 (1~4시즌으로 나뉨)");
            s.day = Calendar.DaysPerSeason;
            Assert.AreEqual(0, Day.AdvanceDay(s).Count);
            Assert.AreEqual(2, s.season);
            Assert.IsFalse(me.gladiators.Contains(g.id), "재계약 안 하면 떠난다");
            Assert.IsTrue(s.freeAgents.Contains(g.id), "무소속 명단에 남는다");
            // AI: 재계약했거나 무소속
            foreach (var id in ai)
            {
                var x = s.gladiators[id];
                if (!x.alive) continue;
                Assert.IsTrue(s.freeAgents.Contains(id) || x.contract?.untilSeason >= 2, $"{x.name}: 만료된 채 남지 않는다");
            }
            Assert.IsTrue(ai.Any(id => s.freeAgents.Contains(id)), "일부는 풀린다");
        }

        [Test]
        public void AwardsAfterFullSeason()
        {
            var s = World.NewGame(84, "aurelia", "시험", 2);
            var rng = new Rng(9);
            RunTo(s, rng, 2, 1);
            var a = s.awards.Where(x => x.season == 1).ToList();
            Assert.AreEqual(Data.Nations.Count * FmRules.MaxTier, a.Count(x => x.kind == "lanista" && x.comp.StartsWith("league:")), "리그마다 라니스타 상");
            Assert.Greater(a.Count(x => x.kind == "best" && x.comp.StartsWith("league:")), 10);
            Assert.IsTrue(a.Any(x => x.comp == "pantheon" && x.kind == "best"));
            Assert.IsTrue(a.Any(x => x.comp.StartsWith("natcup:") && x.kind == "lanista"));
            Assert.AreEqual(1, a.Count(x => x.kind == "world_best"));
            Assert.AreEqual(Data.Nations.Count, a.Count(x => x.kind == "nation_best"));
            Assert.IsTrue(a.Where(x => x.kind == "rookie").All(x => x.gladiatorId != null));
            Assert.IsTrue(s.ceremonies.Contains("awards:season:1"));
            Assert.IsTrue(s.ceremonies.Any(c => c.StartsWith("awards:comp:league:")));
            Assert.IsNull(s.compStats);
        }

        /// <summary>이날까지 우리 경기를 기계적으로 치르며 넘긴다</summary>
        static void RunTo(GameState s, Rng rng, int season, int day)
        {
            while ((s.season < season || (s.season == season && s.day < day)) && s.gameOver == null)
            {
                foreach (var p in s.pendingEvents.ToList()) Events.ResolveEvent(s, rng, p.id, Events.GetEvent(p.eventId).choices.FindIndex(c => c.cost == null || c.cost == 0));
                foreach (var o in Transfers.OpenOffers(s)) Transfers.RejectOffer(s, o.id);
                foreach (var d in (s.decisions ?? new System.Collections.Generic.List<Decision>()).ToList()) Negotiations.Withdraw(s, d.id);
                foreach (var c in s.challenges.Where(x => x.status == "pending" && s.ludi[x.to].isPlayer).ToList()) ChallengeLogic.Respond(s, c, false, rng);
                foreach (var c in ChallengeLogic.TodaysPlayerMatches(s)) ChallengeLogic.PlayChallenge(s, c, rng);
                var ct = CupLogic.TodaysPlayerTie(s);
                if (ct != null) CupLogic.PlayTie(s, ct.id, rng);
                var nt = NatCupLogic.TodaysPlayerTie(s);
                if (nt != null) NatCupLogic.PlayTie(s, s.nation, nt.id, rng);
                if (OlympicaLogic.Current(s)?.offer == true) OlympicaLogic.Accept(s);
                var ot = OlympicaLogic.TodaysPlayerTie(s);
                if (ot != null) OlympicaLogic.PlayTie(s, ot.id, rng);
                Assert.AreEqual(0, Day.AdvanceDay(s).Count, "막힌 일이 남았다");
            }
        }

        [Test]
        public void DividendFollowsRank()
        {
            Assert.AreEqual(1.6, FmRules.DividendCoef(1, 20), 1e-9);
            Assert.AreEqual(0.6, FmRules.DividendCoef(20, 20), 1e-9);
            var s = World.NewGame(90, "aurelia", "시험", 2);
            var top = Economy.TierRanking(s, 1).First();
            var last = Economy.TierRanking(s, 1).Last();
            double before = top.treasury, beforeLast = last.treasury;
            s.day = Calendar.DaysPerSeason;
            Day.EndSeason(s, new Rng(5));
            Assert.Greater(top.treasury - before, last.treasury - beforeLast);
        }

        [Test]
        public void NationalCupFormatAndFreeze()
        {
            var s = World.NewGame(91, "aurelia", "시험", 4);
            var rng = new Rng(11);
            s.day = FmRules.RegularEnd;
            RunTo(s, rng, 1, NatCupLogic.DrawDay);
            var c = NatCupLogic.Of(s, s.nation);
            Assert.AreEqual(80, c.teams.Count, "나라 전 루두스 참가");
            Assert.AreEqual(40, c.ties.Count(t => t.stage == "r80"));
            var frozen = s.ludi.Values.ToDictionary(l => l.id, l => l.rp);
            RunTo(s, rng, 1, CupLogic.FmDrawDay);
            c = NatCupLogic.Of(s, s.nation);
            Assert.AreEqual(20, c.ties.Count(t => t.stage == "r40"));
            Assert.AreEqual(10, c.ties.Count(t => t.stage == "r20"));
            Assert.AreEqual(5, c.ties.Count(t => t.stage == "r10"));
            Assert.AreEqual(5, c.final5.Count);
            Assert.AreEqual(10, c.ties.Count(t => t.stage == "league5"), "5강은 한 번씩");
            Assert.IsNotNull(c.champion);
            // 모두가 네 경기씩
            foreach (var id in c.final5) Assert.AreEqual(4, c.ties.Count(t => t.stage == "league5" && (t.home == id || t.away == id)));
            Assert.AreEqual(NatCupLogic.LeagueTable(s, c)[0].id, c.champion);
            foreach (var kv in frozen) Assert.AreEqual(kv.Value, s.ludi[kv.Key].rp, $"컵이 RP를 바꾸면 안 된다: {s.ludi[kv.Key].name}");
            // 다른 나라도 끝난다
            foreach (var n in Data.Nations) Assert.IsNotNull(NatCupLogic.Of(s, n.id).champion, n.id);
        }

        [Test]
        public void FixtureTieBreaks()
        {
            // 셋 다 1승 1패, 쓰러뜨린 수: A 8, B 6, C 8 → A·C 승자승으로 가린다
            var ties = new[]
            {
                new CupTie { home = "A", away = "B", winner = "B", score = new double[] { 3, 5 } },
                new CupTie { home = "B", away = "C", winner = "C", score = new double[] { 1, 5 } },
                new CupTie { home = "C", away = "A", winner = "A", score = new double[] { 3, 5 } },
            };
            var t = Fixtures.Table(new[] { "A", "B", "C" }, ties, id => 0);
            Assert.AreEqual(new[] { "A", "C", "B" }, t.Select(r => r.id).ToArray());
        }

        [Test]
        public void PantheonPlayoffAndMainDraw()
        {
            var s = World.NewGame(92, "aurelia", "시험", 3);
            var rng = new Rng(13);
            s.day = FmRules.RegularEnd;
            RunTo(s, rng, 1, CupLogic.FmDrawDay + 1);
            var cup = s.cup;
            Assert.AreEqual(true, cup.fm);
            Assert.AreEqual(2, cup.poGroups.Count);
            Assert.IsTrue(cup.poGroups.All(g => g.Count == 3));
            Assert.AreEqual(18, cup.teams.Count, "직행 12 + 플레이오프 6");
            foreach (var n in Data.Nations)
            {
                var top = Economy.TierRanking(s, 1, n.id);
                Assert.IsTrue(top.Take(2).All(l => cup.teams.Contains(l.id) && !cup.poGroups.Any(g => g.Contains(l.id))), "1부 1·2위는 본선 직행");
                var champ = NatCupLogic.Of(s, n.id).champion;
                string po = top.Take(2).Any(l => l.id == champ) ? top[2].id : champ;
                Assert.IsTrue(cup.poGroups.Any(g => g.Contains(po)), "국내 컵 우승(또는 1부 3위)이 플레이오프");
            }
            RunTo(s, rng, 1, CupLogic.FmFinalDay + 1);
            Assert.AreEqual(4, s.cup.groups.Count);
            Assert.IsTrue(s.cup.groups.All(g => g.Count == 4));
            Assert.AreEqual(6, s.cup.ties.Count(t => t.stage == "po"));
            Assert.AreEqual(24, s.cup.ties.Count(t => t.stage == "group"));
            Assert.AreEqual(4, s.cup.ties.Count(t => t.stage == "qf"));
            Assert.AreEqual(1, s.cup.ties.Count(t => t.stage == "final"));
            Assert.IsNotNull(s.cup.champion);
            Assert.Less(CupLogic.FmFinalDay, Calendar.DaysPerSeason);
        }

        [Test]
        public void OlympicaEveryFourSeasons()
        {
            Assert.IsFalse(OlympicaLogic.IsYear(3));
            Assert.IsTrue(OlympicaLogic.IsYear(4));
            var s = World.NewGame(93, "aurelia", "시험", 2);
            var rng = new Rng(17);
            s.season = 4;
            s.day = 1;
            RunTo(s, rng, 4, OlympicaLogic.SetupDay + 1);
            var o = OlympicaLogic.Current(s);
            Assert.IsNotNull(o);
            Assert.AreEqual(6, o.squads.Count);
            Assert.IsTrue(o.squads.Values.All(q => q.Count == OlympicaLogic.Squad), "5명 + 후보 2명");
            Assert.AreEqual(30, o.ties.Count, "6개국 홈·원정 10라운드");
            foreach (var n in Data.Nations)
            {
                Assert.AreEqual(5, o.ties.Count(t => t.home == n.id));
                Assert.AreEqual(5, o.ties.Count(t => t.away == n.id));
            }
            foreach (var day in OlympicaLogic.MatchDays) Assert.AreEqual(3, o.ties.Count(t => t.day == day));
            RunTo(s, rng, 4, OlympicaLogic.MatchDays[9] + 1);
            o = OlympicaLogic.Current(s);
            Assert.IsNotNull(o.champion);
            Assert.AreEqual(OlympicaLogic.Table(s, o)[0].id, o.champion);
            Assert.IsTrue(s.honours.Any(h => h.kind == "olympica" && h.season == 4));
        }

        [Test]
        public void ForfeitKnocksOut()
        {
            var s = World.NewGame(94, "aurelia", "시험", 4);
            var rng = new Rng(19);
            s.day = FmRules.RegularEnd;
            RunTo(s, rng, 1, NatCupLogic.KoDays[0]);
            var t = NatCupLogic.TodaysPlayerTie(s);
            Assert.IsNotNull(t);
            Assert.IsTrue(Day.Blockers(s).Any(b => b.Contains("컵")));
            Assert.IsNull(NatCupLogic.Forfeit(s, t.id));
            Assert.AreNotEqual(s.playerLudusId, t.winner);
            Assert.IsNull(NatCupLogic.TodaysPlayerTie(s));
        }

        [Test]
        public void StaffHaveAttributes()
        {
            var s = World.NewGame(81, "aurelia", "시험", 3);
            var st = s.staff.Values.First();
            Assert.IsNotNull(st.attrs);
            Assert.AreEqual(StaffLogic.AttrKeys.Length, st.attrs.Count);
            Assert.AreEqual(StaffLogic.RatingFromAttrs(st.role, st.attrs), st.rating);
        }

        /// <summary>막는 일(사건·받은 제안·오늘 경기·유소년 말고 다른 역제안)을 치우고 하루 넘긴다</summary>
        static void Next(GameState s)
        {
            var rng = new Rng(11);
            foreach (var p in s.pendingEvents.ToList()) Events.ResolveEvent(s, rng, p.id, 0);
            foreach (var o in Transfers.OpenOffers(s)) Transfers.RejectOffer(s, o.id);
            foreach (var d in (s.decisions ?? new System.Collections.Generic.List<Decision>()).Where(d => d.kind != "youth").ToList()) Negotiations.Withdraw(s, d.id);
            foreach (var c in ChallengeLogic.TodaysPlayerMatches(s)) ChallengeLogic.PlayChallenge(s, c, rng);
            Day.AdvanceDay(s);
        }

        [Test]
        public void YouthTrialMidSeasonWithNegotiation()
        {
            var s = World.NewGame(81, "aurelia", "시험", 3);
            var me = s.ludi[s.playerLudusId];
            me.treasury = 50000;
            Assert.IsNull(me.youthTrial, "새 게임 첫날에는 입단 테스트가 없다");
            Next(s);
            Assert.IsNull(me.youthTrial);
            s.day = Youth.TrialDay - 1;
            Next(s);
            Assert.AreEqual(Youth.TrialDay, s.day);
            Assert.IsNotNull(me.youthTrial, "첫 시즌 중반(입단 테스트 날)에 열린다");
            Assert.AreEqual(Youth.TrialCount(me.facilities.academy), me.youthTrial.Count);

            // 넉넉한 조건: 서명하고 학당에 들어온다
            var kid = s.gladiators[me.youthTrial[0]];
            var d = Youth.TrialDemand(s, kid);
            var t = new ContractTerms { wage = d.wage * 2, seasons = Contracts.PreferredSeasons(kid), signingFee = 50 };
            Assert.AreEqual("sent", Youth.HaggleTrial(s, new Rng(3), kid.id, t).status);
            Assert.AreEqual("error", Youth.HaggleTrial(s, new Rng(3), kid.id, t).status, "답을 기다리는 동안 다시 못 보낸다");
            double before = me.treasury;
            for (int i = 0; i < 3 && !me.youth.Contains(kid.id); i++) Next(s);
            Assert.Contains(kid.id, me.youth);
            Assert.AreEqual(t.wage, kid.wage);
            Assert.IsTrue(kid.contract.clauses.Contains("youth"));
            Assert.AreEqual(s.season + t.seasons - 1, kid.contract.untilSeason);

            // 모자란 조건: 역제안(결정할 일)이 오거나 협상이 깨진다
            if (me.youthTrial.Count == 0) Assert.Inconclusive("후보가 한 명뿐");
            var kid2 = s.gladiators[me.youthTrial[0]];
            var d2 = Youth.TrialDemand(s, kid2);
            Youth.HaggleTrial(s, new Rng(4), kid2.id, new ContractTerms { wage = System.Math.Max(1, System.Math.Round(d2.wage * 0.6)), seasons = Contracts.PreferredSeasons(kid2) });
            for (int i = 0; i < 3 && Negotiations.Pending(s, "youth", kid2.id) != null; i++) Next(s);
            var dec = Negotiations.DecisionFor(s, "youth", kid2.id);
            Assert.IsTrue(dec != null || !s.gladiators.ContainsKey(kid2.id), "역제안이 오거나, 결렬이면 아이가 돌아간다");
            if (dec != null) Assert.IsTrue(Day.Blockers(s).Any(b => b.Contains("협상") || b.Contains("역제안")), "역제안은 다음 날로 가기 전에 정해야 한다");
        }
    }
}
