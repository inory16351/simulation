// 서열표 화면 조립 (웹판 LeagueScreen.tsx + styles.css .t tr.zone-up / zone-down)
using Colosseum.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Colosseum.EditorTools.UiKit;

namespace Colosseum.EditorTools
{
    public static class LeagueBuilder
    {
        // (이름, 머리글, 폭, 오른쪽 정렬)  폭 0 = 남는 폭
        static readonly (string id, string label, float w, bool right)[] Cols =
        {
            ("Rank", "순위", 50, true), ("Name", "루두스", 0, false), ("Personality", "성향", 110, false), ("Count", "검투사", 70, true),
            ("Avg", "평균 전력", 110, false), ("Record", "승-패", 80, true), ("Rp", "RP", 80, true),
        };

        public static ScreenBase Build(Transform main)
        {
            var rt = Rect("Screen_league", main).Stretch(26, 20, 26, 20);
            VCol(rt, 12);
            ScreenHeader(rt, "서열표", "시즌이 끝날 때 RP 순위로 오르내린다. 2부 1·2위는 1부로 올라가고, 1부 9·10위는 2부로 떨어진다. 다른 나라는 1부만 볼 수 있다.");

            var bar = Rect("Filter", rt);
            HRow(bar, 4);
            var t1 = Button("Tier1", bar, "1부 리그", false, 14).Layout(prefW: 110, prefH: 32);
            var t2 = Button("Tier2", bar, "2부 리그", false, 14).Layout(prefW: 110, prefH: 32);
            Rect("Gap", bar).Layout(prefW: 10, flexW: 0);
            var nation = Dropdown("Nation", bar, 220, 32);

            var table = Panel("Table", rt);
            VCol(table.transform, 0, 1, 1, 1, 1);
            table.transform.Find("Fill").gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var head = Box("Head", table.transform, Theme.Bg2).Layout(minH: 30, prefH: 30);
            HRow(head.transform, 10, 12, 12);
            foreach (var c in Cols) Cell(Text(c.id, head.transform, c.label, 13, Theme.TextDim, false, FontStyles.Bold, Align(c.right)).rectTransform, c.w);
            Box("Line", table.transform, Theme.Line).Layout(minH: 1, prefH: 1);

            var row = Box("RowTemplate", table.transform, Color.clear).Layout(minH: 34, prefH: 34);
            HRow(row.transform, 10, 12, 12);
            var zone = Box("Zone", row.transform, Color.clear);
            zone.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            zone.rectTransform.Anchor(Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(3, 0));
            var line = Box("Line", row.transform, new Color(Theme.Line.r, Theme.Line.g, Theme.Line.b, 0.6f));
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            line.rectTransform.Anchor(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 1));
            foreach (var c in Cols)
            {
                var color = c.id is "Name" or "Rp" ? Theme.Parchment : c.id == "Personality" ? Theme.TextDim : c.id == "Avg" ? Theme.Torch : Theme.Text;
                var t = Text(c.id, row.transform, "", c.id == "Avg" ? 13 : 15, color, false, c.id == "Rp" ? FontStyles.Bold : FontStyles.Normal, Align(c.right));
                t.textWrappingMode = TextWrappingModes.NoWrap;
                Cell(t.rectTransform, c.w);
            }
            row.gameObject.SetActive(false);

            var s = rt.gameObject.AddComponent<LeagueScreen>();
            s.screenId = "league";
            Set(s, "tier1", t1);
            Set(s, "tier2", t2);
            Set(s, "nation", nation);
            Set(s, "rowTemplate", row.rectTransform);
            rt.gameObject.SetActive(false);
            return s;
        }

        static TextAlignmentOptions Align(bool right) => right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;

        static void Cell(RectTransform rt, float w)
        {
            if (w > 0) rt.Layout(minW: w, prefW: w, flexW: 0);
            else rt.Layout(minW: 120, flexW: 1);
        }
    }
}
