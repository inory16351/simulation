import { useEffect, useState } from "react";
import { audioSettings, onAudioChange, setAudio, sfx } from "../audio";

/** 상단바 소리 조절: 누르면 음소거, 옆 화살표로 효과음·음악 크기 */
export function AudioControl() {
  const [s, setS] = useState(audioSettings());
  const [open, setOpen] = useState(false);
  useEffect(() => onAudioChange(() => setS(audioSettings())), []);
  return (
    <div className="audio-control">
      <button className="btn" title={s.muted ? "소리 켜기" : "소리 끄기"} onClick={() => setAudio({ muted: !s.muted })}>{s.muted ? "🔇" : "🔊"}</button>
      <button className="btn" title="소리 크기" onClick={() => setOpen(!open)}>▾</button>
      {open && (
        <div className="audio-pop panel">
          <label>효과음 <input type="range" min={0} max={1} step={0.05} value={s.sfx} onChange={(e) => setAudio({ sfx: Number(e.target.value) })} onMouseUp={() => sfx("hit")} /></label>
          <label>음악 <input type="range" min={0} max={1} step={0.05} value={s.music} onChange={(e) => setAudio({ music: Number(e.target.value) })} /></label>
        </div>
      )}
    </div>
  );
}
