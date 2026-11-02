// 경력 패널 (웹판 components/CareerPanel.tsx): 시즌별 기록 표와 일대기 (FM의 경력 화면에 해당).
// 일대기는 웹판처럼 안쪽 스크롤을 두지 않고 최근 것만 보이다가 '전부 보기'로 펼친다 (바깥 스크롤과 겹치지 않게).
using System;
using System.Collections.Generic;
using System.Linq;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class CareerPanel : MonoBehaviour
    {
        const int LogPreview = 12;
        static readonly Color Warn = new Color32(0xe0, 0x8b, 0x80, 0xff);
        static readonly Color Death = new Color32(0xff, 0x8a, 0x7a, 0xff);
        // 폰트에 ⚔ ✚ ✝가 없어 비슷한 글자로
        static readonly Dictionary<string, string> KindIcon = new()
        {
            ["join"] = "◇", ["match"] = "×", ["milestone"] = "★", ["injury"] = "+", ["death"] = "†", ["other"] = "·",
        };

        [Tooltip("시즌 줄 원본: Season, Ludus, Apps, Wins, Losses, Kills, Injuries, Ca, Pop, Earnings")]
        [SerializeField] RectTransform seasonTemplate;
        [Tooltip("통산 줄 (칸 이름은 시즌 줄과 같다)")]
        [SerializeField] RectTransform totalRow;
        [SerializeField] TMP_Text log;
        [SerializeField] GameObject logEmpty;
        [SerializeField] Button logMore;

        TemplateList<RectTransform> seasonRows;
        string gid;
        bool showAll;

        void Awake()
        {
            seasonRows = new TemplateList<RectTransform>(seasonTemplate);
            logMore.onClick.AddListener(() =>
            {
                showAll = !showAll;
                if (gid != null && GameController.I.State.gladiators.TryGetValue(gid, out var g)) Show(g);
            });
        }

        public void Show(Gladiator g)
        {
            if (gid != g.id) { gid = g.id; showAll = false; }
            string faint = Theme.Hex(Theme.TextFaint);

            var history = g.history.OrderBy(h => h.season).ToList();
            int last = history.Count > 0 ? history[history.Count - 1].season : -1;
            seasonRows.Sync(history.Count, (row, i) =>
            {
                var h = history[i];
                double caEnd = h.season == last && g.alive ? GladiatorRules.ComputeCA(g) : h.caEnd;
                double delta = caEnd - h.caStart;
                var dc = delta > 0 ? Theme.Good : delta < 0 ? Warn : Theme.TextFaint;
                string ds = delta > 0 ? $"+{N(delta)}" : delta < 0 ? N(delta) : "±0";
                T(row, "Season").text = h.season.ToString();
                T(row, "Ludus").text = $"{h.ludus} <color={faint}><size=80%>{(h.tier != 0 ? $"{N(h.tier)}부" : "")}</size></color>";
                T(row, "Apps").text = N(h.apps);
                T(row, "Wins").text = N(h.wins);
                T(row, "Losses").text = N(h.losses);
                T(row, "Kills").text = N(h.kills);
                T(row, "Injuries").text = N(h.injuries);
                T(row, "Ca").text = $"{N(caEnd)} <size=75%><color={Theme.Hex(dc)}>{ds}</color></size>";
                T(row, "Pop").text = N(h.popularity);
                T(row, "Earnings").text = h.earnings.ToString("N0");
            });
            T(totalRow, "Apps").text = N(history.Sum(h => h.apps));
            T(totalRow, "Wins").text = N(history.Sum(h => h.wins));
            T(totalRow, "Losses").text = N(history.Sum(h => h.losses));
            T(totalRow, "Kills").text = N(history.Sum(h => h.kills));
            T(totalRow, "Earnings").text = history.Sum(h => h.earnings).ToString("N0");

            // 일대기: 최근 것부터
            var events = Enumerable.Reverse(g.career).ToList();
            logEmpty.SetActive(events.Count == 0);
            log.gameObject.SetActive(events.Count > 0);
            var shown = showAll ? events : events.Take(LogPreview).ToList();
            log.text = string.Join("\n", shown.Select(e =>
            {
                var ic = e.kind == "milestone" ? Theme.Torch : e.kind == "injury" ? Warn : e.kind == "death" ? Death : Theme.BronzeHi;
                string icon = KindIcon.TryGetValue(e.kind ?? "", out var k) ? k : "·";
                string text = e.kind == "death" ? $"<color={Theme.Hex(Death)}>{e.text}</color>" : e.text;
                return $"<color={Theme.Hex(ic)}>{icon}</color>  <color={faint}><size=80%>시즌 {e.season} · {Calendar.DateLabel(e.day)}</size></color>   {text}";
            }));
            logMore.gameObject.SetActive(events.Count > LogPreview);
            logMore.GetComponentInChildren<TMP_Text>().text = showAll ? "접기" : $"전부 보기 ({events.Count})";
        }

        static string N(double v) => Math.Round(v).ToString();
        static TMP_Text T(Transform row, string path) => row.Find(path).GetComponent<TMP_Text>();
    }
}
