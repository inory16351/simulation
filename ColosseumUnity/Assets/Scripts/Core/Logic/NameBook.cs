// 이름 테이블 (Unity판 전용): Tables/LudusName.csv(나라별 루두스 이름), Tables/PersonName.csv(종족·성별별 이름·성, 별명).
// 원본은 Tools/gen-names.mjs가 만든다. FM식 규칙이 켜져 있고 표를 읽었을 때만 쓴다 (골든 테스트는 웹판 names.json 그대로).
using System;
using System.Collections.Generic;
using System.Linq;

namespace Colosseum.Core
{
    public static class NameBook
    {
        static Dictionary<string, List<string>> ludus = new();
        static Dictionary<string, List<string>> given = new();
        static Dictionary<string, List<string>> surname = new();
        static List<string> epithets = new();

        public static bool Ready => FmRules.Enabled && ludus.Count > 0 && given.Count > 0;

        /// <summary>read(이름) = CSV 글 (LudusName, PersonName)</summary>
        public static void LoadFrom(Func<string, string> read)
        {
            ludus = SkillBook.Rows(read("LudusName")).GroupBy(r => r["nation"]).ToDictionary(g => g.Key, g => g.Select(r => r["name"]).ToList());
            var people = SkillBook.Rows(read("PersonName"));
            given = people.Where(r => r["kind"] == "Given").GroupBy(r => r["race"] + "|" + r["gender"]).ToDictionary(g => g.Key, g => g.Select(r => r["text"]).ToList());
            surname = people.Where(r => r["kind"] == "Surname").GroupBy(r => r["race"]).ToDictionary(g => g.Key, g => g.Select(r => r["text"]).ToList());
            epithets = people.Where(r => r["kind"] == "Epithet").Select(r => r["text"]).ToList();
        }

        static List<string> Get(Dictionary<string, List<string>> d, string k) => d.TryGetValue(k, out var l) ? l : new List<string>();

        /// <summary>그 나라 분위기의 루두스 이름 (안 쓴 것). 나라 이름이 바닥나면 공용, 그래도 없으면 번호를 붙인다</summary>
        public static string LudusName(Rng rng, string nation, HashSet<string> used)
        {
            foreach (var pool in new[] { Get(ludus, nation), Get(ludus, "any") })
            {
                var free = pool.Where(n => !used.Contains(n)).ToList();
                if (free.Count == 0) continue;
                var n = rng.Pick(free);
                used.Add(n);
                return n;
            }
            for (int k = 2; ; k++)
            {
                var n = $"{rng.Pick(Get(ludus, nation).Count > 0 ? Get(ludus, nation) : Get(ludus, "any"))} {k}";
                if (used.Add(n)) return n;
            }
        }

        /// <summary>"이름 성" (안 쓴 것). 성별 이름이 없으면 그 종족 아무 이름</summary>
        public static string PersonName(Rng rng, string race, string gender, HashSet<string> used)
        {
            var pool = Get(given, race + "|" + (gender == "f" ? "f" : "m"));
            if (pool.Count == 0) pool = Get(given, race + "|m");
            var sur = Get(surname, race);
            for (int i = 0; i < 40; i++)
            {
                var n = sur.Count > 0 ? $"{rng.Pick(pool)} {rng.Pick(sur)}" : rng.Pick(pool);
                if (used == null || used.Add(n)) return n;
            }
            for (int k = 2; ; k++)
            {
                var n = $"{rng.Pick(pool)} {k}세";
                if (used.Add(n)) return n;
            }
        }

        public static string Epithet(Rng rng) => epithets.Count > 0 ? rng.Pick(epithets) : rng.Pick(Data.Names.epithet);
    }
}
