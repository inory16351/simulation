// 장비 화면 조각: 장비 시장 검색, 창고, 검투사 장비 칸
import { useState } from "react";
import { NATIONS, QUALITY_NAMES } from "../../core/data";
import {
  ITEM_KIND_NAMES, QUALITY_CLASS, SLOT_NAMES, armoryOf, buyListing, cannotEquip, equipItem, gearMarketOf, itemKind, itemName,
  itemPrice, itemStats, qualityOf, sellItem, type ItemKind, type Slot,
} from "../../core/gear";
import type { GameState, Gladiator } from "../../core/types";
import type { Ctx } from "../App";

/** 검증용 복사본에서 먼저 돌려 보고, 되면 진짜 상태에 반영한다 */
function useAction(ctx: Ctx) {
  const [msg, setMsg] = useState<string | null>(null);
  const run = (fn: (s: GameState) => string | null) => {
    const err = fn(structuredClone(ctx.state));
    setMsg(err);
    if (!err) ctx.update((d) => { fn(d); });
  };
  return { msg, run };
}

export function ItemName({ id }: { id: string }) {
  return <span className={`item-name ${QUALITY_CLASS[qualityOf(id)]}`}>{itemName(id)}</span>;
}

export function GearSearch(ctx: Ctx) {
  const { state } = ctx;
  const { msg, run } = useAction(ctx);
  const [kind, setKind] = useState<ItemKind | "">("");
  const [minQ, setMinQ] = useState(0);
  const [nation, setNation] = useState("");
  const [maxPrice, setMaxPrice] = useState("");
  const [name, setName] = useState("");
  const [sort, setSort] = useState<"price" | "quality">("quality");
  const me = state.ludi[state.playerLudusId];

  const list = gearMarketOf(state)
    .filter((l) => !kind || itemKind(l.item) === kind)
    .filter((l) => qualityOf(l.item) >= minQ)
    .filter((l) => !nation || l.nation === nation)
    .filter((l) => !maxPrice || l.price <= Number(maxPrice))
    .filter((l) => !name || itemName(l.item).includes(name))
    .sort((a, b) => (sort === "price" ? a.price - b.price : qualityOf(b.item) - qualityOf(a.item) || a.price - b.price));

  return (
    <>
      <div className="panel">
        <h3>매물 검색</h3>
        <div className="pad search-filters">
          <label>종류 <select value={kind} onChange={(e) => setKind(e.target.value as ItemKind | "")}>
            <option value="">전부</option>{Object.entries(ITEM_KIND_NAMES).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </select></label>
          <label>품질 ≥ <select value={minQ} onChange={(e) => setMinQ(Number(e.target.value))}>
            {QUALITY_NAMES.slice(0, 3).map((q, i) => <option key={i} value={i}>{q || "보통"}</option>)}
          </select></label>
          <label>파는 나라 <select value={nation} onChange={(e) => setNation(e.target.value)}>
            <option value="">전부</option>{NATIONS.map((n) => <option key={n.id} value={n.id}>{n.name}</option>)}
          </select></label>
          <label>가격 ≤ <input type="number" value={maxPrice} onChange={(e) => setMaxPrice(e.target.value)} placeholder="제한 없음" /></label>
          <label>이름 <input type="text" value={name} onChange={(e) => setName(e.target.value)} /></label>
          <label>정렬 <select value={sort} onChange={(e) => setSort(e.target.value as typeof sort)}>
            <option value="quality">품질 높은 순</option><option value="price">싼 순</option>
          </select></label>
        </div>
      </div>
      <div className="panel">
        <h3>매물 {list.length}개 · 금고 {me.treasury.toLocaleString()} D · 매물은 급료일마다 바뀐다</h3>
        {msg && <div className="pad"><span className="tag neg">{msg}</span></div>}
        <table className="t">
          <thead><tr><th>장비</th><th>종류</th><th>성능</th><th>판매자</th><th className="num">가격</th><th /></tr></thead>
          <tbody>
            {list.length === 0 && <tr><td colSpan={6} className="hint">조건에 맞는 매물이 없다.</td></tr>}
            {list.map((l) => (
              <tr key={l.id}>
                <td><ItemName id={l.item} /></td>
                <td>{ITEM_KIND_NAMES[itemKind(l.item)!]}</td>
                <td className="hint">{itemStats(l.item)}</td>
                <td>{l.seller}</td>
                <td className="num">{l.price.toLocaleString()} D</td>
                <td><button className="btn primary" disabled={me.treasury < l.price} onClick={() => run((s) => buyListing(s, l.id))}>사기</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

export function ArmoryPanel(ctx: Ctx) {
  const { msg, run } = useAction(ctx);
  const items = [...armoryOf(structuredClone(ctx.state))].sort((a, b) => qualityOf(b) - qualityOf(a) || itemName(a).localeCompare(itemName(b)));
  return (
    <div className="panel">
      <h3>창고 · {items.length}점 (착용은 막사 → 검투사 화면에서)</h3>
      {msg && <div className="pad"><span className="tag neg">{msg}</span></div>}
      <table className="t">
        <thead><tr><th>장비</th><th>종류</th><th>성능</th><th className="num">팔면</th><th /></tr></thead>
        <tbody>
          {items.length === 0 && <tr><td colSpan={5} className="hint">창고가 비었다. 장비 시장에서 사거나, 던전 원정·사투에서 거둬 온다.</td></tr>}
          {items.map((id, i) => (
            <tr key={`${id}${i}`}>
              <td><ItemName id={id} /></td>
              <td>{ITEM_KIND_NAMES[itemKind(id)!]}</td>
              <td className="hint">{itemStats(id)}</td>
              <td className="num">{Math.round(itemPrice(id) * 0.35).toLocaleString()} D</td>
              <td><button className="btn" onClick={() => run((s) => sellItem(s, id))}>팔기</button></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

const SLOTS: Slot[] = ["main", "off", "armor", "helmet"];

/** 검투사 장비 칸: 창고에서 맞는 것을 골라 끼운다 */
export function EquipPanel({ ctx, g, editable }: { ctx: Ctx; g: Gladiator; editable: boolean }) {
  const { msg, run } = useAction(ctx);
  const armory = armoryOf(structuredClone(ctx.state));
  return (
    <div className="panel">
      <h3>장비{g.deployment ? " (파견 중)" : ""}</h3>
      <div className="pad equip-grid">
        {SLOTS.map((slot) => {
          const cur = g.equipment[slot];
          const options = [...new Set(armory)].filter((id) => !cannotEquip(g, slot, id));
          return (
            <div key={slot} className="equip-slot">
              <div className="slot-name">{SLOT_NAMES[slot]}</div>
              <div className="slot-item">{cur ? <ItemName id={cur} /> : <span className="hint">비었다</span>}</div>
              {cur && <div className="hint">{itemStats(cur)}</div>}
              {editable && !g.deployment && (
                <select value="" onChange={(e) => {
                  const v = e.target.value;
                  run((s) => equipItem(s, g.id, slot, v === "__none" ? null : v));
                }}>
                  <option value="">{options.length ? `바꾸기 (${options.length})` : "창고에 맞는 장비 없음"}</option>
                  {options.map((id) => <option key={id} value={id}>{itemName(id)} — {itemStats(id)}</option>)}
                  {cur && slot !== "main" && slot !== "armor" && <option value="__none">벗기</option>}
                  {cur && (slot === "main" || slot === "armor") && <option value="__none">기본 장비로</option>}
                </select>
              )}
            </div>
          );
        })}
      </div>
      {msg && <div className="pad"><span className="tag neg">{msg}</span></div>}
    </div>
  );
}
