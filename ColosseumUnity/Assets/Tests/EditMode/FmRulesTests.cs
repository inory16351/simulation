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
                for (int t = 1; t <= 4; t++) Assert.AreEqual(10, s.ludi.Values.Count(l => l.nation == n.id && l.tier == t), $"{n.id} {t}부");
            Assert.AreEqual(s.ludi.Count, s.ludi.Values.Select(l => l.name).Distinct().Count(), "루두스 이름이 겹치지 않는다");
            // 시즌을 끝내면 부마다 10개가 그대로 (2 올라가고 2 내려온다)
            s.day = Calendar.DaysPerSeason;
            Day.EndSeason(s, new Rng(5));
            foreach (var n in Data.Nations)
                for (int t = 1; t <= 4; t++) Assert.AreEqual(10, s.ludi.Values.Count(l => l.nation == n.id && l.tier == t));
            Assert.IsTrue(s.ludi.Values.All(l => l.seasons != null && l.seasons.Count == 1), "모든 루두스에 시즌 연혁");
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
            for (int i = 0; i < 3 && s.decisions == null && s.freeAgents.Contains(g.id); i++) Day.AdvanceDay(s);
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
            for (int i = 0; i < 3 && s.freeAgents.Contains(g.id); i++) Day.AdvanceDay(s);
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
            Assert.AreEqual(1.6, FmRules.DividendCoef(1, 10), 1e-9);
            Assert.AreEqual(0.6, FmRules.DividendCoef(10, 10), 1e-9);
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
            Assert.AreEqual(40, c.teams.Count, "나라 전 루두스 참가");
            Assert.AreEqual(20, c.ties.Count(t => t.stage == "r40"));
            var frozen = s.ludi.Values.ToDictionary(l => l.id, l => l.rp);
            RunTo(s, rng, 1, CupLogic.FmDrawDay);
            c = NatCupLogic.Of(s, s.nation);
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
    }
}
