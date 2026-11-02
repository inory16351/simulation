// 효과음과 전투 음악 (웹판 ui/audio.ts). 효과음은 웹판과 같은 재료(걸러 낸 잡음 + 주파수가 미끄러지는 음)로
// 처음 한 번 합성해 AudioClip으로 두고, 음악은 Resources/Audio/battle(웹판 audio/battle.mp3)을 반복한다.
// 설정(효과음·음악 크기, 음소거)은 PlayerPrefs에 둔다.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Colosseum.UI
{
    public class GameAudio : MonoBehaviour
    {
        public static readonly string[] SfxNames = { "swing", "hit", "heavy", "blunt", "clang", "blood", "miss", "cheer", "boo", "fall", "death", "step" };

        const string Key = "colosseum.audio";
        const int Rate = 44100;

        static GameAudio inst;
        public static GameAudio I
        {
            get
            {
                if (inst == null)
                {
                    var go = new GameObject("GameAudio");
                    DontDestroyOnLoad(go);
                    inst = go.AddComponent<GameAudio>();
                }
                return inst;
            }
        }

        public static float SfxVolume { get => PlayerPrefs.GetFloat(Key + ".sfx", 0.7f); set { PlayerPrefs.SetFloat(Key + ".sfx", value); I.Apply(); } }
        public static float MusicVolume { get => PlayerPrefs.GetFloat(Key + ".music", 0.35f); set { PlayerPrefs.SetFloat(Key + ".music", value); I.Apply(); } }
        public static bool Muted { get => PlayerPrefs.GetInt(Key + ".muted", 0) == 1; set { PlayerPrefs.SetInt(Key + ".muted", value ? 1 : 0); I.Apply(); } }
        public static event Action Changed;

        readonly Dictionary<string, AudioClip> clips = new();
        AudioSource music;
        readonly List<AudioSource> voices = new();
        int nextVoice;
        bool musicWanted;
        Coroutine fade;
        float musicGain;

        void Awake()
        {
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.playOnAwake = false;
            music.clip = Resources.Load<AudioClip>("Audio/battle");
            for (int i = 0; i < 10; i++)
            {
                var v = gameObject.AddComponent<AudioSource>();
                v.playOnAwake = false;
                voices.Add(v);
            }
            foreach (var n in SfxNames) clips[n] = Synth.Make(n);
        }

        void Apply()
        {
            music.volume = (Muted ? 0 : MusicVolume) * musicGain;
            Changed?.Invoke();
        }

        /// <summary>효과음. strength는 웹판처럼 세기(치명타 1.3 등)</summary>
        public static void Sfx(string name, float strength = 1)
        {
            var a = I;
            if (Muted || !a.clips.TryGetValue(name, out var clip)) return;
            var v = a.voices[a.nextVoice++ % a.voices.Count];
            v.pitch = 1;
            v.PlayOneShot(clip, Mathf.Clamp01(SfxVolume * strength));
        }

        /// <summary>전투 음악을 켜고 끈다 (켤 때 1.5초, 끌 때 1.2초에 걸쳐)</summary>
        public static void Music(bool on)
        {
            var a = I;
            if (a.musicWanted == on || a.music.clip == null) return;
            a.musicWanted = on;
            if (a.fade != null) a.StopCoroutine(a.fade);
            a.fade = a.StartCoroutine(a.Fade(on));
        }

        IEnumerator Fade(bool on)
        {
            if (on && !music.isPlaying) music.Play();
            float from = musicGain, to = on ? 1 : 0, dur = on ? 1.5f : 1.2f;
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                musicGain = Mathf.Lerp(from, to, t / dur);
                Apply();
                yield return null;
            }
            musicGain = to;
            Apply();
            if (!on) music.Stop();
            fade = null;
        }

        /// <summary>웹판 Web Audio 합성을 그대로 샘플로 굽는다</summary>
        static class Synth
        {
            static readonly System.Random R = new(7);

            public static AudioClip Make(string name)
            {
                float len = name switch { "death" => 2.8f, "cheer" => 1.8f, "boo" => 1.5f, "clang" => 0.7f, _ => 0.5f };
                var buf = new float[(int)(Rate * len)];
                switch (name)
                {
                    case "swing": Noise(buf, 0, Filter.Band, 600, 2600, 1.2, 0.35, 0.18); break;
                    case "miss": Noise(buf, 0, Filter.Band, 900, 3200, 1.5, 0.25, 0.22); Noise(buf, 0.12, Filter.Low, 900, 300, 0.7, 0.12, 0.2); break;
                    case "hit": Tone(buf, 0, Wave.Sine, 180, 60, 0.6, 0.12); Noise(buf, 0, Filter.Band, 1800, 700, 0.9, 0.45, 0.12); break;
                    case "heavy":
                        Tone(buf, 0, Wave.Sine, 140, 40, 0.9, 0.22); Noise(buf, 0, Filter.Low, 2400, 400, 0.8, 0.6, 0.2); Noise(buf, 0.03, Filter.Band, 700, 300, 2, 0.4, 0.25); break;
                    case "blunt": Tone(buf, 0, Wave.Triangle, 110, 35, 1.0, 0.25); Noise(buf, 0, Filter.Low, 800, 150, 0.8, 0.5, 0.18); break;
                    case "clang":
                        foreach (var (f, a) in new[] { (820.0, 0.22), (1330.0, 0.16), (2110.0, 0.12), (3170.0, 0.08) }) Tone(buf, 0, Wave.Sine, f, f * 0.98, a, 0.6);
                        Noise(buf, 0, Filter.High, 3000, 3000, 0.7, 0.25, 0.05); break;
                    case "blood": Noise(buf, 0.04, Filter.Low, 1200, 250, 1.5, 0.35, 0.28); Noise(buf, 0.12, Filter.Low, 900, 200, 2, 0.2, 0.2); break;
                    case "fall": Tone(buf, 0, Wave.Sine, 90, 30, 0.9, 0.35); Noise(buf, 0, Filter.Low, 500, 120, 0.7, 0.5, 0.4); break;
                    case "death":
                        foreach (var (f, a) in new[] { (98.0, 0.5), (147.0, 0.25), (196.0, 0.15), (262.0, 0.08) }) Tone(buf, 0, Wave.Sine, f, f * 0.97, a, 2.6);
                        break;
                    case "cheer": Crowd(buf, 1.6, 0.5, 900); break;
                    case "boo": Crowd(buf, 1.3, 0.4, 300); break;
                    case "step": Noise(buf, 0, Filter.Low, 700, 200, 0.7, 0.15, 0.08); break;
                }
                // 웹판 컴프레서 대신 부드럽게 눌러 준다
                for (int i = 0; i < buf.Length; i++) buf[i] = (float)Math.Tanh(buf[i] * 1.2);
                var clip = AudioClip.Create("sfx_" + name, buf.Length, 1, Rate, false);
                clip.SetData(buf, 0);
                return clip;
            }

            enum Filter { Low, High, Band }
            enum Wave { Sine, Triangle }

            /// <summary>웹판 env: 0.0001에서 peak까지 지수로 올라갔다가 decay 동안 지수로 내려간다</summary>
            static double Env(double t, double attack, double peak, double decay)
            {
                if (t < 0) return 0;
                if (t < attack) return 0.0001 * Math.Pow(peak / 0.0001, t / attack);
                double d = t - attack;
                return d > decay ? 0 : peak * Math.Pow(0.0001 / peak, d / decay);
            }

            static void Tone(float[] buf, double start, Wave wave, double f0, double f1, double peak, double decay)
            {
                f1 = Math.Max(20, f1);
                double phase = 0;
                int s0 = (int)(start * Rate), n = (int)((decay + 0.05) * Rate);
                for (int i = 0; i < n && s0 + i < buf.Length; i++)
                {
                    double t = i / (double)Rate;
                    double f = f0 * Math.Pow(f1 / f0, Math.Min(1, t / decay));
                    phase += f / Rate;
                    double p = phase - Math.Floor(phase);
                    double v = wave == Wave.Sine ? Math.Sin(2 * Math.PI * p) : 1 - 4 * Math.Abs(p - 0.5);
                    buf[s0 + i] += (float)(v * Env(t, 0.004, peak, decay));
                }
            }

            /// <summary>잡음을 RBJ 바이쿼드로 걸러 낸다. 차단 주파수는 decay 동안 f0에서 f1로 지수로 미끄러진다</summary>
            static void Noise(float[] buf, double start, Filter type, double f0, double f1, double q, double peak, double decay, Func<double, double> gain = null, double length = -1)
            {
                double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
                int s0 = (int)(start * Rate), n = (int)(((length > 0 ? length : decay) + 0.05) * Rate);
                for (int i = 0; i < n && s0 + i < buf.Length; i++)
                {
                    double t = i / (double)Rate;
                    double f = f0 * Math.Pow(f1 / f0, Math.Min(1, t / Math.Max(0.001, decay)));
                    double w = 2 * Math.PI * Math.Min(f, Rate * 0.45) / Rate, cw = Math.Cos(w), alpha = Math.Sin(w) / (2 * q);
                    double b0, b1, b2;
                    switch (type)
                    {
                        case Filter.Low: b0 = (1 - cw) / 2; b1 = 1 - cw; b2 = (1 - cw) / 2; break;
                        case Filter.High: b0 = (1 + cw) / 2; b1 = -(1 + cw); b2 = (1 + cw) / 2; break;
                        default: b0 = alpha; b1 = 0; b2 = -alpha; break;
                    }
                    double a0 = 1 + alpha, a1 = -2 * cw, a2 = 1 - alpha;
                    double x = R.NextDouble() * 2 - 1;
                    double y = (b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) / a0;
                    x2 = x1; x1 = x; y2 = y1; y1 = y;
                    double g = gain != null ? gain(t) : Env(t, 0.005, peak, decay);
                    buf[s0 + i] += (float)(y * g);
                }
            }

            /// <summary>관중: 세 대역의 잡음이 30%까지 차올랐다가 사그라든다</summary>
            static void Crowd(float[] buf, double dur, double peak, double center)
            {
                for (int k = 0; k < 3; k++)
                {
                    double f = center * (0.7 + k * 0.35);
                    Noise(buf, 0, Filter.Band, f, f, 0.8, peak, dur,
                        t => t < dur * 0.3 ? peak / 3 * (t / (dur * 0.3)) : Math.Max(0, peak / 3 * (1 - (t - dur * 0.3) / (dur * 0.7))), dur);
                }
            }
        }
    }
}
