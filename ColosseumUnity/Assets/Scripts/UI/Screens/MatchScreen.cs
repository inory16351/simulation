// 모래 마당 (웹판 MatchScreen.tsx): 오늘의 공식 경기, 연습 시합 준비, 경기 중계(Playback).
// 아직 옮기지 않은 것: 탑뷰 전투 장면(ArenaStage), 효과음·전투 음악, 피 튀김 장식.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using Colosseum.Core.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class MatchScreen : ScreenBase
    {
        public static readonly (string id, string label, float sec)[] Speeds = { ("normal", "보통", 0.65f), ("fast", "빠르게", 0.22f), ("instant", "결과만", 0f) };
        static readonly string[] Rules = { "training", "missio", "sudden" };
        static readonly Dictionary<string, string> RuleHint = new()
        {
            ["training"] = "쓰러지면 종료. 사고사 1%",
            ["missio"] = "쓰러지면 관중이 살릴지 죽일지 정한다 (3~25%)",
            ["sudden"] = "지면 거의 죽는다 (85~95%)",
        };
        static readonly Dictionary<string, string> CutLabel = new()
        {
            ["entrance"] = "입장", ["crit"] = "치명타", ["execute"] = "처형", ["race_skill"] = "각성", ["comeback"] = "역전", ["missio"] = "미시오", ["death"] = "사망",
        };
        static readonly Dictionary<string, string> ReasonNames = new() { ["ko"] = "KO", ["surrender"] = "항복", ["timeout"] = "판정" };
        public static readonly Dictionary<string, Color> ToneColors = new()
        {
            ["normal"] = new Color32(0xd9, 0xcb, 0xb0, 0xff),
            ["hit"] = new Color32(0xe8, 0xc8, 0x90, 0xff),
            ["crit"] = new Color32(0xff, 0x8a, 0x5c, 0xff),
            ["blood"] = new Color32(0xd8, 0x5a, 0x48, 0xff),
            ["crowd"] = new Color32(0xb0, 0x8a, 0x4f, 0xff),
            ["system"] = new Color32(0x9a, 0xa8, 0xc0, 0xff),
            ["death"] = new Color32(0xff, 0x55, 0x55, 0xff),
        };
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);

        /// <summary>중계 한 판 (웹판 Live)</summary>
        public class Live
        {
            public MatchResult result;
            public List<(CommentaryLine line, string cut)> items;
            public Gladiator a, b;
            public uint seed;
            public string rule, title, sub;
            public double[] startHp;
            /// <summary>공식 경기: 중계가 끝나면 이 상태로 바꾼다</summary>
            public GameState commit;
        }

        [Header("준비 화면")]
        [SerializeField] GameObject setupRoot;
        [SerializeField] List<Button> speedButtons = new();
        [SerializeField] GameObject officialPanel;
        [SerializeField] RectTransform officialTemplate;
        [SerializeField] RectTransform teamTemplate;
        [SerializeField] Button mineTemplate;
        [SerializeField] TMP_Text mineEmpty;
        [SerializeField] TMP_Dropdown oppLudus;
        [SerializeField] Button oppTemplate;
        [SerializeField] TMP_Text oppEmpty;
        [SerializeField] List<Button> ruleButtons = new();
        [SerializeField] TMP_Text ruleHint;
        [SerializeField] GameObject doctrineField;
        [SerializeField] TMP_Text doctrineTags;
        [SerializeField] Button doctrineEdit;
        [SerializeField] TMP_Dropdown stance;
        [SerializeField] Button start;
        [Header("중계 화면")]
        [SerializeField] GameObject playRoot;
        [SerializeField] TMP_Text playTitle;
        [SerializeField] TMP_Text playSub;
        [SerializeField] RectTransform fighterA;
        [SerializeField] RectTransform fighterB;
        [SerializeField] TMP_Text crowdValue;
        [SerializeField] ArenaStage stage;
        [SerializeField] ScrollRect feedScroll;
        [SerializeField] TMP_Text lineTemplate;
        [SerializeField] GameObject cutOverlay;
        [SerializeField] TMP_Text cutText;
        [SerializeField] Image flash;
        [SerializeField] Button skipButton;
        [SerializeField] Button closeButton;
        [SerializeField] Button rosterButton;

        TemplateList<RectTransform> officials, teams;
        TemplateList<Button> mineRows, oppRows;
        readonly List<GameObject> lines = new();

        string speed = "normal", myId, oppLudusId, oppId, rule = "training", stanceOverride, returnScreen;
        readonly Dictionary<string, List<string>> lineups = new();
        List<Ludus> others = new();
        List<string> stances;

        Live live;
        List<Live> seriesLives;
        int seriesIdx;
        Coroutine playing;
        int shown;
        bool doneCalled;
        readonly Dictionary<string, (double hp, double max)> hp = new();
        readonly Dictionary<RectTransform, string> drawnFor = new();
        /// <summary>장면에 보여 준 이벤트 수</summary>
        int stageCount;

        // 다른 화면에 다녀오면 멈춘 중계를 이어 간다
        void OnEnable()
        {
            if (live != null && shown < live.items.Count && playing == null && lineTemplate != null) playing = StartCoroutine(Play());
        }

        void OnDisable()
        {
            playing = null;
            GameAudio.Music(false);
        }

        float SpeedSec => Speeds.First(s => s.id == speed).sec;

        void Awake()
        {
            officials = new TemplateList<RectTransform>(officialTemplate);
            teams = new TemplateList<RectTransform>(teamTemplate);
            mineRows = new TemplateList<Button>(mineTemplate);
            oppRows = new TemplateList<Button>(oppTemplate);
            lineTemplate.gameObject.SetActive(false);
            stances = Tactics.StanceNames.Keys.ToList();
            stance.ClearOptions();
            stance.AddOptions(new[] { "교범대로" }.Concat(stances.Select(s => Tactics.StanceNames[s])).ToList());
            stance.onValueChanged.AddListener(i => stanceOverride = i == 0 ? null : stances[i - 1]);
            oppLudus.onValueChanged.AddListener(i => { oppLudusId = others[i].id; oppId = null; Refresh(); });
            for (int i = 0; i < speedButtons.Count; i++) { string s = Speeds[i].id; speedButtons[i].onClick.AddListener(() => { speed = s; Refresh(); }); }
            for (int i = 0; i < ruleButtons.Count; i++) { string r = Rules[i]; ruleButtons[i].onClick.AddListener(() => { rule = r; Refresh(); }); }
            doctrineEdit.onClick.AddListener(() => { if (myId != null) G.OpenGladiator(myId); });
            start.onClick.AddListener(StartPractice);
            skipButton.onClick.AddListener(() => { if (playing != null) StopCoroutine(playing); playing = null; ShowAll(); });
            closeButton.onClick.AddListener(OnClose);
            rosterButton.onClick.AddListener(() => { live = null; seriesLives = null; returnScreen = null; G.Go("roster"); });
        }

        public override void Refresh()
        {
            bool playingView = live != null;
            setupRoot.SetActive(!playingView);
            playRoot.SetActive(playingView);
            // 경기 중계 화면에서는 전투 음악 (웹판 battleScreens)
            GameAudio.Music(playingView);
            if (!playingView) RefreshSetup();
        }

        // ───────── 준비 화면 ─────────

        void RefreshSetup()
        {
            var player = S.ludi[S.playerLudusId];
            var mine = player.gladiators.Select(id => S.gladiators[id]).Where(g => GladiatorRules.CanFight(g) && ChallengeLogic.Availability(S, g) == null).ToList();
            others = S.ludi.Values.Where(l => !l.isPlayer && l.nation == S.nation).OrderByDescending(l => l.rp).ToList();
            var official = ChallengeLogic.TodaysPlayerMatches(S);

            for (int i = 0; i < speedButtons.Count; i++) Seg(speedButtons[i], speed == Speeds[i].id);

            officialPanel.SetActive(official.Count > 0);
            var singles = official.Where(c => !(c.format is int f && f != 0)).ToList();
            var teamList = official.Where(c => c.format is int f && f != 0).ToList();
            officials.Sync(singles.Count, (row, i) => BindOfficial(row, singles[i]));
            teams.Sync(teamList.Count, (row, i) => BindTeam(row, teamList[i], player));

            if (myId == null || mine.All(g => g.id != myId)) myId = mine.FirstOrDefault()?.id;
            mineEmpty.gameObject.SetActive(mine.Count == 0);
            mineRows.Sync(mine.Count, (b, i) => BindPick(b, mine[i], myId, id => { myId = id; Refresh(); }));

            if (oppLudusId == null || others.All(l => l.id != oppLudusId)) oppLudusId = (others.FirstOrDefault(l => l.tier == player.tier) ?? others[0]).id;
            var labels = others.Select(l => $"{l.tier}부 · {l.name} (RP {l.rp})").ToList();
            if (!oppLudus.options.Select(o => o.text).SequenceEqual(labels)) { oppLudus.ClearOptions(); oppLudus.AddOptions(labels); }
            oppLudus.SetValueWithoutNotify(others.FindIndex(l => l.id == oppLudusId));
            var oppList = S.ludi[oppLudusId].gladiators.Select(id => S.gladiators[id]).Where(GladiatorRules.CanFight).ToList();
            if (oppId == null || oppList.All(g => g.id != oppId)) oppId = oppList.FirstOrDefault()?.id;
            oppEmpty.gameObject.SetActive(oppList.Count == 0);
            oppRows.Sync(oppList.Count, (b, i) => BindPick(b, oppList[i], oppId, id => { oppId = id; Refresh(); }));

            bool suddenBanned = NationRules.LeagueRules(S).suddenBanned;
            for (int i = 0; i < ruleButtons.Count; i++)
            {
                ruleButtons[i].GetComponentInChildren<TMP_Text>().text = (string)Bal.Rule(Rules[i])["name"];
                ruleButtons[i].interactable = !(Rules[i] == "sudden" && suddenBanned);
                Seg(ruleButtons[i], rule == Rules[i]);
            }
            ruleHint.text = RuleHint[rule];

            doctrineField.SetActive(myId != null);
            if (myId != null)
            {
                var d = S.gladiators[myId].doctrine;
                doctrineTags.text = $"{Tactics.StanceNames[d.stance]}   {Tactics.RangeNames[d.range]}   노림: {Tactics.FocusNames[d.focus]}   숙련 {Math.Round(d.familiarity)}";
                stance.SetValueWithoutNotify(stanceOverride == null ? 0 : stances.IndexOf(stanceOverride) + 1);
            }
            start.interactable = myId != null && oppId != null;
            start.GetComponentInChildren<TMP_Text>().text = rule == "sudden" ? "사투 시작" : "경기 시작";
            start.transform.Find("Fill").GetComponent<Image>().color = rule == "sudden" ? Theme.BloodDark : Theme.ContinueBg;
        }

        void BindPick(Button b, Gladiator g, string selected, Action<string> pick)
        {
            b.GetComponentInChildren<PortraitView>(true).Show(g);
            b.transform.Find("Name").GetComponent<TMP_Text>().text = g.name;
            b.transform.Find("Style").GetComponent<TMP_Text>().text = Data.Style(g.style)?.name;
            b.transform.Find("Stars").GetComponent<TMP_Text>().text = Widgets.Stars(GladiatorRules.ComputeCA(g));
            b.GetComponent<Image>().color = g.id == selected ? new Color(0.89f, 0.6f, 0.23f, 0.12f) : new Color(0, 0, 0, 0);
            b.onClick.RemoveAllListeners();
            string id = g.id;
            b.onClick.AddListener(() => pick(id));
        }

        void BindOfficial(RectTransform row, Challenge c)
        {
            bool iAmFrom = S.ludi[c.from].isPlayer;
            var me = S.gladiators[iAmFrom ? c.fromGladiator : c.toGladiator];
            var them = S.gladiators[iAmFrom ? c.toGladiator : c.fromGladiator];
            var opp = S.ludi[iAmFrom ? c.to : c.from];
            row.Find("Me/Portrait").GetComponentInChildren<PortraitView>(true).Show(me);
            row.Find("Me/Name").GetComponent<TMP_Text>().text = me.name;
            row.Find("Them/Portrait").GetComponentInChildren<PortraitView>(true).Show(them);
            row.Find("Them/Name").GetComponent<TMP_Text>().text = them.name;
            var rt = row.Find("Vs/Rule").GetComponent<TMP_Text>();
            rt.text = RuleName(c.rule);
            rt.color = c.rule == "sudden" ? Neg : Theme.Text;
            row.Find("Vs/Opp").GetComponent<TMP_Text>().text = opp.name;
            EnterButton(row.Find("Enter").GetComponent<Button>(), c);
        }

        void BindTeam(RectTransform row, Challenge c, Ludus player)
        {
            bool iAmFrom = S.ludi[c.from].isPlayer;
            var opp = S.ludi[iAmFrom ? c.to : c.from];
            int n = c.format.Value;
            var pool = player.gladiators.Select(id => S.gladiators[id]).Where(g => GladiatorRules.CanFight(g) && ChallengeLogic.Availability(S, g) == null).ToList();
            if (!lineups.TryGetValue(c.id, out var order))
            {
                var keep = new List<string> { iAmFrom ? c.fromGladiator : c.toGladiator }.Where(x => x != null).ToList();
                order = SeriesLogic.AutoLineup(S, player, n, ChallengeLogic.Now(S), keep).Select(g => g.id).ToList();
            }
            var rt = row.Find("Info/Rule").GetComponent<TMP_Text>();
            rt.text = $"{SeriesLogic.FormatNames[n]} · {RuleName(c.rule)}";
            rt.color = c.rule == "sudden" ? Neg : Theme.Text;
            row.Find("Info/Hint").GetComponent<TMP_Text>().text = $"상대: {opp.name}. 이긴 사람이 남아 다음 상대와 싸운다. 체력은 조금만 돌아온다";
            var slots = row.Find("Lineup");
            var options = new[] { "—" }.Concat(pool.Select(g => $"{g.name} · CA {GladiatorRules.ComputeCA(g)}")).ToList();
            for (int i = 0; i < slots.childCount; i++)
            {
                var slot = slots.GetChild(i);
                slot.gameObject.SetActive(i < n);
                if (i >= n) continue;
                slot.Find("Label").GetComponent<TMP_Text>().text = i == 0 ? "선봉" : i == n - 1 ? "대장" : $"{i + 1}번";
                var dd = slot.Find("Pick").GetComponent<TMP_Dropdown>();
                if (!dd.options.Select(o => o.text).SequenceEqual(options)) { dd.ClearOptions(); dd.AddOptions(options); }
                dd.SetValueWithoutNotify(i < order.Count ? pool.FindIndex(g => g.id == order[i]) + 1 : 0);
                dd.onValueChanged.RemoveAllListeners();
                int slotIdx = i;
                string cid = c.id;
                var cur = order;
                dd.onValueChanged.AddListener(v =>
                {
                    var next = new List<string>(cur);
                    while (next.Count <= slotIdx) next.Add(null);
                    string val = v == 0 ? null : pool[v - 1].id;
                    int j = val == null ? -1 : next.IndexOf(val);
                    if (j >= 0) next[j] = next[slotIdx];
                    next[slotIdx] = val;
                    lineups[cid] = next.Where(x => x != null).ToList();
                    Refresh();
                });
            }
            EnterButton(row.Find("Enter").GetComponent<Button>(), c);
        }

        void EnterButton(Button b, Challenge c)
        {
            b.transform.Find("Fill").GetComponent<Image>().color = c.rule == "sudden" ? Theme.BloodDark : Theme.ContinueBg;
            b.onClick.RemoveAllListeners();
            string cid = c.id;
            b.onClick.AddListener(() => PlayOfficial(cid));
        }

        // ───────── 경기 시작 ─────────

        void PlayOfficial(string challengeId)
        {
            var draft = SaveSystem.FromJson(SaveSystem.ToJson(S));
            var rng = new Rng(draft.rngState);
            var c = draft.challenges.First(x => x.id == challengeId);
            bool meFirst = draft.ludi[c.from].isPlayer;
            if (c.format is int fmt && fmt != 0 && lineups.TryGetValue(challengeId, out var order))
            {
                if (meFirst) c.fromLineup = order; else c.toLineup = order;
            }
            var played = ChallengeLogic.PlayChallenge(draft, c, rng);
            draft.rngState = rng.State;
            if (played.result == null)
            {
                StateUtil.PushMessage(draft, "match", "기권", played.forfeit ?? "");
                G.Replace(draft);
                return;
            }
            string myLudus = draft.playerLudusId;
            double rp = played.summary?.rpChange.GetValueOrDefault(myLudus) ?? 0;
            double money = played.summary?.money.GetValueOrDefault(myLudus) ?? 0;
            if (played.series != null && c.format is int n && n != 0)
            {
                var s = played.series;
                bool won = (s.winner == 0) == meFirst;
                var opp = draft.ludi[meFirst ? c.to : c.from];
                var dead = s.bouts.Where(b => b.result.died != null).Select(b => draft.gladiators[b.result.died].name).ToList();
                StateUtil.PushMessage(draft, dead.Count > 0 ? "death" : "match", $"{SeriesLogic.FormatNames[n]} {(won ? "승리" : "패배")}: {opp.name}",
                    string.Join("\n", s.bouts.Select((b, i) => $"{i + 1}. {draft.gladiators[b.result.winnerId].name} > {draft.gladiators[b.result.loserId].name}")) +
                    $"\nRP {(rp >= 0 ? "+" : "")}{rp} · 수입 {money:N0} D (세전)" + (dead.Count > 0 ? $"\n\n죽은 자: {string.Join(", ", dead)}" : ""));
                seriesLives = SeriesLives(s, meFirst, c.rule, $"{SeriesLogic.FormatNames[n]} · {RuleName(c.rule)}");
                seriesLives[seriesLives.Count - 1].commit = draft;
                seriesIdx = 0;
                Begin(seriesLives[0]);
                return;
            }
            var r = played.result;
            var w = draft.gladiators[r.winnerId];
            var l = draft.gladiators[r.loserId];
            var died = r.died != null ? draft.gladiators[r.died] : null;
            StateUtil.PushMessage(draft, died != null ? "death" : "match",
                died != null ? $"{died.name}, 모래 위에서 숨을 거두다" : $"{RuleName(c.rule)}: {w.name} 승리",
                $"{w.name} vs {l.name} — {w.name} 승리 ({ReasonNames[r.reason]}, {r.rounds}라운드)\n" +
                $"RP {(rp >= 0 ? "+" : "")}{rp} · 수입 {money:N0} D (세전)" +
                (died != null ? $"\n\n{died.name}의 이름이 비석에 새겨졌다. 다시는 돌아오지 않는다." : ""));
            Begin(new Live
            {
                result = r, a = meFirst ? played.a : played.b, b = meFirst ? played.b : played.a,
                items = BuildItems(r, played.a, played.b, (uint)r.rounds), rule = c.rule, commit = draft, sub = "공식 경기",
            });
        }

        void StartPractice()
        {
            var a = S.gladiators[myId];
            var b = S.gladiators[oppId];
            var r = new Rng(S.rngState);
            uint seed = (uint)r.Int(0, int.MaxValue);
            G.Mutate(d => d.rngState = r.State);
            Doctrine myDoc = a.doctrine;
            if (stanceOverride != null)
            {
                myDoc = Newtonsoft.Json.JsonConvert.DeserializeObject<Doctrine>(Newtonsoft.Json.JsonConvert.SerializeObject(a.doctrine));
                myDoc.stance = stanceOverride;
            }
            var result = Sim.Run(a, b, new MatchOptions { rule = rule, seed = seed, doctrines = new[] { myDoc, null }, missioDeathMult = NationRules.LeagueRules(S).missioDeathMult });
            Begin(new Live { result = result, items = BuildItems(result, a, b, seed), a = MatchLogic.CloneGladiator(a), b = MatchLogic.CloneGladiator(b), seed = seed, rule = rule });
        }

        static List<(CommentaryLine, string)> BuildItems(MatchResult result, Gladiator a, Gladiator b, uint seed)
        {
            var lines = Commentary.Build(result.events, new Dictionary<string, Gladiator> { [a.id] = a, [b.id] = b }, seed);
            var byEvent = lines.GroupBy(l => l.ev).ToDictionary(g => g.Key, g => g.ToList());
            var items = new List<(CommentaryLine, string)>();
            foreach (var e in result.events)
            {
                if (e.t == "highlight") items.Add((null, e.kind));
                else if (byEvent.TryGetValue(e, out var ls)) foreach (var l in ls) items.Add((l, null));
            }
            return items;
        }

        /// <summary>여러 판짜리 경기를 중계용으로. 우리 쪽이 늘 왼쪽</summary>
        public static List<Live> SeriesLives(Series series, bool mineIsSide0, string rule, string title)
        {
            int s0 = 0, s1 = 0;
            return series.bouts.Select((bt, i) =>
            {
                if (bt.result.winnerId == bt.a.id) s0++; else s1++;
                return new Live
                {
                    result = bt.result, a = mineIsSide0 ? bt.a : bt.b, b = mineIsSide0 ? bt.b : bt.a, rule = rule,
                    startHp = mineIsSide0 ? bt.startHp : new[] { bt.startHp[1], bt.startHp[0] },
                    items = BuildItems(bt.result, bt.a, bt.b, (uint)(bt.result.rounds + i)),
                    title = title, sub = $"{i + 1}번째 판 · 이 판까지 {(mineIsSide0 ? $"{s0}:{s1}" : $"{s1}:{s0}")}",
                };
            }).ToList();
        }

        // ───────── 중계 ─────────

        void Begin(Live l)
        {
            live = l;
            shown = 0;
            doneCalled = false;
            foreach (var go in lines) Destroy(go);
            lines.Clear();
            hp.Clear();
            if (l.startHp != null)
            {
                hp[l.a.id] = (l.startHp[0] * 100, 100);
                hp[l.b.id] = (l.startHp[1] * 100, 100);
            }
            cutOverlay.SetActive(false);
            flash.gameObject.SetActive(false);
            Refresh();
            playTitle.text = l.title ?? RuleName(l.rule);
            playSub.text = l.sub ?? "모래 마당 연습 시합";
            crowdValue.text = $"<size=160%><b>{Bal.C("crowdStart")}</b></size>\n관중 열광";
            stageCount = 0;
            stage.Begin(l.a, l.b, (bool)Bal.Rule(l.rule)["crowd"]);
            DrawFighters();
            UpdateButtons();
            if (playing != null) StopCoroutine(playing);
            if (SpeedSec <= 0) ShowAll();
            else playing = StartCoroutine(Play());
        }

        IEnumerator Play()
        {
            while (shown < live.items.Count)
            {
                var (line, cut) = live.items[shown];
                if (cut != null)
                {
                    cutOverlay.SetActive(true);
                    cutText.text = CutLabel.TryGetValue(cut, out var lbl) ? lbl : cut;
                    cutText.color = cut == "death" ? ToneColors["death"] : Theme.Torch;
                    yield return new WaitForSeconds(cut == "death" ? 2.4f : cut == "missio" ? 1.6f : 0.9f);
                    cutOverlay.SetActive(false);
                }
                else
                {
                    AddLine(line);
                    PlaySound(line.ev);
                    if (line.tone is "crit" or "blood") StartCoroutine(Flash());
                    Canvas.ForceUpdateCanvases();
                    feedScroll.verticalNormalizedPosition = 0;
                    DrawFighters();
                    yield return new WaitForSeconds(SpeedSec);
                }
                shown++;
            }
            playing = null;
            Finish();
        }

        IEnumerator Flash()
        {
            flash.gameObject.SetActive(true);
            for (float t = 0; t < 0.35f; t += Time.deltaTime)
            {
                flash.color = new Color(0.6f, 0.05f, 0.03f, 0.35f * (1 - t / 0.35f));
                yield return null;
            }
            flash.gameObject.SetActive(false);
        }

        void ShowAll()
        {
            cutOverlay.SetActive(false);
            for (; shown < live.items.Count; shown++)
                if (live.items[shown].line != null) AddLine(live.items[shown].line);
            Canvas.ForceUpdateCanvases();
            feedScroll.verticalNormalizedPosition = 0;
            DrawFighters();
            Finish();
        }

        /// <summary>새로 보인 문장의 사건에 맞춰 효과음 (웹판 playEventSound)</summary>
        void PlaySound(CombatEvent e)
        {
            if (e == null) return;
            switch (e.t)
            {
                case "attack":
                    if (!e.hit) { GameAudio.Sfx("swing"); GameAudio.Sfx("miss"); break; }
                    if (e.blocked) { GameAudio.Sfx("clang"); break; }
                    if (e.magic) { GameAudio.Sfx("heavy", 0.7f); break; }
                    bool big = e.crit || e.damage / e.maxHp > 0.12;
                    float k = e.crit ? 1.3f : 1;
                    if (IsBlunt(e.actor == live.a.id ? live.a : live.b)) GameAudio.Sfx("blunt", k);
                    else if (big) GameAudio.Sfx("heavy", k);
                    else GameAudio.Sfx("hit");
                    if (big) GameAudio.Sfx("blood", k);
                    break;
                case "crowd":
                    if (e.delta >= 8) GameAudio.Sfx("cheer"); else if (e.delta <= -8) GameAudio.Sfx("boo");
                    break;
                case "ko": GameAudio.Sfx("fall"); break;
                case "verdict": if (e.died) GameAudio.Sfx("death"); break;
                case "move": GameAudio.Sfx("step"); break;
            }
        }

        static bool IsBlunt(Gladiator g)
        {
            string cls = g.equipment.main != null ? Data.Weapon(g.equipment.main)?.@class : null;
            return cls == null || cls is "mace" or "unarmed" or "shield";
        }

        void AddLine(CommentaryLine l)
        {
            var t = Instantiate(lineTemplate, lineTemplate.transform.parent);
            t.gameObject.SetActive(true);
            t.text = (l.round > 0 ? $"<color={Theme.Hex(Theme.TextFaint)}>{l.round}R</color>  " : "") + l.text;
            t.color = ToneColors.TryGetValue(l.tone, out var c) ? c : Theme.Text;
            t.fontStyle = l.tone == "death" ? FontStyles.Bold : FontStyles.Normal;
            lines.Add(t.gameObject);
            var e = l.ev;
            // 장면은 지금까지 보여 준 문장의 이벤트까지 진행한다
            int idx = live.result.events.IndexOf(e, stageCount > 0 ? stageCount - 1 : 0);
            if (idx + 1 > stageCount) { stageCount = idx + 1; stage.Show(live.result.events, stageCount); }
            if (e.t == "attack") hp[e.target] = (e.hpAfter, e.maxHp);
            if (e.t == "poison") hp[e.target] = (e.hpAfter, hp.TryGetValue(e.target, out var h) ? h.max : e.hpAfter);
            if (e.t == "crowd") crowdValue.text = $"<size=160%><b>{e.value}</b></size>\n관중 열광";
        }

        void Finish()
        {
            UpdateButtons();
            DrawFighters();
            if (doneCalled) return;
            doneCalled = true;
            if (live.commit != null) { var next = live.commit; live.commit = null; G.Replace(next); return; }
            if (seriesLives != null) return;
            var l = live;
            G.Mutate(d =>
            {
                MatchLogic.ApplyMatchResult(d, l.result, new ApplyOptions { rule = l.rule, ranked = false }, new Rng(l.seed));
                var w = d.gladiators[l.result.winnerId];
                var lo = d.gladiators[l.result.loserId];
                var died = l.result.died != null ? d.gladiators[l.result.died] : null;
                StateUtil.PushMessage(d, died != null ? "death" : "match",
                    died != null ? $"{died.name}, 모래 위에서 숨을 거두다" : $"연습 시합: {w.name} 승리",
                    $"{RuleName(l.rule)} · {w.name} vs {lo.name}\n{w.name} 승리 ({ReasonNames[l.result.reason]}, {l.result.rounds}라운드)" +
                    (died != null ? $"\n\n{died.name}의 이름이 비석에 새겨졌다. 다시는 돌아오지 않는다." : ""));
            });
        }

        void OnClose()
        {
            if (seriesLives != null && seriesIdx < seriesLives.Count - 1) { seriesIdx++; Begin(seriesLives[seriesIdx]); return; }
            live = null;
            seriesLives = null;
            if (returnScreen != null) { var to = returnScreen; returnScreen = null; G.Go(to); return; }
            Refresh();
        }

        /// <summary>다른 화면(판테온 컵)에서 미리 계산한 여러 판 경기를 이 화면에서 중계하고, 끝나면 돌아간다</summary>
        public void RunSeries(List<Live> lives, GameState commit, string speedId, string returnTo)
        {
            speed = speedId;
            returnScreen = returnTo;
            seriesLives = lives;
            seriesLives[seriesLives.Count - 1].commit = commit;
            seriesIdx = 0;
            G.Go("match");
            Begin(seriesLives[0]);
        }

        void UpdateButtons()
        {
            bool finished = live != null && shown >= live.items.Count;
            skipButton.gameObject.SetActive(!finished);
            closeButton.gameObject.SetActive(finished);
            rosterButton.gameObject.SetActive(finished);
            bool lastBout = seriesLives == null || seriesIdx >= seriesLives.Count - 1;
            closeButton.GetComponentInChildren<TMP_Text>().text = seriesLives == null ? "한 판 더" : lastBout ? "돌아간다" : "다음 판";
        }

        void DrawFighters()
        {
            bool finished = shown >= live.items.Count;
            string dead = finished ? live.result.died : null;
            Fighter(fighterA, live.a, dead);
            Fighter(fighterB, live.b, dead);
        }

        void Fighter(RectTransform box, Gladiator g, string dead)
        {
            var pv = box.GetComponentInChildren<PortraitView>(true);
            if (!drawnFor.TryGetValue(box, out var was) || was != g.id) { drawnFor[box] = g.id; pv.Show(g); }
            pv.GetComponentInParent<CanvasGroup>().alpha = dead == g.id ? 0.4f : 1f;
            box.Find("Info/Name").GetComponent<TMP_Text>().text = g.name;
            box.Find("Info/Sub").GetComponent<TMP_Text>().text = $"{Data.Style(g.style)?.name} · <color={Theme.Hex(Theme.Torch)}>{Widgets.Stars(GladiatorRules.ComputeCA(g))}</color>";
            double pct = hp.TryGetValue(g.id, out var h) ? h.hp / h.max : 1;
            var fill = (RectTransform)box.Find("Info/Hp/Fill");
            fill.anchorMax = new Vector2(Mathf.Clamp01((float)pct), 1);
        }

        static string RuleName(string r) => (string)Bal.Rule(r)["name"];

        static void Seg(Button b, bool on)
        {
            b.transform.Find("Fill").GetComponent<Image>().color = on ? Theme.ContinueBg : Theme.Panel2;
            b.GetComponent<Image>().color = on ? Theme.Torch : Theme.Line;
        }
    }
}
