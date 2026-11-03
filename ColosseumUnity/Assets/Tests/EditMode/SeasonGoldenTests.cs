// 시즌 진행 골든 테스트: 웹판과 같은 결정으로 170일을 진행하고 상태 JSON을 비교한다.
// 골든 데이터: npx tsx tools/golden-season.ts (tools/golden-season.ts의 autoplayDay와 같은 순서)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Colosseum.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Colosseum.Tests
{
    public class SeasonGoldenTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            Data.LoadFrom(n => File.ReadAllText($"Assets/Resources/Data/{n}.json"));
            // 웹판에는 경기장 좌표 규칙이 없다
            Colosseum.Core.Combat.Arena.Enabled = false;
            SeriesLogic.Melee = false;
            Honours.Enabled = false;
            Uniques.Enabled = false;
            // 웹판에는 지대 상성·높이 지시가 없다
            Colosseum.Core.Combat.Exchange.Zones = false;
            // 웹판에는 기술 테이블(습득·새 기술)이 없다
            SkillBook.Enabled = false;
            // 웹판에는 FM식 규칙(4부 리그·몸값 모델·계약 조항·식솔 능력치 등)이 없다
            FmRules.Enabled = false;
            // Unity판은 이벤트를 덜 자주 터뜨린다. 골든은 웹판 확률로 뽑았다
            Bal.Events["ludusPerDay"] = 0.12;
            Bal.Events["gladiatorPerDay"] = 0.006;
            Bal.Events["worldPerDay"] = 0.023;
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            Colosseum.Core.Combat.Arena.Enabled = true;
            SeriesLogic.Melee = true;
            Honours.Enabled = true;
            Uniques.Enabled = true;
            Colosseum.Core.Combat.Exchange.Zones = true;
            SkillBook.Enabled = true;
            FmRules.Enabled = true;
            Data.LoadFrom(n => File.ReadAllText($"Assets/Resources/Data/{n}.json"));
        }

        static List<string> AutoplayDay(GameState state, Rng rng)
        {
            foreach (var p in state.pendingEvents.ToList())
            {
                var def = Events.GetEvent(p.eventId);
                int i = def.choices.FindIndex(c => c.cost == null || c.cost == 0 || state.ludi[state.playerLudusId].treasury >= c.cost);
                Events.ResolveEvent(state, rng, p.id, i);
            }
            foreach (var c in state.challenges.Where(x => x.status == "pending" && state.ludi[x.to].isPlayer).ToList()) ChallengeLogic.Respond(state, c, true, rng);
            foreach (var c in ChallengeLogic.TodaysPlayerMatches(state)) ChallengeLogic.PlayChallenge(state, c, rng);
            var tie = CupLogic.TodaysPlayerTie(state);
            if (tie != null) CupLogic.PlayTie(state, tie.id, rng);
            if (state.day % 4 == 0)
            {
                var me = state.ludi[state.playerLudusId];
                var target = state.ludi.Values.FirstOrDefault(l => !l.isPlayer && l.tier == me.tier);
                var g = me.gladiators.Select(id => state.gladiators[id]).FirstOrDefault(x => x.alive);
                if (target != null && g != null) ChallengeLogic.CreateChallenge(state, me, target, g.id, "training");
            }
            return Day.AdvanceDay(state);
        }

        [Test]
        public void NewGameMatchesTs() => AssertSame("world_day0.json", World.NewGame(2024));

        [Test]
        public void Day30MatchesTs()
        {
            var s = World.NewGame(2024);
            var rng = new Rng(4242u);
            for (int i = 1; i <= 30 && s.gameOver == null; i++) AutoplayDay(s, rng);
            AssertSame("world_day30.json", s);
        }

        [Test]
        public void Day170MatchesTs()
        {
            var s = World.NewGame(2024);
            var rng = new Rng(4242u);
            var blocked = new List<List<string>>();
            for (int i = 1; i <= 170 && s.gameOver == null; i++) blocked.Add(AutoplayDay(s, rng));
            AssertSame("world_day170.json", s);
            var expBlocked = JArray.Parse(File.ReadAllText("Assets/Tests/Golden/world_blocked.json"));
            Assert.AreEqual(expBlocked.ToString(Formatting.None), JArray.FromObject(blocked).ToString(Formatting.None));
        }

        static void AssertSame(string file, GameState s)
        {
            var expected = JToken.Parse(File.ReadAllText($"Assets/Tests/Golden/{file}"));
            var actual = JToken.FromObject(s, JsonSerializer.Create(new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
            var diffs = new List<string>();
            Compare(expected, actual, "$", diffs);
            if (diffs.Count > 0) Assert.Fail($"{file}: {diffs.Count}개 다름\n" + string.Join("\n", diffs.Take(40)));
        }

        /// <summary>웹판 JSON(기대)의 모든 값이 C# 쪽에도 같게 있는지. C#에만 있는 기본값(0, false, 빈 목록)은 무시한다</summary>
        public static void Compare(JToken e, JToken a, string path, List<string> diffs)
        {
            if (diffs.Count > 200) return;
            if (e.Type == JTokenType.Null) { if (a != null && a.Type != JTokenType.Null && !IsDefault(a)) diffs.Add($"{path}: 기대 null, 실제 {Short(a)}"); return; }
            if (a == null || a.Type == JTokenType.Null) { if (!IsDefault(e)) diffs.Add($"{path}: 기대 {Short(e)}, 실제 없음"); return; }
            switch (e)
            {
                case JObject eo:
                    if (a is not JObject ao) { diffs.Add($"{path}: 객체가 아님 {Short(a)}"); return; }
                    foreach (var p in eo) Compare(p.Value, ao[p.Key], $"{path}.{p.Key}", diffs);
                    foreach (var p in ao) if (!eo.ContainsKey(p.Key) && !IsDefault(p.Value)) diffs.Add($"{path}.{p.Key}: C#에만 있음 {Short(p.Value)}");
                    break;
                case JArray ea:
                    if (a is not JArray aa) { diffs.Add($"{path}: 배열이 아님 {Short(a)}"); return; }
                    if (ea.Count != aa.Count) diffs.Add($"{path}: 길이 {ea.Count} vs {aa.Count}");
                    for (int i = 0; i < Math.Min(ea.Count, aa.Count); i++) Compare(ea[i], aa[i], $"{path}[{i}]", diffs);
                    break;
                default:
                    if (e.Type is JTokenType.Integer or JTokenType.Float && a.Type is JTokenType.Integer or JTokenType.Float)
                    {
                        double x = e.Value<double>(), y = a.Value<double>();
                        if (Math.Abs(x - y) > 1e-6 * Math.Max(1, Math.Abs(x))) diffs.Add($"{path}: {x} vs {y}");
                    }
                    else if (e.ToString() != a.ToString()) diffs.Add($"{path}: {Short(e)} vs {Short(a)}");
                    break;
            }
        }

        static bool IsDefault(JToken t) => t.Type switch
        {
            JTokenType.Null => true,
            JTokenType.Integer or JTokenType.Float => t.Value<double>() == 0,
            JTokenType.Boolean => !t.Value<bool>(),
            JTokenType.String => t.Value<string>() == "",
            JTokenType.Array => !t.HasValues,
            JTokenType.Object => !t.HasValues,
            _ => false,
        };

        static string Short(JToken t)
        {
            var s = t.ToString(Formatting.None);
            return s.Length > 80 ? s.Substring(0, 80) + "…" : s;
        }
    }
}
