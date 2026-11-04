// 판테온 컵 (웹판 CupScreen.tsx): 오늘의 컵 경기(출전 순서), 나라 계수, 우리 경기, 조별 순위, 토너먼트.
// 경기 중계는 모래 마당 화면(MatchScreen.RunSeries)에서 하고 끝나면 이 화면으로 돌아온다.
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class CupScreen : ScreenBase
    {
        static readonly string[] Positions = { "출전 1", "출전 2", "출전 3" };
        static readonly string[] Stages = { "qf", "sf", "final" };
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);

        [SerializeField] MatchScreen match;
        [SerializeField] Image trophy;
        [SerializeField] TMP_Text status;
        [Header("플레이오프 (FM식)")]
        [SerializeField] GameObject poRow;
        [SerializeField] List<RectTransform> poPanels = new();
        readonly List<TemplateList<RectTransform>> poRows = new();
        [Header("오늘의 컵 경기")]
        [SerializeField] GameObject todayPanel;
        [SerializeField] TMP_Text todayTitle;
        [SerializeField] RectTransform todayHome;
        [SerializeField] RectTransform todayAway;
        [SerializeField] List<TMP_Dropdown> lineupSlots = new();
        [SerializeField] GameObject shortWarn;
        [SerializeField] List<Button> speedButtons = new();
        [SerializeField] Button enter;
        [Header("나라 계수 · 우리 루두스 (줄 원본은 하이어라키에 비활성)")]
        [SerializeField] RectTransform coefTemplate;
        [SerializeField] TMP_Text ourHint;
        [SerializeField] RectTransform ourTemplate;
        [Header("조별 순위 · 토너먼트")]
        [SerializeField] GameObject groupsRow;
        [SerializeField] List<RectTransform> groupPanels = new();
        [SerializeField] GameObject bracketPanel;
        [SerializeField] TMP_Text champion;
        [SerializeField] List<RectTransform> bracketCols = new();

        TemplateList<RectTransform> coefRows, ourRows;
        readonly List<TemplateList<RectTransform>> groupRows = new(), bracketTies = new();
        List<string> order;
        string speed = "fast";

        void Awake()
        {
            coefRows = new TemplateList<RectTransform>(coefTemplate);
            ourRows = new TemplateList<RectTransform>(ourTemplate);
            foreach (var g in groupPanels) groupRows.Add(new TemplateList<RectTransform>((RectTransform)g.Find("Pad/RowTemplate")));
            foreach (var g in poPanels) poRows.Add(new TemplateList<RectTransform>((RectTransform)g.Find("Pad/RowTemplate")));
            foreach (var c in bracketCols) bracketTies.Add(new TemplateList<RectTransform>((RectTransform)c.Find("TieTemplate")));
            for (int i = 0; i < speedButtons.Count; i++) { string s = MatchScreen.Speeds[i].id; speedButtons[i].onClick.AddListener(() => { speed = s; Refresh(); }); }
            enter.onClick.AddListener(Play);
        }

        Ludus Player => S.ludi[S.playerLudusId];

        List<Gladiator> Pool => Player.gladiators.Select(id => S.gladiators[id]).Where(g => GladiatorRules.CanFight(g) && ChallengeLogic.Availability(S, g) == null).ToList();

        List<string> Lineup => order ?? SeriesLogic.AutoLineup(S, Player, CupLogic.Bouts(S), ChallengeLogic.Now(S)).Select(g => g.id).ToList();

        void Play()
        {
            var tie = CupLogic.TodaysPlayerTie(S);
            if (tie == null) return;
            var draft = SaveSystem.FromJson(SaveSystem.ToJson(S));
            var rng = new Rng(draft.rngState);
            var r = CupLogic.PlayTie(draft, tie.id, rng, Lineup);
            draft.rngState = rng.State;
            if (r.error != null) return;
            order = null;
            bool homeIsMe = tie.home == S.playerLudusId;
            if (r.series.melee == null && r.series.bouts.Count == 0) { G.Replace(draft); return; }
            match.RunSeries(MatchScreen.SeriesLives(r.series, homeIsMe, r.rule, $"판테온 컵 {CupLogic.StageNames[tie.stage]}{(r.rule == "sudden" ? " · 사투 (전쟁 중)" : "")}"), draft, speed, "cup");
            // '결과만'은 이번 경기에만. 다음 경기는 기본 속도로 돌아온다
            if (speed == "instant") speed = "fast";
        }

        public override void Refresh()
        {
            var cup = S.cup;
            var player = Player;
            var cupSprite = Resources.Load<Sprite>("Art/icon/trophy/pantheon");
            if (trophy != null) { trophy.sprite = cupSprite; trophy.enabled = cupSprite != null; }
            if (status != null) status.text = StatusLine(cup);

            // 오늘의 컵 경기
            var tie = CupLogic.TodaysPlayerTie(S);
            todayPanel.SetActive(tie != null && cup != null);
            if (tie != null && cup != null)
            {
                bool war = CupLogic.TieRule(S, tie) == "sudden";
                todayTitle.text = $"◆  오늘의 컵 경기 — {CupLogic.StageNames[tie.stage]}" +
                    (war ? $"  <color={Theme.Hex(Theme.Blood)}>사투 · {Tips.Nation(S.ludi[tie.home].nation)}과 {Tips.Nation(S.ludi[tie.away].nation)}이 전쟁 중입니다</color>" : "");
                Team(todayHome, tie.home);
                Team(todayAway, tie.away);
                var pool = Pool;
                var lineup = Lineup;
                var options = new[] { "—" }.Concat(pool.Select(g => $"{g.name} · {Widgets.Stars(Scouting.CaRange(S, g).mid)}")).ToList();
                for (int i = 0; i < lineupSlots.Count; i++)
                {
                    var dd = lineupSlots[i];
                    dd.transform.parent.gameObject.SetActive(i < CupLogic.Bouts(S));
                    if (!dd.options.Select(o => o.text).SequenceEqual(options)) { dd.ClearOptions(); dd.AddOptions(options); }
                    dd.SetValueWithoutNotify(i < lineup.Count ? pool.FindIndex(g => g.id == lineup[i]) + 1 : 0);
                    dd.onValueChanged.RemoveAllListeners();
                    int slot = i;
                    dd.onValueChanged.AddListener(v =>
                    {
                        var next = new List<string>(Lineup);
                        while (next.Count <= slot) next.Add(null);
                        string val = v == 0 ? null : pool[v - 1].id;
                        int j = val == null ? -1 : next.IndexOf(val);
                        if (j >= 0) next[j] = next[slot];
                        next[slot] = val;
                        order = next.Where(x => x != null).ToList();
                        Refresh();
                    });
                }
                shortWarn.SetActive(lineup.Count < CupLogic.Bouts(S));
                for (int i = 0; i < speedButtons.Count; i++) Seg(speedButtons[i], speed == MatchScreen.Speeds[i].id);
            }

            // 나라 계수
            var ranking = CupLogic.CoefficientRanking(S);
            coefRows.Sync(ranking.Count, (row, i) =>
            {
                var r = ranking[i];
                row.GetComponent<Image>().color = r.nation == S.nation ? new Color(0.89f, 0.6f, 0.23f, 0.08f) : Color.clear;
                row.Find("Nation/Crest").GetComponent<Image>().sprite = Resources.Load<Sprite>("Art/crest/" + r.nation);
                T(row, "Nation/Name").text = Tips.Nation(r.nation);
                T(row, "Recent").text = S.coefficients != null && S.coefficients.TryGetValue(r.nation, out var list) ? string.Join(" · ", list) : "";
                T(row, "Sum").text = r.coef.ToString();
            });

            // 우리 루두스
            var mine = cup?.ties.Where(t => t.home == player.id || t.away == player.id).ToList() ?? new List<CupTie>();
            string hint = cup == null || cup.season != S.season ? $"이번 시즌 대진은 {Calendar.DateLabel(CupLogic.FmDrawDay)}에 뽑습니다. 1부 1·2위는 본선에 바로, 국내 컵 우승 팀은 몇 부든 플레이오프에 나갑니다."
                : !cup.teams.Contains(player.id) ? "이번 시즌에는 출전권이 없습니다. 국내 컵을 우승하거나 1부 2위 안에 들어야 합니다." : null;
            ourHint.gameObject.SetActive(hint != null);
            ourHint.text = hint ?? "";
            ourRows.Sync(hint == null ? mine.Count : 0, (row, i) =>
            {
                var t = mine[i];
                bool home = t.home == player.id;
                T(row, "Date").text = Calendar.DateLabel(t.day);
                T(row, "Stage").text = CupLogic.StageNames[t.stage];
                Team((RectTransform)row.Find("Opp"), home ? t.away : t.home, home ? " <size=80%><color=#8c7f6a>(홈)</color></size>" : "");
                var res = T(row, "Result");
                if (!string.IsNullOrEmpty(t.winner))
                {
                    double my = home ? t.score[0] : t.score[1], opp = home ? t.score[1] : t.score[0];
                    res.text = $"{my}:{opp}";
                    res.color = t.winner == player.id ? Theme.Good : Neg;
                }
                else { res.text = "예정"; res.color = Theme.TextDim; }
            });

            // 플레이오프
            bool hasPo = cup?.poGroups != null;
            if (poRow != null) poRow.SetActive(hasPo);
            if (hasPo)
                for (int gi = 0; gi < poPanels.Count && gi < cup.poGroups.Count; gi++)
                {
                    poPanels[gi].Find("Head/Text").GetComponent<TMP_Text>().text = $"◆  플레이오프 {CupLogic.GroupLetters[gi]}조";
                    var table = CupLogic.PlayoffTable(S, cup, gi);
                    poRows[gi].Sync(table.Count, (row, i) => BindGroupRow(row, table[i], i));
                }

            // 조별 순위
            groupsRow.SetActive(cup != null && cup.groups.Count > 0);
            bracketPanel.SetActive(cup != null && cup.groups.Count > 0);
            if (cup == null) return;
            for (int gi = 0; gi < groupPanels.Count; gi++)
            {
                var panel = groupPanels[gi];
                panel.gameObject.SetActive(gi < cup.groups.Count);
                if (gi >= cup.groups.Count) continue;
                panel.Find("Head/Text").GetComponent<TMP_Text>().text = $"◆  {CupLogic.GroupLetters[gi]}조";
                var table = CupLogic.GroupTable(S, cup, gi);
                groupRows[gi].Sync(table.Count, (row, i) => BindGroupRow(row, table[i], i));
            }

            // 토너먼트
            champion.gameObject.SetActive(!string.IsNullOrEmpty(cup.champion));
            champion.text = !string.IsNullOrEmpty(cup.champion) && S.ludi.TryGetValue(cup.champion, out var ch) ? $"우승: {Tips.Ludus(ch)}" : "";
            for (int si = 0; si < Stages.Length; si++)
            {
                string stage = Stages[si];
                var col = bracketCols[si];
                int day = CupLogic.StageDay(cup, stage);
                T(col, "Title").text = $"{CupLogic.StageNames[stage]} · {Calendar.DateLabel(day)}";
                var ties = cup.ties.Where(t => t.stage == stage).ToList();
                col.Find("Empty").gameObject.SetActive(ties.Count == 0);
                bracketTies[si].Sync(ties.Count, (box, i) =>
                {
                    var t = ties[i];
                    var ids = new[] { t.home, t.away };
                    for (int k = 0; k < 2; k++)
                    {
                        var line = (RectTransform)box.Find(k == 0 ? "Home" : "Away");
                        Team(line, ids[k], t.score != null ? $"  <b>{t.score[k]}</b>" : "");
                        var cg = line.GetComponent<CanvasGroup>();
                        cg.alpha = string.IsNullOrEmpty(t.winner) || t.winner == ids[k] ? 1 : 0.45f;
                    }
                });
            }
        }

        void BindGroupRow(RectTransform row, GroupRow r, int i)
        {
            row.GetComponent<Image>().color = S.ludi.TryGetValue(r.id, out var l) && l.isPlayer ? new Color(0.89f, 0.6f, 0.23f, 0.08f) : Color.clear;
            Team((RectTransform)row.Find("Team"), r.id, "", i < 2 ? "▲ " : "");
            T(row, "Played").text = r.played.ToString();
            T(row, "Won").text = r.won.ToString();
            double diff = r.boutsFor - r.boutsAgainst;
            T(row, "Diff").text = (diff >= 0 ? "+" : "") + diff;
            T(row, "Pts").text = $"<b>{r.pts}</b>";
        }

        string StatusLine(Cup cup)
        {
            string flow = $"플레이오프 {Calendar.DateLabel(CupLogic.FmPoDays[0])} → 16강 조별 {Calendar.DateLabel(CupLogic.FmGroupDays[0])} → 8강 {Calendar.DateLabel(CupLogic.FmQfDay)} → 4강 {Calendar.DateLabel(CupLogic.FmSfDay)} → 결승 {Calendar.DateLabel(CupLogic.FmFinalDay)}";
            if (cup == null || cup.season != S.season) return $"대진 추첨 {Calendar.DateLabel(CupLogic.FmDrawDay)} · {flow}";
            if (!string.IsNullOrEmpty(cup.champion) && S.ludi.TryGetValue(cup.champion, out var ch)) return $"시즌 {cup.season} 우승: <color={Theme.Hex(Theme.Torch)}>★</color> {Tips.Ludus(ch)}";
            return $"시즌 {cup.season} 진행 중 · {flow}";
        }

        /// <summary>문장 + 루두스 이름 (웹판 TeamName). 우리 루두스는 횃불색 굵게</summary>
        void Team(RectTransform holder, string id, string suffix = "", string prefix = "")
        {
            var name = holder.Find("Name").GetComponent<TMP_Text>();
            if (!S.ludi.TryGetValue(id ?? "", out var l)) { name.text = prefix + "?"; return; }
            holder.Find("Crest").GetComponent<Image>().sprite = Resources.Load<Sprite>("Art/crest/" + l.nation);
            name.text = prefix + Tips.Ludus(l) + suffix;
            name.color = l.isPlayer ? Theme.Torch : Theme.Parchment;
            name.fontStyle = l.isPlayer ? FontStyles.Bold : FontStyles.Normal;
        }

        static TMP_Text T(Transform row, string path) => row.Find(path).GetComponent<TMP_Text>();

        static void Seg(Button b, bool on)
        {
            b.transform.Find("Fill").GetComponent<Image>().color = on ? Theme.ContinueBg : Theme.Panel2;
            b.GetComponent<Image>().color = on ? Theme.Torch : Theme.Line;
        }
    }
}
