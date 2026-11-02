// 나라별 리그 특징 (src/core/nationRules.ts, data/nations.json의 rules). 값이 없으면 기본값을 쓴다.
using Colosseum.Core.Combat;
using Newtonsoft.Json.Linq;

namespace Colosseum.Core
{
    /// <summary>TS Required&lt;NationRules&gt;</summary>
    public class ResolvedRules
    {
        public double ticketMult = 1, fameMult = 1, taxRate, growthMult = 1;
        public bool surrenderShame;
        public double marketMult = 1, purseMult = 1, suddenPurseMult = 1, aiSuddenMult = 1, templeSubsidy = 0;
        public bool suddenBanned;
        public double missioDeathMult = 1, magicFameMult = 1;
    }

    public static class NationRules
    {
        static double D(JObject r, string k, double def) => r != null && r.TryGetValue(k, out var v) && v.Type != JTokenType.Null ? (double)v : def;
        static bool B(JObject r, string k, bool def) => r != null && r.TryGetValue(k, out var v) && v.Type != JTokenType.Null ? (bool)v : def;

        public static ResolvedRules RulesOfNation(string id)
        {
            var r = Data.Nation(id).rules;
            return new ResolvedRules
            {
                ticketMult = D(r, "ticketMult", 1),
                fameMult = D(r, "fameMult", 1),
                taxRate = D(r, "taxRate", (double)Bal.Economy["taxRate"]),
                growthMult = D(r, "growthMult", 1),
                surrenderShame = B(r, "surrenderShame", false),
                marketMult = D(r, "marketMult", 1),
                purseMult = D(r, "purseMult", 1),
                suddenPurseMult = D(r, "suddenPurseMult", 1),
                aiSuddenMult = D(r, "aiSuddenMult", 1),
                templeSubsidy = D(r, "templeSubsidy", 0),
                suddenBanned = B(r, "suddenBanned", false),
                missioDeathMult = D(r, "missioDeathMult", 1),
                magicFameMult = D(r, "magicFameMult", 1),
            };
        }

        /// <summary>플레이어가 속한 나라 리그의 규칙</summary>
        public static ResolvedRules LeagueRules(GameState state) => RulesOfNation(state.nation);
    }
}
