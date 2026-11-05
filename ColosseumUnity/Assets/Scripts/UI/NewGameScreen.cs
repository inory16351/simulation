// 새 루두스 세우기: 나라와 이름을 고른다 (웹판 NewGameScreen.tsx). 나라 카드 6장은 SceneBuilder가 하이어라키에 만든다.
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class NewGameScreen : MonoBehaviour
    {
        [System.Serializable]
        public class NationCard
        {
            public string id;
            public Button button;
            public Image frame;
            public Image crest;
            public TMP_Text name;
            public TMP_Text motif;
        }

        [SerializeField] List<NationCard> cards = new();
        [SerializeField] TMP_Text detailTitle;
        [SerializeField] TMP_Text detailMeta;
        [SerializeField] TMP_Text detailLore;
        [SerializeField] TMP_Text detailRules;
        [SerializeField] TMP_Text detailCulture;
        [SerializeField] TMP_InputField nameInput;
        [SerializeField] Button startButton;
        [SerializeField] TMP_Text startLabel;
        [SerializeField] Button cancelButton;
        [Tooltip("시작 리그 1~4부 (FM식 난이도)")]
        [SerializeField] List<Button> tierButtons = new();

        string nation = "aurelia";
        int startTier = 2;
        /// <summary>고른 루두스 문장 (나라를 바꾸면 그 나라 문장 중 하나로 다시 뽑는다)</summary>
        string crest;
        Image crestPreview;
        GameObject pickerRoot;
        RectTransform pickerGrid;
        TMP_Text pickerTitle;

        /// <summary>시작 리그 설명 (난이도)</summary>
        static string TierNote(int t) => t switch
        {
            1 => "1부 · 쉬움 — 돈과 명성을 안고 시작하지만 강등을 피해야 합니다",
            2 => "2부 · 보통 — 3위 안에 들면 1부로 올라갑니다",
            3 => "3부 · 어려움 — 적은 돈과 약한 막사. 1부까지 두 번 올라가야 합니다",
            _ => "4부 · 밑바닥 — 가장 낮은 곳. 1부까지 세 번 올라가야 합니다",
        };

        void Awake()
        {
            foreach (var c in cards)
            {
                var id = c.id;
                c.button.onClick.AddListener(() => { if (nation != id) crest = null; nation = id; Refresh(); });
            }
            nameInput.text = "모래의 집";
            nameInput.characterLimit = 16;
            nameInput.onValueChanged.AddListener(_ => Refresh());
            startButton.onClick.AddListener(() => GameController.I.NewGame(nation, nameInput.text.Trim(), startTier, crest));
            BuildCrestRow();
            for (int i = 0; i < tierButtons.Count; i++)
            {
                int t = i + 1;
                tierButtons[i].onClick.AddListener(() => { startTier = t; Refresh(); });
            }
            // 로비에서 왔으면(진행 중인 게임이 없으면) 로비로
            cancelButton.onClick.AddListener(() =>
            {
                if (GameController.I.State == null) GameController.I.ToLobby();
                else GameController.I.Go(GameController.I.Screen);
            });
        }

        void OnEnable()
        {
            Data.Ensure();
            Refresh();
        }

        List<string> CrestPool() => Data.Crests.TryGetValue(nation, out var l) ? l : new List<string>();

        // ── 문장 고르기 (씬 다시 조립 없이 코드로 붙인다): 이름 칸 아래 줄 [문장] [문장 고르기] [무작위], 누르면 그 나라 문장 격자 ──

        TMP_Text MakeText(Transform parent, string text, float size, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = startLabel.font;
            t.fontSize = size;
            t.color = color;
            t.text = text;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return t;
        }

        Button MakeButton(Transform parent, string label, float w, float h, System.Action onClick)
        {
            var go = new GameObject(label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = Theme.Panel2;
            var b = go.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => onClick());
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = w;
            le.preferredHeight = le.minHeight = h;
            var t = MakeText(go.transform, label, 15, Theme.Parchment);
            Stretch((RectTransform)t.transform);
            return b;
        }

        static void Stretch(RectTransform r, float inset = 0)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset);
            r.offsetMax = new Vector2(-inset, -inset);
        }

        void BuildCrestRow()
        {
            if (crestPreview != null) return;
            var row = new GameObject("CrestRow", typeof(RectTransform));
            var parent = nameInput.transform.parent;
            row.transform.SetParent(parent, false);
            row.transform.SetSiblingIndex(nameInput.transform.GetSiblingIndex() + 1);
            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            h.childControlWidth = h.childControlHeight = true;
            var rle = row.AddComponent<LayoutElement>();
            rle.minHeight = rle.preferredHeight = 64;
            var label = MakeText(row.transform, "루두스 문장", 15, Theme.TextDim);
            var lle = label.gameObject.AddComponent<LayoutElement>();
            lle.preferredWidth = 96;
            var holder = new GameObject("Crest", typeof(RectTransform));
            holder.transform.SetParent(row.transform, false);
            var hle = holder.AddComponent<LayoutElement>();
            hle.minWidth = hle.preferredWidth = hle.minHeight = hle.preferredHeight = 60;
            var pic = new GameObject("Image", typeof(RectTransform));
            pic.transform.SetParent(holder.transform, false);
            Stretch((RectTransform)pic.transform);
            crestPreview = pic.AddComponent<Image>();
            crestPreview.preserveAspect = true;
            crestPreview.raycastTarget = false;
            MakeButton(row.transform, "문장 고르기", 120, 34, OpenPicker);
            MakeButton(row.transform, "무작위", 80, 34, () => { var p = CrestPool(); if (p.Count > 0) crest = p[Random.Range(0, p.Count)]; Refresh(); });
            BuildPicker();
        }

        void BuildPicker()
        {
            // 화면 전체를 덮는 어두운 바탕 + 가운데 판 (제목, 스크롤 격자, 닫기)
            pickerRoot = new GameObject("CrestPicker", typeof(RectTransform));
            pickerRoot.transform.SetParent(transform, false);
            Stretch((RectTransform)pickerRoot.transform);
            pickerRoot.AddComponent<Image>().color = new Color(0, 0, 0, 0.72f);
            pickerRoot.AddComponent<Button>().onClick.AddListener(() => pickerRoot.SetActive(false));
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(pickerRoot.transform, false);
            var pr = (RectTransform)panel.transform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(900, 640);
            panel.AddComponent<Image>().color = Theme.Panel;
            panel.AddComponent<Button>().transition = Selectable.Transition.None; // 판을 눌러도 닫히지 않게
            pickerTitle = MakeText(panel.transform, "", 20, Theme.BronzeHi);
            var tr = (RectTransform)pickerTitle.transform;
            tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(1, 1);
            tr.offsetMin = new Vector2(16, -48); tr.offsetMax = new Vector2(-16, -10);
            // 스크롤 영역
            var view = new GameObject("View", typeof(RectTransform));
            view.transform.SetParent(panel.transform, false);
            var vr = (RectTransform)view.transform;
            vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
            vr.offsetMin = new Vector2(16, 60); vr.offsetMax = new Vector2(-16, -56);
            view.AddComponent<Image>().color = Theme.Bg2;
            view.AddComponent<RectMask2D>();
            var content = new GameObject("Grid", typeof(RectTransform));
            content.transform.SetParent(view.transform, false);
            pickerGrid = (RectTransform)content.transform;
            pickerGrid.anchorMin = new Vector2(0, 1); pickerGrid.anchorMax = new Vector2(1, 1);
            pickerGrid.pivot = new Vector2(0.5f, 1);
            pickerGrid.offsetMin = pickerGrid.offsetMax = Vector2.zero;
            var grid = content.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(76, 76);
            grid.spacing = new Vector2(6, 6);
            grid.padding = new RectOffset(8, 8, 8, 8);
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.AddComponent<ScrollRect>();
            scroll.content = pickerGrid;
            scroll.horizontal = false;
            scroll.viewport = vr;
            scroll.scrollSensitivity = 30;
            // 닫기
            var close = MakeButton(panel.transform, "닫기", 120, 36, () => pickerRoot.SetActive(false));
            var cr = (RectTransform)close.transform;
            cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0);
            cr.sizeDelta = new Vector2(120, 36);
            cr.anchoredPosition = new Vector2(0, 30);
            pickerRoot.SetActive(false);
        }

        void OpenPicker()
        {
            pickerRoot.SetActive(true);
            pickerRoot.transform.SetAsLastSibling();
            FillPicker();
        }

        void FillPicker()
        {
            var pool = CrestPool();
            pickerTitle.text = $"◆  {Data.Nation(nation).name} 루두스 문장 ({pool.Count}개) — 하나를 고르세요";
            // 칸을 모자라는 만큼 만들고 남는 칸은 숨긴다
            while (pickerGrid.childCount < pool.Count)
            {
                var cell = new GameObject("Cell", typeof(RectTransform));
                cell.transform.SetParent(pickerGrid, false);
                var bg = cell.AddComponent<Image>();
                var b = cell.AddComponent<Button>();
                b.targetGraphic = bg;
                var icon = new GameObject("Icon", typeof(RectTransform));
                icon.transform.SetParent(cell.transform, false);
                Stretch((RectTransform)icon.transform, 5);
                var im = icon.AddComponent<Image>();
                im.preserveAspect = true;
                im.raycastTarget = false;
            }
            for (int i = 0; i < pickerGrid.childCount; i++)
            {
                var cell = pickerGrid.GetChild(i);
                bool on = i < pool.Count;
                cell.gameObject.SetActive(on);
                if (!on) continue;
                string id = pool[i];
                cell.Find("Icon").GetComponent<Image>().sprite = Widgets.CrestSprite("team/" + id);
                cell.GetComponent<Image>().color = id == crest ? Theme.Torch : Theme.Panel2;
                var b = cell.GetComponent<Button>();
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => { crest = id; pickerRoot.SetActive(false); Refresh(); });
            }
        }

        void Refresh()
        {
            foreach (var c in cards)
            {
                var d = Data.Nation(c.id);
                c.crest.sprite = Resources.Load<Sprite>($"Art/crest/{c.id}");
                c.name.text = d.name;
                c.motif.text = $"{d.league} · 수도 {d.capital}";
                c.frame.color = c.id == nation ? Theme.Torch : Theme.Line;
            }
            var pool = CrestPool();
            if (crest == null || !pool.Contains(crest)) crest = pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
            if (crestPreview != null)
            {
                crestPreview.sprite = crest != null ? Widgets.CrestSprite("team/" + crest) : Widgets.CrestSprite(nation);
                crestPreview.transform.parent.parent.gameObject.SetActive(pool.Count > 0);
            }
            var n = Data.Nation(nation);
            detailTitle.text = $"{n.name} — {n.league}";
            detailMeta.text = $"수도 {n.capital} · 대표 투기장 {n.arena}\n시작 리그: {TierNote(startTier)} (금고 {World.StartTreasury(startTier):N0} D + 시즌 배당)";
            detailLore.text = n.lore;
            detailRules.text = string.Join("\n", n.ruleText.Select(t => "· " + t));
            detailCulture.text = n.culture;
            startButton.interactable = nameInput.text.Trim().Length > 0;
            startLabel.text = $"{n.@short} {startTier}부에 루두스 세우기";
            for (int i = 0; i < tierButtons.Count; i++)
            {
                bool on = startTier == i + 1;
                tierButtons[i].transform.Find("Fill").GetComponent<Image>().color = on ? Theme.ContinueBg : Theme.Panel2;
                tierButtons[i].GetComponent<Image>().color = on ? Theme.Torch : Theme.Line;
            }
            if (pickerRoot != null && pickerRoot.activeSelf) FillPicker();
            // 진행 중인 게임이 있을 때만 돌아갈 수 있다
            cancelButton.gameObject.SetActive(GameController.I != null && GameController.I.State != null);
        }
    }
}
