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
        };
        public static readonly Dictionary<string, string> RoleDesc = new()
        {
            ["doctore"] = "수석 교관. 검투사가 훈련과 실전에서 더 빨리 크고, 교범도 더 빨리 익힙니다",
            ["medicus"] = "의원. 부상을 빨리 낫게 하고, 사투에서 목숨을 건질 확률을 높입니다",
            ["scout"] = "정찰꾼. 검투사의 숨은 그릇(잠재력)을 더 정확히 봅니다",
            ["agent"] = "중개인. 이적료를 깎아 사고, 팔 때는 더 받아 냅니다",
        };

        /// <summary>역할마다 한 명만 둔다</summary>
        public static readonly string[] Roles = { "doctore", "medicus", "scout", "agent" };

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
            ludus == null ? 0 : StaffOf(state, ludus, role) is Staff s ? Attr(s, key) : 0;

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
            if (!FmRules.Enabled || d == null) return CoachMult(RatingOf(state, ludus, "doctore"));
            string key = CoachKeyFor(focus);
            double v = key != null ? Attr(d, key) : d.rating;
            return 0.85 + v * 0.015 + (Attr(d, "motiv") - 10) * 0.002;
        }

        /// <summary>FM식: 메디쿠스 '치료'로 부상 회복 배율</summary>
        public static double HealMultOf(GameState state, Ludus ludus) =>
            FmRules.Enabled && StaffOf(state, ludus, "medicus") is Staff m ? HealMult(Attr(m, "physio")) : HealMult(RatingOf(state, ludus, "medicus"));

        /// <summary>FM식: 메디쿠스 '몸 관리'로 훈련 부상 확률 배율 (없으면 1)</summary>
        public static double InjuryMultOf(GameState state, Ludus ludus) =>
            FmRules.Enabled && StaffOf(state, ludus, "medicus") is Staff m ? Math.Max(0.6, 1 - Attr(m, "sci") * 0.02) : 1;

        /// <summary>FM식: 중개인 '협상'</summary>
        public static double AgentNego(GameState state, Ludus ludus) =>
            FmRules.Enabled && StaffOf(state, ludus, "agent") is Staff a ? Attr(a, "nego") : RatingOf(state, ludus, "agent");

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

        public static Staff StaffOf(GameState state, Ludus ludus, string role)
        {
            if (ludus.staff == null) return null;
            foreach (var id in ludus.staff)
            {
                Staff s = null;
                if (state.staff != null) state.staff.TryGetValue(id, out s);
                if (s?.role == role) return s;
            }
            return null;
        }

        public static double RatingOf(GameState state, Ludus ludus, string role) =>
            ludus != null ? StaffOf(state, ludus, role)?.rating ?? 0 : 0;

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
            foreach (var role in Roles)
            {
                for (int i = 0; i < 9; i++)
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
            if (StaffOf(state, player, s.role) != null) return $"{RoleNames[s.role]} 자리가 이미 차 있습니다. 지금 사람을 먼저 내보내야 합니다";
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
            if (StaffOf(state, player, s.role) != null) return Err($"{RoleNames[s.role]} 자리가 이미 차 있습니다. 지금 사람을 먼저 내보내야 합니다");
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
