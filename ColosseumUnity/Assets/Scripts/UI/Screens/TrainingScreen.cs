// 연무장 (웹판 TrainingScreen.tsx): 검투사마다 훈련 내용·강도를 정하고 컨디션을 본다.
using System;
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class TrainingScreen : ScreenBase
    {
        static readonly Color Warn = new Color32(0xe0, 0x8b, 0x80, 0xff);
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);

        [Header("일괄 지시 (첫 항목은 '훈련 내용…' 같은 안내)")]
        [SerializeField] TMP_Dropdown allFocus;
        [SerializeField] TMP_Dropdown allIntensity;
        [Header("줄 원본 (하이어라키에 비활성)")]
        [SerializeField] RectTransform rowTemplate;

        TemplateList<RectTransform> rows;
        List<string> foci, intensities;

        void Awake()
        {
            rows = new TemplateList<RectTransform>(rowTemplate);
            foci = Training.FocusNames.Keys.ToList();
            intensities = Training.IntensityNames.Keys.ToList();
            Fill(allFocus, new[] { "훈련 내용…" }.Concat(foci.Select(f => Training.FocusNames[f])));
            Fill(allIntensity, new[] { "강도…" }.Concat(intensities.Select(i => Training.IntensityNames[i])));
            allFocus.onValueChanged.AddListener(i => { if (i > 0) SetAll(foci[i - 1], null); allFocus.SetValueWithoutNotify(0); });
            allIntensity.onValueChanged.AddListener(i => { if (i > 0) SetAll(null, intensities[i - 1]); allIntensity.SetValueWithoutNotify(0); });
        }

        static void Fill(TMP_Dropdown d, IEnumerable<string> labels)
        {
            d.ClearOptions();
            d.AddOptions(labels.ToList());
        }

        void SetAll(string focus, string intensity) => G.Mutate(d =>
        {
            foreach (var id in d.ludi[d.playerLudusId].gladiators)
            {
                var t = d.gladiators[id].training;
                if (focus != null) t.focus = focus;
                if (intensity != null) t.intensity = intensity;
            }
        });

        public override void Refresh()
        {
            var roster = S.ludi[S.playerLudusId].gladiators.Select(id => S.gladiators[id]).Where(g => g.alive).ToList();
            rows.Sync(roster.Count, (row, i) => Bind(row, roster[i]));
        }

        void Bind(RectTransform row, Gladiator g)
        {
            string id = g.id;
            row.GetComponentInChildren<PortraitView>(true).Show(g);
            var nameBtn = row.Find("Name").GetComponent<Button>();
            nameBtn.onClick.RemoveAllListeners();
            nameBtn.onClick.AddListener(() => G.OpenGladiator(id));
            row.Find("Name/Text").GetComponent<TMP_Text>().text = g.name;
            row.Find("Style").GetComponent<TMP_Text>().text = Data.Style(g.style)?.name ?? g.style;

            var focus = row.Find("Focus").GetComponent<TMP_Dropdown>();
            if (focus.options.Count != foci.Count) Fill(focus, foci.Select(f => Training.FocusNames[f]));
            focus.SetValueWithoutNotify(foci.IndexOf(g.training.focus));
            focus.onValueChanged.RemoveAllListeners();
            focus.onValueChanged.AddListener(v => G.Mutate(d => d.gladiators[id].training.focus = foci[v]));

            var inten = row.Find("Intensity").GetComponent<TMP_Dropdown>();
            if (inten.options.Count != intensities.Count) Fill(inten, intensities.Select(x => Training.IntensityNames[x]));
            inten.SetValueWithoutNotify(intensities.IndexOf(g.training.intensity));
            inten.interactable = g.training.focus != "rest";
            inten.onValueChanged.RemoveAllListeners();
            inten.onValueChanged.AddListener(v => G.Mutate(d => d.gladiators[id].training.intensity = intensities[v]));

            Widgets.SetBar((RectTransform)row.Find("Fitness/Bar"), 100 - g.fatigue);
            Widgets.SetBar((RectTransform)row.Find("Sharp/Bar"), g.sharpness, 100, Theme.BronzeHi);
            row.Find("Sharp/Value").GetComponent<TMP_Text>().text = Math.Round(g.sharpness).ToString();
            Widgets.SetBar((RectTransform)row.Find("Wear/Bar"), g.wear, 100, g.wear >= 85 ? Theme.Blood : g.wear >= 50 ? Theme.Torch : Theme.Good);
            row.Find("Wear/Value").GetComponent<TMP_Text>().text = Math.Round(g.wear).ToString();
            row.Find("Doctrine").GetComponent<TMP_Text>().text = Math.Round(g.doctrine.familiarity).ToString();

            double perf = GladiatorRules.Performance(g);
            var pt = row.Find("Perf").GetComponent<TMP_Text>();
            pt.text = $"{Math.Round(perf * 100)}%";
            pt.color = perf >= 1 ? Theme.Good : perf >= 0.85 ? Theme.Parchment : Warn;

            var st = row.Find("Status").GetComponent<TMP_Text>();
            if (g.injury != null) { st.text = GladiatorTable.InjuryLabel(g); st.color = new Color32(0xf0, 0xb3, 0xaa, 0xff); }
            else if (g.wear >= 85) { st.text = "탈진 — 휴식 필요"; st.color = Neg; }
            else if (g.unpaidWeeks >= 3) { st.text = "태업"; st.color = Neg; }
            else { st.text = "정상"; st.color = Theme.Good; }
        }
    }
}
