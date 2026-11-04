// 도전장 화면 조립 (웹판 ChallengeScreen.tsx + styles.css .field, .seg). 화면 전체가 스크롤 영역이다.
using System.Collections.Generic;
using Colosseum.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Colosseum.EditorTools.UiKit;

namespace Colosseum.EditorTools
{
    public static class ChallengeBuilder
    {
        static readonly Color Neg = new Color32(0xd9, 0xa2, 0x9b, 0xff);

        public static ScreenBase Build(Transform main)
        {
            var rt = Rect("Screen_challenge", main).Stretch(26, 20, 26, 20);
            // 내용이 늘어도 넘치지 않게 화면 전체를 스크롤 영역으로
            var page = ScrollView("Scroll", rt, out var pageScroll, 14);
            pageScroll.Stretch();
            ScreenHeader(page, "도전장", "");
            var sub = page.Find("Sub").GetComponent<TMP_Text>();

            // 받은 도전장
            var incPad = TitledPanel("Incoming", page, "받은 도전장", 0);
            Head(incPad, ("상대", 200), ("상대 검투사", 230), ("규칙", 150), ("답할 기한", 100), ("우리 출전자", 0), ("", 170));
            var inc = Row("IncomingTemplate", incPad);
            Cell(Text("Opp", inc, "", 14).rectTransform, 200);
            var their = Rect("Their", inc);
            Cell(their, 230);
            HRow(their, 8);
            Portrait("Portrait", their, 26);
            Text("Name", their, "", 14, Theme.Parchment).Layout(flexW: 1);
            Cell(Text("Rule", inc, "", 13).rectTransform, 150);
            Cell(Text("RespondBy", inc, "", 14).rectTransform, 100);
            var def = Dropdown("Defender", inc, 0);
            Cell((RectTransform)def.transform, 0);
            Button("Accept", inc, "받기", true, 14).Layout(minW: 84, prefW: 84, prefH: 30, flexW: 0);
            Button("Decline", inc, "거절", false, 14).Layout(minW: 76, prefW: 76, prefH: 30, flexW: 0);
            inc.gameObject.SetActive(false);

            // 두 칸: 보내기 | 잡힌 경기 + 답 기다림
            var cols = Rect("Cols", page);
            HRow(cols, 14, 0, 0, 0, 0, TextAnchor.UpperLeft);

            var sendPad = TitledPanel("Send", cols, "도전장 보내기", 10);
            sendPad.parent.Layout(minW: 0, prefW: 0, flexW: 1);
            var target = Field(sendPad, "상대 루두스", out _);
            var targetDd = Dropdown("Target", target, 0);
            targetDd.Layout(minH: 30, prefH: 30);
            var block = Text("BlockHint", target, "", 13, Theme.TextDim);
            var mineF = Field(sendPad, "우리 출전자", out var myLabel);
            var mineDd = Dropdown("Mine", mineF, 0);
            mineDd.Layout(minH: 30, prefH: 30);
            var theirF = Field(sendPad, "맞붙을 상대 (안 골라도 됩니다)", out _);
            var theirDd = Dropdown("Theirs", theirF, 0);
            theirDd.Layout(minH: 30, prefH: 30);
            var fmtF = Field(sendPad, "형식", out _);
            var fmtButtons = Seg(fmtF, "Format", "1:1", "3:3 단체전", "5:5 단체전");
            var fmtHint = Text("Hint", fmtF, "", 13, Theme.TextDim);
            var ruleF = Field(sendPad, "생사 규칙", out _);
            var ruleButtons = Seg(ruleF, "Rule", "훈련전", "미시오", "사투");
            var ruleHint = Text("Hint", ruleF, "", 13, Theme.TextDim);
            var err = Text("Error", sendPad, "", 14, Neg);
            var sendRow = Rect("SendRow", sendPad);
            HRow(sendRow, 0);
            var send = Button("Send", sendRow, "도전장 보내기", true, 15).Layout(prefW: 200, prefH: 38);

            var right = Rect("Right", cols).Layout(minW: 0, prefW: 0, flexW: 1);
            VCol(right, 14);
            var schPad = TitledPanel("Scheduled", right, "잡힌 경기", 0);
            var schEmpty = Text("Empty", schPad, "아직 잡힌 경기가 없습니다.", 14, Theme.TextDim);
            var sch = Row("ScheduledTemplate", schPad);
            Cell(Text("Date", sch, "", 14).rectTransform, 90);
            Cell(Text("Rule", sch, "", 13).rectTransform, 130);
            Cell(Text("Desc", sch, "", 14).rectTransform, 0);
            Cell(Text("Opp", sch, "", 13, Theme.TextDim).rectTransform, 120);
            Button("Go", sch, "모래 마당으로", true, 13).Layout(prefW: 120, minW: 120, prefH: 30, flexW: 0);
            sch.gameObject.SetActive(false);

            var outPad = TitledPanel("Outgoing", right, "답을 기다리는 도전장", 0);
            var outEmpty = Text("Empty", outPad, "없습니다.", 14, Theme.TextDim);
            var outRow = Row("OutgoingTemplate", outPad);
            Cell(Text("Opp", outRow, "", 14).rectTransform, 0);
            Cell(Text("Rule", outRow, "", 13).rectTransform, 90);
            Cell(Text("Fighter", outRow, "", 14).rectTransform, 140);
            Cell(Text("Hint", outRow, "내일 답이 옵니다", 13, Theme.TextDim).rectTransform, 110);
            outRow.gameObject.SetActive(false);

            var s = rt.gameObject.AddComponent<ChallengeScreen>();
            s.screenId = "challenge";
            Set(s, "sub", sub);
            Set(s, "incomingPanel", incPad.parent.gameObject);
            Set(s, "incomingTemplate", inc);
            Set(s, "target", targetDd);
            Set(s, "blockHint", block);
            Set(s, "myLabel", myLabel);
            Set(s, "mine", mineDd);
            Set(s, "theirs", theirDd);
            Set(s, "formatHint", fmtHint);
            Set(s, "ruleHint", ruleHint);
            Set(s, "error", err);
            Set(s, "send", send);
            Set(s, "scheduledTemplate", sch);
            Set(s, "scheduledEmpty", schEmpty.gameObject);
            Set(s, "outgoingTemplate", outRow);
            Set(s, "outgoingEmpty", outEmpty.gameObject);
            SetList(s, "formatButtons", fmtButtons);
            SetList(s, "ruleButtons", ruleButtons);
            rt.gameObject.SetActive(false);
            return s;
        }

