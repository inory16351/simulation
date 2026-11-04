// 올림피카 컵 화면 (Unity판 전용): 감독 제안, 오늘의 경기(감독일 때), 순위표, 대표 명단, 일정·결과.
// 대회가 없는 시즌에는 다음 대회 시즌과 역대 우승만 보여 준다.
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class OlympicaScreen : ScreenBase
    {
        [SerializeField] MatchScreen match;
        [SerializeField] Image trophy;
        [SerializeField] TMP_Text status;
        [Header("감독 제안")]
        [SerializeField] GameObject offerPanel;
        [SerializeField] Button accept;
        [SerializeField] Button decline;
        [Header("오늘의 경기 (감독)")]
        [SerializeField] GameObject todayPanel;
        [SerializeField] TMP_Text todayTitle;
        [SerializeField] RectTransform todayHome;
        [SerializeField] RectTransform todayAway;
        [SerializeField] List<TMP_Dropdown> lineupSlots = new();
        [SerializeField] List<Button> speedButtons = new();
        [SerializeField] Button enter;
        [Header("순위 · 명단 · 일정 (줄 원본은 하이어라키에 비활성)")]
        [SerializeField] GameObject bodyRoot;
        [SerializeField] RectTransform tableTemplate;
        [SerializeField] TMP_Dropdown squadNation;
        [SerializeField] RectTransform squadTemplate;
        [SerializeField] GameObject addRow;
        [SerializeField] TMP_Dropdown addPick;
        [SerializeField] Button addButton;
        [SerializeField] TMP_Text squadMsg;
        [SerializeField] RectTransform fixtureTemplate;

        TemplateList<RectTransform> table, squad, fixtures;
        readonly LineupPicker picker = new();
        string nationId, speed = "fast", msg;
        List<Gladiator> addCands = new();

        void Awake()
        {
            table = new TemplateList<RectTransform>(tableTemplate);
            squad = new TemplateList<RectTransform>(squadTemplate);
            fixtures = new TemplateList<RectTransform>(fixtureTemplate);
            accept.onClick.AddListener(() => G.Mutate(OlympicaLogic.Accept));
            decline.onClick.AddListener(() => G.Mutate(OlympicaLogic.Decline));
            enter.onClick.AddListener(Play);
            squadNation.onValueChanged.AddListener(i => { nationId = Data.Nations[i].id; msg = null; Refresh(); });
            addButton.onClick.AddListener(AddPicked);
            for (int i = 0; i < speedButtons.Count; i++) { string s = MatchScreen.Speeds[i].id; speedButtons[i].onClick.AddListener(() => { speed = s; Refresh(); }); }
        }

        void Play()
        {
            var tie = OlympicaLogic.TodaysPlayerTie(S);
            if (tie == null) return;
            var draft = SaveSystem.FromJson(SaveSystem.ToJson(S));
            var rng = new Rng(draft.rngState);
            var r = OlympicaLogic.PlayTie(draft, tie.id, rng, picker.Picked);
            draft.rngState = rng.State;
            if (r.error != null) return;
            picker.Reset();
            if (r.series.melee == null) { G.Replace(draft); return; }
            string opp = tie.home == S.nation ? tie.away : tie.home;
            match.RunSeries(MatchScreen.SeriesLives(r.series, tie.home == S.nation, "missio", $"{OlympicaLogic.Name} · {Data.Nation(opp).@short}전"), draft, speed, "olympica");
            // '결과만'은 이번 경기에만. 다음 경기는 기본 속도로 돌아온다
            if (speed == "instant") speed = "fast";
        }

        void AddPicked()
        {
            if (addPick.value <= 0 || addPick.value > addCands.Count) return;
            string id = addCands[addPick.value - 1].id;
            msg = G.Try(s =>
            {
                var cur = new List<string>(OlympicaLogic.Current(s).squads[s.nation]);
                if (cur.Count >= OlympicaLogic.Squad) return $"대표는 {OlympicaLogic.Squad}명까지입니다. 먼저 한 명을 빼야 합니다";
                cur.Add(id);
                return OlympicaLogic.SetSquad(s, cur);
            });
            Refresh();
        }

        public override void Refresh()
        {
            var sprite = Resources.Load<Sprite>("Art/icon/trophy/olympica");
            trophy.sprite = sprite;
            trophy.enabled = sprite != null;
            var o = OlympicaLogic.Current(S) ?? S.olympica;
            bool live = OlympicaLogic.Current(S) != null;
            status.text = Status(o, live);
            offerPanel.SetActive(live && o.offer == true);
            DrawToday();
            bodyRoot.SetActive(o != null);
            if (o == null) return;
            DrawTable(o);
            DrawSquad(o, live);
            DrawFixtures(o);
        }

        string Status(Olympica o, bool live)
        {
            int next = OlympicaLogic.NextSeason(S.season);
            string past = string.Join(" · ", (S.honours ?? new List<HonourEntry>()).Where(h => h.kind == "olympica").OrderByDescending(h => h.season).Take(4)
                .Select(h => $"시즌 {h.season} {Tips.Nation(h.winnerId)}"));
            string hist = past.Length > 0 ? $"\n역대 우승: {past}" : "";
            if (!live)
            {
                string when = next == S.season
                    ? (S.day < OlympicaLogic.SetupDay ? $"이번 시즌 {Calendar.DateLabel(OlympicaLogic.SetupDay)}에 대표를 뽑습니다." : "이번 시즌 대회가 끝났습니다.")
                    : $"다음 대회는 시즌 {next}입니다 ({next - S.season}시즌 뒤).";
                return $"4년에 한 번 여섯 나라 대표(5명 + 후보 2명)가 5:5 미시오 홈·원정 풀리그로 겨룹니다. {when}{hist}";
            }
            if (!string.IsNullOrEmpty(o.champion)) return $"시즌 {o.season} 우승: <color={Theme.Hex(Theme.Torch)}>★</color> {Tips.Nation(o.champion)} · 준우승 {Tips.Nation(o.runnerUp)}{hist}";
            string role = o.playerManager == true ? $"우리 루두스가 {Tips.Nation(S.nation)} 대표팀을 맡고 있습니다." : "대표는 나라에서 실력과 명성 순으로 뽑혔습니다.";
            return $"시즌 {o.season} 진행 중 · 대표 주간 {Calendar.DateLabel(OlympicaLogic.MatchDays[0])}부터 8일마다 (그날은 리그 경기가 없습니다). {role}{hist}";
        }

        void DrawToday()
        {
            var tie = OlympicaLogic.TodaysPlayerTie(S);
            todayPanel.SetActive(tie != null);
            if (tie == null) return;
            todayTitle.text = $"◆  오늘의 경기 — {OlympicaLogic.Name} {(tie.group ?? 0) + 1}라운드";
            NationName(todayHome, tie.home);
            NationName(todayAway, tie.away);
            var o = S.olympica;
            var pool = o.squads[S.nation].Select(id => S.gladiators[id]).Where(g => g.alive && g.injury?.severity != "serious" && g.deployment == null).ToList();
            var lineup = picker.Picked ?? OlympicaLogic.LineupFor(S, S.nation).Select(g => g.id).ToList();
            picker.BindPool(S, pool, lineup, lineupSlots, Refresh);
            for (int i = 0; i < speedButtons.Count; i++) Seg(speedButtons[i], speed == MatchScreen.Speeds[i].id);
        }

        void DrawTable(Olympica o)
        {
            var rows = OlympicaLogic.Table(S, o);
            table.Sync(rows.Count, (row, i) =>
            {
                var r = rows[i];
                row.GetComponent<Image>().color = r.id == S.nation ? new Color(0.89f, 0.6f, 0.23f, 0.08f) : Color.clear;
                T(row, "Rank").text = (i + 1).ToString();
                NationName((RectTransform)row.Find("Nation"), r.id);
                T(row, "Played").text = r.played.ToString();
                T(row, "Won").text = r.won.ToString();
                T(row, "Lost").text = r.lost.ToString();
                T(row, "Downs").text = $"{r.downs:0}";
            });
        }

        void DrawSquad(Olympica o, bool live)
        {
            nationId ??= S.nation;
            var labels = Data.Nations.Select(n => n.name).ToList();
            if (!squadNation.options.Select(x => x.text).SequenceEqual(labels)) { squadNation.ClearOptions(); squadNation.AddOptions(labels); }
            squadNation.SetValueWithoutNotify(Data.Nations.FindIndex(n => n.id == nationId));
            var ids = o.squads.TryGetValue(nationId, out var q) ? q : new List<string>();
            bool editable = live && o.playerManager == true && nationId == S.nation && S.day < OlympicaLogic.MatchDays[0];
            squad.Sync(ids.Count, (row, i) =>
            {
                var g = S.gladiators[ids[i]];
                T(row, "Name").text = Tips.Name(g);
                T(row, "Style").text = Tips.Style(g.style);
                T(row, "Stars").text = Widgets.CaStars(S, g);
                T(row, "Club").text = Career.LudusOf(S, g.id) is Ludus l ? Tips.Ludus(l) : "—";
                T(row, "State").text = !g.alive ? "<color=#d9a29b>사망</color>" : g.injury?.severity == "serious" ? "<color=#d9a29b>중상</color>" : i < OlympicaLogic.Lineup ? "주전" : "후보";
                var rm = row.Find("Remove").GetComponent<Button>();
                rm.gameObject.SetActive(editable);
                rm.onClick.RemoveAllListeners();
                string id = g.id;
                rm.onClick.AddListener(() =>
                {
                    msg = G.Try(s => OlympicaLogic.SetSquad(s, OlympicaLogic.Current(s).squads[s.nation].Where(x => x != id).ToList()));
                    Refresh();
                });
            });
            addRow.SetActive(editable);
            squadMsg.gameObject.SetActive(msg != null);
            squadMsg.text = msg ?? "";
            if (!editable) return;
            // 후보: 나라에서 잘하는 순 30명 (이미 뽑힌 자 빼고)
            addCands = OlympicaLogic.Candidates(S, S.nation).Where(g => !ids.Contains(g.id)).Take(30).ToList();
            var opts = new[] { "대표로 뽑을 검투사" }.Concat(addCands.Select(g => $"{g.name} · {Widgets.Stars(Scouting.CaRange(S, g).mid)} · {Career.LudusOf(S, g.id)?.name}")).ToList();
            if (!addPick.options.Select(x => x.text).SequenceEqual(opts)) { addPick.ClearOptions(); addPick.AddOptions(opts); addPick.SetValueWithoutNotify(0); }
        }

        void DrawFixtures(Olympica o)
        {
            var list = o.ties.OrderBy(t => t.day).ToList();
            fixtures.Sync(list.Count, (row, i) =>
            {
                var t = list[i];
                bool mine = t.home == S.nation || t.away == S.nation;
                row.GetComponent<Image>().color = mine ? new Color(0.89f, 0.6f, 0.23f, 0.08f) : Color.clear;
                T(row, "Date").text = $"{(t.group ?? 0) + 1}R · {Calendar.DateLabel(t.day)}";
                NationName((RectTransform)row.Find("Home"), t.home);
                NationName((RectTransform)row.Find("Away"), t.away);
                bool played = !string.IsNullOrEmpty(t.winner);
                T(row, "Score").text = played ? $"{t.score[0]:0} : {t.score[1]:0}" : "vs";
                row.Find("Home").GetComponent<CanvasGroup>().alpha = !played || t.winner == t.home ? 1 : 0.45f;
                row.Find("Away").GetComponent<CanvasGroup>().alpha = !played || t.winner == t.away ? 1 : 0.45f;
            });
        }

        void NationName(RectTransform holder, string id)
        {
            holder.Find("Crest").GetComponent<Image>().sprite = Resources.Load<Sprite>("Art/crest/" + id);
            var name = holder.Find("Name").GetComponent<TMP_Text>();
            name.text = Tips.Nation(id, true);
            name.color = id == S.nation ? Theme.Torch : Theme.Parchment;
            name.fontStyle = id == S.nation ? FontStyles.Bold : FontStyles.Normal;
        }

        static TMP_Text T(Transform row, string path) => row.Find(path).GetComponent<TMP_Text>();

        static void Seg(Button b, bool on)
        {
            b.transform.Find("Fill").GetComponent<Image>().color = on ? Theme.ContinueBg : Theme.Panel2;
            b.GetComponent<Image>().color = on ? Theme.Torch : Theme.Line;
        }
    }
}
