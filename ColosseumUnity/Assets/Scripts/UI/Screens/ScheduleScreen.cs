// 일정 (Unity판 전용): 달력으로 시즌 일정을 보고, 고른 날짜에 도전장(정규 리그 기간)이나 연습전(휴식기, 컵에서 탈락한 팀)을 보낸다.
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class ScheduleScreen : ScreenBase
    {
        static readonly string[] Rules = { "training", "missio", "sudden" };
        static readonly int[] Formats = { 1, 3, 5 };
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);

        [Header("달 넘기기")]
        [SerializeField] Button prevMonth;
        [SerializeField] Button nextMonth;
        [SerializeField] TMP_Text monthTitle;
        [Tooltip("하루 칸 원본 (그리드 안에 비활성). 28개를 복제한다")]
        [SerializeField] RectTransform cellTemplate;
        [Header("고른 날")]
        [SerializeField] TMP_Text dayTitle;
        [SerializeField] TMP_Text dayPeriod;
        [SerializeField] TMP_Text dayEvents;
        [Header("도전장 · 연습전 보내기")]
        [SerializeField] TMP_Text formTitle;
        [SerializeField] TMP_Text blockHint;
        [SerializeField] GameObject formBody;
        [SerializeField] TMP_Dropdown target;
        [SerializeField] TMP_Dropdown mine;
        [Tooltip("1:1, 3:3, 5:5 순서")]
        [SerializeField] List<Button> formatButtons = new();
        [Tooltip("훈련전, 미시오, 사투 순서")]
        [SerializeField] List<Button> ruleButtons = new();
        [SerializeField] TMP_Text ruleHint;
        [SerializeField] TMP_Text error;
        [SerializeField] Button send;

        TemplateList<RectTransform> cells;
        int month = -1, selected;
        string targetId, myId, errorText;
        string rule = "training";
        int format = 1;
        List<Ludus> targets = new();
        List<Gladiator> myList = new();

        /// <summary>달력 칸에 쓰는 짧은 글과 오른쪽 상세에 쓰는 긴 글</summary>
        struct Ev { public string brief, full; public Color color; }

        void Awake()
        {
            cells = new TemplateList<RectTransform>(cellTemplate);
            prevMonth.onClick.AddListener(() => { month = Mathf.Max(0, month - 1); Refresh(); });
            nextMonth.onClick.AddListener(() => { month = Mathf.Min(Calendar.Months.Length - 1, month + 1); Refresh(); });
            target.onValueChanged.AddListener(i => { if (i < targets.Count) targetId = targets[i].id; errorText = null; Refresh(); });
            mine.onValueChanged.AddListener(i => { if (i < myList.Count) myId = myList[i].id; errorText = null; Refresh(); });
            for (int i = 0; i < formatButtons.Count; i++) { int f = Formats[i]; formatButtons[i].onClick.AddListener(() => { format = f; errorText = null; Refresh(); }); }
            for (int i = 0; i < ruleButtons.Count; i++) { string r = Rules[i]; ruleButtons[i].onClick.AddListener(() => { rule = r; Refresh(); }); }
            send.onClick.AddListener(Send);
        }

        void OnEnable()
        {
            // 화면을 열 때마다 오늘이 있는 달, 오늘 날짜부터
            month = -1;
        }

        Ludus Player => S.ludi[S.playerLudusId];

        void Send()
        {
            if (myId == null) { errorText = "출전자 고르기"; Refresh(); return; }
            if (targetId == null) return;
            string err = null;
            string pid = Player.id, tid = targetId, me = myId, r = rule;
            int f = format, day = selected;
            G.Act((d, rng) => ChallengeLogic.TryCreateChallengeOn(d, d.ludi[pid], d.ludi[tid], me, r, day, out err, f));
            errorText = err;
            if (err == null) targetId = null;
            Refresh();
        }

        public override void Refresh()
        {
            if (month < 0)
            {
                month = (S.day - 1) / Calendar.DaysPerMonth;
                selected = S.day;
            }
            int first = month * Calendar.DaysPerMonth + 1;
            monthTitle.text = $"{S.season}시즌 · {Calendar.Months[month]}";
            prevMonth.interactable = month > 0;
            nextMonth.interactable = month < Calendar.Months.Length - 1;
            if (selected < first || selected >= first + Calendar.DaysPerMonth) selected = first;

            cells.Sync(Calendar.DaysPerMonth, (cell, i) => BindCell(cell, first + i));
            RefreshDay();
        }

        // ───────── 달력 칸 ─────────

        void BindCell(RectTransform cell, int day)
        {
            bool past = day < S.day, today = day == S.day, sel = day == selected;
            bool league = ChallengeLogic.IsLeagueDay(day);
            var evs = EventsOn(day);
            cell.GetComponent<Image>().color = sel ? Theme.Torch : today ? Theme.BronzeHi : Theme.Line;
            cell.Find("Fill").GetComponent<Image>().color = past ? Theme.Bg : league ? Theme.Panel2 : Theme.Bg2;
            cell.Find("Today").gameObject.SetActive(today);
            var num = T(cell, "Num");
            num.text = today ? $"{Calendar.DayOfMonth(day)} <size=75%>오늘</size>" : Calendar.DayOfMonth(day).ToString();
            num.color = past ? Theme.TextFaint : today ? Theme.Torch : Theme.Parchment;
            T(cell, "Tag").text = Calendar.IsPayday(day) ? "급료일" : "";
            var lines = evs.Take(3).Select(e => $"<color={Theme.Hex(past ? Theme.TextFaint : e.color)}>{e.brief}</color>").ToList();
            if (evs.Count > 3) lines.Add($"<color={Theme.Hex(Theme.TextDim)}>+{evs.Count - 3}</color>");
            T(cell, "Lines").text = string.Join("\n", lines);
            var b = cell.GetComponent<Button>();
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => { selected = day; errorText = null; targetId = null; Refresh(); });
        }

        /// <summary>그날의 일정: 우리 경기가 먼저, 그다음 대회·시장 같은 달력 표시</summary>
        List<Ev> EventsOn(int day)
        {
            var o = new List<Ev>();
            var p = Player;
            string me = p.id;
            double abs = Calendar.AbsDay(S.season, day);

            foreach (var c in S.challenges.Where(c => (c.from == me || c.to == me) && c.status == "scheduled" && c.matchAt == abs))
            {
                bool iAmFrom = c.from == me;
                var opp = S.ludi[iAmFrom ? c.to : c.from];
                string mine = iAmFrom ? c.fromGladiator : c.toGladiator, theirs = iAmFrom ? c.toGladiator : c.fromGladiator;
                string fmt = c.format is int f && f > 1 ? $"{SeriesLogic.FormatNames[f]} " : "";
                string who = c.format is int f2 && f2 > 1 ? "" : $" ({Tips.Name(G1(mine))} vs {Tips.Name(G1(theirs))})";
                o.Add(new Ev
                {
                    brief = $"{ChallengeLogic.RuleLabel(c)} {opp.name}",
                    full = $"{fmt}{ChallengeLogic.RuleLabel(c)} · {Tips.Ludus(opp)}{who}",
                    color = c.rule == "sudden" ? Neg : c.friendly == true ? Theme.Good : Theme.Torch,
                });
            }
            foreach (var c in S.challenges.Where(c => c.from == me && c.status == "pending" && c.wantAt == abs))
            {
                var opp = S.ludi[c.to];
                o.Add(new Ev { brief = $"답 대기 {opp.name}", full = $"{Tips.Ludus(opp)}에 보낸 {ChallengeLogic.RuleLabel(c)} — 내일 답이 옵니다", color = Theme.TextDim });
            }

            if (FmRules.Enabled)
            {
                // 우리 컵 경기 (이미 대진이 나온 것)
                var nc = NatCupLogic.Of(S, S.nation);
                bool ourNat = false, ourPan = false;
                if (nc != null && nc.season == S.season)
                    foreach (var t in nc.ties.Where(t => t.day == day && (t.home == me || t.away == me)))
                    {
                        ourNat = true;
                        o.Add(TieEv(NatCupLogic.Name(S.nation), Tips.NatCup(S.nation), NatCupLogic.StageNames.GetValueOrDefault(t.stage, t.stage), t));
                    }
                if (S.cup != null && S.cup.season == S.season)
                    foreach (var t in S.cup.ties.Where(t => t.day == day && (t.home == me || t.away == me)))
                    {
                        ourPan = true;
                        o.Add(TieEv("판테온", Tips.Cup(), CupLogic.StageNames.GetValueOrDefault(t.stage, t.stage), t));
                    }

                // 대회 달력
                if (day == 15) o.Add(Mark("리그 개막", "정규 리그 개막 — 오늘부터 도전장을 주고받습니다", Theme.Parchment));
                if (day == FmRules.RegularEnd) o.Add(Mark("리그 마지막 날", "정규 리그 마지막 날 — 다음 날 순위가 굳고 승강이 정해집니다", Theme.Parchment));
                if (day == NatCupLogic.DrawDay) o.Add(Mark("국내 컵 추첨", $"{Tips.NatCup(S.nation)} 추첨 — 1~4부 40팀이 모두 나옵니다", Theme.Parchment));
                int ko = System.Array.IndexOf(NatCupLogic.KoDays, day);
                if (ko >= 0 && !ourNat) o.Add(Mark($"국내 컵 {NatCupLogic.StageNames[NatCupLogic.KoStages[ko]]}", $"{Tips.NatCup(S.nation)} {NatCupLogic.StageNames[NatCupLogic.KoStages[ko]]}", Theme.TextDim));
                if (NatCupLogic.LeagueDays.Contains(day) && !ourNat) o.Add(Mark("국내 컵 5강", $"{Tips.NatCup(S.nation)} 5강 리그", Theme.TextDim));
                if (day == CupLogic.FmDrawDay) o.Add(Mark("판테온 추첨", $"{Tips.Cup()} 추첨", Theme.Parchment));
                string pan = CupLogic.FmPoDays.Contains(day) ? "플레이오프" : CupLogic.FmGroupDays.Contains(day) ? "조별 리그"
                    : day == CupLogic.FmQfDay ? "8강" : day == CupLogic.FmSfDay ? "4강" : day == CupLogic.FmFinalDay ? "결승" : null;
                if (pan != null && !ourPan) o.Add(Mark($"판테온 {pan}", $"{Tips.Cup()} {pan}", Theme.TextDim));
                if (OlympicaLogic.IsMatchDay(S, day))
                {
                    var tie = OlympicaLogic.Current(S)?.ties.FirstOrDefault(t => t.day == day && (t.home == S.nation || t.away == S.nation));
                    string vs = tie != null ? $" — {Tips.Nation(tie.home == S.nation ? tie.away : tie.home)}전" : "";
                    o.Add(Mark("대표 주간", $"{Tips.Olympica()} 대표 주간{vs}. 이날은 리그 경기가 없습니다", Theme.TextDim));
                }
            }
            if (day == 1) o.Add(Mark("시즌 시작", "시즌 시작 — 이적 시장이 열려 있습니다 (14일까지)", Theme.Parchment));
            if (day == 14 || day == 91) o.Add(Mark("이적 시장 닫힘", "이적 시장이 닫히는 날입니다", Theme.TextDim));
            if (day == 78) o.Add(Mark("이적 시장 열림", "이적 시장이 열립니다 (91일까지 14일 동안)", Theme.Parchment));
            if (day == Calendar.DaysPerSeason) o.Add(Mark("시즌 끝", "시즌 마지막 날 — 승강과 계약 만료가 처리됩니다", Theme.Parchment));
            return o;
        }

        Ev TieEv(string brief, string link, string stage, CupTie t)
        {
            string me = S.playerLudusId;
            var opp = S.ludi.TryGetValue(t.home == me ? t.away : t.home, out var x) ? x : null;
            string res = string.IsNullOrEmpty(t.winner) ? "" : t.winner == me ? " — 승리" : " — 패배";
            return new Ev { brief = $"{brief} {stage}", full = $"{link} {stage} · {Tips.Ludus(opp)}{res}", color = Theme.Torch };
        }

        static Ev Mark(string brief, string full, Color c) => new Ev { brief = brief, full = full, color = c };

        Gladiator G1(string id) => id != null && S.gladiators.TryGetValue(id, out var g) ? g : null;

        // ───────── 고른 날 ─────────

        void RefreshDay()
        {
            int day = selected;
            int diff = day - S.day;
            string when = diff == 0 ? "오늘" : diff == 1 ? "내일" : diff > 0 ? $"{diff}일 뒤" : $"{-diff}일 전";
            dayTitle.text = $"{Calendar.DateLabel(day)} <size=75%><color={Theme.Hex(Theme.TextDim)}>({when})</color></size>";
            bool league = ChallengeLogic.IsLeagueDay(day);
            dayPeriod.text = league ? "정규 리그 기간 — 리그 도전장을 보낼 수 있습니다" : "휴식기 — 컵에서 탈락한 팀과 연습전을 잡을 수 있습니다";
            var evs = EventsOn(day);
            dayEvents.text = evs.Count == 0 ? $"<color={Theme.Hex(Theme.TextDim)}>잡힌 일정이 없습니다.</color>" : string.Join("\n", evs.Select(e => "· " + e.full));

            formTitle.text = league ? "이날 도전장 보내기" : "이날 연습전 잡기";
            var p = Player;
            string block = ChallengeLogic.DateBlock(S, day);
            double at = Calendar.AbsDay(S.season, day);
            int fmtNow = format;

            // 상대: 이날 보낼 수 있는 루두스만
            var all = S.ludi.Values.Where(l => !l.isPlayer && l.nation == p.nation).OrderBy(l => l.tier).ThenByDescending(l => l.rp).ToList();
            targets = block != null ? new List<Ludus>() : all.Where(l => ChallengeLogic.CanChallengeOn(S, p, l, day, fmtNow) == null).ToList();
            if (block == null && targets.Count == 0)
                block = all.Select(l => ChallengeLogic.CanChallengeOn(S, p, l, day, fmtNow)).Where(x => x != null).GroupBy(x => x).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key
                    ?? (league ? "도전할 수 있는 루두스가 없습니다" : "연습전을 받을 팀이 없습니다");
            blockHint.gameObject.SetActive(block != null);
            blockHint.text = block ?? "";
            formBody.SetActive(block == null);
            if (block != null) return;

            if (targetId == null || targets.All(l => l.id != targetId)) targetId = targets[0].id;
            Fill(target, targets.Select(l => $"{l.tier}부 · {l.name} (RP {l.rp})"), targets.FindIndex(l => l.id == targetId));

            myList = p.gladiators.Select(id => S.gladiators[id]).Where(g => g.alive).ToList();
            if (myId == null || myList.All(g => g.id != myId) || !ChallengeLogic.FreeOn(S, S.gladiators[myId], at, league))
                myId = myList.FirstOrDefault(g => ChallengeLogic.FreeOn(S, g, at, league))?.id ?? myId;
            Fill(mine, myList.Select(g => FighterLabel(g) + (ChallengeLogic.FreeOn(S, g, at, league) ? "" : $" — {ChallengeLogic.Availability(S, g, at, league) ?? "그날 경기 예정"}")),
                myId == null ? 0 : Mathf.Max(0, myList.FindIndex(g => g.id == myId)));

            for (int i = 0; i < formatButtons.Count; i++)
            {
                formatButtons[i].GetComponentInChildren<TMP_Text>().text = SeriesLogic.FormatNames[Formats[i]];
                Seg(formatButtons[i], format == Formats[i]);
            }

            // 연습전은 훈련전만 (관중·상금·명성·RP 없음)
            if (!league) rule = "training";
            bool suddenBanned = NationRules.RulesOfNation(S.nation).suddenBanned;
            for (int i = 0; i < ruleButtons.Count; i++)
            {
                ruleButtons[i].GetComponentInChildren<TMP_Text>().text = (string)Bal.Rule(Rules[i])["name"];
                ruleButtons[i].interactable = league ? !(Rules[i] == "sudden" && suddenBanned) : Rules[i] == "training";
                Seg(ruleButtons[i], rule == Rules[i]);
            }
            ruleHint.text = !league ? "연습전은 훈련전 규칙으로만 치릅니다. 명성·상금·관중 수입·RP가 없고, 실전 경험으로 성장만 얻습니다."
                : rule == "training" ? "관중과 상금, 명성이 없습니다. RP는 절반만 오르내립니다"
                : rule == "missio" ? "상금 ×3과 관중 수입, 명성을 얻습니다. 지면 관중이 살릴지 죽일지를 정합니다"
                : "상금 ×6, 관중 수입 ×2.2, 명성 ×2를 얻습니다. 지면 대부분 목숨을 잃습니다";

            error.gameObject.SetActive(errorText != null);
            error.text = errorText ?? "";
            send.GetComponentInChildren<TMP_Text>().text = league ? "도전장 보내기" : "연습전 제안하기";
            send.interactable = myId != null && targetId != null;
            send.transform.Find("Fill").GetComponent<Image>().color = rule == "sudden" ? Theme.BloodDark : Theme.ContinueBg;
        }

        string FighterLabel(Gladiator g) => $"{g.name} · {Data.Style(g.style)?.name} · {Widgets.Stars(Scouting.CaRange(S, g).mid)}";
        static TMP_Text T(Transform row, string path) => row.Find(path).GetComponent<TMP_Text>();

        static void Fill(TMP_Dropdown d, IEnumerable<string> labels, int value)
        {
            var list = labels.ToList();
            bool same = d.options.Count == list.Count && d.options.Select(o => o.text).SequenceEqual(list);
            if (!same) { d.ClearOptions(); d.AddOptions(list); }
            d.SetValueWithoutNotify(Mathf.Clamp(value, 0, Mathf.Max(0, list.Count - 1)));
        }

        static void Seg(Button b, bool on)
        {
            b.transform.Find("Fill").GetComponent<Image>().color = on ? Theme.ContinueBg : Theme.Panel2;
            b.GetComponent<Image>().color = on ? Theme.Torch : Theme.Line;
        }
    }
}
