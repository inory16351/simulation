// 유소년 학당 (웹판 YouthScreen.tsx): 학생마다 훈련 내용·강도를 정하고, 데뷔 나이가 되면 1군으로 올리거나 내보낸다.
using System;
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class YouthScreen : ScreenBase
    {
        // 웹판 FOCI·강도: 학당에선 이것만 고른다
        static readonly string[] Foci = { "physical", "weapon", "defense", "grapple", "mental", "show", "rest" };
        static readonly string[] Intensities = { "low", "normal", "high" };
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);

        [SerializeField] TMP_Text sub;
        [SerializeField] TMP_Text msg;
        [Header("학당이 없을 때")]
        [SerializeField] GameObject noneRoot;
        [SerializeField] Button goFacilities;
        [Header("학생 표 (줄 원본은 하이어라키에 비활성)")]
        [SerializeField] GameObject tableRoot;
        [SerializeField] GameObject empty;
        [SerializeField] RectTransform rowTemplate;

        TemplateList<RectTransform> rows;
        // 화면 상태: 오류 글, 내보내기 확인 중인 아이 (웹판 confirm)
        string msgText, confirmRelease;

        void Awake()
        {
            rows = new TemplateList<RectTransform>(rowTemplate);
            goFacilities.onClick.AddListener(() => G.Go("facilities"));
        }

        Ludus Player => S.ludi[S.playerLudusId];

        public override void Refresh()
        {
            var player = Player;
            int lvl = player.facilities.academy;
            var kids = (player.youth ?? new List<string>()).Select(id => S.gladiators.TryGetValue(id, out var g) ? g : null).Where(g => g != null).ToList();
            bool full = player.gladiators.Count >= Economy.RosterCapacity(player);

            noneRoot.SetActive(lvl == 0);
            tableRoot.SetActive(lvl != 0);
            msg.gameObject.SetActive(lvl != 0 && msgText != null);
            msg.text = msgText ?? "";
            msg.color = Neg;
            if (lvl == 0)
            {
                sub.text = "";
                return;
            }
            sub.text = $"학당 {lvl}단계 · 학생 {kids.Count}/{Youth.YouthCapacity(player)}명. 학생들은 {Youth.ApprenticeInterval}일마다 목검으로 안전하게 실전 훈련을 하고, 데뷔 나이가 되면 1군으로 올라갑니다." +
                (full ? $"  <color={Theme.Hex(Neg)}>막사가 꽉 찼습니다</color>" : "");

            empty.SetActive(kids.Count == 0);
            if (confirmRelease != null && !kids.Any(g => g.id == confirmRelease)) confirmRelease = null;
            rows.Sync(kids.Count, (row, i) => Bind(row, kids[i], full));
        }

        void Bind(RectTransform row, Gladiator g, bool full)
        {
            string id = g.id;
            int ca = GladiatorRules.ComputeCA(g);
            bool ready = GladiatorRules.AgeStage(g) != "youth";
            double spread = StaffLogic.PaSpread(S, g);

            row.GetComponentInChildren<PortraitView>(true).Show(g);
            var nameBtn = row.Find("Name").GetComponent<Button>();
            nameBtn.onClick.RemoveAllListeners();
            nameBtn.onClick.AddListener(() => G.OpenGladiator(id));
            T(row, "Name/Text").text = g.name;
            T(row, "Race").text = Data.Race(g.race).name;
            string when = ready ? "데뷔 가능" : $"데뷔까지 {Data.Race(g.race).age.debut - g.age}년";
            T(row, "Age").text = $"{g.age} <size=80%><color={Theme.Hex(ready ? Theme.Good : Theme.TextFaint)}>{when}</color></size>";
            T(row, "Style").text = Data.Style(g.style)?.name ?? g.style;
            T(row, "Ca").text = Widgets.Stars(ca);
            T(row, "Pa").text = Widgets.RangeStars(Math.Max(ca, g.hidden.pa - spread), Math.Min(200, g.hidden.pa + spread));

            // 훈련 내용: 학당 목록에 없는 값이면 끝에 붙여 보여 준다 (웹판 select는 빈 칸)
            var foci = Foci.Contains(g.training.focus) ? Foci.ToList() : Foci.Append(g.training.focus).ToList();
            var focus = row.Find("Focus").GetComponent<TMP_Dropdown>();
            Fill(focus, foci.Select(f => Training.FocusNames.TryGetValue(f, out var n) ? n : f), foci.IndexOf(g.training.focus));
            focus.onValueChanged.RemoveAllListeners();
            focus.onValueChanged.AddListener(v => G.Mutate(d => d.gladiators[id].training.focus = foci[v]));

            var ints = Intensities.Contains(g.training.intensity) ? Intensities.ToList() : Intensities.Append(g.training.intensity).ToList();
            var inten = row.Find("Intensity").GetComponent<TMP_Dropdown>();
            Fill(inten, ints.Select(x => Training.IntensityNames.TryGetValue(x, out var n) ? n : x), ints.IndexOf(g.training.intensity));
            inten.onValueChanged.RemoveAllListeners();
            inten.onValueChanged.AddListener(v => G.Mutate(d => d.gladiators[id].training.intensity = ints[v]));

            var promote = row.Find("Act/Promote").GetComponent<Button>();
            promote.interactable = ready && !full;
            promote.onClick.RemoveAllListeners();
            promote.onClick.AddListener(() =>
            {
                msgText = G.Try(d => Youth.PromoteYouth(d, d.ludi[d.playerLudusId], id));
                Refresh();
            });

            // 웹판 confirm(): 한 번 더 누르면 내보낸다
            bool asking = confirmRelease == id;
            var release = row.Find("Act/Release").GetComponent<Button>();
            release.GetComponentInChildren<TMP_Text>().text = asking ? "정말 내보내겠습니까?" : "내보내기";
            release.onClick.RemoveAllListeners();
            release.onClick.AddListener(() =>
            {
                if (confirmRelease != id) { confirmRelease = id; Refresh(); return; }
                confirmRelease = null;
                msgText = null;
                G.Mutate(d => Youth.ReleaseYouth(d, d.ludi[d.playerLudusId], id));
            });
        }

        static TMP_Text T(Transform row, string path) => row.Find(path).GetComponent<TMP_Text>();

        static void Fill(TMP_Dropdown d, IEnumerable<string> labels, int value)
        {
            var list = labels.ToList();
            bool same = d.options.Count == list.Count && d.options.Select(o => o.text).SequenceEqual(list);
            if (!same) { d.ClearOptions(); d.AddOptions(list); }
            d.SetValueWithoutNotify(Mathf.Clamp(value, 0, Mathf.Max(0, list.Count - 1)));
        }
    }
}
