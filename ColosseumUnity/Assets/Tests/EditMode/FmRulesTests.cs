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
