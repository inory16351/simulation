// 검투사 표 (웹판 RosterScreen.tsx GladiatorTable). 막사·시장·학당 등에서 같이 쓴다.
// 머리줄 정렬 버튼과 줄 원본(비활성)은 하이어라키에 있고, 줄은 원본을 복제한다.
using System;
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class GladiatorTable : MonoBehaviour
    {
        /// <summary>정렬 기준: 버튼 이름이 Sort_{key}</summary>
        public static readonly string[] SortKeys = { "name", "age", "ca", "morale", "wage" };

        [Tooltip("잠재 칸이 있는지 (빌더가 칸을 만들 때 씁니다)")]
        [SerializeField] bool showPa = true;
        public bool ShowPa => showPa;
        [SerializeField] List<Button> sortButtons = new();
        [SerializeField] Button rowTemplate;

        TemplateList<Button> rows;
        string sort = "ca";
        List<Gladiator> list = new();
        GameState state;

        static readonly Color InjText = new Color32(0xf0, 0xb3, 0xaa, 0xff);

        void Awake()
        {
            rows = new TemplateList<Button>(rowTemplate);
            foreach (var b in sortButtons)
            {
                string key = b.name.Substring("Sort_".Length);
                b.onClick.AddListener(() => { sort = key; Draw(); });
            }
        }

        public void Show(IEnumerable<Gladiator> gladiators, GameState s)
        {
            if (rows == null) Awake();
            list = gladiators.ToList();
            state = s;
            Draw();
        }

        IEnumerable<Gladiator> Sorted() => sort switch
        {
            "age" => list.OrderBy(g => g.age),
            "wage" => list.OrderByDescending(g => g.wage),
            "morale" => list.OrderByDescending(g => g.morale),
            "name" => list.OrderBy(g => g.name, StringComparer.Ordinal),
            _ => list.OrderByDescending(g => Scouting.CaRange(GameController.I.State, g).mid),
        };

        void Draw()
        {
            foreach (var b in sortButtons)
                b.GetComponentInChildren<TMP_Text>().color = b.name == "Sort_" + sort ? Theme.Torch : Theme.TextDim;
            var sorted = Sorted().ToList();
            rows.Sync(sorted.Count, (row, i) => Bind(row, sorted[i]));
        }

        void Bind(Button row, Gladiator g)
        {
            var t = row.transform;
            int ca = GladiatorRules.ComputeCA(g);
            string stage = GladiatorRules.AgeStage(g);
            Color text = g.alive ? Theme.Text : Theme.TextFaint;

            row.GetComponentInChildren<PortraitView>(true).Show(g);
            Txt(t, "Name/Text/Name", g.name, g.alive ? Theme.Parchment : Theme.TextFaint);
            var nick = t.Find("Name/Text/Nick").GetComponent<TMP_Text>();
            nick.gameObject.SetActive(!string.IsNullOrEmpty(g.nickname));
            nick.text = $"\"{g.nickname}\"";
            Txt(t, "Race", Tips.Race(g.race), text);
            Txt(t, "Age", $"{g.age} <size=75%><color={Theme.Hex(Theme.TextFaint)}>{GladiatorRules.AgeStageNames[stage]}</color></size>", text);
            Txt(t, "Style", Tips.Style(g.style), text);
            Txt(t, "Ca", Widgets.CaStars(GameController.I.State, g), Theme.Torch);
            var pa = t.Find("Pa");
            if (pa != null)
            {
                double spread = StaffLogic.PaSpread(state, g);
                string lo = Widgets.Stars(Math.Max(ca, g.hidden.pa - spread)), hi = Widgets.Stars(Math.Min(200, g.hidden.pa + spread));
                pa.GetComponent<TMP_Text>().color = Theme.Torch;
                pa.GetComponent<TMP_Text>().text = hi.Length > lo.Length ? $"{lo}<color={Theme.Hex(Theme.TextFaint)}>~{hi}</color>" : lo;
            }
            Widgets.SetBar((RectTransform)t.Find("Morale/Bar"), g.morale);
            Widgets.SetBar((RectTransform)t.Find("Fatigue/Bar"), 100 - g.fatigue);
            Widgets.SetBar((RectTransform)t.Find("Sharp/Bar"), g.sharpness, 100, Theme.BronzeHi);
            Widgets.SetBar((RectTransform)t.Find("Wear/Bar"), g.wear, 100, g.wear >= 85 ? Theme.Blood : g.wear >= 50 ? Theme.Torch : Theme.Good);
            var status = t.Find("Status").GetComponent<TMP_Text>();
            if (!g.alive) { status.text = "사망"; status.color = new Color32(0xd9, 0xa2, 0x9b, 0xff); }
            else if (g.injury != null) { status.text = InjuryLabel(g); status.color = InjText; }
            else if (!Registration.IsRegistered(state, g)) { status.text = "미등록"; status.color = Theme.TextDim; }
            else { status.text = "출전 가능"; status.color = Theme.Good; }
            Txt(t, "Record", $"{g.record.wins}-{g.record.losses}", text);
            Txt(t, "Wage", $"{g.wage} D", text);

            row.onClick.RemoveAllListeners();
            string id = g.id;
            row.onClick.AddListener(() => GameController.I.ShowGladiator(id));
        }

        /// <summary>웹판 InjuryTag 글자</summary>
        public static string InjuryLabel(Gladiator g) => $"✚ {g.injury.desc} {Math.Ceiling(g.injury.daysLeft)}일";

        static void Txt(Transform t, string path, string s, Color c)
        {
            var x = t.Find(path).GetComponent<TMP_Text>();
            x.text = s;
            x.color = c;
        }
    }
}
