// 아직 옮기지 않은 화면 자리
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    /// <summary>아직 옮기지 않은 화면</summary>
    public class PlaceholderScreen : ScreenBase
    {
        [SerializeField] TMP_Text title;
        public override void Refresh() { }
    }
}
