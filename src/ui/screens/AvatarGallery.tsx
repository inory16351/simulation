// 개발용: 종족별 무작위 초상화 모음 (?gallery 로 연다)
import { getNation } from "../../core/data";
import { generateGladiator } from "../../core/generators/gladiator";
import { Rng } from "../../core/rng";
import type { NationId, RaceId } from "../../core/types";
import { Portrait } from "../components/common";

const RACES: [RaceId, NationId][] = [
  ["human", "aurelia"], ["elf", "sylvaren"], ["dwarf", "dolgar"], ["orc", "grommar"],
  ["minotaur", "grommar"], ["lizardfolk", "karsea"], ["beastkin", "kemer"],
];

export function AvatarGallery() {
  const rng = new Rng(Number(new URLSearchParams(location.search).get("gallery")) || 7);
  return (
    <div style={{ padding: 20 }}>
      {RACES.map(([race, nation]) => (
        <div key={race} style={{ display: "flex", gap: 10, marginBottom: 10, alignItems: "center" }}>
          <div style={{ width: 90, color: "var(--parchment)" }}>{race}<br /><span className="hint">{getNation(nation).short}</span></div>
          {Array.from({ length: 4 }, (_, i) => {
            const g = generateGladiator(rng, { id: `${race}${i}`, nation, target: 9, race });
            if (i === 3) g.record.wins = 20;
            // 같은 검투사를 기존 그림 / 동화책 시안으로 나란히
            return (
              <div key={i} style={{ display: "flex", gap: 3, marginRight: 10 }}>
                <Portrait g={g} size={110} />
                <Portrait g={g} size={110} painted />
              </div>
            );
          })}
        </div>
      ))}
    </div>
  );
}
