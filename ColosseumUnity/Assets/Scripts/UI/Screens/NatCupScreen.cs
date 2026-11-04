// 국내 컵 화면 (Unity판 전용): 오늘의 경기(출전 5명, 불참), 우리 경기, 5강 리그표, 라운드별 대진. 나라를 바꿔 다른 나라 컵도 본다.
// 경기 중계는 모래 마당 화면(MatchScreen.RunSeries)에서 하고 끝나면 이 화면으로 돌아온다.
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class NatCupScreen : ScreenBase
    {
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);
        static readonly string[] Stages = { "r40", "r20", "r10", "league5" };

        [SerializeField] MatchScreen match;
        [SerializeField] Image trophy;
        [SerializeField] TMP_Text status;
        [Header("오늘의 경기")]
        [SerializeField] GameObject todayPanel;
        [SerializeField] TMP_Text todayTitle;
        [SerializeField] RectTransform todayHome;
        [SerializeField] RectTransform todayAway;
        [SerializeField] List<TMP_Dropdown> lineupSlots = new();
        [SerializeField] GameObject shortWarn;
        [SerializeField] List<Button> speedButtons = new();
        [SerializeField] Button enter;
        [SerializeField] Button forfeit;
        [Header("나라 · 라운드")]
        [SerializeField] TMP_Dropdown nation;
        [SerializeField] List<Button> stageTabs = new();
        [SerializeField] TMP_Text emptyHint;
        [SerializeField] RectTransform tieTemplate;
        [Header("우리 경기 · 5강 리그")]
        [SerializeField] TMP_Text ourHint;
        [SerializeField] RectTransform ourTemplate;
        [SerializeField] GameObject leaguePanel;
        [SerializeField] RectTransform leagueTemplate;

        TemplateList<RectTransform> ties, ours, league;
        readonly LineupPicker picker = new();
        string nationId, speed = "fast";
        int stageIdx = -1;

        void Awake()
        {
            ties = new TemplateList<RectTransform>(tieTemplate);
            ours = new TemplateList<RectTransform>(ourTemplate);
            league = new TemplateList<RectTransform>(leagueTemplate);
            for (int i = 0; i < speedButtons.Count; i++) { string s = MatchScreen.Speeds[i].id; speedButtons[i].onClick.AddListener(() => { speed = s; Refresh(); }); }
            for (int i = 0; i < stageTabs.Count; i++) { int k = i; stageTabs[i].onClick.AddListener(() => { stageIdx = k; Refresh(); }); }
            nation.onValueChanged.AddListener(i => { nationId = Data.Nations[i].id; stageIdx = -1; Refresh(); });
            enter.onClick.AddListener(Play);
            forfeit.onClick.AddListener(AskForfeit);
        }

        // 화면을 열 때마다 진행 중인 라운드 탭으로
        void OnEnable() => stageIdx = -1;

        Ludus Player => S.ludi[S.playerLudusId];

        void Play()
        {
            var tie = NatCupLogic.TodaysPlayerTie(S);
            if (tie == null) return;
            var draft = SaveSystem.FromJson(SaveSystem.ToJson(S));
            var rng = new Rng(draft.rngState);
            var r = NatCupLogic.PlayTie(draft, S.nation, tie.id, rng, picker.Lineup(S, Player, NatCupLogic.Lineup));
            draft.rngState = rng.State;
            if (r.error != null) return;
            picker.Reset();
            if (r.series.melee == null && r.series.bouts.Count == 0) { G.Replace(draft); return; }
            match.RunSeries(MatchScreen.SeriesLives(r.series, tie.home == S.playerLudusId, "missio", $"{NatCupLogic.Name(S.nation)} {NatCupLogic.StageNames[tie.stage]}"), draft, speed, "natcup");
            // '결과만'은 이번 경기에만. 다음 경기는 기본 속도로 돌아온다
            if (speed == "instant") speed = "fast";
        }

        void AskForfeit()
        {
            var tie = NatCupLogic.TodaysPlayerTie(S);
            if (tie == null) return;
            string id = tie.id;
            string detail = tie.stage == "league5" ? "이 경기는 기권패가 되고, 남은 5강 경기는 계속 치릅니다." : "기권패로 탈락합니다. 남는 시간은 던전 원정이나 훈련에 쓸 수 있습니다.";
            ConfirmPopup.Ask("이 경기에 나가지 않겠습니까?", detail, () => G.Mutate(s => NatCupLogic.Forfeit(s, id)));
        }

        public override void Refresh()
        {
            nationId ??= S.nation;
            var labels = Data.Nations.Select(n => NatCupLogic.Name(n.id) + (n.id == S.nation ? " (우리 나라)" : "")).ToList();
            if (!nation.options.Select(o => o.text).SequenceEqual(labels)) { nation.ClearOptions(); nation.AddOptions(labels); }
            nation.SetValueWithoutNotify(Data.Nations.FindIndex(n => n.id == nationId));
            var sprite = Resources.Load<Sprite>("Art/icon/trophy/natcup_" + nationId);
            trophy.sprite = sprite;
            trophy.enabled = sprite != null;

            var c = NatCupLogic.Of(S, nationId);
            bool current = c != null && c.season == S.season;
            status.text = StatusLine(c, current);
            DrawToday();
            DrawOurs();

            // 라운드 탭: 지금 진행 중인 단계로 먼저 연다
            if (stageIdx < 0) stageIdx = c == null ? 0 : System.Array.FindLastIndex(Stages, st => c.ties.Any(t => t.stage == st));
            for (int i = 0; i < stageTabs.Count; i++)
            {
                stageTabs[i].GetComponentInChildren<TMP_Text>().text = NatCupLogic.StageNames[Stages[i]];
                Seg(stageTabs[i], stageIdx == i);
            }
            string stage = Stages[Mathf.Clamp(stageIdx, 0, Stages.Length - 1)];
            var list = c?.ties.Where(t => t.stage == stage).OrderBy(t => t.day).ToList() ?? new List<CupTie>();
            emptyHint.gameObject.SetActive(list.Count == 0);
            emptyHint.text = c == null ? $"컵은 정규 리그가 끝나면 시작합니다 ({Calendar.DateLabel(NatCupLogic.DrawDay)} 대진 추첨)." : "아직 이 라운드 대진이 나오지 않았습니다.";
            ties.Sync(list.Count, (row, i) => BindTie(row, list[i]));

            // 5강 리그표
            bool hasLeague = c?.final5 != null;
            leaguePanel.SetActive(hasLeague);
            if (hasLeague)
            {
                var table = NatCupLogic.LeagueTable(S, c);
                league.Sync(table.Count, (row, i) =>
                {
                    var r = table[i];
                    row.GetComponent<Image>().color = r.id == S.playerLudusId ? new Color(0.89f, 0.6f, 0.23f, 0.08f) : Color.clear;
                    T(row, "Rank").text = (i + 1).ToString();
                    Team((RectTransform)row.Find("Team"), r.id);
                    T(row, "Played").text = r.played.ToString();
                    T(row, "Won").text = r.won.ToString();
                    T(row, "Lost").text = r.lost.ToString();
                    T(row, "Downs").text = $"{r.downs:0}";
                });
            }
        }

        string StatusLine(NatCup c, bool current)
        {
            string name = Tips.NatCup(nationId);
            if (c == null) return $"{name}: 정규 리그가 끝나는 {Calendar.DateLabel(NatCupLogic.DrawDay)}에 대진을 뽑습니다. 40강 {Calendar.DateLabel(NatCupLogic.KoDays[0])}, 5강 리그는 {Calendar.DateLabel(NatCupLogic.LeagueDays[0])}부터입니다.";
            if (!string.IsNullOrEmpty(c.champion)) return $"시즌 {c.season} {name} 우승: <color={Theme.Hex(Theme.Torch)}>★</color> {Tips.Ludus(S.ludi[c.champion])} · 준우승 {Tips.Ludus(S.ludi[c.runnerUp])}";
            return $"시즌 {c.season} {name} 진행 중 · 40강 {Calendar.DateLabel(NatCupLogic.KoDays[0])} → 20강 {Calendar.DateLabel(NatCupLogic.KoDays[1])} → 10강 {Calendar.DateLabel(NatCupLogic.KoDays[2])} → 5강 리그 {Calendar.DateLabel(NatCupLogic.LeagueDays[0])}~{Calendar.DateLabel(NatCupLogic.LeagueDays[4])}";
        }

        void DrawToday()
        {
            var tie = NatCupLogic.TodaysPlayerTie(S);
            todayPanel.SetActive(tie != null);
            if (tie == null) return;
            todayTitle.text = $"◆  오늘의 경기 — {NatCupLogic.Name(S.nation)} {NatCupLogic.StageNames[tie.stage]}";
            Team(todayHome, tie.home);
            Team(todayAway, tie.away);
            picker.Bind(S, Player, lineupSlots, NatCupLogic.Lineup, Refresh);
            shortWarn.SetActive(picker.Lineup(S, Player, NatCupLogic.Lineup).Count < NatCupLogic.Lineup);
            for (int i = 0; i < speedButtons.Count; i++) Seg(speedButtons[i], speed == MatchScreen.Speeds[i].id);
        }

        void DrawOurs()
        {
            var c = NatCupLogic.Of(S, S.nation);
            string me = S.playerLudusId;
            var mine = c == null ? new List<CupTie>() : c.ties.Where(t => t.home == me || t.away == me).OrderBy(t => t.day).ToList();
            ourHint.gameObject.SetActive(mine.Count == 0);
            ourHint.text = "아직 우리 경기가 없습니다.";
            ours.Sync(mine.Count, (row, i) =>
            {
                var t = mine[i];
                bool home = t.home == me;
                T(row, "Date").text = Calendar.DateLabel(t.day);
                T(row, "Stage").text = NatCupLogic.StageNames[t.stage];
                Team((RectTransform)row.Find("Opp"), home ? t.away : t.home, home ? " <size=80%><color=#8c7f6a>(홈)</color></size>" : "");
                var res = T(row, "Result");
                if (string.IsNullOrEmpty(t.winner)) { res.text = "예정"; res.color = Theme.TextDim; return; }
                res.text = t.forfeit == true ? (t.winner == me ? "부전승" : "기권") : $"{(home ? t.score[0] : t.score[1]):0}:{(home ? t.score[1] : t.score[0]):0}";
                res.color = t.winner == me ? Theme.Good : Neg;
            });
        }

        void BindTie(RectTransform row, CupTie t)
        {
            T(row, "Date").text = Calendar.DateLabel(t.day);
            Team((RectTransform)row.Find("Home"), t.home);
            Team((RectTransform)row.Find("Away"), t.away);
            bool played = !string.IsNullOrEmpty(t.winner);
            T(row, "Score").text = !played ? "vs" : t.forfeit == true ? "기권" : $"{t.score[0]:0} : {t.score[1]:0}";
            row.Find("Home").GetComponent<CanvasGroup>().alpha = !played || t.winner == t.home ? 1 : 0.45f;
            row.Find("Away").GetComponent<CanvasGroup>().alpha = !played || t.winner == t.away ? 1 : 0.45f;
            bool mine = t.home == S.playerLudusId || t.away == S.playerLudusId;
            row.GetComponent<Image>().color = mine ? new Color(0.89f, 0.6f, 0.23f, 0.08f) : Color.clear;
        }

        /// <summary>문장 + 루두스 이름 + 몇 부. 우리 루두스는 횃불색 굵게</summary>
        void Team(RectTransform holder, string id, string suffix = "")
        {
            var name = holder.Find("Name").GetComponent<TMP_Text>();
            if (!S.ludi.TryGetValue(id ?? "", out var l)) { name.text = "?"; return; }
            holder.Find("Crest").GetComponent<Image>().sprite = Resources.Load<Sprite>("Art/crest/" + l.nation);
            name.text = $"{Tips.Ludus(l)} <size=80%><color={Theme.Hex(Theme.TextFaint)}>{l.tier}부</color></size>{suffix}";
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

    /// <summary>출전 명단 드롭다운 (국내 컵·판테온 컵·올림피카 공용)</summary>
    public class LineupPicker
    {
        List<string> order;

        public void Reset() => order = null;

        public List<string> Lineup(GameState s, Ludus me, int n) =>
            order ?? SeriesLogic.AutoLineup(s, me, n, ChallengeLogic.Now(s)).Select(g => g.id).ToList();

        /// <summary>우리 루두스 명단으로 칸을 채운다</summary>
        public void Bind(GameState s, Ludus me, List<TMP_Dropdown> slots, int n, System.Action changed)
        {
            var pool = me.gladiators.Select(id => s.gladiators[id]).Where(g => GladiatorRules.CanFight(g) && ChallengeLogic.Availability(s, g) == null).ToList();
            BindPool(s, pool, Lineup(s, me, n), slots, changed);
        }

        /// <summary>정해진 후보(대표 명단 등)로 칸을 채운다</summary>
        public void BindPool(GameState s, List<Gladiator> pool, List<string> lineup, List<TMP_Dropdown> slots, System.Action changed)
        {
            var options = new[] { "—" }.Concat(pool.Select(g => $"{g.name} · {Widgets.Stars(Scouting.CaRange(s, g).mid)}")).ToList();
            for (int i = 0; i < slots.Count; i++)
            {
                var dd = slots[i];
                if (!dd.options.Select(o => o.text).SequenceEqual(options)) { dd.ClearOptions(); dd.AddOptions(options); }
                dd.SetValueWithoutNotify(i < lineup.Count ? pool.FindIndex(g => g.id == lineup[i]) + 1 : 0);
                dd.onValueChanged.RemoveAllListeners();
                int slot = i;
                var current = lineup;
                dd.onValueChanged.AddListener(v =>
                {
                    var next = new List<string>(current);
                    while (next.Count <= slot) next.Add(null);
                    string val = v == 0 ? null : pool[v - 1].id;
                    int j = val == null ? -1 : next.IndexOf(val);
                    if (j >= 0) next[j] = next[slot];
                    next[slot] = val;
                    order = next.Where(x => x != null).ToList();
                    changed();
                });
            }
        }

        /// <summary>손으로 고른 명단 (없으면 null)</summary>
        public List<string> Picked => order;
    }
}
