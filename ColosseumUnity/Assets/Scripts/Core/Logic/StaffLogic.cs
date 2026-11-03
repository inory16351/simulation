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
            double r = RatingOf(state, player, "scout");
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

        public static Staff MakeStaff(GameState state, Rng rng, string role, double rating)
        {
            var races = Data.Nation(state.nation).races;
            string race = rng.Weighted(races);
            var female = new HashSet<string>(Data.Names.female);
            string gender = rng.Chance(0.25) ? "f" : "m";
            var pool = Data.Names.gladiator[race].Where(n => female.Contains(n) == (gender == "f")).ToList();
            var s = new Staff();
            s.id = $"S{state.nextId++}";
            string first = rng.Pick(pool);
            string last = rng.Pick(Data.Names.surname[race]);
            s.name = $"{first} {last}";
            s.race = race;
            s.gender = gender;
            s.age = rng.Int(30, 60);
            s.role = role;
            s.rating = rating;
            s.wage = JsMath.Round(10 + rating * rating * 0.6);
            s.portraitSeed = rng.Int(0, 1_000_000);
            state.staff[s.id] = s;
            return s;
        }

        /// <summary>식솔 후보 새로 고침 (이적 시장이 열릴 때)</summary>
        public static void RefreshStaffMarket(GameState state, Rng rng)
        {
            StateUtil.RemoveOrdered(state.staff, state.staffMarket);
            state.staffMarket = new List<string>();
            foreach (var role in Roles)
            {
                for (int i = 0; i < 9; i++)
                {
                    double rating = Math.Max(1, Math.Min(20, JsMath.Round(rng.Range(2, 20))));
                    state.staffMarket.Add(MakeStaff(state, rng, role, rating).id);
                }
            }
        }

        /// <summary>새 게임: 낡은 독토레 한 명으로 시작</summary>
        public static void StartingStaff(GameState state, Rng rng, Ludus ludus)
        {
            ludus.staff.Add(MakeStaff(state, rng, "doctore", 6).id);
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
