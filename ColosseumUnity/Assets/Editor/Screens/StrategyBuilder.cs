// 전략 화면 조립 (Unity판): 탭(개인 / 3:3 / 5:5). 개인 전술은 왼쪽 검투사 목록 + 교범 패널(GladiatorBuilder.BuildDoctrine 재사용),
// 단체 전술은 팀 방침 버튼 + 출전자 줄(역할 버튼 넷, 호위면 지킬 동료).
using Colosseum.Core;
using Colosseum.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Colosseum.EditorTools.UiKit;

namespace Colosseum.EditorTools
{
    public static class StrategyBuilder
    {
        public static ScreenBase Build(Transform main)
        {
            var rt = Rect("Screen_strategy", main).Stretch(26, 20, 26, 20);
            var content = ScrollView("Scroll", rt, out var scroll, 14);
            scroll.Stretch();
            ScreenHeader(content, "전략", "검투사마다 싸우는 법(개인 전술)과 3:3·5:5 난전에서 편이 움직이는 법(단체 전술)을 정합니다. 기술은 조건이 맞으면 저절로 나갑니다.");

            var tabRow = Rect("Tabs", content);
            HRow(tabRow, 4);
            var tabs = new System.Collections.Generic.List<Button>();
            foreach (var label in new[] { "개인 전술", "3:3 단체 전술", "5:5 단체 전술" })
                tabs.Add(Button("Tab_" + tabs.Count, tabRow, label, false, 15).Layout(prefW: 170, prefH: 36));

            // ── 개인 전술: 왼쪽 목록 | 교범 ──
            var personal = Rect("Personal", content);
            HRow(personal, 14, 0, 0, 0, 0, TextAnchor.UpperLeft);
            var listPad = TitledPanel("List", personal, "검투사", 4);
            listPad.parent.Layout(minW: 240, prefW: 240, flexW: 0);
            var glad = Button("GladTemplate", listPad, "", false, 14).Layout(minH: 46, prefH: 46);
            glad.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
            glad.gameObject.SetActive(false);
            var right = Rect("Right", personal).Layout(minW: 0, prefW: 0, flexW: 1);
            VCol(right, 0);
            var doctrine = GladiatorBuilder.BuildDoctrine(right);

            // ── 단체 전술 ──
            var teamPad = TitledPanel("Team", content, "단체 전술", 10);
            var team = teamPad.parent;
            var teamTitle = team.Find("Head/Text").GetComponent<TMP_Text>();
            Text("PolicyLabel", teamPad, "팀 방침", 13, Theme.BronzeHi, false, FontStyles.Bold);
            var policy = Rect("Policy", teamPad);
            var pg = policy.gameObject.AddComponent<GridLayoutGroup>();
            pg.cellSize = new Vector2(150, 32);
            pg.spacing = new Vector2(6, 6);
            foreach (var k in Tactics.Policies) Button(k, policy, Tactics.PolicyNames[k], false, 14);
            var policyDesc = Text("PolicyDesc", teamPad, "", 13, Theme.TextDim);
            Text("Note", teamPad, "출전자는 경기 날 컨디션으로 자동으로 고릅니다. 역할을 정하지 않은 사람은 능력·스타일에 맞춰 자동으로 맡습니다.", 12, Theme.TextFaint);

            Text("MemberLabel", teamPad, "출전자 역할", 13, Theme.BronzeHi, false, FontStyles.Bold);
            var member = Rect("MemberTemplate", teamPad).Layout(minH: 52, prefH: 52);
            HRow(member, 10, 0, 0, 4, 4);
            Text("Name", member, "", 15, Theme.Parchment).Layout(minW: 200, prefW: 200, flexW: 0);
            var roles = Rect("Roles", member).Layout(minW: 4 * 96 + 3 * 4, prefW: 4 * 96 + 3 * 4, flexW: 0);
            var rg = roles.gameObject.AddComponent<GridLayoutGroup>();
            rg.cellSize = new Vector2(96, 30);
            rg.spacing = new Vector2(4, 4);
            foreach (var r in Tactics.Roles) Button(r, roles, Tactics.RoleNames[r], false, 13);
            Button("Guard", member, "지킬 동료", false, 13).Layout(minW: 220, prefW: 220, prefH: 30, flexW: 0);
            Text("Desc", member, "", 13, Theme.TextDim).Layout(minW: 0, prefW: 0, flexW: 1);
            member.gameObject.SetActive(false);
            var empty = Text("Empty", teamPad, "", 14, Theme.TextDim);
            var resetRow = Rect("ResetRow", teamPad);
            HRow(resetRow, 0);
            var reset = Button("Reset", resetRow, "자동으로 되돌리기", false, 14).Layout(prefW: 180, prefH: 32);

            var s = rt.gameObject.AddComponent<StrategyScreen>();
            s.screenId = "strategy";
            var so = new UnityEditor.SerializedObject(s);
            void S(string p, Object v) => so.FindProperty(p).objectReferenceValue = v;
            var tp = so.FindProperty("tabButtons");
            tp.arraySize = tabs.Count;
            for (int i = 0; i < tabs.Count; i++) tp.GetArrayElementAtIndex(i).objectReferenceValue = tabs[i];
            S("personalRoot", personal.gameObject); S("teamRoot", team.gameObject);
            S("gladTemplate", glad); S("doctrine", doctrine);
            S("teamTitle", teamTitle); S("policyGrid", policy); S("policyDesc", policyDesc);
            S("memberTemplate", member); S("teamEmpty", empty.gameObject.GetComponent<TMP_Text>()); S("resetTeam", reset);
            so.ApplyModifiedPropertiesWithoutUndo();
            rt.gameObject.SetActive(false);
            return s;
        }
    }
}
