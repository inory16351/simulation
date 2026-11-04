// 게임 씬(Assets/Scenes/Game.unity) 조립: 상단 바, 경고줄, 왼쪽 메뉴, 화면 자리, 새 게임 화면.
// MCP(execute_code: Colosseum.EditorTools.GameSceneBuilder.Build())나 메뉴로 부른다. 다시 부르면 씬을 새로 만든다.
using System.Collections.Generic;
using Colosseum.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Colosseum.EditorTools.UiKit;

namespace Colosseum.EditorTools
{
    public static class GameSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Game.unity";

        /// <summary>옮긴 화면의 조립 함수. 없는 화면은 '옮기는 중' 자리로 둔다</summary>
        static readonly Dictionary<string, System.Func<Transform, ScreenBase>> ScreenBuilders = new()
        {
            ["inbox"] = InboxBuilder.Build,
            ["overview"] = OverviewBuilder.Build,
            ["roster"] = RosterBuilder.Build,
            ["strategy"] = StrategyBuilder.Build,
            ["training"] = TrainingBuilder.Build,
            ["challenge"] = ChallengeBuilder.Build,
            ["schedule"] = ScheduleBuilder.Build,
            ["match"] = MatchBuilder.Build,
            ["league"] = LeagueBuilder.Build,
            ["cup"] = CupBuilder.Build,
            ["natcup"] = CompBuilder.BuildNatCup,
            ["olympica"] = CompBuilder.BuildOlympica,
            ["market"] = MarketBuilder.Build,
            ["staff"] = StaffBuilder.Build,
            ["facilities"] = FacilitiesBuilder.Build,
            ["finance"] = FinanceBuilder.Build,
            ["youth"] = YouthBuilder.Build,
            ["dungeon"] = DungeonBuilder.Build,
            ["world"] = WorldBuilder.Build,
            ["hall"] = HallBuilder.Build,
        };

        [MenuItem("Colosseum/Build Game Scene")]
        public static string Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Theme.Bg;
            cam.orthographic = true;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var controller = new GameObject("Game").AddComponent<GameController>();

            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            var canvas = canvasGo.transform;
            var bg = Box("Background", canvas, Theme.Bg);
            ((RectTransform)bg.transform).Stretch();
            // 웹판 body: 어두운 덮개 + bg_main
            CoverBackground(bg.transform, "ui/bg_main", new Color32(14, 11, 9, 209), new Color32(14, 11, 9, 230));

            // ── 게임 ──
            var gameRoot = Rect("GameRoot", canvas).Stretch();
            VCol(gameRoot, 0).childForceExpandHeight = false;
            var shell = gameRoot.gameObject.AddComponent<Shell>();

            // 상단 바
            var top = Box("Topbar", gameRoot, Theme.TopbarTop).Layout(minH: 64, prefH: 64);
            HRow(top.transform, 18, 18, 18);
            CoverBackground(top.transform, "ui/topbar", new Color32(20, 16, 13, 235), new Color32(20, 16, 13, 140), 0.78f, true);
            var crest = Sprite("Crest", top.transform, "crest/aurelia", 42, 42);
            var club = Rect("Club", top.transform);
            VCol(club, 0).childAlignment = TextAnchor.MiddleLeft;
            var clubName = Text("Name", club, "루두스", 22, Theme.Parchment, true, FontStyles.Bold);
            var clubSub = Text("Sub", club, "리그", 14, Theme.TextDim);
            Rect("Spacer", top.transform).Layout(flexW: 1);
            var (dateText, payText) = Stat(top.transform, "Date", "시즌 1 · 서리달 1일", "급료일까지 7일");
            var (treasury, wages) = Stat(top.transform, "Treasury", "0 D", "주급 합계 0 D");
            BuildAudioControl(top.transform);
            var skip = Button("SkipButton", top.transform, "일정까지 ▶▶").Layout(prefW: 150, prefH: 40);
            var next = Button("NextButton", top.transform, "다음 날로 ▶", true, 17).Layout(prefW: 190, prefH: 44);
            Box("TopLine", gameRoot, Theme.Bronze).Layout(minH: 1, prefH: 1);

