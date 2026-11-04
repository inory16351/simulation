// 랜덤 이벤트 (src/core/events.ts, docs/11_events.md). 정의는 data/events.json(Data.Events), 효과 타입만 코드로 구현한다.
using Colosseum.Core.Combat;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Colosseum.Core
{
    public class EventEffect
    {
        public string type;
        public double? value, min, max;
        /// <summary>random | self | same | youngest | lowestMorale</summary>
        public string target;
        public string attr;
        public double? days;
        public string desc;
        public bool serious;
        public double? chance;
        public string cause;
        public double? win, lose;
        /// <summary>ticket | food | market</summary>
        public string kind;
        public double? mult;
    }

    public class EventChoice
    {
        public string label;
        public double? cost;
        public List<EventEffect> effects = new();
    }

    public class EventDef
    {
        public string id;
        /// <summary>ludus | gladiator | world</summary>
        public string scope;
        /// <summary>luck | opportunity | trial | disaster</summary>
        public string grade;
        public double weight;
        /// <summary>lowMorale | growthStage | popular | lowDiscipline</summary>
        public string condition;
        public string title, text;
        public List<EventChoice> choices = new();
    }

    public static class Events
    {
        static List<EventDef> byIdSource;
        static Dictionary<string, EventDef> byIdCache;

        static Dictionary<string, EventDef> ById
        {
            get
            {
                if (byIdCache == null || !ReferenceEquals(byIdSource, Data.Events))
                {
                    var d = new Dictionary<string, EventDef>();
                    foreach (var e in Data.Events) d[e.id] = e;
                    byIdCache = d;
                    byIdSource = Data.Events;
                }
                return byIdCache;
            }
        }

        public static List<EventDef> All => Data.Events;

        public static EventDef GetEvent(string id) => id != null && ById.TryGetValue(id, out var e) ? e : null;

        static double EV(string k) => (double)Bal.Events[k];

        static void Msg(GameState s, string kind, string title, string body) =>
            StateUtil.PushMessage(s, new InboxMessage { kind = kind, title = title, body = body });

        /// <summary>JS 숫자 → 문자열 (`${n}`)</summary>
        static string Num(double v)
        {
            if (v == Math.Floor(v) && Math.Abs(v) < 1e15) return ((long)v).ToString(CultureInfo.InvariantCulture);
            return v.ToString("R", CultureInfo.InvariantCulture);
        }

        static string Num(double? v) => v.HasValue ? Num(v.Value) : "undefined";

        static List<Gladiator> Roster(GameState state) =>
            state.ludi[state.playerLudusId].gladiators.Select(id => state.gladiators[id]).Where(g => g.alive).ToList();

        static bool ConditionMet(GameState state, EventDef def, Gladiator g = null)
        {
            var r = Roster(state);
            switch (def.condition)
            {
                case "lowMorale":
                {
                    if (r.Count == 0) return false;
                    double sum = 0;
                    foreach (var x in r) sum += x.morale;
                    return sum / r.Count < 40;
                }
                case "growthStage": return g != null && GladiatorRules.AgeStage(g) == "growth";
                case "popular": return g != null && g.popularity >= 45;
                case "lowDiscipline": return g != null && g.attrs["disc"] <= 10;
                default: return true;
            }
        }

        static string Fill(string text, Gladiator g)
        {
            if (g == null) return text;
            // JS String.replace(문자열)은 처음 하나만 바꾼다
            const string pat = "{name}이(가)";
            int i = text.IndexOf(pat, StringComparison.Ordinal);
            if (i >= 0) text = text.Substring(0, i) + Josa.J(g.name, "이/가") + text.Substring(i + pat.Length);
            return text.Replace("{name}", g.name);
        }

        static EventDef PickWeighted(GameState state, Rng rng, List<EventDef> defs)
        {
            if (defs.Count == 0) return null;
            // 운 보정: 악재가 이어지면 행운이, 행운이 이어지면 시련이 조금 더 잘 나온다
            double s = state.luckStreak;
            var w = new Dictionary<string, double>();
            foreach (var d in defs)
            {
                double m = 1;
                if (d.grade == "luck") m = Math.Min(2, 1 + Math.Max(0, s) * 0.25);
                if (d.grade == "trial" || d.grade == "disaster") m = Math.Min(2, 1 + Math.Max(0, -s) * 0.25);
                w[d.id] = d.weight * m;
            }
            return ById[rng.Weighted(w)];
        }

        /// <summary>매일 아침 이벤트 판정</summary>
        public static void RollDailyEvents(GameState state, Rng rng)
        {
            double now = Calendar.AbsDay(state.season, state.day);
            bool disasterOk = now - state.lastDisasterAt >= EV("disasterCooldownDays");
            bool Allowed(EventDef d) => disasterOk || d.grade != "disaster";

            if (rng.Chance(EV("ludusPerDay")))
            {
                var def = PickWeighted(state, rng, Data.Events.Where(d => d.scope == "ludus" && Allowed(d) && ConditionMet(state, d)).ToList());
                if (def != null) Raise(state, rng, def);
            }
            foreach (var g in Roster(state))
            {
                if (!rng.Chance(EV("gladiatorPerDay"))) continue;
                var def = PickWeighted(state, rng, Data.Events.Where(d => d.scope == "gladiator" && Allowed(d) && ConditionMet(state, d, g)).ToList());
                if (def != null) Raise(state, rng, def, g);
                break; // 하루에 검투사 이벤트는 하나만
            }
            if (rng.Chance(EV("worldPerDay")))
            {
                var def = PickWeighted(state, rng, Data.Events.Where(d => d.scope == "world" && Allowed(d)).ToList());
                if (def != null) Raise(state, rng, def);
            }
        }

        /// <summary>Unity판(FM식)은 노예·자유민 구분이 없어 노예 이야기를 바꿔 보여 준다 (웹판 사건 자료는 그대로)</summary>
        static string Unity(string text) => !FmRules.Enabled || text == null ? text : text
            .Replace("노예선 난파", "상선 난파")
            .Replace("카르세아 노예선이 해안에 좌초했습니다. 살아남은 자들이 헐값에 팔려 나옵니다.", "카르세아 상선이 해안에 좌초했습니다. 살아남은 선원과 호위병들이 싼 주급에 일자리를 찾습니다.");

        static void Raise(GameState state, Rng rng, EventDef def, Gladiator target = null)
        {
            if (def.grade == "disaster") state.lastDisasterAt = Calendar.AbsDay(state.season, state.day);
            if (def.grade == "luck") state.luckStreak = Math.Min(state.luckStreak, 0) - 1;
            if (def.grade == "trial" || def.grade == "disaster") state.luckStreak = Math.Max(state.luckStreak, 0) + 1;
            string title = Unity(def.title);
            var pending = new PendingEvent
            {
                id = $"E{state.nextId++}",
                eventId = def.id,
                targetId = target?.id,
                title = title,
                text = Unity(Fill(def.text, target)),
            };
            // 선택지가 하나뿐이면 바로 적용하고 알리기만 한다
            if (def.choices.Count == 1)
            {
                string result = ApplyEffects(state, rng, def.choices[0].effects, target);
                Msg(state, "event", title, $"{pending.text}\n\n{result}".Trim());
            }
            else
            {
                state.pendingEvents.Add(pending);
                Msg(state, "event", $"{title} — 어떻게 할지 정해야 합니다", pending.text);
            }
        }

        /// <summary>플레이어가 고른 선택지를 적용한다</summary>
        public static string ResolveEvent(GameState state, Rng rng, string pendingId, int choiceIdx)
        {
            var p = state.pendingEvents.FirstOrDefault(x => x.id == pendingId);
            if (p == null) return "";
            var def = ById[p.eventId];
            var choice = def.choices[choiceIdx];
            var player = state.ludi[state.playerLudusId];
            if (choice.cost.HasValue && choice.cost.Value != 0 && player.treasury < choice.cost.Value) return "금고에 돈이 모자랍니다.";
            state.pendingEvents = state.pendingEvents.Where(x => x != p).ToList();
            Gladiator target = null;
            if (!string.IsNullOrEmpty(p.targetId)) state.gladiators.TryGetValue(p.targetId, out target);
            string result = ApplyEffects(state, rng, choice.effects, target);
            Msg(state, "event", $"{Unity(def.title)}: {choice.label}", string.IsNullOrEmpty(result) ? "별일 없이 지나갔습니다." : result);
            return result;
        }

        static string Signed(double? v) => (v.Value > 0 ? "+" : "") + Num(v);

        static string ApplyEffects(GameState state, Rng rng, List<EventEffect> effects, Gladiator self)
        {
            var player = state.ludi[state.playerLudusId];
            var lines = new List<string>();
            Gladiator last = self;
            double now = Calendar.AbsDay(state.season, state.day);

            Gladiator ResolveTarget(string t)
            {
                var r = Roster(state);
                if (r.Count == 0) return null;
                switch (t)
                {
                    case "self": return self ?? last;
                    case "same": return last;
                    case "youngest": return r.OrderBy(x => x.age).First();
                    case "lowestMorale": return r.OrderBy(x => x.morale).First();
                    default: return rng.Pick(r);
                }
            }

            foreach (var e in effects)
            {
                var g = !string.IsNullOrEmpty(e.target) ? ResolveTarget(e.target) : null;
                if (g != null) last = g;
                switch (e.type)
                {
                    case "gold":
                    {
                        double v = e.value ?? rng.Int((int)e.min.Value, (int)e.max.Value);
                        Economy.AddMoney(state, player, v, "사건");
                        lines.Add(v >= 0 ? $"금고 +{Num(v)} D" : $"금고 {Num(v)} D");
                        break;
                    }
                    case "goldPercent":
                    {
                        double v = JsMath.Round(player.treasury * e.value.Value);
                        Economy.AddMoney(state, player, v, "사건");
                        lines.Add($"금고 {Num(v)} D");
                        break;
                    }
                    case "moraleAll":
                        foreach (var x in Roster(state)) x.morale = Math.Max(0, Math.Min(100, x.morale + e.value.Value));
                        lines.Add($"모두의 사기 {Signed(e.value)}");
                        break;
                    case "morale":
                        if (g != null) { g.morale = Math.Max(0, Math.Min(100, g.morale + e.value.Value)); lines.Add($"{g.name} 사기 {Signed(e.value)}"); }
                        break;
                    case "attr":
                        if (g != null)
                        {
                            string a = e.attr;
                            g.attrs[a] = (int)Math.Max(1, Math.Min(20, g.attrs[a] + e.value.Value));
                            lines.Add($"{g.name} {Attrs.Names[a]} {Signed(e.value)}");
                        }
                        break;
                    case "growth":
                        if (g != null)
                        {
                            Training.Grow(g, rng, "match", (int)e.days.Value, 1, player.facilities.training);
                            lines.Add($"{Josa.J(g.name, "이/가")} 한층 성장했습니다");
                            Career.LogCareer(state, g, "깨달음을 얻었습니다", "milestone");
                        }
                        break;
                    case "pa":
                        if (g != null)
                        {
                            g.hidden.pa = Math.Min(200, g.hidden.pa + rng.Int((int)e.min.Value, (int)e.max.Value));
                            lines.Add($"{g.name}의 그릇이 커졌습니다");
                            Career.LogCareer(state, g, "숨은 재능이 깨어났습니다", "milestone");
                        }
                        break;
                    case "popularity":
                        if (g != null) { g.popularity = Math.Min(100, g.popularity + e.value.Value); lines.Add($"{g.name} 인기 +{Num(e.value)}"); }
                        break;
                    case "fatigue":
                        if (g != null) g.fatigue = Math.Min(100, g.fatigue + e.value.Value);
                        break;
                    case "fatigueAll":
                        foreach (var x in Roster(state)) x.fatigue = Math.Min(100, x.fatigue + e.value.Value);
                        lines.Add($"모두 지쳤습니다 (피로 +{Num(e.value)})");
                        break;
                    case "injury":
                        if (g != null && g.injury == null)
                        {
                            g.injury = new Injury { severity = e.serious ? "serious" : "light", daysLeft = e.days.Value, desc = e.desc };
                            lines.Add($"{g.name}: {e.desc} ({Num(e.days)}일)");
                            Career.LogCareer(state, g, $"{Josa.J(e.desc, "으로/로")} 앓아누웠습니다", "injury");
                        }
                        break;
                    case "deathChance":
                        if (g != null && rng.Chance(e.chance.Value))
                        {
                            g.alive = false;
                            g.diedAt = new DiedAt { season = state.season, day = state.day, cause = e.cause };
                            var gid = g.id;
                            player.gladiators = player.gladiators.Where(x => x != gid).ToList();
                            state.fallen.Add(g.id);
                            Career.LogCareer(state, g, $"{Josa.J(e.cause, "으로/로")} 죽었습니다", "death");
                            lines.Add($"{Josa.J(g.name, "이/가")} 끝내 숨을 거뒀습니다.");
                        }
                        break;
                    case "loyalty":
                        if (g != null) g.hidden.loyalty = (int)Math.Min(20, g.hidden.loyalty + e.value.Value);
                        break;
                    case "wageRaise":
                        foreach (var x in Roster(state)) x.wage = JsMath.Round(x.wage * (1 + e.value.Value));
                        lines.Add($"모든 주급 +{Num(JsMath.Round(e.value.Value * 100))}%");
                        break;
                    case "gamble":
                        if (rng.Chance(e.chance.Value))
                        {
                            if (e.win.HasValue && e.win.Value != 0) { Economy.AddMoney(state, player, e.win.Value, "사건"); lines.Add($"운이 따랐습니다. +{Num(e.win)} D"); }
                            else lines.Add("무사히 넘어갔습니다.");
                        }
                        else if (e.lose.HasValue && e.lose.Value != 0) { Economy.AddMoney(state, player, e.lose.Value, "사건"); lines.Add($"운이 없었습니다. {Num(e.lose)} D"); }
                        else lines.Add("헛돈만 썼습니다.");
                        break;
                    case "modifier":
                        state.modifiers.Add(new Modifier { kind = e.kind, mult = e.mult.Value, until = now + e.days.Value });
                        break;
                    case "refreshMarket":
                        Economy.RefreshFreeAgents(state, rng);
                        break;
                }
            }
            return string.Join("\n", lines);
        }
    }
}
