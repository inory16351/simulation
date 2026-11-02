// 왼쪽 메뉴 버튼 하나 (SceneBuilder가 만들고 Shell이 칠한다)
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class MenuButton : MonoBehaviour
    {
        public string id;
        public Button button;
        public Image bg;
        public Image accent;
        public TMP_Text label;
        public GameObject badge;
        public TMP_Text badgeText;
    }
}
