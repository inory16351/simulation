// 스카우팅 (src/core/scouting.ts): 다른 루두스·무소속 검투사에 대한 '정보'. 정찰할수록 능력과 잠재력 추정 범위가 좁아진다.
// ScoutingState 타입은 StateTypes.cs에 있다.
using Colosseum.Core.Combat;
using System;
using System.Globalization;
using System.Linq;

namespace Colosseum.Core
{
    /// <summary>scouting.ts의 Range (System.Range와 헷갈리지 않게 이름을 바꿨다)</summary>
    public class ScoutRange { public double lo, hi, mid; }

    public static class Scouting
    {
        static void Msg(GameState s, string kind, string title, string body) =>
            StateUtil.PushMessage(s, new InboxMessage { kind = kind, title = title, body = body });

        static string Num(double v)
        {
            if (v == Math.Floor(v) && Math.Abs(v) < 1e15) return ((long)v).ToString(CultureInfo.InvariantCulture);
            return v.ToString("R", CultureInfo.InvariantCulture);
        }

        public static ScoutingState ScoutingOf(GameState s) => s.scouting ??= new ScoutingState();

        public static bool IsMine(GameState s, Gladiator g)
        {
            var me = s.ludi[s.playerLudusId];
            return me.gladiators.Contains(g.id) || (me.youth != null && me.youth.Contains(g.id)) || s.fallen.Contains(g.id);
        }

        /// <summary>정보량: 우리 검투사는 다 안다. 유명한 검투사는 소문만으로도 어느 정도 안다</summary>
        public static double KnowledgeOf(GameState s, Gladiator g)
        {
            if (IsMine(s, g)) return 100;
            double k = ScoutingOf(s).knowledge.TryGetValue(g.id, out var v) ? v : 0;
            return Math.Max(k, Math.Min(60, g.popularity * 0.6));
        }

        /// <summary>동시에 정찰할 수 있는 대상 수: 정찰꾼이 없으면 1명, 있으면 실력에 따라 2~4명</summary>
        public static double ScoutSlots(GameState s)
        {
            double r = StaffLogic.RatingOf(s, s.ludi[s.playerLudusId], "scout");
            return r != 0 ? 2 + Math.Floor(r / 8) : 1;
        }

        public static string AssignScout(GameState s, string id)
        {
            var sc = ScoutingOf(s);
            if (sc.assignments.Contains(id)) return null;
            if (sc.assignments.Count >= ScoutSlots(s)) return $"정찰은 동시에 {Num(ScoutSlots(s))}명까지 맡길 수 있습니다";
            sc.assignments.Add(id);
            return null;
        }

        public static void UnassignScout(GameState s, string id)
        {
            var sc = ScoutingOf(s);
            sc.assignments = sc.assignments.Where(x => x != id).ToList();
        }

        /// <summary>매일: 정찰 진행</summary>
        public static void DailyScouting(GameState s)
        {
            var sc = ScoutingOf(s);
            double r = StaffLogic.RatingOf(s, s.ludi[s.playerLudusId], "scout");
            double gain = 4 + r * 0.7;
            foreach (var id in sc.assignments.ToList())
            {
                if (!s.gladiators.TryGetValue(id, out var g) || g == null || !g.alive) { UnassignScout(s, id); continue; }
                sc.knowledge[id] = Math.Min(100, (sc.knowledge.TryGetValue(id, out var k) ? k : 0) + gain);
                if (sc.knowledge[id] >= 100)
                {
                    UnassignScout(s, id);
                    Msg(s, "info", $"정찰 보고서: {g.name}", $"{g.name} 정찰이 끝났습니다. 이제 능력과 잠재력이 꽤 정확히 보입니다.");
                }
            }
        }

        /// <summary>시드로 정해진 치우침: 범위의 가운데가 진짜 값에서 살짝 비껴 있다 (진짜 값은 항상 범위 안).
        /// 주의: Math.Sin은 JS와 마지막 자리까지 같다는 보장이 없다 (화면 표시용이라 상태에는 영향 없음)</summary>
        static double Skew(double seed, double salt)
        {
            double x = Math.Sin(seed * 12.9898 + salt * 78.233) * 43758.5453;
            return (x - Math.Floor(x)) * 2 - 1;
        }

        static ScoutRange Range(double value, double spread, double seed, double salt, double min, double max)
        {
            if (spread <= 0) return new ScoutRange { lo = value, hi = value, mid = value };
            double off = Skew(seed, salt) * spread * 0.5;
            double lo = Math.Max(min, JsMath.Round(value + off - spread));
            double hi = Math.Min(max, JsMath.Round(value + off + spread));
            return new ScoutRange { lo = lo, hi = hi, mid = JsMath.Round((lo + hi) / 2) };
        }

        /// <summary>능력치 하나의 추정 범위 (1~20)</summary>
        public static ScoutRange AttrRange(GameState s, Gladiator g, double value, double salt)
        {
            double k = KnowledgeOf(s, g);
            double spread = IsMine(s, g) ? 0 : JsMath.Round(4 * (1 - k / 100));
            return Range(value, spread, g.portraitSeed, salt, 1, 20);
        }

        /// <summary>현재 능력(CA) 추정. FM식이면 남의 검투사는 정찰꾼 '능력 판단'만큼, 우리 검투사는 독토레 '능력 판단'만큼 흐리다</summary>
        public static ScoutRange CaRange(GameState s, Gladiator g)
        {
            double k = KnowledgeOf(s, g);
            if (FmRules.Enabled && IsMine(s, g))
            {
                double v = Judged(s, g);
                return new ScoutRange { lo = v, hi = v, mid = v };
            }
            double judge = FmRules.Enabled ? StaffLogic.AttrOf(s, s.ludi[s.playerLudusId], "scout", "judgeA") : 10;
            double spread = 24 * (1 - k / 100) * (FmRules.Enabled ? 1.3 - judge * 0.035 : 1);
            return Range(GladiatorRules.ComputeCA(g), IsMine(s, g) ? 0 : JsMath.Round(spread), g.portraitSeed, 1, 1, 200);
        }

        /// <summary>우리 독토레가 보는 우리 검투사의 실력 (FM 교관 평가 별점). 판단이 서툴수록 시즌마다 다르게 틀린다</summary>
        public static double Judged(GameState s, Gladiator g)
        {
            double ca = GladiatorRules.ComputeCA(g);
            if (!FmRules.Enabled) return ca;
            double judge = StaffLogic.AttrOf(s, s.ludi[s.playerLudusId], "doctore", "judgeA");
            double spread = Math.Max(0, (20 - judge) * 1.1);
            return Math.Max(1, Math.Min(200, JsMath.Round(ca + Skew(g.portraitSeed, 7 + s.season) * spread)));
        }

        /// <summary>잠재력(PA) 추정: 정찰꾼 실력과 정보량에 따라</summary>
        public static ScoutRange PaRange(GameState s, Gladiator g)
        {
            double r = StaffLogic.RatingOf(s, s.ludi[s.playerLudusId], "scout");
            double k = KnowledgeOf(s, g);
            double @base = IsMine(s, g) ? Math.Max(3, 30 - r * 1.35) : Math.Max(5, 45 - r * 2);
            double spread = JsMath.Round(@base * (1 - (IsMine(s, g) ? 0 : k / 100) * 0.7));
            double ca = GladiatorRules.ComputeCA(g);
            var rr = Range(g.hidden.pa, spread, g.portraitSeed, 2, ca, 200);
            return new ScoutRange { lo = Math.Max(ca, rr.lo), hi = rr.hi, mid = rr.mid };
        }
    }
}
