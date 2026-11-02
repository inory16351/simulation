// 서열표 (웹판 LeagueScreen.tsx): 리그별 RP 순위. 다른 나라는 1부만 본다.
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class LeagueScreen : ScreenBase
    {
        static readonly Dictionary<string, string> PersonalityNames = new()
        {
            ["aggressive"] = "공격형", ["stable"] = "안정형", ["grudge"] = "원한형", ["merchant"] = "상인형", ["developer"] = "육성형",
        };

        [SerializeField] Button tier1;
        [SerializeField] Button tier2;
        [SerializeField] TMP_Dropdown nation;
        [Header("줄 원본 (하이어라키에 비활성)")]
        [SerializeField] RectTransform rowTemplate;

        TemplateList<RectTransform> rows;
        int tier;
        string nationId;

        void Awake()
        {
            rows = new TemplateList<RectTransform>(rowTemplate);
            tier1.onClick.AddListener(() => { tier = 1; Refresh(); });
            tier2.onClick.AddListener(() => { tier = 2; Refresh(); });
            nation.onValueChanged.AddListener(i => { nationId = Data.Nations[i].id; Refresh(); });
        }

        public override void Refresh()
        {
            var player = S.ludi[S.playerLudusId];
            if (tier == 0) tier = player.tier;
            nationId ??= S.nation;
            bool foreign = nationId != S.nation;

            var labels = Data.Nations.Select(n => n.name + (n.id == S.nation ? " (우리 나라)" : "")).ToList();
            if (!nation.options.Select(o => o.text).SequenceEqual(labels)) { nation.ClearOptions(); nation.AddOptions(labels); }
            nation.SetValueWithoutNotify(Data.Nations.FindIndex(n => n.id == nationId));
            Seg(tier1, tier == 1 || foreign);
            Seg(tier2, tier == 2 && !foreign);
            tier2.interactable = !foreign;

            int showTier = foreign ? 1 : tier;
            var ludi = S.ludi.Values.Where(l => l.tier == showTier && l.nation == nationId).OrderByDescending(l => l.rp).ToList();
            rows.Sync(ludi.Count, (row, i) =>
            {
                var l = ludi[i];
                var alive = l.gladiators.Select(id => S.gladiators[id]).Where(g => g.alive).ToList();
                double avg = alive.Count > 0 ? alive.Sum(g => GladiatorRules.ComputeCA(g)) / (double)alive.Count : 0;
                // 승강 구역: 2부 1·2위는 올라가고 1부 9·10위는 떨어진다
                var zone = foreign ? Color.clear : tier == 2 ? (i < 2 ? Theme.Good : Color.clear) : (i >= 8 ? Theme.Blood : Color.clear);
                row.Find("Zone").GetComponent<Image>().color = zone;
                row.GetComponent<Image>().color = l.isPlayer ? new Color(0.89f, 0.6f, 0.23f, 0.08f) : Color.clear;
                T(row, "Rank").text = (i + 1).ToString();
                var name = T(row, "Name");
                name.text = l.name;
                name.fontStyle = l.isPlayer ? FontStyles.Bold : FontStyles.Normal;
                T(row, "Personality").text = l.isPlayer ? "—" : PersonalityNames.TryGetValue(l.aiPersonality ?? "", out var p) ? p : l.aiPersonality;
                T(row, "Count").text = alive.Count.ToString();
                T(row, "Avg").text = Widgets.Stars(avg);
                T(row, "Record").text = $"{l.record.wins}-{l.record.losses}";
                T(row, "Rp").text = l.rp.ToString();
            });
        }

        static TMP_Text T(Transform row, string path) => row.Find(path).GetComponent<TMP_Text>();

        static void Seg(Button b, bool on)
        {
            b.transform.Find("Fill").GetComponent<Image>().color = on ? Theme.ContinueBg : Theme.Panel2;
            b.GetComponent<Image>().color = on ? Theme.Torch : Theme.Line;
        }
    }
}
