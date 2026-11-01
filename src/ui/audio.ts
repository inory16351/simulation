import { asset } from "./assets";

// 효과음과 전투 음악. 효과음은 Web Audio로 그때그때 합성하고, 음악은 파일(없으면 합성)로 튼다.
// 브라우저는 사용자가 한 번 누르기 전에는 소리를 못 내므로 첫 입력 때 켠다.

export type Sfx = "swing" | "hit" | "heavy" | "blunt" | "clang" | "blood" | "miss" | "cheer" | "boo" | "fall" | "death" | "step";
export type Music = "battle" | null;

const KEY = "colosseum-league.audio";
interface Settings { sfx: number; music: number; muted: boolean }
const load = (): Settings => {
  try { return { sfx: 0.7, music: 0.35, muted: false, ...JSON.parse(localStorage.getItem(KEY) ?? "{}") }; } catch { return { sfx: 0.7, music: 0.35, muted: false }; }
};
let settings = load();
let ctx: AudioContext | null = null;
let sfxBus: GainNode, musicBus: GainNode;
let noise: AudioBuffer;
const listeners = new Set<() => void>();

export const audioSettings = () => settings;
export function setAudio(p: Partial<Settings>): void {
  settings = { ...settings, ...p };
  try { localStorage.setItem(KEY, JSON.stringify(settings)); } catch { /* 저장 못 해도 된다 */ }
  if (ctx) applyVolumes();
  listeners.forEach((f) => f());
}
export const onAudioChange = (f: () => void) => { listeners.add(f); return () => { listeners.delete(f); }; };

function applyVolumes() {
  const t = ctx!.currentTime;
  sfxBus.gain.setTargetAtTime(settings.muted ? 0 : settings.sfx, t, 0.05);
  musicBus.gain.setTargetAtTime(settings.muted ? 0 : settings.music, t, 0.3);
}

