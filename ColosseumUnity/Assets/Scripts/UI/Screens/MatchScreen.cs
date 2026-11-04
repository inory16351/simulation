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
            /// <summary>난전: 우리 편(왼쪽)·상대 편, 그리고 시뮬레이션 순서의 두 편 (장면 좌표가 이 순서)</summary>
            public List<Gladiator> teamA, teamB;
            public List<Gladiator>[] simSides;
            public int mySimSide;
            /// <summary>던전 전투 다시 보기: 상태를 바꾸지 않고, 언제든 나갈 수 있다</summary>
            public bool replay;
        }

        [Header("준비 화면")]
        [SerializeField] GameObject setupRoot;
        [SerializeField] List<Button> speedButtons = new();
        [SerializeField] GameObject officialPanel;
        [SerializeField] RectTransform officialTemplate;
        [SerializeField] RectTransform teamTemplate;
        [Tooltip("오늘 치를 경기가 없을 때 안내")]
        [SerializeField] TMP_Text noMatch;
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
        [Header("결과창 (평점 매니저 기록)")]
        [SerializeField] GameObject resultPanel;
        [SerializeField] TMP_Text resultMvp;
        [SerializeField] RectTransform resultTemplate;

        TemplateList<RectTransform> officials, teams, resultRows;
        readonly List<GameObject> lines = new();

        string speed = "normal", returnScreen;
        readonly Dictionary<string, List<string>> lineups = new();

        Live live;
        List<Live> seriesLives;
        int seriesIdx;
        Coroutine playing;
        int shown;
        bool doneCalled;
        readonly Dictionary<string, (double hp, double max)> hp = new();
        readonly Dictionary<RectTransform, string> drawnFor = new();
        string rosterLabel;
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
            resultRows = new TemplateList<RectTransform>(resultTemplate);
            lineTemplate.gameObject.SetActive(false);
            for (int i = 0; i < speedButtons.Count; i++) { string s = Speeds[i].id; speedButtons[i].onClick.AddListener(() => { speed = s; Refresh(); }); }
            // 중계 중에는 결과로 건너뛰기, 끝난 뒤에는 결과창 ↔ 중계 기록 전환
            skipButton.onClick.AddListener(() =>
            {
                if (live != null && shown >= live.items.Count) { resultPanel.SetActive(!resultPanel.activeSelf); UpdateButtons(); return; }
                if (playing != null) StopCoroutine(playing);
                playing = null;
                ShowAll();
            });
            closeButton.onClick.AddListener(OnClose);
            rosterButton.onClick.AddListener(() =>
            {
                string back = live?.replay == true && returnScreen != null ? returnScreen : "roster";
                if (playing != null) StopCoroutine(playing);
                playing = null;
                live = null; seriesLives = null; returnScreen = null;
                G.Go(back);
            });
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
            var official = ChallengeLogic.TodaysPlayerMatches(S);

            for (int i = 0; i < speedButtons.Count; i++) Seg(speedButtons[i], speed == Speeds[i].id);

            // 정식으로 수락된 경기(도전장·컵)만 치른다. 연습 시합은 없다
            officialPanel.SetActive(official.Count > 0);
            noMatch.gameObject.SetActive(official.Count == 0);
            var singles = official.Where(c => !(c.format is int f && f != 0)).ToList();
            var teamList = official.Where(c => c.format is int f && f != 0).ToList();
            officials.Sync(singles.Count, (row, i) => BindOfficial(row, singles[i]));
            teams.Sync(teamList.Count, (row, i) => BindTeam(row, teamList[i], player));
        }


        void BindOfficial(RectTransform row, Challenge c)
        {
            bool iAmFrom = S.ludi[c.from].isPlayer;
            var me = S.gladiators[iAmFrom ? c.fromGladiator : c.toGladiator];
            var them = S.gladiators[iAmFrom ? c.toGladiator : c.fromGladiator];
            var opp = S.ludi[iAmFrom ? c.to : c.from];
            row.Find("Me/Portrait").GetComponentInChildren<PortraitView>(true).Show(me);
            row.Find("Me/Name").GetComponent<TMP_Text>().text = Tips.Name(me);
            row.Find("Them/Portrait").GetComponentInChildren<PortraitView>(true).Show(them);
            row.Find("Them/Name").GetComponent<TMP_Text>().text = Tips.Name(them);
            var rt = row.Find("Vs/Rule").GetComponent<TMP_Text>();
            rt.text = ChallengeLogic.RuleLabel(c);
            rt.color = c.rule == "sudden" ? Neg : Theme.Text;
            row.Find("Vs/Opp").GetComponent<TMP_Text>().text = Tips.Ludus(opp);
            EnterButton(row.Find("Enter").GetComponent<Button>(), c);
        }

        void BindTeam(RectTransform row, Challenge c, Ludus player)
        {
            bool iAmFrom = S.ludi[c.from].isPlayer;
            var opp = S.ludi[iAmFrom ? c.to : c.from];
            int n = c.format.Value;
            var pool = player.gladiators.Select(id => S.gladiators[id]).Where(g => GladiatorRules.CanFight(g) && ChallengeLogic.Availability(S, g, null, c.friendly != true) == null).ToList();
            if (!lineups.TryGetValue(c.id, out var order))
            {
                var keep = new List<string> { iAmFrom ? c.fromGladiator : c.toGladiator }.Where(x => x != null).ToList();
                order = SeriesLogic.AutoLineup(S, player, n, ChallengeLogic.Now(S), keep, c.friendly != true).Select(g => g.id).ToList();
            }
            var rt = row.Find("Info/Rule").GetComponent<TMP_Text>();
            rt.text = $"{SeriesLogic.FormatNames[n]} · {ChallengeLogic.RuleLabel(c)}";
            rt.color = c.rule == "sudden" ? Neg : Theme.Text;
            row.Find("Info/Hint").GetComponent<TMP_Text>().text = $"상대: {opp.name}. 양쪽 출전자 전원이 한꺼번에 싸웁니다. 한쪽이 모두 쓰러지거나 마지막 한 명이 항복하면 끝납니다";
            var slots = row.Find("Lineup");
            var options = new[] { "—" }.Concat(pool.Select(g => $"{g.name} · {Widgets.Stars(Scouting.CaRange(S, g).mid)}")).ToList();
            for (int i = 0; i < slots.childCount; i++)
            {
                var slot = slots.GetChild(i);
                slot.gameObject.SetActive(i < n);
                if (i >= n) continue;
                slot.Find("Label").GetComponent<TMP_Text>().text = $"출전 {i + 1}";
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
            BindTactic(row.Find("Info/Tactic"), player, opp, n);
            EnterButton(row.Find("Enter").GetComponent<Button>(), c);
        }

        /// <summary>
        /// 이 경기 전술 줄: 팀 방침과 집중 대상을 고르면 그 인원(3:3·5:5) 기본 전술에 바로 저장된다.
        /// 집중 대상 후보는 상대가 낼 법한 명단(AutoLineup). 경기 날 명단에 없으면 상대 최강자를 노린다
        /// </summary>
        void BindTactic(Transform t, Ludus player, Ludus opp, int n)
        {
            string fmt = Tactics.FormatKey(n);
            var saved = player.teamTactics != null && player.teamTactics.TryGetValue(fmt, out var st) ? st : null;
            string policy = saved?.policy ?? Tactics.AutoTeam(player, new List<Gladiator>()).policy;

            var pd = t.Find("Policy").GetComponent<TMP_Dropdown>();
            var policies = Tactics.Policies.ToList();
            var pOptions = policies.Select(k => Tactics.PolicyNames[k]).ToList();
            if (!pd.options.Select(o => o.text).SequenceEqual(pOptions)) { pd.ClearOptions(); pd.AddOptions(pOptions); }
            pd.SetValueWithoutNotify(Math.Max(0, policies.IndexOf(policy)));
            pd.onValueChanged.RemoveAllListeners();
            pd.onValueChanged.AddListener(v => G.Mutate(s => Tactics.SavedTeam(s.ludi[s.playerLudusId], fmt).policy = policies[v]));

            var foes = SeriesLogic.AutoLineup(S, opp, n, ChallengeLogic.Now(S));
            var fd = t.Find("Focus").GetComponent<TMP_Dropdown>();
            fd.gameObject.SetActive(policy == "focus");
            var fOptions = new[] { "첫 목표: 상대 최강자 (자동)" }.Concat(foes.Select(g => $"첫 목표: {g.name} · {Widgets.Stars(Scouting.CaRange(S, g).mid)}")).ToList();
            if (!fd.options.Select(o => o.text).SequenceEqual(fOptions)) { fd.ClearOptions(); fd.AddOptions(fOptions); }
            fd.SetValueWithoutNotify(saved?.focusTarget == null ? 0 : foes.FindIndex(g => g.id == saved.focusTarget) + 1);
            fd.onValueChanged.RemoveAllListeners();
            fd.onValueChanged.AddListener(v => G.Mutate(s => Tactics.SavedTeam(s.ludi[s.playerLudusId], fmt).focusTarget = v == 0 ? null : foes[v - 1].id));

            var roles = t.Find("Roles").GetComponent<Button>();
            roles.onClick.RemoveAllListeners();
            roles.onClick.AddListener(() => G.Go("strategy"));
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
                var dead = s.melee != null
                    ? s.melee.deaths.Select(id => draft.gladiators[id].name).ToList()
                    : s.bouts.Where(b => b.result.died != null).Select(b => draft.gladiators[b.result.died].name).ToList();
                string detail = s.melee != null
                    ? string.Join("\n", s.melee.downs.Select((d, i) => $"{i + 1}. {Josa.J(draft.gladiators[d.Value].name, "이/가")} {Josa.J(draft.gladiators[d.Key].name, "을/를")} 쓰러뜨렸습니다"))
                    : string.Join("\n", s.bouts.Select((b, i) => $"{i + 1}. {draft.gladiators[b.result.winnerId].name} > {draft.gladiators[b.result.loserId].name}"));
                StateUtil.PushMessage(draft, dead.Count > 0 ? "death" : "match", $"{SeriesLogic.FormatNames[n]} {(won ? "승리" : "패배")}: {opp.name}",
                    detail +
                    $"\nRP {(rp >= 0 ? "+" : "")}{rp} · 수입 {money:N0} D (세전)" + (dead.Count > 0 ? $"\n\n죽은 자: {string.Join(", ", dead)}" : ""));
                seriesLives = SeriesLives(s, meFirst, c.rule, $"{SeriesLogic.FormatNames[n]} · {ChallengeLogic.RuleLabel(c)}");
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
                died != null ? $"{died.name}, 모래 위에서 숨을 거두었습니다" : $"{ChallengeLogic.RuleLabel(c)}: {w.name} 승리",
                $"{w.name} vs {l.name} — {w.name} 승리 ({ReasonNames[r.reason]}, {r.rounds}라운드)\n" +
                $"RP {(rp >= 0 ? "+" : "")}{rp} · 수입 {money:N0} D (세전)" +
                (died != null ? $"\n\n{died.name}의 이름이 비석에 새겨졌습니다. 다시는 돌아오지 않습니다." : ""));
            Begin(new Live
            {
                result = r, a = meFirst ? played.a : played.b, b = meFirst ? played.b : played.a,
                items = BuildItems(r, played.a, played.b, (uint)r.rounds), rule = c.rule, commit = draft,
                title = ChallengeLogic.RuleLabel(c), sub = c.friendly == true ? "연습전 · 훈련전 규칙, 명성·상금·RP 없음" : "공식 경기",
            });
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
            if (series.melee != null)
            {
                var sides = series.meleeSides;
                var all = sides[0].Concat(sides[1]).ToDictionary(g => g.id);
                var r = series.melee;
                var lines = Commentary.Build(r.events, all, (uint)r.rounds);
                var byEvent = lines.GroupBy(l => l.ev).ToDictionary(g => g.Key, g => g.ToList());
                var items = new List<(CommentaryLine, string)>();
                foreach (var e in r.events)
                {
                    if (e.t == "highlight") items.Add((null, e.kind));
                    else if (byEvent.TryGetValue(e, out var ls)) foreach (var l in ls) items.Add((l, null));
                }
                return new List<Live>
                {
                    new Live
                    {
                        result = r, items = items, rule = rule, title = title,
                        a = mineIsSide0 ? sides[0][0] : sides[1][0], b = mineIsSide0 ? sides[1][0] : sides[0][0],
                        teamA = mineIsSide0 ? sides[0] : sides[1], teamB = mineIsSide0 ? sides[1] : sides[0],
                        simSides = sides, mySimSide = mineIsSide0 ? 0 : 1,
                        sub = $"{sides[0].Count}:{sides[1].Count} 난전 · 한쪽이 모두 쓰러지거나 항복하면 끝납니다",
                    },
                };
            }
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

        // ───────── 던전 전투 다시 보기 ─────────

        /// <summary>링크(fight:원정:층 순서)를 눌렀을 때: 그 층의 대결들을 이 화면에서 차례로 보여 준다</summary>
        public static void WatchFight(string link)
        {
            var G = GameController.I;
            var p = link.Split(':');
            if (p.Length != 3 || !int.TryParse(p[2], out int idx)) return;
            var ex = DungeonLogic.ExpeditionsOf(G.State).FirstOrDefault(x => x.id == p[1]);
            if (ex?.battles == null || idx < 0 || idx >= ex.battles.Count) return;
            var screen = FindFirstObjectByType<MatchScreen>(FindObjectsInactive.Include);
            if (screen == null) return;
            // 공식 경기 중계가 아직 끝나지 않았으면 (결과를 반영하기 전이니) 그쪽으로 돌려보낸다
            if (screen.live?.commit != null || screen.seriesLives?.LastOrDefault()?.commit != null) { G.Go("match"); return; }
            var lives = DungeonLives(ex, ex.battles[idx]);
            if (lives.Count == 0) return;
            screen.RunSeries(lives, null, screen.speed, G.Screen);
        }

        static List<Live> DungeonLives(Expedition ex, FloorBattle fb)
        {
            var d = DungeonLogic.GetDungeon(ex.dungeon);
            string form = DungeonLogic.Formations.TryGetValue(ex.formation, out var f) ? f.name : ex.formation;
            string title = $"{d.name} {fb.floor}층{(fb.boss ? " · 우두머리" : "")}";
            var bouts = DungeonLogic.Replay(fb);
            return bouts.Select((x, i) => new Live
            {
                result = x.result, a = x.a, b = x.b, rule = "training", startHp = x.startHp, replay = true, title = title,
                items = BuildItems(x.result, x.a, x.b, (uint)(x.result.rounds + i)),
                sub = $"{i + 1}/{bouts.Count}번째 대결 · 진형 {form} · {(fb.won ? "이 층을 이겼습니다" : "이 층에서 물러났습니다")}",
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
            resultPanel.SetActive(false);
            Refresh();
            playTitle.text = l.title ?? RuleName(l.rule);
            playSub.text = l.sub ?? "모래 마당 연습 시합";
            crowdValue.text = $"<size=160%><b>{Bal.C("crowdStart")}</b></size>\n관중 열광";
            stageCount = 0;
            if (l.simSides != null) stage.Begin(l.simSides[0], l.simSides[1], (bool)Bal.Rule(l.rule)["crowd"], true, l.mySimSide);
            else stage.Begin(l.a, l.b, (bool)Bal.Rule(l.rule)["crowd"]);
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
                    var who = live.teamA != null ? live.teamA.Concat(live.teamB).FirstOrDefault(g => g.id == e.actor) ?? live.a : e.actor == live.a.id ? live.a : live.b;
                    if (IsBlunt(who)) GameAudio.Sfx("blunt", k);
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

        /// <summary>중계 문장 속 출전자 이름을 검투사 링크로 (FM처럼 눌러서 카드). 긴 이름부터 바꿔 겹침을 피한다</summary>
        string LinkNames(string text)
        {
            var people = live.teamA != null ? live.teamA.Concat(live.teamB) : new[] { live.a, live.b };
            foreach (var g in people.Where(x => x != null && !string.IsNullOrEmpty(x.name)).OrderByDescending(x => x.name.Length))
                if (text.Contains(g.name) && !text.Contains($"\"g:{g.id}\"")) text = text.Replace(g.name, Tips.Name(g));
            return text;
        }

        void AddLine(CommentaryLine l)
        {
            var t = Instantiate(lineTemplate, lineTemplate.transform.parent);
            t.gameObject.SetActive(true);
            t.text = (l.round > 0 ? $"<color={Theme.Hex(Theme.TextFaint)}>{l.round}R</color>  " : "") + LinkNames(l.text);
            t.color = ToneColors.TryGetValue(l.tone, out var c) ? c : Theme.Text;
            t.fontStyle = l.tone == "death" ? FontStyles.Bold : FontStyles.Normal;
            lines.Add(t.gameObject);
            var e = l.ev;
            // 장면은 지금까지 보여 준 문장의 이벤트까지 진행한다
            int idx = live.result.events.IndexOf(e, stageCount > 0 ? stageCount - 1 : 0);
            if (idx + 1 > stageCount) { stageCount = idx + 1; stage.Show(live.result.events, stageCount); }
            if (e.t == "attack" || e.t == "thorns") hp[e.target] = (e.hpAfter, e.maxHp);
            if (e.t == "poison" || e.t == "regen") hp[e.target] = (e.hpAfter, e.maxHp);
            if (e.t == "crowd") crowdValue.text = $"<size=160%><b>{e.value}</b></size>\n관중 열광";
        }

        void Finish()
        {
            if (!doneCalled) ShowResult();
            UpdateButtons();
            DrawFighters();
            if (doneCalled) return;
            doneCalled = true;
            // 공식 경기는 미리 계산한 결과를 중계가 끝난 뒤 반영한다
            if (live.commit != null) { var next = live.commit; live.commit = null; G.Replace(next); }
        }

        /// <summary>결과창: 평점 매니저가 매긴 출전자별 기록. 우리 편 먼저, 경기 최우수 검투사 표시</summary>
        void ShowResult()
        {
            var st = live.result?.stats;
            if (st == null || st.Count == 0) { resultPanel.SetActive(false); return; }
            var mine = live.teamA ?? new List<Gladiator> { live.a };
            var theirs = live.teamB ?? new List<Gladiator> { live.b };
            var people = mine.Select(g => (g, ours: true)).Concat(theirs.Select(g => (g, ours: false))).Where(x => x.g != null && st.ContainsKey(x.g.id)).ToList();
            if (people.Count == 0) { resultPanel.SetActive(false); return; }
            var best = people.OrderByDescending(x => st[x.g.id].rating).ThenByDescending(x => st[x.g.id].dealt).First();
            var bs = st[best.g.id];
            resultMvp.text = $"<color={Theme.Hex(Theme.Torch)}>★ 경기 최우수 검투사</color>  <b>{Tips.Name(best.g)}</b>  평점 {Rate(bs.rating)}  " +
                $"<color={Theme.Hex(Theme.TextDim)}>입힌 피해 {bs.dealt:0} · 쓰러뜨림 {bs.downs}</color>";
            resultRows.Sync(people.Count, (row, i) =>
            {
                var (g, ours) = people[i];
                var f = st[g.id];
                row.GetComponent<Image>().color = ours ? new Color(0.89f, 0.6f, 0.23f, 0.08f) : Color.clear;
                string mark = f.died ? $" <color={Theme.Hex(ToneColors["death"])}>†</color>" : f.downed ? $" <color={Theme.Hex(Theme.TextFaint)}>(쓰러짐)</color>" : "";
                string side = live.teamA != null ? $"<color={Theme.Hex(Theme.TextFaint)}>{(ours ? "우리" : "상대")}</color>  " : "";
                RT(row, "Name").text = side + Tips.Name(g) + mark;
                RT(row, "Rating").text = Rate(f.rating);
                RT(row, "Dealt").text = f.dealt.ToString("0");
                RT(row, "Taken").text = f.taken.ToString("0");
                RT(row, "Acc").text = f.attacks > 0 ? $"{f.hits}/{f.attacks}" : "—";
                RT(row, "Block").text = (f.blocks + f.parries).ToString();
                RT(row, "Dodge").text = f.dodges.ToString();
                RT(row, "Crit").text = f.crits.ToString();
                RT(row, "Downs").text = f.downs + (f.kills > 0 ? $" <color={Theme.Hex(ToneColors["death"])}>({f.kills})</color>" : "");
            });
            resultPanel.SetActive(true);
        }

        /// <summary>평점 글자 (FM처럼 7.5 이상 녹색, 6.5 아래 붉은빛)</summary>
        static string Rate(double r) => $"<b><color={Theme.Hex(r >= 7.5 ? Theme.Good : r >= 6.5 ? Theme.Parchment : Neg)}>{r:0.0}</color></b>";

        static TMP_Text RT(Transform row, string path) => row.Find(path).GetComponent<TMP_Text>();

        void OnClose()
        {
            resultPanel.SetActive(false);
            if (seriesLives != null && seriesIdx < seriesLives.Count - 1) { seriesIdx++; Begin(seriesLives[seriesIdx]); return; }
            live = null;
            seriesLives = null;
            if (returnScreen != null) { var to = returnScreen; returnScreen = null; G.Go(to); return; }
            // 오늘 남은 경기가 없으면 서판으로 (FM처럼 경기 뒤 다음 일로)
            if (ChallengeLogic.TodaysPlayerMatches(S).Count == 0) { G.Go("inbox"); return; }
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
            bool hasResult = live?.result?.stats != null;
            skipButton.gameObject.SetActive(!finished || hasResult);
            skipButton.GetComponentInChildren<TMP_Text>().text = !finished ? "결과 보기" : resultPanel.activeSelf ? "중계 기록" : "결과창";
            closeButton.gameObject.SetActive(finished);
            bool replay = live?.replay == true;
            rosterButton.gameObject.SetActive(finished || replay);
            var rosterText = rosterButton.GetComponentInChildren<TMP_Text>();
            rosterLabel ??= rosterText.text;
            rosterText.text = replay ? "그만 보기" : rosterLabel;
            bool lastBout = seriesLives == null || seriesIdx >= seriesLives.Count - 1;
            closeButton.GetComponentInChildren<TMP_Text>().text = lastBout ? "다음으로" : "다음 판";
        }

        void DrawFighters()
        {
            bool finished = shown >= live.items.Count;
            if (live.teamA != null)
            {
                TeamBox(fighterA, live.teamA, true);
                TeamBox(fighterB, live.teamB, false);
                return;
            }
            string dead = finished ? live.result.died : null;
            Fighter(fighterA, live.a, dead);
            Fighter(fighterB, live.b, dead);
        }

        /// <summary>난전 머리 칸: 편 명단(쓰러지면 흐리게, 죽으면 붉게), 서 있는 수, 편 전체 체력. 초상화는 그 편에서 마지막으로 움직인 사람</summary>
        void TeamBox(RectTransform box, List<Gladiator> side, bool mine)
        {
            var seen = live.result.events.Take(stageCount).ToList();
            var downed = seen.Where(e => e.t == "ko" || e.t == "surrender").Select(e => e.target ?? e.actor).ToHashSet();
            var dead = seen.Where(e => e.t == "verdict" && e.died).Select(e => e.target).ToHashSet();
            var ids = side.Select(g => g.id).ToHashSet();
            var lastActor = seen.LastOrDefault(e => e.actor != null && ids.Contains(e.actor) && !downed.Contains(e.actor))?.actor;
            var face = side.FirstOrDefault(g => g.id == lastActor) ?? side.FirstOrDefault(g => !downed.Contains(g.id)) ?? side[0];
            var pv = box.GetComponentInChildren<PortraitView>(true);
            if (!drawnFor.TryGetValue(box, out var was) || was != face.id) { drawnFor[box] = face.id; pv.Show(face); }
            pv.GetComponentInParent<CanvasGroup>().alpha = downed.Contains(face.id) ? 0.4f : 1f;
            string faint = Theme.Hex(Theme.TextFaint), neg = Theme.Hex(ToneColors["death"]);
            box.Find("Info/Name").GetComponent<TMP_Text>().text = "<size=72%>" + string.Join("  ", side.Select(g =>
                dead.Contains(g.id) ? $"<color={neg}>{Tips.Name(g)}(죽음)</color>"
                : downed.Contains(g.id) ? $"<color={faint}><s>{Tips.Name(g)}</s></color>"
                : Tips.Name(g))) + "</size>";
            int up = side.Count(g => !downed.Contains(g.id));
            box.Find("Info/Sub").GetComponent<TMP_Text>().text = $"{(mine ? "우리 편" : "상대 편")} · 서 있는 사람 {up}/{side.Count}";
            double sum = 0;
            foreach (var g in side) sum += downed.Contains(g.id) ? 0 : hp.TryGetValue(g.id, out var h) ? h.hp / h.max : 1;
            var fill = (RectTransform)box.Find("Info/Hp/Fill");
            fill.anchorMax = new Vector2(Mathf.Clamp01((float)(sum / side.Count)), 1);
        }

        void Fighter(RectTransform box, Gladiator g, string dead)
        {
            var pv = box.GetComponentInChildren<PortraitView>(true);
            if (!drawnFor.TryGetValue(box, out var was) || was != g.id) { drawnFor[box] = g.id; pv.Show(g); }
            pv.GetComponentInParent<CanvasGroup>().alpha = dead == g.id ? 0.4f : 1f;
            box.Find("Info/Name").GetComponent<TMP_Text>().text = Tips.Name(g);
            box.Find("Info/Sub").GetComponent<TMP_Text>().text = $"{Tips.Style(g.style)} · <color={Theme.Hex(Theme.Torch)}>{Widgets.CaStars(S, g)}</color>";
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
