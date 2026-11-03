// 역대 기록 (Unity판 전용, FM 클럽 연혁·대회 역사처럼): 시즌이 끝날 때 나라마다 1·2부 우승·준우승과
// 모든 루두스의 시즌 성적(부·순위·전적·RP·승강)을 남기고, 판테온 컵 결승이 끝나면 컵 우승을 남긴다.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Colosseum.Core
{
    public static class Honours
    {
        /// <summary>웹판과 상태를 비교하는 골든 테스트만 끈다</summary>
        public static bool Enabled = true;

        public const string CupName = "판테온 컵";

        public static string LeagueName(string nation, int tier) => $"{Data.Nation(nation).@short} {tier}부 리그";

        /// <summary>시즌 종료 직전(승강 반영 전)에 부른다. promoted·relegated는 우리 나라의 승강 명단</summary>
        public static void RecordSeason(GameState state, ICollection<Ludus> promoted, ICollection<Ludus> relegated)
        {
            if (!Enabled) return;
            state.honours ??= new List<HonourEntry>();
            bool cupDone = state.cup?.season == state.season && !string.IsNullOrEmpty(state.cup.champion);
            foreach (var n in Data.Nations)
                for (int tier = 1; tier <= 2; tier++)
                {
                    var table = Economy.TierRanking(state, tier, n.id);
                    if (table.Count == 0) continue;
                    state.honours.Add(new HonourEntry
                    {
                        season = state.season, kind = "league", nation = n.id, tier = tier,
                        winnerId = table[0].id, winnerName = table[0].name,
                        runnerUpId = table.Count > 1 ? table[1].id : null, runnerUpName = table.Count > 1 ? table[1].name : null,
                    });
                    for (int i = 0; i < table.Count; i++)
                    {
                        var l = table[i];
                        l.seasons ??= new List<LudusSeason>();
                        double prevW = l.seasons.Sum(x => x.wins), prevL = l.seasons.Sum(x => x.losses);
                        var trophies = new List<string>();
                        if (i == 0) trophies.Add(LeagueName(n.id, tier));
                        if (cupDone && state.cup.champion == l.id) trophies.Add(CupName);
                        l.seasons.Add(new LudusSeason
                        {
                            season = state.season, nation = l.nation, tier = tier, rank = i + 1, teams = table.Count,
                            wins = Math.Max(0, l.record.wins - prevW), losses = Math.Max(0, l.record.losses - prevL), rp = l.rp,
                            result = promoted.Contains(l) ? "promoted" : relegated.Contains(l) ? "relegated" : i == 0 ? "champion" : null,
                            trophies = trophies.Count > 0 ? trophies : null,
                        });
                    }
                }
            var me = state.ludi[state.playerLudusId];
            var mine = state.honours.FirstOrDefault(h => h.season == state.season && h.kind == "league" && h.winnerId == me.id);
            if (mine != null)
            {
                StateUtil.PushMessage(state, new InboxMessage
                {
                    kind = "info",
                    title = $"{LeagueName(mine.nation, mine.tier)} 우승!",
                    body = $"{Josa.J(me.name, "이/가")} 시즌 {state.season} {LeagueName(mine.nation, mine.tier)} 정상에 섰습니다. 이 우승은 비석의 언덕 역대 기록에 새겨집니다." +
                        (mine.runnerUpName != null ? $"\n준우승: {mine.runnerUpName}" : ""),
                });
            }
        }

        /// <summary>판테온 컵 결승이 끝나면 부른다</summary>
        public static void RecordCup(GameState state, Ludus winner, Ludus runnerUp)
        {
            if (!Enabled) return;
            state.honours ??= new List<HonourEntry>();
            state.honours.Add(new HonourEntry
            {
                season = state.season, kind = "cup", nation = winner.nation,
                winnerId = winner.id, winnerName = winner.name, runnerUpId = runnerUp.id, runnerUpName = runnerUp.name,
            });
        }

        /// <summary>한 루두스가 든 우승컵 수 (이름별)</summary>
        public static List<(string name, List<int> seasons)> TrophiesOf(GameState state, string ludusId) =>
            (state.honours ?? new List<HonourEntry>())
                .Where(h => h.winnerId == ludusId)
                .GroupBy(h => h.kind == "cup" ? CupName : LeagueName(h.nation, h.tier))
                .Select(g => (g.Key, g.Select(h => h.season).OrderBy(x => x).ToList()))
                .OrderBy(x => x.Key == CupName ? 0 : 1).ThenBy(x => x.Key)
                .ToList();
    }
}
