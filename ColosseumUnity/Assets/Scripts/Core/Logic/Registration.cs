// 등록 로스터 (Unity판 전용, FM 대회 등록처럼): 루두스는 검투사를 얼마든지 둘 수 있지만 리그·컵 공식 경기에는
// 등록된 15명만 나간다. 미등록은 훈련·연습전·던전만. 등록 해제는 이적 시장 기간(시즌 초 1~14일, 78~91일)에만,
// 빈자리(사망·이적·계약 만료)는 언제든 채울 수 있다. 새로 들어온 검투사는 자리가 있으면 바로 등록된다.
using System.Collections.Generic;
using System.Linq;

namespace Colosseum.Core
{
    public static class Registration
    {
        public const int Max = 15;
        public static bool Enabled => FmRules.Enabled;

        /// <summary>공식 경기에 나갈 수 있게 등록됐나 (등록 명단이 아직 없으면 모두 등록된 것으로)</summary>
        public static bool IsRegistered(GameState s, Gladiator g)
        {
            if (!Enabled) return true;
            var l = Career.LudusOf(s, g.id);
            return l?.registered == null || l.registered.Contains(g.id);
        }

        public static int Count(Ludus l) => l.registered?.Count ?? 0;

        static IEnumerable<Gladiator> Owned(GameState s, Ludus l) =>
            l.gladiators.Where(id => s.gladiators.TryGetValue(id, out var g) && g.alive).Select(id => s.gladiators[id]);

        /// <summary>등록한다. 실패하면 이유</summary>
        public static string Register(GameState s, Ludus l, string id)
        {
            l.registered ??= new List<string>();
            if (l.registered.Contains(id)) return null;
            if (!l.gladiators.Contains(id) || !s.gladiators.TryGetValue(id, out var g) || !g.alive) return "우리 검투사가 아닙니다";
            if (l.registered.Count >= Max) return $"등록은 {Max}명까지입니다. 이적 시장 기간에 다른 검투사를 해제하세요";
            l.registered.Add(id);
            return null;
        }

        /// <summary>등록을 해제한다 (이적 시장 기간에만). 실패하면 이유</summary>
        public static string Unregister(GameState s, Ludus l, string id)
        {
            if (l.registered == null || !l.registered.Contains(id)) return null;
            if (!Transfers.InWindow(s)) return $"등록 해제는 이적 시장 기간({Calendar.DateLabel(1)}~{Calendar.DateLabel(14)}, {Calendar.DateLabel(78)}~{Calendar.DateLabel(91)})에만 할 수 있습니다";
            l.registered.Remove(id);
            return null;
        }

        /// <summary>매일 (하루 처리 처음): 떠난·죽은 검투사 정리, 새로 들어온 검투사는 자리가 있으면 등록.
        /// AI는 이적 시장이 열리는 날(1일·78일) 가장 강한 15명으로 다시 고른다</summary>
        public static void Daily(GameState s)
        {
            if (!Enabled) return;
            bool window = s.day == 1 || s.day == 78;
            foreach (var l in s.ludi.Values)
            {
                var owned = Owned(s, l).ToList();
                var ids = owned.Select(g => g.id).ToHashSet();
                if (l.registered == null)
                {
                    // 처음(새 게임·옛 저장): 가장 강한 15명
                    l.registered = owned.OrderByDescending(g => GladiatorRules.ComputeCA(g)).Take(Max).Select(g => g.id).ToList();
                    l.regSeen = ids.ToList();
                    continue;
                }
                if (l.registered.Any(id => !ids.Contains(id))) l.registered = l.registered.Where(ids.Contains).ToList();
                l.regSeen ??= new List<string>();
                if (l.regSeen.Any(id => !ids.Contains(id))) l.regSeen = l.regSeen.Where(ids.Contains).ToList();
                // 능력 정렬은 비싸니 바꿀 일이 있을 때만 (새로 온 사람, AI 빈자리, AI 시장 여는 날)
                var seen = l.regSeen.ToHashSet();
                var fresh = owned.Where(g => !seen.Contains(g.id)).ToList();
                bool aiFill = !l.isPlayer && l.registered.Count < Max && owned.Count > l.registered.Count;
                if (fresh.Count == 0 && !aiFill && !(window && !l.isPlayer)) continue;
                if (!l.isPlayer && window)
                    l.registered = owned.OrderByDescending(g => GladiatorRules.ComputeCA(g)).Take(Max).Select(g => g.id).ToList();
                foreach (var g in fresh.OrderByDescending(g => GladiatorRules.ComputeCA(g)))
                {
                    l.regSeen.Add(g.id);
                    if (l.registered.Count < Max && !l.registered.Contains(g.id)) l.registered.Add(g.id);
                }
                // AI는 빈자리를 늘 가장 강한 미등록으로 채운다
                if (!l.isPlayer && l.registered.Count < Max)
                    foreach (var g in owned.Where(g => !l.registered.Contains(g.id)).OrderByDescending(g => GladiatorRules.ComputeCA(g)))
                        if (l.registered.Count < Max) l.registered.Add(g.id);
            }
        }
    }
}
