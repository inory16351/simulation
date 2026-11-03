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

        /// <summary>시작 리그 설명 (난이도)</summary>
        static string TierNote(int t) => t switch
        {
            1 => "1부 · 쉬움 — 돈과 명성을 안고 시작하지만 강등을 피해야 합니다",
            2 => "2부 · 보통 — 웹판과 같은 출발. 2위 안에 들면 1부로 올라갑니다",
            3 => "3부 · 어려움 — 적은 돈과 약한 막사. 1부까지 두 번 올라가야 합니다",
            _ => "4부 · 밑바닥 — 가장 낮은 곳. 1부까지 세 번 올라가야 합니다",
        };

        void Awake()
        {
            foreach (var c in cards)
            {
                var id = c.id;
                c.button.onClick.AddListener(() => { nation = id; Refresh(); });
            }
            nameInput.text = "모래의 집";
            nameInput.characterLimit = 16;
            nameInput.onValueChanged.AddListener(_ => Refresh());
            startButton.onClick.AddListener(() => GameController.I.NewGame(nation, nameInput.text.Trim(), startTier));
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
            // 진행 중인 게임이 있을 때만 돌아갈 수 있다
            cancelButton.gameObject.SetActive(GameController.I != null && GameController.I.State != null);
        }
    }
}
