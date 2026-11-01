import { OPTIONAL_SLOTS, allowedParts, pickPortrait, portraitOf } from "../../core/portrait";
import type { PartSlot } from "../../core/portraitLayout";
import type { Gladiator, PortraitParts } from "../../core/types";

const SLOT_NAMES: Record<PartSlot, string> = {
  body: "몸통", head: "얼굴형", eyes: "눈", nose: "코", mouth: "입", hair: "머리", beard: "수염", helmet: "투구",
};
const SLOTS: PartSlot[] = ["head", "eyes", "nose", "mouth", "hair", "beard", "helmet", "body"];

/** 초상화 파츠를 직접 고르는 편집기. 아무것도 안 고치면 시드로 자동 조합된다 */
export function PortraitEditor({ g, onChange }: { g: Gladiator; onChange: (p: PortraitParts | undefined) => void }) {
  const cur = portraitOf(g);

  const cycle = (slot: PartSlot, dir: 1 | -1) => {
    const list = allowedParts(g, slot).map((p) => p.id as string | null);
    if (OPTIONAL_SLOTS.includes(slot)) list.unshift(null);
    if (!list.length) return;
    const i = list.indexOf(cur[slot] ?? null);
    const next = list[(i + dir + list.length) % list.length];
    onChange({ ...cur, [slot]: next });
  };

  return (
    <div className="portrait-editor">
      {SLOTS.map((slot) => {
        const list = allowedParts(g, slot);
        if (!list.length && !OPTIONAL_SLOTS.includes(slot)) return null;
        if (!list.length) return null;
        const idx = list.findIndex((p) => p.id === cur[slot]);
        return (
          <div key={slot} className="pe-row">
            <span className="pe-label">{SLOT_NAMES[slot]}</span>
            <button className="mini" onClick={() => cycle(slot, -1)}>◀</button>
            <span className="pe-value">{idx >= 0 ? `${idx + 1}/${list.length}` : "없음"}</span>
            <button className="mini" onClick={() => cycle(slot, 1)}>▶</button>
          </div>
        );
      })}
      <div className="pe-actions">
        <button className="btn" onClick={() => onChange(pickPortrait(g, Math.floor(Math.random() * 1e9)))}>무작위</button>
        <button className="btn" onClick={() => onChange(undefined)}>자동으로</button>
      </div>
    </div>
  );
}
