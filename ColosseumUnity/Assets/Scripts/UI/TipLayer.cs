// 맨 위 오버레이: 마우스 아래 TMP 링크를 읽어 툴팁을 띄우고(race:/style:/item:/nation:), 검투사 이름(g:)·초상화를 누르면 팝업 카드,
// 나머지 링크(nation:/ludus:/comp:/item:/race:/style:)를 누르면 정보 카드(InfoPopup)를 연다.
// 글자마다 컴포넌트를 붙이지 않아도 되게, 마우스 아래 UI를 레이캐스트해 그 안의 TMP와 PortraitView를 찾는다.
// 버튼처럼 누르기를 따로 받는 곳 안에서는 팝업을 열지 않는다 (그 버튼이 할 일을 한다).
using System.Collections.Generic;
using Colosseum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Colosseum.UI
{
    public class TipLayer : MonoBehaviour
    {
        [SerializeField] RectTransform tip;
        [SerializeField] TMP_Text tipText;

        readonly List<RaycastResult> hits = new();
        PointerEventData ped;
        Vector3 lastMouse = new(-1, -1);
        string link, portraitId;
        bool linkClickable, portraitClickable;

        void Awake() => tip.gameObject.SetActive(false);

        void Update()
        {
            var es = EventSystem.current;
            if (es == null) return;
            var mouse = Input.mousePosition;
            if (mouse != lastMouse)
            {
                lastMouse = mouse;
                Probe(es, mouse);
                string text = link != null ? Tips.For(link) : null;
                tip.gameObject.SetActive(text != null);
                if (text != null)
                {
                    if (tipText.text != text) tipText.text = text;
                    Place(mouse);
                }
            }
            if (Input.GetMouseButtonUp(0))
            {
                var G = GameController.I;
                if (G == null || G.State == null) return;
                // 검투사는 팝업 카드, 나머지 링크(나라·루두스·대회·장비·종족·스타일)는 정보 카드
                if (link != null && linkClickable && !link.StartsWith("g:")) { G.ShowInfo(link); return; }
                string id = link != null && link.StartsWith("g:") && linkClickable ? link.Substring(2)
                    : portraitClickable ? portraitId : null;
                if (id != null && G.State.gladiators.ContainsKey(id)) G.ShowGladiator(id);
            }
        }

        void Probe(EventSystem es, Vector3 mouse)
        {
            link = portraitId = null;
            ped ??= new PointerEventData(es);
            ped.position = mouse;
            hits.Clear();
            es.RaycastAll(ped, hits);
            for (int h = 0; h < hits.Count && h < 4; h++)
            {
                var go = hits[h].gameObject;
                // 누르기를 받는 버튼이 따로 있으면 팝업은 그쪽에 맡긴다
                bool free = ExecuteEvents.GetEventHandler<IPointerClickHandler>(go) == null;
                foreach (var t in go.GetComponentsInChildren<TMP_Text>())
                {
                    if (t.textInfo == null || t.textInfo.linkCount == 0) continue;
                    if (!RectTransformUtility.RectangleContainsScreenPoint(t.rectTransform, mouse, null)) continue;
                    int i = TMP_TextUtilities.FindIntersectingLink(t, mouse, null);
                    if (i < 0) continue;
                    link = t.textInfo.linkInfo[i].GetLinkID();
                    linkClickable = free;
                    return;
                }
                if (portraitId == null)
                {
                    var pv = go.GetComponentInParent<PortraitView>();
                    if (pv == null)
                        foreach (var p in go.GetComponentsInChildren<PortraitView>())
                            if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)p.transform, mouse, null)) { pv = p; break; }
                    if (pv != null && pv.GladiatorId != null) { portraitId = pv.GladiatorId; portraitClickable = free; }
                }
            }
        }

        /// <summary>마우스 오른쪽 아래에 두되 화면 밖으로 나가면 안쪽으로 접는다</summary>
        void Place(Vector3 mouse)
        {
            var parent = (RectTransform)tip.parent;
            Canvas.ForceUpdateCanvases();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, mouse, null, out var local);
            var size = tip.rect.size;
            var r = parent.rect;
            float x = local.x + 18, y = local.y - 22;
            if (x + size.x > r.xMax - 8) x = local.x - 12 - size.x;
            if (y - size.y < r.yMin + 8) y = local.y + 12 + size.y;
            tip.anchoredPosition = new Vector2(x - r.xMin, y - r.yMax);
        }
    }
}
