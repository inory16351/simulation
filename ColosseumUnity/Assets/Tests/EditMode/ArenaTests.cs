// 경기장 좌표 규칙 (Unity판에만 있음): 좌표가 경기장 안에 있고, 거리 단계가 실제 간격과 맞고, 같은 시드면 같은 결과.
using System.IO;
using System.Linq;
using Colosseum.Core;
using Colosseum.Core.Combat;
using NUnit.Framework;

namespace Colosseum.Tests
{
    public class ArenaTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            Data.LoadFrom(n => File.ReadAllText($"Assets/Resources/Data/{n}.json"));
            Arena.Enabled = true;
            // 기술 테이블 원본 (Tables/*.csv)
            SkillBook.LoadFrom(n => File.ReadAllText($"Tables/{n}.csv"));
            SkillBook.Enabled = true;
        }

        [Test]
        public void SkillTablesLearnAndFight()
        {
            Assert.AreEqual(60, SkillBook.Skills.Count);
            // 새 능동 기술은 전투 기술로 등록된다 (기존 33개는 JSON 그대로)
            Assert.IsNotNull(Data.Skill("skill.cleave"));
            Assert.AreEqual(1.8, Data.Skill("skill.cleave").mult);
            Assert.IsTrue(Data.SkillFromJson("skill.power_strike"));
            // 조건을 채우면 익힌다: 근력·대검 숙련을 올린 검투사에게 쪼개 내리기
            var (a, b) = Pair(11);
            a.attrs["str"] = 18;
            a.prof["greatsword"] = 14;
            var cleave = SkillBook.Row("skill.cleave");
            Assert.IsTrue(SkillBook.Meets(a, cleave));
            SkillBook.Learn(a, cleave);
            CollectionAssert.Contains(a.skills.active, "skill.cleave");
            // 상시 기술은 보정치가 된다
            SkillBook.Learn(a, SkillBook.Row("skill.iron_skin"));
            Assert.Less(SkillBook.PassiveFx(a)["taken"], 0);
            // 반응 기술: 피하면 비켜서기 (사건 반응은 react 이벤트로 남는다)
            b.attrs["agi"] = 18; b.attrs["eva"] = 18;
            SkillBook.Learn(b, SkillBook.Row("skill.sidestep"));
            int reacts = 0;
            // 새 기술을 든 채로 경기가 끝까지 돈다
            for (uint seed = 1; seed <= 20; seed++)
            {
                var r = Sim.Run(MatchLogic.CloneGladiator(a), MatchLogic.CloneGladiator(b), new MatchOptions { rule = "missio", seed = seed, medicalLevel = new double[] { 1, 1 } });
                Assert.IsNotNull(r.winnerId);
                reacts += r.events.Count(e => e.t == "react" && e.skill == "skill.sidestep");
            }
            Assert.Greater(reacts, 0, "sidestep fires on dodges");
        }

        static (Gladiator a, Gladiator b) Pair(uint seed)
        {
            var rng = new Rng(seed);
            var a = GladiatorGen.Generate(rng, new GenOptions { id = "a", nation = "aurelia", target = rng.Range(8, 13) });
            var b = GladiatorGen.Generate(rng, new GenOptions { id = "b", nation = "grommar", target = rng.Range(8, 13) });
            return (a, b);
        }

        [Test]
        public void PositionsStayInsideAndMatchRange()
        {
            int moves = 0, pushes = 0;
            for (uint seed = 1; seed <= 60; seed++)
            {
                var (a, b) = Pair(seed * 7919u);
                var r = Sim.Run(a, b, new MatchOptions { rule = seed % 3 == 0 ? "sudden" : "missio", seed = seed });
                Assert.IsNotNull(r.events[0].pos, "start has positions");
                foreach (var e in r.events.Where(e => e.pos != null))
                {
                    for (int i = 0; i < 2; i++)
                        Assert.LessOrEqual(Arena.Norm(e.pos[i * 2], e.pos[i * 2 + 1]), 1 + 1e-9, $"seed {seed} inside arena");
                    double sep = System.Math.Sqrt(System.Math.Pow(e.pos[0] - e.pos[2], 2) + System.Math.Pow(e.pos[1] - e.pos[3], 2));
                    // 돌진(closeIn)은 근접으로 정해 두므로 빼고, 이동 사건의 거리 단계는 실제 간격에서 나온다
                    if (e.t == "move" && (e.skill == null || e.skill == "skill.disengage"))
                    {
                        Assert.AreEqual(Arena.Bucket(sep), e.dist, $"seed {seed} dist bucket");
                        moves++;
                    }
                    if (e.push) pushes++;
                }
            }
            Assert.Greater(moves, 0);
            Assert.Greater(pushes, 0, "big hits knock back");
        }

        static System.Collections.Generic.List<Gladiator> Squad(Rng rng, string side, string nation, int n) =>
            Enumerable.Range(0, n).Select(i => GladiatorGen.Generate(rng, new GenOptions { id = side + i, nation = nation, target = rng.Range(8, 13) })).ToList();

        /// <summary>난전: 한쪽이 전멸하거나 항복(또는 시간 끝)으로 끝나고, 쓰러진 자는 다시 움직이지 않고, 죽은 자는 쓰러진 자 중에 있다</summary>
        [Test]
        public void TeamMeleeEndsCleanly()
        {
            int deaths = 0, kos = 0;
            for (uint seed = 1; seed <= 40; seed++)
            {
                var rng = new Rng(seed * 104729u);
                int n = seed % 2 == 0 ? 3 : 5;
                var s0 = Squad(rng, "a", "aurelia", n);
                var s1 = Squad(rng, "b", "grommar", n);
                var r = Sim.RunTeam(s0, s1, new MatchOptions { rule = seed % 3 == 0 ? "sudden" : "missio", seed = seed, medicalLevel = new double[] { 1, 1 } });
                Assert.IsTrue(r.IsTeam);
                Assert.That(r.winnerSide, Is.EqualTo(0).Or.EqualTo(1));
                Assert.AreEqual(n, r.sides[0].Count);
                var losers = r.sides[1 - r.winnerSide];
                if (r.reason == "ko") CollectionAssert.IsSubsetOf(losers, r.downs.Select(d => d.Key).ToList(), $"seed {seed}: loser side wiped");
                foreach (var d in r.deaths) CollectionAssert.Contains(r.downs.Select(x => x.Key).ToList(), d);
                Assert.AreEqual(r.downs.Count, r.downs.Select(d => d.Key).Distinct().Count(), "each falls once");
                foreach (var e in r.events.Where(e => e.pos != null))
                {
                    Assert.AreEqual(n * 4, e.pos.Length);
                    for (int i = 0; i < n * 2; i++) Assert.LessOrEqual(Arena.Norm(e.pos[i * 2], e.pos[i * 2 + 1]), 1 + 1e-9, $"seed {seed} inside arena");
                }
                // 쓰러진 뒤에는 행동하지 않는다
                var downAt = r.events.Select((e, i) => (e, i)).Where(x => x.e.t == "ko").ToDictionary(x => x.e.target, x => x.i);
                foreach (var (e, i) in r.events.Select((e, i) => (e, i)))
                    if ((e.t == "attack" || e.t == "move") && downAt.TryGetValue(e.actor, out var at)) Assert.Less(i, at, $"seed {seed}: {e.actor} acted after falling");
                deaths += r.deaths.Count;
                kos += r.downs.Count;
            }
            Assert.Greater(kos, 40, "people fall");
            Assert.Greater(deaths, 0, "some die");
        }

        [Test]
        public void TeamTacticsSteerTargets()
        {
            // 집중 공격 + 첫 목표 지정이면 다른 방침보다 그 적에게 공격이 더 몰린다
            int focusHits = 0, spreadHits = 0;
            for (uint seed = 1; seed <= 30; seed++)
            {
                var rng = new Rng(seed * 7919u);
                var s0 = Squad(rng, "a", "aurelia", 5);
                var s1 = Squad(rng, "b", "grommar", 5);
                string mark = s1[4].id;
                int Hits(string policy)
                {
                    var t0 = new TeamTactic { policy = policy, focusTarget = mark };
                    foreach (var g in s0) t0.roles[g.id] = "hunter";
                    var r = Sim.RunTeam(s0.Select(MatchLogic.CloneGladiator).ToList(), s1.Select(MatchLogic.CloneGladiator).ToList(),
                        new MatchOptions { rule = "missio", seed = seed, medicalLevel = new double[] { 1, 1 }, teamTactics = new[] { t0, Tactics.AutoTeam(null, s1) } });
                    Assert.That(r.winnerSide, Is.EqualTo(0).Or.EqualTo(1));
                    return r.events.Count(e => e.t == "attack" && e.target == mark && s0.Any(g => g.id == e.actor));
                }
                focusHits += Hits("focus");
                spreadHits += Hits("spread");
            }
            Assert.Greater(focusHits, spreadHits, "focus target draws more attacks");
        }

        /// <summary>고유 장비: 명품·전설로 굴리면 고유 이름이 붙고, 팔·다리 칸과 효과(출혈)가 전투에 들어간다</summary>
        [Test]
        public void UniqueGearWorksInCombat()
        {
            var rng = new Rng(5);
            string legend = Uniques.Assign(rng, "gladius+3");
            Assert.AreEqual("gladius+3~senate_edict", legend);
            Assert.AreEqual("원로원의 칙령", Data.Weapon(legend).name);
            Assert.AreEqual(3, Data.SplitItemId(legend).Item2);
            Assert.AreEqual("gladius", Data.SplitItemId(legend).Item1);
            // 고유가 없는 장비는 같은 계열 전설로 (곡검 → 검 계열)
            Assert.IsNotNull(Uniques.Of(Uniques.Assign(rng, "longsword+3")));
            Assert.AreEqual("gladius+1", Uniques.Assign(rng, "gladius+1"), "정교한은 그대로");

            int bleeds = 0, thorns = 0;
            for (uint seed = 1; seed <= 30; seed++)
            {
                var (a, b) = Pair(seed * 31u);
                a.equipment.main = "gladius+2~red_tooth";
                a.equipment.arms = "plate_vambrace";
                b.equipment.armor = "manica+2~scar_sleeve";
                b.equipment.legs = "ocrea+3~rooted_oak";
                var r = Sim.Run(a, b, new MatchOptions { rule = "missio", seed = seed });
                bleeds += r.events.Count(e => e.t == "status" && e.status == "poison" && e.target == "b");
                thorns += r.events.Count(e => e.t == "thorns");
            }
            Assert.Greater(bleeds, 0, "붉은 이빨이 출혈을 낸다");
            Assert.Greater(thorns, 0, "흉터 소매가 되갚는다");
        }

        [Test]
        public void SameSeedSameResult()
        {
            var (a, b) = Pair(4242u);
            var r1 = Sim.Run(a, b, new MatchOptions { rule = "missio", seed = 99 });
            var r2 = Sim.Run(a, b, new MatchOptions { rule = "missio", seed = 99 });
            Assert.AreEqual(r1.events.Count, r2.events.Count);
            Assert.AreEqual(r1.winnerId, r2.winnerId);
            var p1 = r1.events.Where(e => e.pos != null).SelectMany(e => e.pos).ToArray();
            var p2 = r2.events.Where(e => e.pos != null).SelectMany(e => e.pos).ToArray();
            CollectionAssert.AreEqual(p1, p2);
        }
    }
}
