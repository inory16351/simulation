// 도전장 (웹판 ChallengeScreen.tsx): 받은 도전장에 답하고, 도전장을 보내고, 잡힌 경기를 본다.
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class ChallengeScreen : ScreenBase
    {
        static readonly string[] Rules = { "training", "missio", "sudden" };
        static readonly int[] Formats = { 1, 3, 5 };
        static readonly Dictionary<string, string> RuleHint = new()
        {
            ["training"] = "관중과 상금, 명성이 없습니다. 안전하게 실력을 키우지만 RP는 절반만 오르내립니다",
            ["missio"] = "상금 ×3과 관중 수입, 명성을 얻습니다. 지면 관중이 살릴지 죽일지를 정합니다",
            ["sudden"] = "상금 ×6, 관중 수입 ×2.2, 명성 ×2를 얻습니다. 지면 대부분 목숨을 잃습니다",
        };
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);

        [SerializeField] TMP_Text sub;
        [Header("받은 도전장 (줄 원본은 하이어라키에 비활성)")]
        [SerializeField] GameObject incomingPanel;
        [SerializeField] RectTransform incomingTemplate;
        [Header("도전장 보내기")]
        [SerializeField] TMP_Dropdown target;
        [SerializeField] TMP_Text blockHint;
        [SerializeField] TMP_Text myLabel;
        [SerializeField] TMP_Dropdown mine;
        [SerializeField] TMP_Dropdown theirs;
        [Tooltip("1:1, 3:3, 5:5 순서")]
        [SerializeField] List<Button> formatButtons = new();
        [SerializeField] TMP_Text formatHint;
        [Tooltip("훈련전, 미시오, 사투 순서")]
        [SerializeField] List<Button> ruleButtons = new();
        [SerializeField] TMP_Text ruleHint;
        [SerializeField] TMP_Text error;
        [SerializeField] Button send;
        [Header("잡힌 경기 · 답을 기다리는 도전장")]
        [SerializeField] RectTransform scheduledTemplate;
        [SerializeField] GameObject scheduledEmpty;
        [SerializeField] RectTransform outgoingTemplate;
        [SerializeField] GameObject outgoingEmpty;

        TemplateList<RectTransform> incoming, scheduled, outgoing;
        // 화면 상태 (웹판 useState)
        string targetId, myId, theirId, errorText;
        string rule = "training";
        int format = 1;
        readonly Dictionary<string, string> defenders = new();
        List<Ludus> targets = new();
        List<Gladiator> myList = new(), theirList = new();

        void Awake()
        {
            incoming = new TemplateList<RectTransform>(incomingTemplate);
            scheduled = new TemplateList<RectTransform>(scheduledTemplate);
            outgoing = new TemplateList<RectTransform>(outgoingTemplate);
            target.onValueChanged.AddListener(i => { targetId = targets[i].id; theirId = null; errorText = null; Refresh(); });
            mine.onValueChanged.AddListener(i => { myId = myList[i].id; Refresh(); });
            theirs.onValueChanged.AddListener(i => { theirId = i == 0 ? null : theirList[i - 1].id; });
            for (int i = 0; i < formatButtons.Count; i++) { int f = Formats[i]; formatButtons[i].onClick.AddListener(() => { format = f; Refresh(); }); }
            for (int i = 0; i < ruleButtons.Count; i++) { string r = Rules[i]; ruleButtons[i].onClick.AddListener(() => { rule = r; Refresh(); }); }
            send.onClick.AddListener(Send);
        }

        Ludus Player => S.ludi[S.playerLudusId];

        void Send()
        {
            if (myId == null) { errorText = "출전자 고르기"; Refresh(); return; }
            string err = null;
            string pid = Player.id, tid = targetId, me = myId, them = theirId, r = rule;
            int f = format;
            errorText = null;
            G.Act((d, rng) => ChallengeLogic.TryCreateChallenge(d, d.ludi[pid], d.ludi[tid], me, r, out err, them, 0, f));
            errorText = err;
            Refresh();
        }

        public override void Refresh()
        {
            var p = Player;
            var bal = Bal.League;
            int now = ChallengeLogic.Now(S);
            double delay = (double)bal["matchDelayMin"];
            sub.text = $"이번 시즌 {p.matchesThisSeason}/{bal["minMatches"]}경기 · 거절 {p.refusalsUsed}/{bal["freeRefusals"]}회 무료 · 정규 기간은 서리달 15일부터 그림자달 14일까지";
            var alive = p.gladiators.Select(id => S.gladiators[id]).Where(g => g.alive).ToList();

            // 받은 도전장
            var inc = S.challenges.Where(c => c.status == "pending" && c.to == p.id).ToList();
            incomingPanel.SetActive(inc.Count > 0);
            incoming.Sync(inc.Count, (row, i) => BindIncoming(row, inc[i], alive, now + delay));

            // 도전장 보내기
            targets = S.ludi.Values.Where(l => !l.isPlayer && l.nation == S.nation).OrderByDescending(l => l.rp).ToList();
            if (targetId == null || targets.All(l => l.id != targetId))
                targetId = (targets.FirstOrDefault(l => ChallengeLogic.CanChallenge(S, p, l) == null) ?? targets[0]).id;
            Fill(target, targets.Select(l => $"{l.tier}부 · {l.name} (RP {l.rp}){(ChallengeLogic.CanChallenge(S, p, l) != null ? " — 불가" : "")}"),
                targets.FindIndex(l => l.id == targetId));
            var tgt = S.ludi[targetId];
            string block = ChallengeLogic.CanChallenge(S, p, tgt);
            blockHint.gameObject.SetActive(block != null);
            blockHint.text = block ?? "";

            myList = alive;
            if (myId == null || myList.All(g => g.id != myId)) myId = myList.FirstOrDefault(g => ChallengeLogic.Availability(S, g) == null)?.id;
            myLabel.text = format > 1 ? "선봉 (나머지 순서는 경기 날 정합니다)" : "우리 출전자";
            Fill(mine, myList.Select(g => { var busy = ChallengeLogic.Availability(S, g); return FighterLabel(g) + (busy != null ? $" — {busy}" : ""); }),
                myId == null ? 0 : myList.FindIndex(g => g.id == myId));

            theirList = tgt.gladiators.Select(id => S.gladiators[id]).Where(g => g.alive).ToList();
            Fill(theirs, new[] { "상대에게 맡기기" }.Concat(theirList.Select(g => $"{g.name} · {Data.Style(g.style)?.name}")),
                theirId == null ? 0 : theirList.FindIndex(g => g.id == theirId) + 1);

            for (int i = 0; i < formatButtons.Count; i++)
            {
                formatButtons[i].GetComponentInChildren<TMP_Text>().text = SeriesLogic.FormatNames[Formats[i]];
                Seg(formatButtons[i], format == Formats[i]);
            }
            var fm = SeriesLogic.FormatMultTable.TryGetValue(format, out var m) ? m : null;
            formatHint.text = format == 1 ? "한 명씩 한 판" : $"{format}명씩 출전합니다. 이긴 검투사가 남아서 다음 상대와 싸웁니다. RP ×{fm?.rp}, 돈 ×{fm?.money}";

            bool suddenBanned = NationRules.RulesOfNation(S.nation).suddenBanned;
            for (int i = 0; i < ruleButtons.Count; i++)
            {
                ruleButtons[i].GetComponentInChildren<TMP_Text>().text = (string)Bal.Rule(Rules[i])["name"];
                ruleButtons[i].interactable = !(Rules[i] == "sudden" && suddenBanned);
                Seg(ruleButtons[i], rule == Rules[i]);
            }
            ruleHint.text = RuleHint[rule];

            error.gameObject.SetActive(errorText != null);
            error.text = errorText ?? "";
            send.interactable = myId != null && block == null;
            send.transform.Find("Fill").GetComponent<Image>().color = rule == "sudden" ? Theme.BloodDark : Theme.ContinueBg;

            // 잡힌 경기
            var sch = S.challenges.Where(c => c.status == "scheduled" && (c.from == p.id || c.to == p.id)).OrderBy(c => c.matchAt).ToList();
            scheduledEmpty.SetActive(sch.Count == 0);
            scheduled.Sync(sch.Count, (row, i) => BindScheduled(row, sch[i], now));

            var outs = S.challenges.Where(c => c.status == "pending" && c.from == p.id).ToList();
            outgoingEmpty.SetActive(outs.Count == 0);
            outgoing.Sync(outs.Count, (row, i) =>
            {
                var c = outs[i];
                T(row, "Opp").text = S.ludi[c.to].name;
                T(row, "Rule").text = RuleName(c.rule);
                T(row, "Fighter").text = S.gladiators[c.fromGladiator].name;
            });
        }

        void BindIncoming(RectTransform row, Challenge c, List<Gladiator> alive, double at)
        {
            var from = S.ludi[c.from];
            var their = S.gladiators[c.fromGladiator];
            var ready = alive.Where(g => ChallengeLogic.Availability(S, g, at) == null).ToList();
            if (!defenders.TryGetValue(c.id, out var pick) || ready.All(g => g.id != pick)) pick = ready.FirstOrDefault()?.id;

            T(row, "Opp").text = $"{from.name} <size=80%><color={Theme.Hex(Theme.TextDim)}>RP {from.rp}</color></size>";
            row.GetComponentInChildren<PortraitView>(true).Show(their);
            T(row, "Their/Name").text = $"{their.name} <color={Theme.Hex(Theme.Torch)}>{Widgets.Stars(GladiatorRules.ComputeCA(their))}</color>";
            var rt = T(row, "Rule");
            rt.text = RuleLabel(c);
            rt.color = c.rule == "sudden" ? Neg : Theme.Text;
            T(row, "RespondBy").text = Calendar.DateLabel(Calendar.FromAbs(c.respondBy).day);

            var dd = row.Find("Defender").GetComponent<TMP_Dropdown>();
            Fill(dd, ready.Select(FighterLabel), Mathf.Max(0, ready.FindIndex(g => g.id == pick)));
            dd.onValueChanged.RemoveAllListeners();
            string cid = c.id;
            dd.onValueChanged.AddListener(v => defenders[cid] = ready[v].id);

            var accept = row.Find("Accept").GetComponent<Button>();
            accept.interactable = pick != null;
            accept.onClick.RemoveAllListeners();
            accept.onClick.AddListener(() =>
            {
                string def = defenders.TryGetValue(cid, out var d0) ? d0 : pick;
                G.Act((d, rng) => ChallengeLogic.Respond(d, d.challenges.First(x => x.id == cid), true, rng, def));
            });
            var decline = row.Find("Decline").GetComponent<Button>();
            decline.onClick.RemoveAllListeners();
            decline.onClick.AddListener(() => G.Act((d, rng) => ChallengeLogic.Respond(d, d.challenges.First(x => x.id == cid), false, rng)));
        }

        void BindScheduled(RectTransform row, Challenge c, int now)
        {
            bool iAmFrom = c.from == S.playerLudusId;
            var opp = S.ludi[iAmFrom ? c.to : c.from];
            string meId = iAmFrom ? c.fromGladiator : c.toGladiator, themId = iAmFrom ? c.toGladiator : c.fromGladiator;
            var me = meId != null && S.gladiators.TryGetValue(meId, out var a) ? a : null;
            var them = themId != null && S.gladiators.TryGetValue(themId, out var b) ? b : null;
            bool today = c.matchAt == now;
            row.GetComponent<Image>().color = today ? new Color(0.89f, 0.6f, 0.23f, 0.08f) : new Color(0, 0, 0, 0);
            var date = T(row, "Date");
            date.text = today ? "<b>오늘</b>" : Calendar.DateLabel(Calendar.FromAbs(c.matchAt.Value).day);
            date.color = today ? Theme.Torch : Theme.Text;
            var rt = T(row, "Rule");
            rt.text = RuleLabel(c);
            rt.color = c.rule == "sudden" ? Neg : Theme.Text;
            T(row, "Desc").text = c.format is int f && f != 0 ? $"{Josa.J(opp.name, "과/와")} 단체전 (선봉 {me?.name ?? "미정"})" : $"{me?.name} vs {them?.name}";
            T(row, "Opp").text = opp.name;
            var go = row.Find("Go").GetComponent<Button>();
            go.gameObject.SetActive(today);
            go.onClick.RemoveAllListeners();
            go.onClick.AddListener(() => G.Go("match"));
        }

        static string RuleName(string r) => (string)Bal.Rule(r)["name"];
        static string RuleLabel(Challenge c) => (c.format is int f && f != 0 ? $"{SeriesLogic.FormatNames[f]} · " : "") + RuleName(c.rule);
        static string FighterLabel(Gladiator g) => $"{g.name} · {Data.Style(g.style)?.name} · CA {GladiatorRules.ComputeCA(g)}";
        static TMP_Text T(Transform row, string path) => row.Find(path).GetComponent<TMP_Text>();

        static void Fill(TMP_Dropdown d, IEnumerable<string> labels, int value)
        {
            var list = labels.ToList();
            bool same = d.options.Count == list.Count && d.options.Select(o => o.text).SequenceEqual(list);
            if (!same) { d.ClearOptions(); d.AddOptions(list); }
            d.SetValueWithoutNotify(Mathf.Clamp(value, 0, Mathf.Max(0, list.Count - 1)));
        }

        /// <summary>웹판 .seg 버튼: 고른 것만 횃불색</summary>
        static void Seg(Button b, bool on)
        {
            b.transform.Find("Fill").GetComponent<Image>().color = on ? Theme.ContinueBg : Theme.Panel2;
            b.GetComponent<Image>().color = on ? Theme.Torch : Theme.Line;
        }
    }
}
