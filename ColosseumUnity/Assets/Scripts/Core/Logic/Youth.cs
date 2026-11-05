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

        /// <summary>FM식 모래 시험 응시자 수: 학당 단계가 높을수록 많다 (1단계 3명 ~ 5단계 11명)</summary>
        public static int TrialCount(int lvl) => lvl * 2 + 1;
        /// <summary>모래 시험 응시자를 붙잡아 둘 날 수</summary>
        public const int TrialDays = 28;

        public static readonly string[] OutlookNames = { "잿빛 아이들", "쓸 만한 아이들", "날 선 아이들", "전설의 씨앗" };

        /// <summary>후보 수준: 평범 60% · 괜찮음 25% · 뛰어남 10% · 전설의 씨앗 5%</summary>
        static int RollOutlook(Rng rng)
        {
            double r = rng.Next();
            return r < 0.05 ? 3 : r < 0.15 ? 2 : r < 0.40 ? 1 : 0;
        }

        /// <summary>FM식 우리 루두스 모래 시험 날 (불꽃달 1일, 시즌 중반·정규 리그 한가운데). 첫 시즌도 이날 처음 열린다</summary>
        public const int TrialDay = 85;

        /// <summary>모래 시험 예고일 (모래 시험 4주 전, 꽃달 1일)</summary>
        public const int PreviewDay = TrialDay - 28;

        public static string TrialDate => Calendar.DateLabel(TrialDay);

        /// <summary>FM처럼 모래 시험 예고: 학당장이 다음 입단 후보의 수준을 미리 알린다 (우리 루두스)</summary>
        public static void Preview(GameState state, Rng rng, Ludus ludus)
        {
            if (ludus.facilities.academy == 0) return;
            int o = RollOutlook(rng);
            ludus.youthOutlook = o;
            string line = o switch
            {
                3 => "\"이런 눈빛은 평생 한 번 볼까 말까 합니다. 전설의 씨앗입니다.\"",
                2 => "\"올해는 칼끝이 선 아이가 여럿 보입니다. 기대하셔도 좋습니다.\"",
                1 => "\"모래 위에서 버틸 아이가 한둘은 있을 겁니다.\"",
                _ => "\"솔직히 올해는 잿빛입니다. 큰 기대는 마십시오.\"",
            };
            StateUtil.PushMessage(state, new InboxMessage
            {
                kind = "info",
                title = $"모래 시험 예고: {OutlookNames[o]}",
                body = $"학당 교관의 귀띔입니다. {line}\n\n{TrialDate}에 아이들 {TrialCount(ludus.facilities.academy)}명이 모래 마당에 올라 시험을 치릅니다. 학당 단계가 높을수록 많이 찾아오고 타고난 그릇도 큽니다.",
            });
        }

        // ── FM 세계 단계 4: 유소년 잠재력은 상한 없이 뽑는다. 학당이 낮아도 높은 PA가 나올 수 있지만 드물다 ──

        /// <summary>학당 레벨별 PA 160 이상·180 이상이 나올 확률 (레벨 1~5)</summary>
        public static readonly double[] Pa160 = { 0, 0.015, 0.04, 0.08, 0.14, 0.22 };
        public static readonly double[] Pa180 = { 0, 0.002, 0.006, 0.015, 0.03, 0.06 };

        /// <summary>그 레벨 학당에서 PA 160 이상·180 이상이 나올 확률 (레벨이 표 밖이면 끝값, 소수 레벨은 보간). bonus = 유소년 책임자 등 (0.3 = ×1.3)</summary>
        public static (double p160, double p180) PaOdds(double lvl, double bonus = 0)
        {
            double L = Math.Max(1, Math.Min(Pa160.Length - 1, lvl));
            int a = (int)Math.Floor(L), b = Math.Min(Pa160.Length - 1, a + 1);
            double f = L - a;
            double p160 = Pa160[a] + (Pa160[b] - Pa160[a]) * f, p180 = Pa180[a] + (Pa180[b] - Pa180[a]) * f;
            double m = 1 + Math.Max(0, bonus);
            return (Math.Min(0.6, p160 * m), Math.Min(0.3, p180 * m));
        }

        /// <summary>유소년 잠재력 뽑기 (우리 모래 시험·AI 입학 공용). 대부분 60~159, 확률표대로 160~179 / 180~200</summary>
        public static int RollPa(Rng rng, double lvl, int ca, double bonus = 0)
        {
            var (p160, p180) = PaOdds(lvl, bonus);
            double u = rng.Next();
            int pa;
            if (u < p180) pa = (int)JsMath.Round(rng.Range(180, 200));
            else if (u < p160) pa = (int)JsMath.Round(rng.Range(160, 179.4));
            else
            {
                // 그 밖: 학당이 좋을수록 위쪽으로 쏠린다 (레벨 1 평균 약 95, 레벨 5 약 115)
                double k = Math.Max(0.6, 1.6 - 0.18 * lvl);
                pa = (int)JsMath.Round(60 + 99 * Math.Pow(rng.Next(), k));
            }
            return Math.Max(ca + 10, Math.Min(200, pa));
        }

        /// <summary>유소년 책임자 몫의 확률 가산: 평점 20이면 ×1.5</summary>
        public static double YouthHeadBonus(GameState state, Ludus ludus) => FmRules.Enabled ? StaffLogic.RatingOf(state, ludus, "youthHead") * 0.025 : 0;

        /// <summary>FM식 (우리 루두스): 모래 시험 날(TrialDay)에 후보를 뽑는다. 후보는 학당 서약을 협상해 맺어야 학당에 들어온다</summary>
        public static List<Gladiator> Trial(GameState state, Rng rng, Ludus ludus)
        {
            int lvl = ludus.facilities.academy;
            if (lvl == 0) return new List<Gladiator>();
            ClearTrial(state, ludus);
            // 예고에서 정한 수준대로 (예고가 없었으면 지금 정한다)
            int outlook = ludus.youthOutlook ?? RollOutlook(rng);
            ludus.youthOutlook = null;
            bool golden = outlook == 3;
            var used = StateUtil.UsedNames(state);
            var output = new List<Gladiator>();
            for (int i = 0; i < TrialCount(lvl); i++)
            {
                string race = rng.Weighted(Data.Nation(ludus.nation).races);
                var r = Data.Race(race).age;
                var g = GladiatorGen.Generate(rng, new GenOptions
                {
                    id = $"G{state.nextId++}", nation = ludus.nation, race = race, status = "free",
                    target = rng.Range(2.5, 4.5), age = r.debut - rng.Int(1, 4), paCap = FmRules.Enabled ? null : (int)FacilitiesLogic.AcademyPaCap(lvl), usedNames = used,
                });
                if (FmRules.Enabled) g.hidden.pa = RollPa(rng, lvl, GladiatorRules.ComputeCA(g), YouthHeadBonus(state, ludus));
                // 수준: 괜찮음 +8, 뛰어남 +15, 전설의 씨앗 +20~40 (잠재력)
                g.hidden.pa = Math.Min(200, g.hidden.pa + outlook switch { 1 => 8, 2 => 15, 3 => rng.Int(20, 40), _ => 0 });
                g.wage = rng.Int(2, 5);
                g.training = new TrainingPlan { focus = "physical", intensity = "normal" };
                state.gladiators[g.id] = g;
                output.Add(g);
            }
            // FM처럼 입단 날 후보 명단 카드를 띄운다 (Shell이 차례로)
            (state.ceremonies ??= new List<string>()).Add("youth:trial");
            ludus.youthTrial = output.Select(g => g.id).ToList();
            ludus.youthTrialUntil = Calendar.AbsDay(state.season, state.day) + TrialDays;
            StateUtil.PushMessage(state, new InboxMessage
            {
                kind = "info",
                title = golden ? $"모래 시험: 전설의 씨앗 {output.Count}명!" : $"모래 시험에 아이들 {output.Count}명이 찾아왔습니다",
                body = $"{string.Join("\n", output.Select(g => $"{g.name} ({Data.Race(g.race).name}, {N(g.age)}세)"))}\n\n" +
                    $"유소년 학당 화면에서 아이를 골라 학당 서약을 받으세요. {TrialDays}일이 지나면 서약하지 않은 아이들은 돌아갑니다." +
                    (golden ? "\n교관들이 술렁입니다. 올해 아이들은 그릇이 다릅니다." : ""),
            });
            return output;
        }

        /// <summary>남은 후보를 돌려보낸다 (세계에서 지운다). 그 아이들과의 협상(답 기다림·역제안·흥정)도 없앤다</summary>
        static void ClearTrial(GameState state, Ludus ludus)
        {
            if (ludus.youthTrial == null) return;
            var del = new HashSet<string>(ludus.youthTrial.Where(id => !Economy.IsOwned(state, id)));
            foreach (var id in del) Forget(state, id);
            StateUtil.RemoveOrdered(state.gladiators, del);
            ludus.youthTrial = null;
            ludus.youthTrialUntil = null;
        }

        /// <summary>후보 한 명과의 협상 흔적을 지운다</summary>
        static void Forget(GameState state, string id)
        {
            // 빈 목록은 그대로 둔다 (Negotiations.Daily가 답장을 도는 중일 수 있다 — 거기서 비면 null로)
            state.bids?.RemoveAll(b => b.kind == "youth" && b.gladiatorId == id);
            state.decisions?.RemoveAll(d => d.kind == "youth" && d.gladiatorId == id);
            if (state.decisions?.Count == 0) state.decisions = null;
            Haggling.Forget(state, YouthKey(id));
        }

        // ── 학당 서약 협상 (FM식, 우리 루두스) ──

        public static string YouthKey(string id) => "youth:" + id;

        /// <summary>아이가 바라는 학당 서약 주급: 그릇(잠재력)이 클수록 조금 더 바란다 (보통 3~15 D). 명성 격차는 따지지 않는다</summary>
        public static Demand TrialDemand(GameState s, Gladiator g)
        {
            double w = (3 + Math.Max(0, g.hidden.pa - 70) / 10.0) * Data.Race(g.race).wageMult;
            bool wanted = g.hidden.pa >= 140;
            return new Demand
            {
                wage = Math.Max(2, JsMath.Round(w)),
                reason = wanted ? "\"다른 학당에서도 오라는 말을 들었어요.\"" : "\"여기서 배우고 싶어요.\"",
            };
        }

        /// <summary>협상할 수 있는 후보인지. 안 되면 이유</summary>
        static string CanNegotiate(GameState s, Ludus me, string id, ContractTerms t)
        {
            if (me.youthTrial == null || !me.youthTrial.Contains(id) || !s.gladiators.ContainsKey(id)) return "모래 시험 응시자가 아닙니다";
            if (me.youth.Count >= YouthCapacity(me)) return $"학당 정원({YouthCapacity(me)}명)이 찼습니다. 학당을 증축하거나 다른 아이를 내보내세요";
            if (t != null && me.treasury < t.signingFee) return "계약금을 낼 돈이 없습니다";
            return null;
        }

        /// <summary>학당 서약 제안을 보낸다 (답은 하루이틀 뒤 서판으로). 다른 루두스와 경쟁은 없다</summary>
        public static HaggleResult HaggleTrial(GameState s, Rng rng, string id, ContractTerms t)
        {
            var me = s.ludi[s.playerLudusId];
            var err = CanNegotiate(s, me, id, t);
            if (err != null) return new HaggleResult { status = "error", line = err };
            return Negotiations.Send(s, rng, "youth", id, null, t.wage, t.seasons, t);
        }

        /// <summary>학당 서약 답장 (Negotiations.Daily). 받아들이면 학당에 들어오고, 협상이 깨지면 아이는 집으로 돌아간다</summary>
        internal static HaggleResult ResolveTrial(GameState s, Rng rng, string id, ContractTerms t)
        {
            var me = s.ludi[s.playerLudusId];
            var err = CanNegotiate(s, me, id, t);
            if (err != null) return new HaggleResult { status = "error", line = err };
            var g = s.gladiators[id];
            var res = Contracts.Resolve(s, rng, YouthKey(id), g, t, TrialDemand(s, g), me);
            if (res.Accepted)
            {
                Haggling.Forget(s, YouthKey(id));
                if (t.signingFee > 0) Economy.AddMoney(s, me, -t.signingFee, $"{g.name} 학당 서약금");
                SignTrial(s, me, id, t);
            }
            else if (res.status == "walked")
            {
                me.youthTrial.Remove(id);
                Forget(s, id);
                s.gladiators.Remove(id);
            }
            return res;
        }

        /// <summary>후보와 학당 서약을 맺는다 (협상이 끝난 뒤)</summary>
        static void SignTrial(GameState state, Ludus ludus, string id, ContractTerms t)
        {
            var g = state.gladiators[id];
            ludus.youthTrial.Remove(id);
            ludus.youth.Add(id);
            // 학당 서약: 합의한 조항 그대로, 1군으로 올릴 때 정식 계약을 다시 맺는다
            Contracts.Apply(state, g, t);
            g.contract.clauses.Add("youth");
            Career.Joined(state, g, null, "학당");
            Career.LogCareer(state, g, $"{ludus.name} 유소년 학당과 {t.seasons}시즌 학당 서약을 맺었습니다 ({N(g.age)}세, 주급 {N(t.wage)} D)", "join");
        }

        /// <summary>매일: 모래 시험 마감이 지나면 남은 후보는 돌아간다</summary>
        public static void DailyTrial(GameState state)
        {
            int now = Calendar.AbsDay(state.season, state.day);
            foreach (var l in state.ludi.Values)
            {
                if (l.youthTrial == null || now <= (l.youthTrialUntil ?? 0)) continue;
                int left = l.youthTrial.Count;
                ClearTrial(state, l);
                if (l.isPlayer && left > 0)
                    StateUtil.PushMessage(state, "info", "모래 시험 끝", $"서약하지 않은 아이 {left}명이 돌아갔습니다. 다음 모래 시험은 다음 시즌 {TrialDate}입니다.");
            }
        }

        /// <summary>시즌 시작(서리달 1일) 입학. FM식 우리 루두스는 여기서 받지 않고 시즌 중반 모래 시험(TrialDay, Day.AdvanceDay)에서 골라 협상한다</summary>
        public static List<Gladiator> Intake(GameState state, Rng rng, Ludus ludus)
        {
            int lvl = ludus.facilities.academy;
            if (lvl == 0) return new List<Gladiator>();
            if (FmRules.Enabled && ludus.isPlayer) return new List<Gladiator>();
            int room = YouthCapacity(ludus) - ludus.youth.Count;
            int count = Math.Min(room, lvl + 1);
            if (count <= 0) return new List<Gladiator>();
            // 전설의 씨앗: 드물게 그해 입학생 전원의 그릇이 크다
            bool golden = rng.Chance(0.05);
            var used = StateUtil.UsedNames(state);
            var output = new List<Gladiator>();
            for (int i = 0; i < count; i++)
            {
                string race = rng.Weighted(Data.Nation(ludus.nation).races);
                var r = Data.Race(race).age;
                // 객체 리터럴 순서대로: id(nextId++) → target(rng.range) → age(rng.int)
                var opt = new GenOptions
                {
                    id = $"G{state.nextId++}", nation = ludus.nation, race = race, status = "free",
                    target = rng.Range(2.5, 4.5), age = r.debut - rng.Int(1, 4), paCap = FmRules.Enabled ? null : (int)FacilitiesLogic.AcademyPaCap(lvl), usedNames = used,
                };
                var g = GladiatorGen.Generate(rng, opt);
                if (FmRules.Enabled) g.hidden.pa = RollPa(rng, lvl, GladiatorRules.ComputeCA(g), YouthHeadBonus(state, ludus));
                if (golden) g.hidden.pa = Math.Min(200, g.hidden.pa + rng.Int(20, 40));
                g.wage = rng.Int(2, 5);
                g.training = new TrainingPlan { focus = "physical", intensity = "normal" };
                state.gladiators[g.id] = g;
                ludus.youth.Add(g.id);
                Career.Joined(state, g, null, "학당");
                Career.LogCareer(state, g, $"{ludus.name} 유소년 학당에 들어왔습니다 ({N(g.age)}세)", "join");
                output.Add(g);
            }
            if (ludus.isPlayer && output.Count > 0)
            {
                StateUtil.PushMessage(state, new InboxMessage
                {
                    kind = "info",
                    title = golden ? "전설의 씨앗이 입학했습니다!" : $"유소년 학당 입학생 {output.Count}명",
                    body = $"{string.Join("\n", output.Select(g => $"{g.name} ({Data.Race(g.race).name}, {N(g.age)}세)"))}\n\n{(golden ? "교관들이 술렁입니다. 올해 아이들은 그릇이 다릅니다." : "유소년 학당 화면에서 볼 수 있습니다.")}",
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
                    body = $"{string.Join("\n", lines)}\n\n가장 눈에 띈 아이: {star.name}. {(star.popularity >= 15 ? "다른 루두스에서도 이름을 묻기 시작했습니다." : "")}",
                });
            }
        }

        /// <summary>데뷔 나이가 된 유소년을 1군으로 올린다. null이면 성공</summary>
        public static string PromoteYouth(GameState state, Ludus ludus, string id)
        {
            var g = state.gladiators[id];
            if (GladiatorRules.AgeStage(g) == "youth") return "아직 어립니다";
            ludus.youth = ludus.youth.FindAll(x => x != id);
            ludus.gladiators.Add(id);
            g.wage = Math.Max(8, JsMath.Round(GladiatorRules.ComputeCA(g) * 0.5));
            // FM식: 1군으로 올리면 정식 계약 (바라는 기간, 받을 만한 주급)
            if (FmRules.Enabled)
            {
                // 학당 출신 첫 정식 계약은 FM처럼 가볍게: 받을 만한 주급의 60%
                g.wage = Math.Max(8, JsMath.Round(Valuation.FairWage(state, g) * 0.6));
                g.contract = new GladiatorContract { untilSeason = state.season + Contracts.PreferredSeasons(g) - 1, clauses = new List<string>() };
            }
            // 학당에서 자란 아이는 루두스에 대한 정이 깊다
            g.hidden.loyalty = Math.Min(20, g.hidden.loyalty + 3);
            if (!g.traits.Contains("academy")) g.traits.Add("academy");
            Career.LogCareer(state, g, $"{ludus.name} 1군으로 올라갔습니다", "milestone");
            return null;
        }

        public static void ReleaseYouth(GameState state, Ludus ludus, string id)
        {
            ludus.youth = ludus.youth.FindAll(x => x != id);
            var g = state.gladiators[id];
            Career.LogCareer(state, g, $"{ludus.name} 학당에서 쫓겨났습니다", "other");
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
                body = $"{string.Join("\n", ready.Select(g => $"{Josa.J(g.name, "이/가")} 데뷔할 나이가 됐습니다"))}\n\n유소년 학당 화면에서 1군으로 올리거나 내보낼 수 있습니다.",
            });
        }

        public static bool IsApprenticeDay(GameState state) => (int)Calendar.AbsDay(state.season, state.day) % ApprenticeInterval == 0;
    }
}
