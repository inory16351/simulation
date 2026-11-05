// 식솔: 독토레(교관), 메디쿠스(의원), 정찰꾼, 중개인 (src/core/staff.ts)
using Colosseum.Core.Combat;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Colosseum.Core
{
    public static class StaffLogic
    {
        public static readonly Dictionary<string, string> RoleNames = new()
        {
            ["doctore"] = "독토레", ["medicus"] = "메디쿠스", ["scout"] = "정찰꾼", ["agent"] = "중개인",
            ["weaponCoach"] = "무기 교관", ["fitnessCoach"] = "체력 교관", ["skillCoach"] = "기술 교관", ["youthHead"] = "유소년 책임자",
        };
        public static readonly Dictionary<string, string> RoleDesc = new()
        {
            ["doctore"] = "수석 교관. 검투사가 훈련과 실전에서 더 빨리 크고, 교범도 더 빨리 익힙니다",
            ["medicus"] = "의원. 부상을 빨리 낫게 하고, 사투에서 목숨을 건질 확률을 높입니다",
            ["scout"] = "정찰꾼. 검투사의 숨은 그릇(잠재력)을 더 정확히 봅니다",
            ["agent"] = "중개인. 이적료를 깎아 사고, 팔 때는 더 받아 냅니다",
            ["weaponCoach"] = "무기 교관. 무기술·방어·주술 훈련 효율을 올립니다 (독토레와 함께 셈)",
            ["fitnessCoach"] = "체력 교관. 체력 단련·격투 훈련 효율을 올리고 훈련 부상을 줄입니다",
            ["skillCoach"] = "기술 교관. 정신 수양·실전 감각·교범 훈련 효율을 올립니다",
            ["youthHead"] = "유소년 책임자. 모래 시험에 높은 잠재력의 아이가 찾아올 확률을 올립니다",
        };

        static readonly string[] BaseRoles = { "doctore", "medicus", "scout", "agent" };
        static readonly string[] FmRoles = { "doctore", "weaponCoach", "fitnessCoach", "skillCoach", "youthHead", "scout", "medicus", "agent" };

        /// <summary>식솔 역할. 웹판 규칙은 넷(역할마다 한 명), FM 세계는 여덟(역할마다 여러 명, RoleCap)</summary>
        public static string[] Roles => FmRules.Enabled ? FmRoles : BaseRoles;

        /// <summary>훈련을 맡는 역할 (교관 1명당 선수 CoachLoad명까지 효율 그대로)</summary>
        public static readonly string[] CoachRoles = { "doctore", "weaponCoach", "fitnessCoach", "skillCoach" };
        public const int CoachLoad = 6;

        /// <summary>FM 세계: 역할마다 둘 수 있는 수 (부·시설에 따라 1~4명)</summary>
        public static int RoleCap(Ludus l, string role)
        {
            if (!FmRules.Enabled) return 1;
            int t = l?.tier ?? 4;
            var f = l?.facilities;
            int cap = role switch
            {
                "doctore" or "youthHead" => 1,
                "weaponCoach" or "fitnessCoach" or "skillCoach" => 1 + (t <= 2 ? 1 : 0) + (t == 1 ? 1 : 0) + ((f?.training ?? 0) >= 3 ? 1 : 0),
                "medicus" => 1 + ((f?.medical ?? 0) >= 3 ? 1 : 0) + (t == 1 ? 1 : 0),
                "scout" => 1 + (t <= 2 ? 1 : 0) + (t == 1 ? 1 : 0),
                "agent" => 1 + (t <= 2 ? 1 : 0),
                _ => 1,
            };
            return Math.Min(4, cap);
        }

        /// <summary>여러 명의 효과를 합친다: 높은 순으로 첫째 ×1, 둘째 ×0.5, 셋째 ×0.25 ...</summary>
        public static double Combine(IEnumerable<double> values)
        {
            double sum = 0, w = 1;
            foreach (var v in values.OrderByDescending(x => x)) { sum += v * w; w *= 0.5; }
            return sum;
        }

        /// <summary>그 역할 식솔 전원 (평점 높은 순)</summary>
        public static List<Staff> StaffsOf(GameState state, Ludus ludus, string role)
        {
            var o = new List<Staff>();
            if (ludus?.staff == null || state.staff == null) return o;
            foreach (var id in ludus.staff)
                if (state.staff.TryGetValue(id, out var s) && s.role == role) o.Add(s);
            return o.OrderByDescending(s => s.rating).ToList();
        }

        // ── 세부 능력치 (FM 직원 능력치처럼) ──

        public static readonly string[] AttrKeys =
            { "coachPhy", "coachTec", "coachMen", "coachTac", "judgeA", "judgeP", "physio", "sci", "motiv", "disc", "manage", "det", "nego", "network" };

        public static readonly Dictionary<string, string> AttrNames = new()
        {
            ["coachPhy"] = "신체 지도", ["coachTec"] = "기술 지도", ["coachMen"] = "정신 지도", ["coachTac"] = "전술 지도",
            ["judgeA"] = "능력 판단", ["judgeP"] = "잠재력 판단", ["physio"] = "치료", ["sci"] = "몸 관리",
            ["motiv"] = "동기 부여", ["disc"] = "규율", ["manage"] = "인간 관리", ["det"] = "결단력", ["nego"] = "협상", ["network"] = "인맥",
        };

        public static readonly Dictionary<string, string> AttrDesc = new()
        {
            ["coachPhy"] = "체력 단련·격투 훈련에서 검투사가 크는 속도",
            ["coachTec"] = "무기술·방어·주술 훈련에서 크는 속도",
            ["coachMen"] = "정신 수양·쇼맨십·실전 감각 훈련에서 크는 속도",
            ["coachTac"] = "교범 수련 속도와 기술을 익히는 빠르기",
            ["judgeA"] = "검투사의 지금 실력을 얼마나 정확히 보나 (교관 평가 별점)",
            ["judgeP"] = "검투사의 숨은 그릇(잠재력)을 얼마나 정확히 보나",
            ["physio"] = "부상을 낫게 하는 빠르기, 사투에서 목숨을 건질 확률",
            ["sci"] = "훈련 중 부상을 막고 피로를 빨리 풀어 줍니다",
            ["motiv"] = "검투사들의 사기를 매일 조금씩 끌어올립니다",
            ["disc"] = "급료가 밀렸을 때 탈주·반란을 누릅니다",
            ["manage"] = "재계약 때 검투사가 바라는 주급을 누그러뜨립니다",
            ["det"] = "맡은 일을 끝까지 해냅니다. 다른 능력이 조금씩 오르내리는 폭을 줄입니다",
            ["nego"] = "이적료를 깎아 사고 더 받아 팝니다",
            ["network"] = "이적 시장에 이름을 알려 제안이 더 자주 오고, 무소속 명단이 넓어집니다",
        };

        /// <summary>역할마다 중요한 능력치와 무게 (종합 평점 = 가중 평균)</summary>
        public static readonly Dictionary<string, (string key, double w)[]> RoleWeights = new()
        {
            ["weaponCoach"] = new[] { ("coachTec", 2.0), ("coachTac", 0.5), ("motiv", 0.3) },
            ["fitnessCoach"] = new[] { ("coachPhy", 2.0), ("sci", 0.7), ("motiv", 0.3) },
            ["skillCoach"] = new[] { ("coachMen", 1.5), ("coachTac", 1.0), ("motiv", 0.3) },
            ["youthHead"] = new[] { ("judgeP", 1.5), ("coachMen", 0.7), ("motiv", 0.8), ("manage", 0.5) },
            ["doctore"] = new[] { ("coachPhy", 1.0), ("coachTec", 1.0), ("coachMen", 1.0), ("coachTac", 1.0), ("judgeA", 0.5), ("motiv", 0.6), ("disc", 0.4) },
            ["medicus"] = new[] { ("physio", 2.0), ("sci", 1.5), ("manage", 0.3), ("det", 0.3) },
            ["scout"] = new[] { ("judgeA", 1.5), ("judgeP", 1.5), ("network", 1.0), ("det", 0.3) },
            ["agent"] = new[] { ("nego", 2.0), ("network", 1.0), ("manage", 0.5), ("det", 0.3) },
        };

        public static int Attr(Staff s, string key)
        {
            if (s == null) return 0;
            if (s.attrs != null && s.attrs.TryGetValue(key, out var v)) return v;
            return (int)Math.Round(s.rating);
        }

        /// <summary>이 루두스의 그 역할 식솔의 능력치 하나 (없으면 0)</summary>
        public static double AttrOf(GameState state, Ludus ludus, string role, string key) =>
            ludus == null ? 0 : FmRules.Enabled ? Combine(StaffsOf(state, ludus, role).Select(s => (double)Attr(s, key))) : StaffOf(state, ludus, role) is Staff s ? Attr(s, key) : 0;

        /// <summary>세부 능력치에서 역할 종합 평점을 낸다</summary>
        public static double RatingFromAttrs(string role, Dictionary<string, int> attrs)
        {
            double sum = 0, w = 0;
            foreach (var (k, wt) in RoleWeights[role]) { sum += attrs[k] * wt; w += wt; }
            return Math.Max(1, Math.Min(20, JsMath.Round(sum / w)));
        }

        /// <summary>훈련 종류에 맞는 지도 능력치</summary>
        public static string CoachKeyFor(string focus) => focus switch
        {
            "physical" or "grapple" => "coachPhy",
            "weapon" or "defense" or "magic" => "coachTec",
            "mental" or "show" or "sense" => "coachMen",
            "doctrine" => "coachTac",
            _ => null,
        };

        /// <summary>FM식: 훈련 종류별 독토레 성장 배율 (동기 부여도 조금). 꺼져 있으면 CoachMult(rating)</summary>
        public static double CoachMultFor(GameState state, Ludus ludus, string focus)
        {
            var d = StaffOf(state, ludus, "doctore");
            if (!FmRules.Enabled) return CoachMult(RatingOf(state, ludus, "doctore"));
            // FM 세계: 독토레 + 그 분야 교관들의 지도 능력을 합친다 (1·½·¼)
            string key = CoachKeyFor(focus);
            string coach = focus switch
            {
                "weapon" or "defense" or "magic" => "weaponCoach",
                "physical" or "grapple" => "fitnessCoach",
                "mental" or "show" or "sense" or "doctrine" => "skillCoach",
                _ => null,
            };
            var people = new List<Staff>();
            if (d != null) people.Add(d);
            if (coach != null) people.AddRange(StaffsOf(state, ludus, coach));
            if (people.Count == 0) return CoachMult(0);
            double v = Combine(people.Select(p => key != null ? (double)Attr(p, key) : p.rating));
            double bonus = v * 0.015 + (d != null ? (Attr(d, "motiv") - 10) * 0.002 : 0);
            // 교관 1명당 선수 6명까지는 그대로, 그보다 많으면 효율이 떨어진다
            int coaches = CoachRoles.Sum(r => StaffsOf(state, ludus, r).Count);
            int roster = ludus.gladiators.Count + (ludus.youth?.Count ?? 0);
            double load = roster <= 0 ? 1 : Math.Min(1, CoachLoad * Math.Max(1, coaches) / (double)roster);
            return 0.85 + bonus * load;
        }

        /// <summary>FM식: 메디쿠스 '치료'로 부상 회복 배율</summary>
        public static double HealMultOf(GameState state, Ludus ludus) =>
            FmRules.Enabled && StaffOf(state, ludus, "medicus") != null ? HealMult(AttrOf(state, ludus, "medicus", "physio")) : HealMult(RatingOf(state, ludus, "medicus"));

        /// <summary>FM식: 메디쿠스 '몸 관리'로 훈련 부상 확률 배율 (없으면 1)</summary>
        public static double InjuryMultOf(GameState state, Ludus ludus) =>
            FmRules.Enabled && (StaffOf(state, ludus, "medicus") != null || StaffOf(state, ludus, "fitnessCoach") != null)
                ? Math.Max(0.6, 1 - Combine(StaffsOf(state, ludus, "medicus").Concat(StaffsOf(state, ludus, "fitnessCoach")).Select(m => (double)Attr(m, "sci"))) * 0.02) : 1;

        /// <summary>FM식: 중개인 '협상'</summary>
        public static double AgentNego(GameState state, Ludus ludus) =>
            FmRules.Enabled && StaffOf(state, ludus, "agent") != null ? AttrOf(state, ludus, "agent", "nego") : RatingOf(state, ludus, "agent");

        /// <summary>새 식솔의 세부 능력치: 역할에 중요한 것은 평점 근처, 나머지는 낮게 흩어진다</summary>
        static Dictionary<string, int> MakeAttrs(Rng rng, string role, double rating)
        {
            var a = new Dictionary<string, int>();
            var main = new HashSet<string>(RoleWeights[role].Where(x => x.w >= 1).Select(x => x.key));
            var side = new HashSet<string>(RoleWeights[role].Where(x => x.w < 1).Select(x => x.key));
            foreach (var k in AttrKeys)
            {
                double c = main.Contains(k) ? rating + rng.Range(-3.5, 3.5) : side.Contains(k) ? rating * 0.75 + rng.Range(-4, 4) : rating * 0.4 + rng.Range(-3, 5);
                a[k] = (int)Math.Max(1, Math.Min(20, Math.Round(c)));
            }
            // 한 가지는 남다르게 잘한다 (개성)
            var star = rng.Pick(main.ToList());
            a[star] = Math.Min(20, a[star] + rng.Int(1, 3));
            return a;
        }

        /// <summary>그 역할의 대표(가장 잘하는 사람). 없으면 null</summary>
        public static Staff StaffOf(GameState state, Ludus ludus, string role)
        {
            if (ludus.staff == null) return null;
            if (FmRules.Enabled) return StaffsOf(state, ludus, role).FirstOrDefault();
            foreach (var id in ludus.staff)
            {
                Staff s = null;
                if (state.staff != null) state.staff.TryGetValue(id, out s);
                if (s?.role == role) return s;
            }
            return null;
        }

        /// <summary>그 역할의 효과 평점. FM 세계는 여러 명을 합친다 (1·½·¼, 20을 넘을 수 있다)</summary>
        public static double RatingOf(GameState state, Ludus ludus, string role) =>
            ludus == null ? 0 : FmRules.Enabled ? Combine(StaffsOf(state, ludus, role).Select(s => s.rating)) : StaffOf(state, ludus, role)?.rating ?? 0;

        /// <summary>그 역할을 더 둘 수 있나. 못 두면 이유</summary>
        public static string RoleFull(GameState state, Ludus ludus, string role)
        {
            int cap = RoleCap(ludus, role), n = StaffsOf(state, ludus, role).Count;
            if (n < cap) return null;
            return cap == 1 ? $"{RoleNames[role]} 자리가 이미 차 있습니다. 지금 사람을 먼저 내보내야 합니다"
                : $"{RoleNames[role]}는 지금 {cap}명까지 둘 수 있습니다 (부·시설이 오르면 늘어납니다)";
        }

        /// <summary>독토레: 성장 배율 (없으면 0.85, 능력 20이면 1.15)</summary>
        public static double CoachMult(double rating) => 0.85 + rating * 0.015;
        /// <summary>메디쿠스: 부상 회복 배율</summary>
        public static double HealMult(double rating) => 1 + rating * 0.03;
        /// <summary>중개인: 구매가 배율</summary>
        public static double BuyMult(double rating) => 1 - rating * 0.01;
        public static double SellMult(double rating) => 1 + rating * 0.01;

        /// <summary>정찰꾼 실력에 따른 잠재력 추정 범위 (±CA)</summary>
        public static double PaSpread(GameState state, Gladiator g)
        {
            var player = state.ludi[state.playerLudusId];
            double r = FmRules.Enabled ? AttrOf(state, player, "scout", "judgeP") : RatingOf(state, player, "scout");
            bool mine = player.gladiators.Contains(g.id);
            return mine ? Math.Max(3, 30 - r * 1.35) : Math.Max(5, 45 - r * 2);
        }

        public static double StaffWages(GameState state, Ludus ludus)
        {
            double s = 0;
            if (ludus.staff == null) return s;
            foreach (var id in ludus.staff)
            {
                Staff st = null;
                if (state.staff != null) state.staff.TryGetValue(id, out st);
                s = s + (st?.wage ?? 0);
            }
            return s;
        }

        /// <summary>tier: 이 사람을 쓸 루두스의 부 (FM식은 아래 부일수록 주급이 싸다)</summary>
        public static Staff MakeStaff(GameState state, Rng rng, string role, double rating, int tier = 2)
        {
            var races = Data.Nation(state.nation).races;
            string race = rng.Weighted(races);
            string gender = rng.Chance(0.25) ? "f" : "m";
            var s = new Staff();
            s.id = $"S{state.nextId++}";
            if (NameBook.Ready)
                s.name = NameBook.PersonName(rng, race, gender, new HashSet<string>(state.staff.Values.Select(x => x.name).Concat(state.gladiators.Values.Select(x => x.name))));
            else
            {
            var female = new HashSet<string>(Data.Names.female);
            var pool = Data.Names.gladiator[race].Where(n => female.Contains(n) == (gender == "f")).ToList();
            string first = rng.Pick(pool);
            string last = rng.Pick(Data.Names.surname[race]);
            s.name = $"{first} {last}";
            }
            s.race = race;
            s.gender = gender;
            s.age = rng.Int(30, 60);
            s.role = role;
            s.rating = rating;
            if (FmRules.Enabled)
            {
                s.attrs = MakeAttrs(rng, role, rating);
                s.rating = RatingFromAttrs(role, s.attrs);
                rating = s.rating;
            }
            s.wage = JsMath.Round((10 + rating * rating * 0.6) * FmRules.CostTierMult(tier));
            s.portraitSeed = rng.Int(0, 1_000_000);
            state.staff[s.id] = s;
            return s;
        }

        /// <summary>식솔 후보 새로 고침 (이적 시장이 열릴 때)</summary>
        public static void RefreshStaffMarket(GameState state, Rng rng)
        {
            StateUtil.RemoveOrdered(state.staff, state.staffMarket);
            state.staffMarket = new List<string>();
            // 후보는 우리 루두스가 쓸 사람들이라 우리 부 기준 주급
            int tier = state.playerLudusId != null && state.ludi.TryGetValue(state.playerLudusId, out var me) ? me.tier : 2;
            // FM 세계는 AI 루두스도 같은 시장에서 고용하므로 팀 수에 비례해 늘린다
            int perRole = FmRules.Enabled ? 3 * FmRules.TeamsPerTier / 2 : 9;
            foreach (var role in Roles)
            {
                for (int i = 0; i < perRole; i++)
                {
                    double rating = Math.Max(1, Math.Min(20, JsMath.Round(rng.Range(2, 20))));
                    state.staffMarket.Add(MakeStaff(state, rng, role, rating, tier).id);
                }
            }
        }

        /// <summary>새 게임: 낡은 독토레 한 명으로 시작</summary>
        public static void StartingStaff(GameState state, Rng rng, Ludus ludus)
        {
            ludus.staff.Add(MakeStaff(state, rng, "doctore", 6, ludus.tier).id);
        }

        /// <summary>우리 명성에서 이 사람이 원하는 조건</summary>
        public static Demand StaffDemand(GameState state, Staff s) =>
            Reputation.DemandFor(Reputation.StaffStanding(s), s.wage, Reputation.LudusReputation(state, state.ludi[state.playerLudusId]));

        /// <summary>협상해서 고용한다. 실패하면 이유, 성공하면 null</summary>
        public static string HireStaff(GameState state, string id, double offer, double roll)
        {
            var player = state.ludi[state.playerLudusId];
            state.staff.TryGetValue(id, out var s);
            if (!state.staffMarket.Contains(id)) return "이미 떠난 사람입니다";
            if (RoleFull(state, player, s.role) is string full) return full;
            var d = StaffDemand(state, s);
            if (d.refuse) return $"{s.name}: {d.reason}";
            if (!Reputation.AcceptsOffer(offer, d, roll))
            {
                state.staffMarket = state.staffMarket.Where(x => x != id).ToList();
                return $"{Josa.J(s.name, "이/가")} 제안을 거절하고 떠났습니다 (바라던 주급 {StateUtil.Num(d.wage)} D)";
            }
            JoinStaff(state, id, offer);
            return null;
        }

        /// <summary>식솔 주급 흥정 (Unity판 전용)</summary>
        public static HaggleResult HaggleStaff(GameState state, Rng rng, string id, double offer)
        {
            var player = state.ludi[state.playerLudusId];
            state.staff.TryGetValue(id, out var s);
            HaggleResult Err(string line) => new HaggleResult { status = "error", line = line };
            if (!state.staffMarket.Contains(id)) return Err("이미 떠난 사람입니다");
            if (RoleFull(state, player, s.role) is string full) return Err(full);
            var d = StaffDemand(state, s);
            if (d.refuse) return Err($"{s.name}: {d.reason}");
            string key = "staff:" + id;
            Haggling.Open(state, rng, key, d.wage, 0.03, 0.12);
            var res = Haggling.Offer(state, key, offer);
            if (res.Accepted) JoinStaff(state, id, offer);
            return res;
        }

        static void JoinStaff(GameState state, string id, double offer)
        {
            var player = state.ludi[state.playerLudusId];
            state.staff[id].wage = offer;
            state.staffMarket = state.staffMarket.Where(x => x != id).ToList();
            player.staff.Add(id);
        }

        public static void FireStaff(GameState state, string id)
        {
            var player = state.ludi[state.playerLudusId];
            player.staff = player.staff.Where(x => x != id).ToList();
            StateUtil.RemoveOrdered(state.staff, id);
        }
    }
}
