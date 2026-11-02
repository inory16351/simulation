// 유소년 학당 (docs/07_youth.md): 입학, 훈련, 견습 투기, 1군 승격 (src/core/youth.ts)
using Colosseum.Core.Combat;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Colosseum.Core
{
    public static class Youth
    {
        static string N(double x) => x.ToString(CultureInfo.InvariantCulture);

        public static int YouthCapacity(Ludus ludus) => ludus.facilities.academy != 0 ? ludus.facilities.academy * 3 + 3 : 0;
        public const int ApprenticeInterval = 28;

        /// <summary>시즌 시작(서리달 1일) 입학</summary>
        public static List<Gladiator> Intake(GameState state, Rng rng, Ludus ludus)
        {
            int lvl = ludus.facilities.academy;
            if (lvl == 0) return new List<Gladiator>();
            int room = YouthCapacity(ludus) - ludus.youth.Count;
            int count = Math.Min(room, lvl + 1);
            if (count <= 0) return new List<Gladiator>();
            // 황금 세대: 드물게 그해 입학생 전원의 그릇이 크다
            bool golden = rng.Chance(0.05);
            var used = new HashSet<string>(state.gladiators.Values.Select(x => x.name));
            var output = new List<Gladiator>();
            for (int i = 0; i < count; i++)
            {
                string race = rng.Weighted(Data.Nation(ludus.nation).races);
                var r = Data.Race(race).age;
                // 객체 리터럴 순서대로: id(nextId++) → target(rng.range) → age(rng.int)
                var opt = new GenOptions
                {
                    id = $"G{state.nextId++}", nation = ludus.nation, race = race, status = "free",
                    target = rng.Range(2.5, 4.5), age = r.debut - rng.Int(1, 4), paCap = (int)FacilitiesLogic.AcademyPaCap(lvl), usedNames = used,
                };
                var g = GladiatorGen.Generate(rng, opt);
                if (golden) g.hidden.pa = Math.Min(200, g.hidden.pa + rng.Int(20, 40));
                g.wage = rng.Int(2, 5);
                g.training = new TrainingPlan { focus = "physical", intensity = "normal" };
                state.gladiators[g.id] = g;
                ludus.youth.Add(g.id);
                Career.SeasonRecord(state, g);
                Career.LogCareer(state, g, $"{ludus.name} 유소년 학당에 들어왔다 ({N(g.age)}세)", "join");
                output.Add(g);
            }
            if (ludus.isPlayer && output.Count > 0)
            {
                StateUtil.PushMessage(state, new InboxMessage
                {
                    kind = "info",
                    title = golden ? "황금 세대가 입학했다!" : $"유소년 학당 입학생 {output.Count}명",
                    body = $"{string.Join("\n", output.Select(g => $"{g.name} ({Data.Race(g.race).name}, {N(g.age)}세)"))}\n\n{(golden ? "교관들이 술렁인다. 올해 아이들은 그릇이 다르다." : "유소년 학당 화면에서 볼 수 있다.")}",
                });
            }
            return output;
        }

        /// <summary>매일: 유소년 훈련 (학당 단계가 훈련 시설 역할)</summary>
        public static void DailyYouth(GameState state, Rng rng, Ludus ludus)
        {
            foreach (var id in ludus.youth)
            {
                state.gladiators.TryGetValue(id, out var g);
                if (g == null || !g.alive || g.training.focus == "rest") continue;
                double intensity = g.training.intensity == "high" ? 1.3 : g.training.intensity == "low" ? 0.7 : 1;
                Training.Grow(g, rng, g.training.focus == "doctrine" ? "mental" : g.training.focus, 1, intensity, ludus.facilities.academy);
            }
        }

        static readonly string[] NeighborSchools = { "옆 학당", "이웃 학당", "도시 학당" };

        /// <summary>28일마다: 견습 투기. 목검 시합이라 죽지 않고, 실전 경험과 소문이 남는다</summary>
        public static void ApprenticeBouts(GameState state, Rng rng, Ludus ludus)
        {
            var kids = ludus.youth.Select(id => state.gladiators.TryGetValue(id, out var x) ? x : null).Where(g => g != null && g.alive).ToList();
            if (kids.Count == 0) return;
            var lines = new List<string>();
            int wins = 0;
            foreach (var g in kids)
            {
                var r = Data.Race(g.race).age;
                // 객체 리터럴 순서대로: target(rng.range)만 난수를 쓴다
                var gen = new GenOptions { id = "tmp", nation = ludus.nation, target = rng.Range(2.5, 5.5), age = r.debut - 2, status = "free" };
                var opp = GladiatorGen.Generate(rng, gen);
                opp.name = $"{rng.Pick(NeighborSchools)}의 {opp.name}";
                var res = Sim.Run(g, opp, new MatchOptions { rule = "training", seed = MatchLogic.Seed31(rng), noDeath = true });
                bool won = res.winnerId == g.id;
                if (won) wins++;
                Training.Grow(g, rng, "match", won ? 8 : 6, 1, ludus.facilities.academy);
                g.popularity = Math.Min(100, g.popularity + (won ? 2 : 0));
                var rec = Career.SeasonRecord(state, g);
                rec.apps++;
                if (won) rec.wins++; else rec.losses++;
                lines.Add($"{g.name}: {(won ? "승리" : "패배")}");
            }
            // 잘하는 유망주는 소문이 난다
            var star = kids.OrderByDescending(x => GladiatorRules.ComputeCA(x)).First();
            if (ludus.isPlayer)
            {
                StateUtil.PushMessage(state, new InboxMessage
                {
                    kind = "match",
                    title = $"견습 투기 {wins}승 {kids.Count - wins}패",
                    body = $"{string.Join("\n", lines)}\n\n가장 눈에 띈 아이: {star.name}. {(star.popularity >= 15 ? "다른 루두스에서도 이름을 묻기 시작했다." : "")}",
                });
            }
        }

        /// <summary>데뷔 나이가 된 유소년을 1군으로 올린다. null이면 성공</summary>
        public static string PromoteYouth(GameState state, Ludus ludus, string id)
        {
            var g = state.gladiators[id];
            if (GladiatorRules.AgeStage(g) == "youth") return "아직 어리다";
            ludus.youth = ludus.youth.FindAll(x => x != id);
            ludus.gladiators.Add(id);
            g.wage = Math.Max(8, JsMath.Round(GladiatorRules.ComputeCA(g) * 0.5));
            // 학당에서 자란 아이는 루두스에 대한 정이 깊다
            g.hidden.loyalty = Math.Min(20, g.hidden.loyalty + 3);
            if (!g.traits.Contains("academy")) g.traits.Add("academy");
            Career.LogCareer(state, g, $"{ludus.name} 1군으로 올라갔다", "milestone");
            return null;
        }

        public static void ReleaseYouth(GameState state, Ludus ludus, string id)
        {
            ludus.youth = ludus.youth.FindAll(x => x != id);
            var g = state.gladiators[id];
            Career.LogCareer(state, g, $"{ludus.name} 학당에서 내보내졌다", "other");
        }

        /// <summary>시즌 끝: 데뷔 나이가 된 아이들을 알린다 (AI는 자동 승격)</summary>
        public static void SeasonEndYouth(GameState state, Ludus ludus)
        {
            var ready = ludus.youth.Select(id => state.gladiators.TryGetValue(id, out var x) ? x : null)
                .Where(g => g != null && GladiatorRules.AgeStage(g) != "youth").ToList();
            if (ready.Count == 0) return;
            if (!ludus.isPlayer)
            {
                foreach (var g in ready) PromoteYouth(state, ludus, g.id);
                return;
            }
            StateUtil.PushMessage(state, new InboxMessage
            {
                kind = "info",
                title = $"졸업할 나이가 된 아이 {ready.Count}명",
                body = $"{string.Join("\n", ready.Select(g => $"{Josa.J(g.name, "이/가")} 데뷔할 나이가 됐다"))}\n\n유소년 학당 화면에서 1군으로 올리거나 내보내라.",
            });
        }

        public static bool IsApprenticeDay(GameState state) => (int)Calendar.AbsDay(state.season, state.day) % ApprenticeInterval == 0;
    }
}