            // 경고줄
            var alert = Box("AlertBar", gameRoot, Theme.BloodDark).Layout(minH: 28, prefH: 28);
            var alertBtn = alert.gameObject.AddComponent<Button>();
            var alertText = Text("Text", alert.transform, "", 14, Theme.AlertText);
            ((RectTransform)alertText.transform).Stretch(18, 0, 18, 0);

            // 본문: 메뉴 + 화면
            var body = Rect("Body", gameRoot).Layout(flexH: 1);
            HRow(body, 0).childControlHeight = true;
            body.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
            var side = Box("Sidebar", body, Theme.Bg2).Layout(minW: 230, prefW: 230, flexW: 0);
            VCol(side.transform, 0, 0, 0, 12, 12);
            CoverBackground(side.transform, "ui/sidebar", new Color32(16, 13, 11, 153), new Color32(16, 13, 11, 217));
            var menuButtons = new List<MenuButton>();
            foreach (var (id, label) in Shell.Menu) menuButtons.Add(MenuItem(side.transform, id, label));
            Box("Sep", side.transform, Theme.Line).Layout(minH: 1, prefH: 1);
            var restart = MenuItem(side.transform, "restart", "로비로", false).button;

            var main = Rect("Main", body).Layout(flexW: 1);
            var screens = new List<ScreenBase>();
            foreach (var (id, label) in Shell.Menu) screens.Add(ScreenBuilders.TryGetValue(id, out var b) ? b(main) : Placeholder(main, id, label));
            screens.Add(GladiatorBuilder.Build(main));
            var gameOver = Panel("GameOver", main, Theme.Panel);
            ((RectTransform)gameOver.transform).Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(26, -140), new Vector2(-26, -20));
            var goText = Text("Text", gameOver.transform, "루두스를 잃었습니다", 18, Theme.Parchment);
            ((RectTransform)goText.transform).Stretch(16, 12, 16, 12);
            gameOver.gameObject.SetActive(false);

            // ── 새 게임 ──
            var newGame = BuildNewGame(canvas);
            // ── 로비 (타이틀) ──
            var lobby = LobbyBuilder.Build(canvas);

            // ── 맨 위: 정보 카드, 검투사 팝업 카드, 툴팁 ──
            PopupBuilder.BuildInfoPopup(canvas);
            PopupBuilder.BuildGladiatorPopup(canvas);
            PopupBuilder.BuildItemPicker(canvas);
            PopupBuilder.BuildNegotiationPopup(canvas);
            PopupBuilder.BuildConfirmPopup(canvas);
            PopupBuilder.BuildTipLayer(canvas);

            var so = new SerializedObject(shell);
            void S(string p, Object v) => so.FindProperty(p).objectReferenceValue = v;
            S("crest", crest); S("clubName", clubName); S("clubSub", clubSub); S("dateText", dateText); S("paydayText", payText);
            S("treasuryText", treasury); S("wagesText", wages); S("skipButton", skip); S("nextButton", next);
            S("nextLabel", next.GetComponentInChildren<TMP_Text>()); S("alertBar", alert.gameObject); S("alertText", alertText); S("alertButton", alertBtn);
            S("restartButton", restart); S("gameOverPanel", gameOver.gameObject); S("gameOverText", goText);
            S("gameRoot", gameRoot.gameObject); S("newGameRoot", newGame); S("lobbyRoot", lobby);
            var mb = so.FindProperty("menuButtons");
            mb.arraySize = menuButtons.Count;
            for (int i = 0; i < menuButtons.Count; i++) mb.GetArrayElementAtIndex(i).objectReferenceValue = menuButtons[i];
            var sc = so.FindProperty("screens");
            sc.arraySize = screens.Count;
            for (int i = 0; i < screens.Count; i++) sc.GetArrayElementAtIndex(i).objectReferenceValue = screens[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true), new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true) };
            return "ok " + ScenePath;
        }

        /// <summary>소리 조절: 음소거 버튼 + 크기 버튼(누르면 효과음·음악 막대가 아래로 펼쳐진다)</summary>
        static void BuildAudioControl(Transform parent)
        {
            var root = Rect("Audio", parent);
            HRow(root, 4);
            var mute = Button("Mute", root, "소리 켬", false, 14).Layout(prefW: 84, prefH: 36);
            var toggle = Button("Volume", root, "▼", false, 12).Layout(prefW: 32, prefH: 36);
            var pop = Panel("Popup", root);
            pop.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var prt = (RectTransform)pop.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0, 0);
            prt.pivot = new Vector2(0, 1);
            prt.anchoredPosition = new Vector2(0, -6);
            prt.sizeDelta = new Vector2(240, 84);
            pop.gameObject.AddComponent<Canvas>().overrideSorting = true;
            pop.GetComponent<Canvas>().sortingOrder = 50;
            pop.gameObject.AddComponent<GraphicRaycaster>();
            var col = Rect("Pad", pop.transform).Stretch(12, 10, 12, 10);
            VCol(col, 6);
            Slider Row(string label)
            {
                var r = Rect(label, col);
                HRow(r, 8);
                Text("Label", r, label, 13, Theme.TextDim).Layout(prefW: 50, minW: 50);
                var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
                go.transform.SetParent(r, false);
                var sl = go.GetComponent<Slider>();
                sl.minValue = 0; sl.maxValue = 1;
                go.transform.Find("Background").GetComponent<Image>().color = Theme.Bg2;
                go.transform.Find("Fill Area/Fill").GetComponent<Image>().color = Theme.BronzeHi;
                go.transform.Find("Handle Slide Area/Handle").GetComponent<Image>().color = Theme.Parchment;
                sl.Layout(prefW: 150, prefH: 20, minH: 20);
                return sl;
            }
            var sfx = Row("효과음");
            var music = Row("음악");
            var ac = root.gameObject.AddComponent<AudioControl>();
            var so = new SerializedObject(ac);
            so.FindProperty("mute").objectReferenceValue = mute;
            so.FindProperty("toggle").objectReferenceValue = toggle;
            so.FindProperty("popup").objectReferenceValue = pop.gameObject;
            so.FindProperty("sfx").objectReferenceValue = sfx;
            so.FindProperty("music").objectReferenceValue = music;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static (TextMeshProUGUI, TextMeshProUGUI) Stat(Transform parent, string name, string big, string small)
        {
            var box = Rect(name, parent);
            VCol(box, 0).childAlignment = TextAnchor.MiddleRight;
            var b = Text("Value", box, big, 18, Theme.Parchment, false, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            var s = Text("Sub", box, small, 13, Theme.TextDim, false, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
            return (b, s);
        }

        static MenuButton MenuItem(Transform parent, string id, string label, bool icon = true)
        {
            var bg = Box("Menu_" + id, parent, new Color(0, 0, 0, 0)).Layout(minH: 35, prefH: 35);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.highlightedColor = Theme.Panel;
            btn.colors = colors;
            HRow(bg.transform, 10, 20, 14);
            var accent = Box("Accent", bg.transform, Theme.Torch);
            accent.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            ((RectTransform)accent.transform).Anchor(Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(4, 0));
            accent.enabled = false;
            if (icon) Sprite("Icon", bg.transform, $"icon/menu/{id}", 26, 26);
            var t = Text("Label", bg.transform, label, 16).Layout(flexW: 1);
            var badge = Box("Badge", bg.transform, Theme.Blood).Layout(minW: 26, prefW: 26, prefH: 20);
            var bt = Text("Text", badge.transform, "1", 13, Color.white, false, FontStyles.Bold, TextAlignmentOptions.Center);
            ((RectTransform)bt.transform).Stretch();
            badge.gameObject.SetActive(false);
            var m = bg.gameObject.AddComponent<MenuButton>();
            m.id = id; m.button = btn; m.bg = bg; m.accent = accent; m.label = t; m.badge = badge.gameObject; m.badgeText = bt;
            return m;
        }

        static ScreenBase Placeholder(Transform main, string id, string label)
        {
            var rt = Rect("Screen_" + id, main).Stretch(26, 22, 26, 22);
            var title = Text("Title", rt, label, 26, Theme.Parchment, true, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            ((RectTransform)title.transform).Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -40), Vector2.zero);
            var note = Text("Note", rt, "준비 중인 화면입니다.", 16, Theme.TextDim, false, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            ((RectTransform)note.transform).Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -80), new Vector2(0, -46));
            var p = rt.gameObject.AddComponent<PlaceholderScreen>();
            p.screenId = id;
            Set(p, "title", title);
            rt.gameObject.SetActive(false);
            return p;
        }

        static GameObject BuildNewGame(Transform canvas)
        {
            var root = Rect("NewGameRoot", canvas).Stretch(40, 34, 40, 34);
            var title = Text("Title", root, "새 루두스 세우기", 30, Theme.Parchment, true, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            ((RectTransform)title.transform).Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -44), Vector2.zero);
            var sub = Text("Sub", root, "루두스를 세울 나라를 고릅니다. 나라마다 리그 규칙과 관중 열기, 자금 흐름이 다릅니다.", 16, Theme.TextDim, false, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            ((RectTransform)sub.transform).Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -76), new Vector2(0, -48));

            var grid = Rect("NationPick", root).Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -276), new Vector2(0, -90));
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(498, 86);
            gl.spacing = new Vector2(12, 12);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 3;
            var screen = root.gameObject.AddComponent<NewGameScreen>();
            var so = new SerializedObject(screen);
            var cards = so.FindProperty("cards");
            string[] nations = { "aurelia", "grommar", "sylvaren", "dolgar", "karsea", "kemer" };
            cards.arraySize = nations.Length;
            for (int i = 0; i < nations.Length; i++)
            {
                var frame = Box("Nation_" + nations[i], grid, Theme.Line);
                var fill = Box("Fill", frame.transform, Theme.Panel);
                ((RectTransform)fill.transform).Stretch(2, 2, 2, 2);
                fill.raycastTarget = false;
                var btn = frame.gameObject.AddComponent<Button>();
                btn.targetGraphic = frame;
                HRow(frame.transform, 14, 16, 16);
                var crest = Sprite("Crest", frame.transform, $"crest/{nations[i]}", 56, 56);
                var texts = Rect("Texts", frame.transform).Layout(flexW: 1);
                VCol(texts, 2).childAlignment = TextAnchor.MiddleLeft;
                var nm = Text("Name", texts, nations[i], 20, Theme.Parchment, true, FontStyles.Bold);
                var mo = Text("Motif", texts, "", 14, Theme.TextDim);
                var el = cards.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("id").stringValue = nations[i];
                el.FindPropertyRelative("button").objectReferenceValue = btn;
                el.FindPropertyRelative("frame").objectReferenceValue = frame;
                el.FindPropertyRelative("crest").objectReferenceValue = crest;
                el.FindPropertyRelative("name").objectReferenceValue = nm;
                el.FindPropertyRelative("motif").objectReferenceValue = mo;
            }

            var detail = Panel("Detail", root);
            ((RectTransform)detail.transform).Anchor(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 90), new Vector2(0, -292));
            var dTitle = Text("Title", detail.transform, "", 15, Theme.BronzeHi, false, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            ((RectTransform)dTitle.transform).Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -38), new Vector2(-16, -10));
            Box("Line", detail.transform, Theme.Line).rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, -42), new Vector2(-1, -41));
            var left = Rect("Left", detail.transform).Anchor(new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(16, 14), new Vector2(-10, -54));
            var meta = Text("Meta", left, "", 14, Theme.TextDim, false, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            ((RectTransform)meta.transform).Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -46), Vector2.zero);
            var lore = Text("Lore", left, "", 16, Theme.Text, false, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            ((RectTransform)lore.transform).Stretch(0, 54, 0, 0);
            var right = Rect("Right", detail.transform).Anchor(new Vector2(0.5f, 0), new Vector2(1, 1), new Vector2(10, 14), new Vector2(-16, -54));
            var rulesLabel = Text("RulesLabel", right, "리그 특징", 13, Theme.BronzeHi, false, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            ((RectTransform)rulesLabel.transform).Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -20), Vector2.zero);
            var rules = Text("Rules", right, "", 15, Theme.Text, false, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            ((RectTransform)rules.transform).Anchor(new Vector2(0, 0.35f), new Vector2(1, 1), Vector2.zero, new Vector2(0, -24));
            var cultLabel = Text("CultureLabel", right, "문화", 13, Theme.BronzeHi, false, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            ((RectTransform)cultLabel.transform).Anchor(new Vector2(0, 0.35f), new Vector2(1, 0.35f), new Vector2(0, -22), new Vector2(0, -2));
            var culture = Text("Culture", right, "", 15, Theme.Text, false, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            ((RectTransform)culture.transform).Anchor(Vector2.zero, new Vector2(1, 0.35f), Vector2.zero, new Vector2(0, -26));

            var bottom = Rect("Start", root).Anchor(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 60));
            HRow(bottom, 12);
            Text("NameLabel", bottom, "루두스 이름", 16, Theme.Text).Layout(prefW: 100);
            var inputGo = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            inputGo.name = "NameInput";
            inputGo.transform.SetParent(bottom, false);
            inputGo.GetComponent<Image>().color = Theme.Bg2;
            var input = inputGo.GetComponent<TMP_InputField>();
            foreach (var t in inputGo.GetComponentsInChildren<TextMeshProUGUI>(true)) { t.font = Sans; t.fontSize = 18; t.color = Theme.Parchment; }
            inputGo.AddComponent<LayoutElement>().preferredWidth = 280;
            inputGo.GetComponent<LayoutElement>().preferredHeight = 44;
            Text("TierLabel", bottom, "시작 리그", 16, Theme.Text).Layout(prefW: 76);
            var tierButtons = new Button[4];
            string[] tierNames = { "1부 · 쉬움", "2부 · 보통", "3부 · 어려움", "4부 · 밑바닥" };
            for (int i = 0; i < 4; i++) tierButtons[i] = Button("Tier" + (i + 1), bottom, tierNames[i], false, 14).Layout(prefW: 112, prefH: 40);
            var start = Button("StartButton", bottom, "루두스 세우기", true, 17).Layout(prefW: 300, prefH: 48);
            var cancel = Button("CancelButton", bottom, "돌아가기").Layout(prefW: 140, prefH: 44);

            void S(string p, Object v) => so.FindProperty(p).objectReferenceValue = v;
            S("detailTitle", dTitle); S("detailMeta", meta); S("detailLore", lore); S("detailRules", rules); S("detailCulture", culture);
            var tiers = so.FindProperty("tierButtons");
            tiers.arraySize = 4;
            for (int i = 0; i < 4; i++) tiers.GetArrayElementAtIndex(i).objectReferenceValue = tierButtons[i];
            S("nameInput", input); S("startButton", start); S("startLabel", start.GetComponentInChildren<TMP_Text>()); S("cancelButton", cancel);
            so.ApplyModifiedPropertiesWithoutUndo();
            root.gameObject.SetActive(false);
            return root.gameObject;
        }
    }
}
