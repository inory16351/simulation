// 너비에 맞춰 칸 수와 칸 너비를 정하는 격자 (GridLayoutGroup 보조). 씬 다시 조립 없이 코드로 붙인다
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    [RequireComponent(typeof(GridLayoutGroup))]
    public class AutoGrid : MonoBehaviour
    {
        /// <summary>칸 하나의 가장 작은 너비 (이보다 좁아지면 칸 수를 줄인다)</summary>
        public float minCell = 260;
        public float cellHeight = 300;
        public int maxColumns = 5;
        float lastWidth = -1;

        void OnEnable() => Fit();
        void OnRectTransformDimensionsChange() => Fit();

        public void Fit()
        {
            var g = GetComponent<GridLayoutGroup>();
            // 스크롤바(오른쪽)에 걸리지 않게 조금 남긴다
            float w = ((RectTransform)transform).rect.width - g.padding.left - g.padding.right - 14;
            if (w <= 0) w = 1200;
            if (Mathf.Approximately(w, lastWidth) && Mathf.Approximately(g.cellSize.y, cellHeight)) return;
            lastWidth = w;
            int cols = Mathf.Clamp(Mathf.FloorToInt((w + g.spacing.x) / (minCell + g.spacing.x)), 1, maxColumns);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = cols;
            g.cellSize = new Vector2(Mathf.Floor((w - g.spacing.x * (cols - 1)) / cols), cellHeight);
        }
    }
}