/** 첫 사용자 입력에서 부른다 */
export function unlockAudio(): void {
  if (ctx) { if (ctx.state === "suspended") void ctx.resume(); return; }
  const AC = window.AudioContext ?? (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
  if (!AC) return;
  ctx = new AC();
  const master = ctx.createDynamicsCompressor();
  master.connect(ctx.destination);
  sfxBus = ctx.createGain(); sfxBus.connect(master);
  musicBus = ctx.createGain(); musicBus.connect(master);
  noise = ctx.createBuffer(1, ctx.sampleRate * 2, ctx.sampleRate);
  const d = noise.getChannelData(0);
  for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
  applyVolumes();
  if (wanted) void startMusic(wanted);
}

// ── 효과음 재료 ──
function env(g: GainNode, t: number, a: number, peak: number, decay: number) {
  g.gain.setValueAtTime(0.0001, t);
  g.gain.exponentialRampToValueAtTime(peak, t + a);
  g.gain.exponentialRampToValueAtTime(0.0001, t + a + decay);
}
function noiseHit(t: number, type: BiquadFilterType, f0: number, f1: number, q: number, peak: number, decay: number, dest: AudioNode = sfxBus) {
  const c = ctx!;
  const src = c.createBufferSource(); src.buffer = noise; src.playbackRate.value = 0.8 + Math.random() * 0.4;
  const f = c.createBiquadFilter(); f.type = type; f.Q.value = q;
  f.frequency.setValueAtTime(f0, t); f.frequency.exponentialRampToValueAtTime(f1, t + decay);
  const g = c.createGain(); env(g, t, 0.005, peak, decay);
  src.connect(f).connect(g).connect(dest);
  src.start(t, Math.random()); src.stop(t + decay + 0.05);
}
function tone(t: number, type: OscillatorType, f0: number, f1: number, peak: number, decay: number, dest: AudioNode = sfxBus) {
  const c = ctx!;
  const o = c.createOscillator(); o.type = type;
  o.frequency.setValueAtTime(f0, t); o.frequency.exponentialRampToValueAtTime(Math.max(20, f1), t + decay);
  const g = c.createGain(); env(g, t, 0.004, peak, decay);
  o.connect(g).connect(dest); o.start(t); o.stop(t + decay + 0.05);
}

export function sfx(name: Sfx, strength = 1): void {
  if (!ctx || settings.muted) return;
  const t = ctx.currentTime + 0.005;
  const k = strength;
  switch (name) {
    case "swing": noiseHit(t, "bandpass", 600, 2600, 1.2, 0.35 * k, 0.18); break;
    case "miss": noiseHit(t, "bandpass", 900, 3200, 1.5, 0.25, 0.22); noiseHit(t + 0.12, "lowpass", 900, 300, 0.7, 0.12, 0.2); break;
    case "hit": // 살을 베는 소리: 짧은 퍽 + 젖은 잡음
      tone(t, "sine", 180, 60, 0.6 * k, 0.12); noiseHit(t, "bandpass", 1800, 700, 0.9, 0.45 * k, 0.12); break;
    case "heavy":
      tone(t, "sine", 140, 40, 0.9 * k, 0.22); noiseHit(t, "lowpass", 2400, 400, 0.8, 0.6 * k, 0.2); noiseHit(t + 0.03, "bandpass", 700, 300, 2, 0.4 * k, 0.25); break;
    case "blunt":
      tone(t, "triangle", 110, 35, 1.0 * k, 0.25); noiseHit(t, "lowpass", 800, 150, 0.8, 0.5 * k, 0.18); break;
    case "clang": // 쇠가 부딪히는 소리: 어긋난 배음
      for (const [f, a] of [[820, 0.22], [1330, 0.16], [2110, 0.12], [3170, 0.08]] as const) tone(t, "sine", f, f * 0.98, a * k, 0.6);
      noiseHit(t, "highpass", 3000, 3000, 0.7, 0.25 * k, 0.05); break;
    case "blood": noiseHit(t + 0.04, "lowpass", 1200, 250, 1.5, 0.35 * k, 0.28); noiseHit(t + 0.12, "lowpass", 900, 200, 2, 0.2 * k, 0.2); break;
    case "fall": tone(t, "sine", 90, 30, 0.9, 0.35); noiseHit(t, "lowpass", 500, 120, 0.7, 0.5, 0.4); break;
    case "death": // 낮은 징
      for (const [f, a] of [[98, 0.5], [147, 0.25], [196, 0.15], [262, 0.08]] as const) tone(t, "sine", f, f * 0.97, a, 2.6);
      break;
    case "cheer": crowd(t, 1.6, 0.5 * k, 900); break;
    case "boo": crowd(t, 1.3, 0.4 * k, 300); break;
    case "step": noiseHit(t, "lowpass", 700, 200, 0.7, 0.15, 0.08); break;
  }
}
function crowd(t: number, dur: number, peak: number, center: number) {
  const c = ctx!;
  for (let i = 0; i < 3; i++) {
    const src = c.createBufferSource(); src.buffer = noise; src.loop = true;
    const f = c.createBiquadFilter(); f.type = "bandpass"; f.frequency.value = center * (0.7 + i * 0.35); f.Q.value = 0.8;
    const g = c.createGain();
    g.gain.setValueAtTime(0.0001, t); g.gain.linearRampToValueAtTime(peak / 3, t + dur * 0.3); g.gain.linearRampToValueAtTime(0.0001, t + dur);
    src.connect(f).connect(g).connect(sfxBus); src.start(t, Math.random()); src.stop(t + dur + 0.1);
  }
}

// ── 배경 음악: 미리 정한 선율 없이, 선법 안에서 그때그때 짓는다 ──
let wanted: Music = null;
let playing: { name: Music; stop: () => void } | null = null;

export function playMusic(name: Music): void {
  wanted = name;
  if (!ctx) return;
  if (playing?.name === name) return;
  playing?.stop();
  playing = null;
  if (name) void startMusic(name);
}

/** 음악 파일 (Gemini로 만든 곡). 없으면 합성 음악으로 대신한다 */
const TRACKS: Record<Exclude<Music, null>, string> = { battle: "audio/battle.mp3" };
const decoded = new Map<string, AudioBuffer | null>();

async function startMusic(name: Exclude<Music, null>) {
  const c = ctx!;
  const src = TRACKS[name];
  if (!decoded.has(src)) {
    try {
      const res = await fetch(asset(src));
      decoded.set(src, res.ok ? await c.decodeAudioData(await res.arrayBuffer()) : null);
    } catch { decoded.set(src, null); }
  }
  // 불러오는 사이에 다른 음악으로 바뀌었으면 그만둔다
  if (wanted !== name || playing) return;
  const buf = decoded.get(src);
  if (buf) {
    const out = c.createGain(); out.gain.value = 0; out.connect(musicBus);
    out.gain.linearRampToValueAtTime(1, c.currentTime + 1.5);
    const node = c.createBufferSource(); node.buffer = buf; node.loop = true; node.connect(out); node.start();
    playing = {
      name,
      stop: () => {
        const t = c.currentTime;
        out.gain.cancelScheduledValues(t); out.gain.setValueAtTime(out.gain.value, t); out.gain.linearRampToValueAtTime(0, t + 1.2);
        node.stop(t + 1.3);
      },
    };
    return;
  }
  synthMusic(name);
}

function synthMusic(name: Exclude<Music, null>) {
  const c = ctx!;
  const out = c.createGain(); out.gain.value = 0; out.connect(musicBus);
  out.gain.linearRampToValueAtTime(1, c.currentTime + 2.5);
  const battle = name === "battle";
  // 프리지안 선법 (어둡고 고대풍)
  const root = battle ? 110 : 98;
  const scale = [0, 1, 3, 5, 7, 8, 10, 12, 13, 15];
  const hz = (st: number) => root * Math.pow(2, st / 12);
  // 저음 지속음
  const drones: OscillatorNode[] = [];
  const lp = c.createBiquadFilter(); lp.type = "lowpass"; lp.frequency.value = battle ? 500 : 380; lp.connect(out);
  for (const [st, det] of [[0, 0], [7, 4], [-12, -3]] as const) {
    const o = c.createOscillator(); o.type = "sawtooth"; o.frequency.value = hz(st); o.detune.value = det;
    const g = c.createGain(); g.gain.value = battle ? 0.05 : 0.04;
    o.connect(g).connect(lp); o.start(); drones.push(o);
  }
  const bpm = battle ? 112 : 70;
  const beat = 60 / bpm;
  let next = c.currentTime + 0.1, step = 0, alive = true;
  let note = 4;
  const timer = window.setInterval(() => {
    if (!alive) return;
    while (next < c.currentTime + 0.4) {
      const s = step % 16;
      if (battle) {
        // 프레임 드럼: 쿵 . 탁 쿵 | 쿵 . 탁 .
        if ([0, 3, 8, 11].includes(s)) drum(next, 70, 0.55, out);
        if ([4, 12].includes(s)) noiseHit(next, "bandpass", 1500, 900, 1.2, 0.18, 0.08, out);
        if (s % 2 === 1) noiseHit(next, "highpass", 6000, 6000, 0.7, 0.03, 0.03, out);
      } else if (s === 0 || s === 10) drum(next, 60, 0.25, out);
      // 선율: 박마다 확률적으로, 음계 안에서 한두 칸씩 움직인다
      if ((battle ? s % 2 === 0 : s % 4 === 0) && Math.random() < (battle ? 0.55 : 0.5)) {
        note = Math.max(0, Math.min(scale.length - 1, note + (Math.random() < 0.5 ? -1 : 1) * (Math.random() < 0.7 ? 1 : 2)));
        pluck(next, hz(scale[note] + 12), battle ? 0.09 : 0.07, battle ? 0.5 : 1.4, out);
      }
      next += beat / 4;
      step++;
    }
  }, 100);
  playing = {
    name,
    stop: () => {
      alive = false; window.clearInterval(timer);
      const t = c.currentTime;
      out.gain.cancelScheduledValues(t); out.gain.setValueAtTime(out.gain.value, t); out.gain.linearRampToValueAtTime(0, t + 1.2);
      drones.forEach((o) => o.stop(t + 1.3));
    },
  };
}
function drum(t: number, f: number, peak: number, dest: AudioNode) {
  tone(t, "sine", f * 2.2, f, peak, 0.3, dest);
  noiseHit(t, "lowpass", 400, 120, 0.7, peak * 0.4, 0.12, dest);
}
function pluck(t: number, f: number, peak: number, decay: number, dest: AudioNode) {
  const c = ctx!;
  const o = c.createOscillator(); o.type = "triangle"; o.frequency.value = f;
  const o2 = c.createOscillator(); o2.type = "sine"; o2.frequency.value = f * 2.01;
  const g = c.createGain(); env(g, t, 0.01, peak, decay);
  const g2 = c.createGain(); g2.gain.value = 0.3;
  o.connect(g); o2.connect(g2).connect(g); g.connect(dest);
  o.start(t); o2.start(t); o.stop(t + decay + 0.05); o2.stop(t + decay + 0.05);
}
