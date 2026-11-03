// 웹판(TS)과 같은 시드로 같은 결과가 나오는지 확인한다. 골든 데이터: npx tsx tools/golden-dump.ts
using System.IO;
using System.Linq;
using Colosseum.Core;
using Colosseum.Core.Combat;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Colosseum.Tests
{
    public class GoldenTests
    {
        static JObject golden;

        [OneTimeSetUp]
        public void Setup()
        {
            Data.LoadFrom(n => File.ReadAllText($"Assets/Resources/Data/{n}.json"));
            golden = JObject.Parse(File.ReadAllText("Assets/Tests/Golden/golden.json"));
            // 웹판에는 경기장 좌표 규칙이 없다
            Arena.Enabled = false;
            // 웹판에는 팔·다리 칸과 고유 장비가 없다
            Uniques.Enabled = false;
            // 웹판에는 지대 상성·높이 지시가 없다
            Colosseum.Core.Combat.Exchange.Zones = false;
            // 웹판에는 기술 테이블(습득·새 기술)이 없다
            SkillBook.Enabled = false;
            // 웹판에는 FM식 규칙(4부 리그·몸값 모델·계약 조항·식솔 능력치 등)이 없다
            FmRules.Enabled = false;
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            Arena.Enabled = true;
            Uniques.Enabled = true;
            Colosseum.Core.Combat.Exchange.Zones = true;
            SkillBook.Enabled = true;
            FmRules.Enabled = true;
        }

        [Test]
        public void RngMatchesTs()
        {
            var r = new Rng(12345u);
            foreach (var v in golden["rng"]) Assert.AreEqual((double)v, r.Next(), 0);
            var r2 = new Rng(99u);
            foreach (var v in golden["rngNormal"]) Assert.AreEqual((double)v, r2.Normal(10, 2.2), 1e-9);
        }

        static Gladiator[] Generate()
        {
            var gr = new Rng(7u);
            string[] nations = { "aurelia", "grommar", "sylvaren", "dolgar", "karsea", "kemer" };
            return Enumerable.Range(0, 12).Select(i => GladiatorGen.Generate(gr, new GenOptions { id = $"g{i}", nation = nations[i % 6], target = 5 + i % 10 })).ToArray();
        }

        [Test]
        public void GeneratorMatchesTs()
        {
            var gs = Generate();
            var exp = (JArray)golden["gladiators"];
            for (int i = 0; i < gs.Length; i++)
            {
                var g = gs[i];
                var e = (JObject)exp[i];
                string at = $"g{i}";
                Assert.AreEqual((string)e["name"], g.name, at);
                Assert.AreEqual((string)e["race"], g.race, at);
                Assert.AreEqual((string)e["style"], g.style, at);
                Assert.AreEqual((double)e["age"], g.age, at);
                foreach (var p in (JObject)e["attrs"]) Assert.AreEqual((int)p.Value, g.attrs[p.Key], $"{at} attr {p.Key}");
                foreach (var p in (JObject)e["prof"]) Assert.AreEqual((int)p.Value, g.prof[p.Key], $"{at} prof {p.Key}");
                Assert.AreEqual((int)e["hidden"]["pa"], g.hidden.pa, at + " pa");
                Assert.AreEqual((int)e["hidden"]["consistency"], g.hidden.consistency, at);
                CollectionAssert.AreEqual(e["traits"].ToObject<string[]>(), g.traits, at);
                CollectionAssert.AreEqual(e["skills"]["active"].ToObject<string[]>(), g.skills.active, at);
                Assert.AreEqual((string)e["equipment"]["armor"], g.equipment.armor, at);
                Assert.AreEqual((double)e["morale"], g.morale, at);
                Assert.AreEqual((double)e["popularity"], g.popularity, at);
                Assert.AreEqual((int)e["portraitSeed"], g.portraitSeed, at);
                Assert.AreEqual((double)e["wage"], g.wage, at);
                Assert.AreEqual((string)e["nickname"], g.nickname, at);
                Assert.AreEqual((double)e["doctrine"]["familiarity"], g.doctrine.familiarity, at);
                foreach (var p in (JObject)e["portrait"]) Assert.AreEqual((string)p.Value, g.portrait[p.Key], $"{at} portrait {p.Key}");
            }
        }

        [Test]
        public void MatchesMatchTs()
        {
            var gs = Generate().ToDictionary(g => g.id);
            foreach (JObject m in (JArray)golden["matches"])
            {
                var r = Sim.Run(gs[(string)m["a"]], gs[(string)m["b"]], new MatchOptions { rule = (string)m["rule"], seed = (uint)m["seed"] });
                string at = $"seed {m["seed"]}";
                Assert.AreEqual((int)m["events"], r.events.Count, at + " events");
                Assert.AreEqual((string)m["winnerId"], r.winnerId, at);
                Assert.AreEqual((string)m["reason"], r.reason, at);
                Assert.AreEqual((int)m["rounds"], r.rounds, at);
                Assert.AreEqual((string)m["died"], r.died, at);
                Assert.AreEqual((double)m["crowd"], r.crowd, 1e-9, at);
                foreach (var p in (JObject)m["damageDealt"]) Assert.AreEqual((double)p.Value, r.damageDealt[p.Key], 1e-9, at);
                var fighters = new System.Collections.Generic.Dictionary<string, Gladiator> { [(string)m["a"]] = gs[(string)m["a"]], [(string)m["b"]] = gs[(string)m["b"]] };
                var lines = Commentary.Build(r.events, fighters, (uint)m["seed"]).Select(l => l.text).ToArray();
                CollectionAssert.AreEqual(m["commentary"].ToObject<string[]>(), lines, at + " commentary");
            }
        }
    }
}
