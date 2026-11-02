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
