// 웹판 styles.css의 색 (:root 변수와 같은 이름)
using UnityEngine;

namespace Colosseum.UI
{
    public static class Theme
    {
        static Color C(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        public static readonly Color Bg = C(0x14110f);
        public static readonly Color Bg2 = C(0x1b1714);
        public static readonly Color Panel = C(0x221c18);
        public static readonly Color Panel2 = C(0x2a231e);
        public static readonly Color Line = C(0x3a2f25);
        public static readonly Color Bronze = C(0x7a5c33);
        public static readonly Color BronzeHi = C(0xb08a4f);
        public static readonly Color Parchment = C(0xd9cbb0);
        public static readonly Color Text = C(0xcfc2a8);
        public static readonly Color TextDim = C(0x8c7f6a);
        public static readonly Color TextFaint = C(0x5f5546);
        public static readonly Color Torch = C(0xe39a3b);
        public static readonly Color Blood = C(0xa3241d);
        public static readonly Color BloodDark = C(0x7a1e1a);
        public static readonly Color Good = C(0x8fa25a);
        public static readonly Color Sand = C(0x8a6e4b);
        public static readonly Color TopbarTop = C(0x211b17);
        public static readonly Color ContinueBg = C(0x7a4f1d);
        public static readonly Color ContinueText = C(0xfff3dc);
        public static readonly Color AlertText = C(0xf3d6cf);

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }
}
