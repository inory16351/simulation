// 상단 바 소리 조절 (웹판 AudioControl.tsx): 누르면 음소거, 옆 버튼으로 효과음·음악 크기 막대를 편다.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.UI
{
    public class AudioControl : MonoBehaviour
    {
        [SerializeField] Button mute;
        [SerializeField] Button toggle;
        [SerializeField] GameObject popup;
        [SerializeField] Slider sfx;
        [SerializeField] Slider music;

        void Awake()
        {
            popup.SetActive(false);
            mute.onClick.AddListener(() => GameAudio.Muted = !GameAudio.Muted);
            toggle.onClick.AddListener(() => popup.SetActive(!popup.activeSelf));
            sfx.SetValueWithoutNotify(GameAudio.SfxVolume);
            music.SetValueWithoutNotify(GameAudio.MusicVolume);
            sfx.onValueChanged.AddListener(v => GameAudio.SfxVolume = v);
            music.onValueChanged.AddListener(v => GameAudio.MusicVolume = v);
            GameAudio.Changed += Draw;
            Draw();
        }

        void OnDestroy() => GameAudio.Changed -= Draw;

        void Update()
        {
            // 웹판 onMouseUp: 효과음 막대를 놓으면 들려준다
            if (popup.activeSelf && Input.GetMouseButtonUp(0) && sfx.gameObject == UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject)
                GameAudio.Sfx("hit");
        }

        void Draw() => mute.GetComponentInChildren<TMP_Text>().text = GameAudio.Muted ? "소리 끔" : "소리 켬";
    }
}
