import { avatarOf, avatarOptions } from "../../core/avatar";
import type { Gladiator, PortraitParts } from "../../core/types";

const LABELS: Record<string, string> = {
  tuft: "삐죽", short: "짧게", long: "긴 머리", pony: "꽁지", mohawk: "모히칸", bun: "올림", braids: "땋음", messy: "헝클",
  round: "동그란", fierce: "사나운", sleepy: "졸린", wide: "큰",
  line: "일자", smile: "미소", grit: "이 악묾", frown: "찡그림", open: "벌림",
  stubble: "까끌", full: "덥수룩", braided: "땋은 수염", goatee: "염소",
  crest: "볏 투구", horned: "뿔 투구", cap: "철모", hood: "두건",
};

/** 초상화 직접 고치기. 아무것도 안 고치면 시드로 자동으로 생긴다 */
export function PortraitEditor({ g, onChange }: { g: Gladiator; onChange: (p: PortraitParts | undefined) => void }) {
  const cur = avatarOf(g) as unknown as Record<string, string | number | null>;

  const cycle = (key: string, values: (string | null)[], dir: 1 | -1) => {
    const now = cur[key] === null ? null : String(cur[key]);
    const i = values.indexOf(now);
    const next = values[(i + dir + values.length) % values.length];
    onChange({ ...(g.portrait ?? {}), [key]: next });
  };

  return (
    <div className="portrait-editor">
      {avatarOptions(g).map((o) => {
        const val = cur[o.key] === null ? null : String(cur[o.key]);
        const isColor = typeof val === "string" && val.startsWith("#");
        return (
          <div key={o.key} className="pe-row">
            <span className="pe-label">{o.label}</span>
            <button className="mini" onClick={() => cycle(o.key, o.values, -1)}>◀</button>
            <span className="pe-value">
              {isColor ? <i className="swatch" style={{ background: val! }} /> : val === null ? "없음" : LABELS[val] ?? `${Number(val) + 1}번`}
            </span>
            <button className="mini" onClick={() => cycle(o.key, o.values, 1)}>▶</button>
          </div>
        );
      })}
      <div className="pe-actions">
        <button className="btn" onClick={() => onChange(undefined)}>처음대로</button>
      </div>
    </div>
  );
}
