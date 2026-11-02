// 한국어 조사 처리. 마지막 글자의 받침 유무로 이/가, 을/를 등을 고른다. (src/core/josa.ts)
namespace Colosseum.Core
{
    public static class Josa
    {
        static bool HasBatchim(string w)
        {
            if (string.IsNullOrEmpty(w)) return false;
            int ch = w[w.Length - 1];
            if (ch < 0xac00 || ch > 0xd7a3) return false;
            return (ch - 0xac00) % 28 != 0;
        }

        static bool HasRieul(string w)
        {
            if (string.IsNullOrEmpty(w)) return false;
            int ch = w[w.Length - 1];
            if (ch < 0xac00 || ch > 0xd7a3) return false;
            return (ch - 0xac00) % 28 == 8;
        }

        /// <summary>pair 예: "이/가", "을/를", "은/는", "과/와", "으로/로", "이다/다"</summary>
        public static string J(string word, string pair)
        {
            var p = pair.Split('/');
            if (pair == "으로/로") return word + (HasBatchim(word) && !HasRieul(word) ? p[0] : p[1]);
            return word + (HasBatchim(word) ? p[0] : p[1]);
        }
    }
}
