// 막사 (웹판 RosterScreen.tsx): 소속 검투사 표, 의무실(부상자), 모래에 묻힌 자들.
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class RosterScreen : ScreenBase
    {
        [SerializeField] TMP_Text sub;
        [SerializeField] GladiatorTable table;
        [Header("의무실 (줄 원본은 하이어라키에 비활성)")]
        [SerializeField] GameObject infirmary;
        [SerializeField] Button injuredTemplate;
        [Header("모래에 묻힌 자들")]
        [SerializeField] GameObject fallenPanel;
        [SerializeField] GladiatorTable fallenTable;

        TemplateList<Button> injured;

        void Awake() => injured = new TemplateList<Button>(injuredTemplate);

        public override void Refresh()
        {
            var player = S.ludi[S.playerLudusId];
            var list = player.gladiators.Select(id => S.gladiators[id]).ToList();
            sub.text = $"검투사 {list.Count}명 · 주급 합계 {list.Sum(g => g.wage):N0} D";
            table.Show(list, S);

            var hurt = list.Where(g => g.injury != null).OrderByDescending(g => g.injury.daysLeft).ToList();
            infirmary.SetActive(hurt.Count > 0);
            injured.Sync(hurt.Count, (row, i) =>
            {
                var g = hurt[i];
                var t = row.transform;
                row.GetComponentInChildren<PortraitView>(true).Show(g);
                t.Find("Name").GetComponent<TMP_Text>().text = Tips.Name(g);
                t.Find("Injury").GetComponent<TMP_Text>().text = GladiatorTable.InjuryLabel(g);
                double total = g.injury.total ?? g.injury.daysLeft;
                Widgets.SetBar((RectTransform)t.Find("Bar"), total - g.injury.daysLeft, total, Theme.Good);
                t.Find("Hint").GetComponent<TMP_Text>().text =
                    (g.injury.severity == "serious" ? "출전 불가" : "무리하면 출전 가능") + " · " +
                    (g.training.focus == "rest" ? "휴식 중 (회복 1.5배)" : "연무장에서 휴식으로 바꾸면 빨리 낫습니다");
                row.onClick.RemoveAllListeners();
                string id = g.id;
                row.onClick.AddListener(() => G.ShowGladiator(id));
            });

            var fallen = S.fallen.Select(id => S.gladiators[id]).ToList();
            fallenPanel.SetActive(fallen.Count > 0);
            if (fallen.Count > 0) fallenTable.Show(fallen, S);
        }
    }
}