        /// <summary>웹판 .field: 위에 작은 이름표, 아래 입력</summary>
        static RectTransform Field(Transform parent, string label, out TMP_Text labelText)
        {
            var f = Rect("Field", parent);
            VCol(f, 4);
            labelText = Text("Label", f, label, 13, Theme.TextDim);
            return f;
        }

        static List<Button> Seg(Transform parent, string name, params string[] labels)
        {
            var row = Rect(name, parent);
            HRow(row, 4);
            var list = new List<Button>();
            foreach (var l in labels) list.Add(Button(name + "_" + list.Count, row, l, false, 14).Layout(prefW: 110, prefH: 32));
            return list;
        }

        static void Head(Transform parent, params (string label, float w)[] cols)
        {
            var head = Box("Head", parent, Theme.Bg2).Layout(minH: 28, prefH: 28);
            HRow(head.transform, 8, 8, 8);
            foreach (var (label, w) in cols) Cell(Text("H", head.transform, label, 13, Theme.TextDim, false, FontStyles.Bold).rectTransform, w);
        }

        static RectTransform Row(string name, Transform parent)
        {
            var row = Box(name, parent, new Color(0, 0, 0, 0)).Layout(minH: 40, prefH: 40);
            HRow(row.transform, 8, 8, 8);
            var line = Box("Line", row.transform, new Color(Theme.Line.r, Theme.Line.g, Theme.Line.b, 0.6f));
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            line.rectTransform.Anchor(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 1));
            return row.rectTransform;
        }

        static void Cell(RectTransform rt, float w)
        {
            if (w > 0) rt.Layout(minW: w, prefW: w, flexW: 0, minH: 30, prefH: 30);
            else rt.Layout(minW: 120, flexW: 1, minH: 30, prefH: 30);
        }

        static void SetList(Object target, string prop, List<Button> items)
        {
            var so = new UnityEditor.SerializedObject(target);
            var p = so.FindProperty(prop);
            p.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
