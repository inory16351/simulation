import type { AvatarSpec } from "../../core/avatar";

const INK = "#1a120e";
const SW = 1.6;
const ink = { stroke: INK, strokeWidth: SW, strokeLinejoin: "round" as const, strokeLinecap: "round" as const };

/** 피부색을 조금 밝게/어둡게 (주둥이, 귀 안쪽 등) */
function shade(hex: string, amt: number): string {
  const n = parseInt(hex.slice(1), 16);
  const f = (c: number) => Math.max(0, Math.min(255, Math.round(c + amt * 255)));
  return `#${[(n >> 16) & 255, (n >> 8) & 255, n & 255].map(f).map((v) => v.toString(16).padStart(2, "0")).join("")}`;
}

interface Props {
  spec: AvatarSpec;
  bg: string;
  scars?: number;
  injured?: "light" | "serious";
  dead?: boolean;
  seed?: number;
}

/** 실크송·림월드풍 귀여운 검투사 초상화 (100×100) */
export function AvatarSvg({ spec: s, bg, scars = 0, injured, dead, seed = 1 }: Props) {
  const v = Number(s.variant) || 0;
  const beastly = s.race === "minotaur" || s.race === "lizardfolk" || s.race === "beastkin";
  const headCy = s.race === "dwarf" ? 46 : 44;
  const eyeY = s.race === "lizardfolk" ? 40 : beastly ? 43 : s.race === "dwarf" ? 46 : 46;
  const eyeDx = s.race === "lizardfolk" ? 11 : s.race === "minotaur" ? 10 : 9;
  const id = `a${seed}`;

  return (
    <svg viewBox="0 0 100 100" className={dead ? "avatar dead" : "avatar"}>
      <defs>
        <radialGradient id={`${id}bg`} cx="50%" cy="38%" r="72%">
          <stop offset="0" stopColor={bg} stopOpacity="0.85" />
          <stop offset="1" stopColor="#120d0a" />
        </radialGradient>
      </defs>
      <rect width="100" height="100" fill={`url(#${id}bg)`} />

      {/* 뒷머리·갈기 */}
      {s.race === "beastkin" && v === 0 && <Mane color={s.race === "beastkin" ? shade(s.skin, -0.18) : s.hairColor} />}
      {!s.helmet && s.hair === "long" && <path d="M27 40 C23 60 25 72 31 78 L40 75 C35 64 35 52 35 44 Z M73 40 C77 60 75 72 69 78 L60 75 C65 64 65 52 65 44 Z" fill={s.hairColor} {...ink} />}
      {!s.helmet && s.hair === "pony" && <path d="M66 26 C82 30 86 50 80 70 C78 60 74 50 66 42 Z" fill={s.hairColor} {...ink} />}
      {!s.helmet && s.hair === "braids" && (
        <g fill={s.hairColor} {...ink}>
          {[52, 60, 68, 76].map((y) => <ellipse key={`l${y}`} cx="26" cy={y} rx="4.5" ry="5" />)}
          {[52, 60, 68, 76].map((y) => <ellipse key={`r${y}`} cx="74" cy={y} rx="4.5" ry="5" />)}
        </g>
      )}

      {/* 몸 */}
      <Body spec={s} />

      {/* 귀·뿔 (머리 뒤) */}
      <EarsAndHorns s={s} v={v} />

      {/* 머리 */}
      <Head s={s} v={v} cy={headCy} />

      {/* 볼 홍조 */}
      {!beastly && <g fill="#e07a6a" opacity="0.22"><ellipse cx={50 - eyeDx - 3} cy={eyeY + 7} rx="4" ry="2.4" /><ellipse cx={50 + eyeDx + 3} cy={eyeY + 7} rx="4" ry="2.4" /></g>}

      {/* 수염 (입 아래) */}
      {s.beard && <Beard style={s.beard} color={s.hairColor} />}

      {/* 코 */}
      {s.race === "dwarf" && <ellipse cx="50" cy="53" rx="4.2" ry="3.6" fill={shade(s.skin, -0.06)} {...ink} />}
      {(s.race === "human" || s.race === "elf") && <path d="M49 51 Q50 53.5 51.5 51.5" fill="none" {...ink} strokeWidth={1.2} />}

      {/* 입 */}
      {s.mouth && <Mouth style={s.mouth} y={s.race === "dwarf" ? 59 : 57} orc={s.race === "orc"} />}

      {/* 눈 */}
      <Eyes s={s} y={eyeY} dx={eyeDx} />

      {/* 앞머리 */}
      {!s.helmet && s.hair && <FrontHair style={s.hair} color={s.hairColor} />}

      {/* 투구 */}
      {s.helmet && <Helmet style={s.helmet} cloth={s.cloth} />}

      {/* 흉터·부상 */}
      {Array.from({ length: Math.min(4, scars) }, (_, i) => {
        const x = 36 + ((seed * 7 + i * 13) % 26), y = 34 + ((seed * 3 + i * 11) % 20);
        return <path key={i} d={`M${x} ${y} l5 6 M${x + 1} ${y + 3} l3 -1`} stroke="#7a1e1a" strokeWidth="1.3" fill="none" strokeLinecap="round" />;
      })}
      {injured && (
        <g>
          <path d="M26 33 C38 27 62 27 74 33 L73 39 C62 34 38 34 27 39 Z" fill="#e8dcc4" {...ink} strokeWidth={1.2} />
          <ellipse cx="62" cy="34" rx="3" ry="2" fill="#a3241d" />
          {injured === "serious" && <path d="M62 36 C62 42 60 44 61 48" stroke="#a3241d" strokeWidth="1.8" fill="none" strokeLinecap="round" />}
        </g>
      )}
    </svg>
  );
}

function Mane({ color }: { color: string }) {
  const pts: string[] = [];
  for (let i = 0; i < 18; i++) {
    const a = (i / 18) * Math.PI * 2;
    const rad = i % 2 ? 26 : 34;
    pts.push(`${50 + Math.cos(a) * rad},${44 + Math.sin(a) * rad * 0.95}`);
  }
  return <polygon points={pts.join(" ")} fill={color} {...ink} />;
}

function Body({ spec: s }: { spec: AvatarSpec }) {
  const base = "M16 100 C16 84 30 76 50 76 C70 76 84 84 84 100 Z";
  switch (s.armor) {
    case "heavy":
      return (
        <g>
          <path d={base} fill="#a8823f" {...ink} />
          <path d="M40 78 L50 92 L60 78" fill="none" stroke="#7a5c33" strokeWidth="1.5" />
          <path d="M30 79 C40 86 60 86 70 79" fill="none" stroke={s.cloth} strokeWidth="4" />
          <circle cx="25" cy="86" r="9" fill="#c49a52" {...ink} />
          <circle cx="75" cy="86" r="9" fill="#c49a52" {...ink} />
          <circle cx="25" cy="86" r="2" fill="#7a5c33" /><circle cx="75" cy="86" r="2" fill="#7a5c33" />
        </g>
      );
    case "medium":
      return (
        <g>
          <path d={base} fill="#6f7378" {...ink} />
          {[84, 89, 94, 99].map((y) => <path key={y} d={`M24 ${y} Q50 ${y - 3} 76 ${y}`} fill="none" stroke="#50545a" strokeWidth="1" />)}
          <path d="M16 100 C16 86 24 79 34 77 L36 100 Z M84 100 C84 86 76 79 66 77 L64 100 Z" fill={s.cloth} {...ink} />
        </g>
      );
    case "robe":
      return (
        <g>
          <path d={base} fill={shade(s.cloth, -0.08)} {...ink} />
          <path d="M38 77 L50 96 L62 77" fill="none" stroke="#c9a45a" strokeWidth="2.4" />
          <circle cx="50" cy="94" r="3" fill="#c9a45a" {...ink} strokeWidth={1} />
        </g>
      );
    default:
      return (
        <g>
          <path d={base} fill={s.cloth} {...ink} />
          <path d="M30 80 L70 100" stroke="#5a3b22" strokeWidth="4" />
          <path d="M30 80 L70 100" stroke={INK} strokeWidth="0.8" opacity="0.5" />
        </g>
      );
  }
}

function EarsAndHorns({ s, v }: { s: AvatarSpec; v: number }) {
  const skin = s.skin;
  switch (s.race) {
    case "human":
    case "dwarf":
      return <g fill={skin} {...ink}><ellipse cx="26" cy="47" rx="4" ry="5.5" /><ellipse cx="74" cy="47" rx="4" ry="5.5" /></g>;
    case "elf":
      return <g fill={skin} {...ink}><path d="M30 42 L6 28 L29 52 Z" /><path d="M70 42 L94 28 L71 52 Z" /></g>;
    case "orc":
      return <g fill={skin} {...ink}><path d="M28 42 L12 38 L28 52 Z" /><path d="M72 42 L88 38 L72 52 Z" /></g>;
    case "minotaur":
      return (
        <g {...ink}>
          <path d="M33 30 C22 28 13 21 11 8 C20 17 27 20 38 24 Z" fill="#e6d8b8" />
          <path d="M67 30 C78 28 87 21 89 8 C80 17 73 20 62 24 Z" fill={v === 1 ? "#e6d8b8" : "#e6d8b8"} />
          <ellipse cx="24" cy="38" rx="7" ry="3.6" fill={skin} transform="rotate(-20 24 38)" />
          <ellipse cx="76" cy="38" rx="7" ry="3.6" fill={skin} transform="rotate(20 76 38)" />
        </g>
      );
    case "beastkin":
      return v === 1 ? (
        <g {...ink}>
          <path d="M35 30 L28 3 L46 24 Z" fill={skin} /><path d="M65 30 L72 3 L54 24 Z" fill={skin} />
          <path d="M35 26 L31 10 L42 23 Z" fill={shade(skin, 0.15)} stroke="none" /><path d="M65 26 L69 10 L58 23 Z" fill={shade(skin, 0.15)} stroke="none" />
        </g>
      ) : (
        <g fill={skin} {...ink}><circle cx="33" cy="26" r="6" /><circle cx="67" cy="26" r="6" /></g>
      );
    case "lizardfolk":
      return (
        <g fill={shade(skin, -0.15)} {...ink}>
          {v === 0
            ? [36, 43, 50, 57, 64].map((x, i) => <path key={x} d={`M${x - 4} 26 L${x} ${i === 2 ? 8 : 14} L${x + 4} 26 Z`} />)
            : <><path d="M30 34 L14 22 L30 46 Z" /><path d="M70 34 L86 22 L70 46 Z" /></>}
        </g>
      );
  }
}

function Head({ s, v, cy }: { s: AvatarSpec; v: number; cy: number }) {
  const skin = s.skin;
  switch (s.race) {
    case "dwarf":
      return <ellipse cx="50" cy={cy} rx="25" ry="21" fill={skin} {...ink} />;
    case "orc":
      return <path d="M28 40 C28 24 38 20 50 20 C62 20 72 24 72 40 C74 54 70 66 50 67 C30 66 26 54 28 40 Z" fill={skin} {...ink} />;
    case "minotaur":
      return (
        <g {...ink}>
          <ellipse cx="50" cy="42" rx="23" ry="21" fill={skin} />
          <ellipse cx="50" cy="56" rx="14" ry="9" fill={shade(skin, 0.18)} />
          <ellipse cx="45" cy="56" rx="2" ry="2.6" fill={INK} stroke="none" /><ellipse cx="55" cy="56" rx="2" ry="2.6" fill={INK} stroke="none" />
          <circle cx="50" cy="62" r="3.6" fill="none" stroke="#c9a45a" strokeWidth="1.8" />
        </g>
      );
    case "lizardfolk":
      return (
        <g {...ink}>
          <ellipse cx="50" cy="40" rx="21" ry="19" fill={skin} />
          <path d="M34 48 C36 62 44 66 50 66 C56 66 64 62 66 48 Z" fill={skin} />
          <path d="M40 60 Q50 63 60 60" fill="none" strokeWidth={1.2} />
          <circle cx="46" cy="54" r="0.9" fill={INK} /><circle cx="54" cy="54" r="0.9" fill={INK} />
        </g>
      );
    case "beastkin":
      return (
        <g {...ink}>
          <ellipse cx="50" cy="44" rx="20" ry="19" fill={s.skin} />
          {v === 1
            ? <path d="M42 50 C44 64 48 66 50 66 C52 66 56 64 58 50 Z" fill={s.skin} />
            : <ellipse cx="50" cy="55" rx="9" ry="6.5" fill={shade(s.skin, 0.2)} />}
          <path d="M47 52 L53 52 L50 55.5 Z" fill={INK} stroke="none" />
        </g>
      );
    case "elf":
      return <ellipse cx="50" cy={cy} rx="21" ry="23" fill={skin} {...ink} />;
    default:
      return <ellipse cx="50" cy={cy} rx="23" ry="22" fill={skin} {...ink} />;
  }
}

function Eyes({ s, y, dx }: { s: AvatarSpec; y: number; dx: number }) {
  const xs = [50 - dx, 50 + dx];
  // 짐승형 종족: 빛나는 눈
  if (s.race === "lizardfolk" || s.race === "beastkin") {
    return (
      <g>
        {xs.map((x, i) => (s.oneEyed && i === 1 ? null : (
          <g key={x}>
            <ellipse cx={x} cy={y} rx="4.4" ry="4.6" fill="#f2b632" {...ink} strokeWidth={1.3} />
            <ellipse cx={x} cy={y} rx="1.1" ry="3.6" fill={INK} />
          </g>
        )))}
        {s.oneEyed && <Patch x={xs[1]} y={y} />}
      </g>
    );
  }
  if (s.race === "minotaur") {
    return (
      <g>
        {xs.map((x, i) => (s.oneEyed && i === 1 ? null : (
          <g key={x}><circle cx={x} cy={y} r="3.4" fill={INK} /><circle cx={x - 1} cy={y - 1} r="1" fill="#e04a2a" /></g>
        )))}
        <path d={`M${xs[0] - 6} ${y - 6} L${xs[0] + 4} ${y - 3} M${xs[1] + 6} ${y - 6} L${xs[1] - 4} ${y - 3}`} stroke={INK} strokeWidth="2.4" strokeLinecap="round" />
        {s.oneEyed && <Patch x={xs[1]} y={y} />}
      </g>
    );
  }
  const big = s.eyes === "wide" ? 1.15 : 1;
  return (
    <g>
      {xs.map((x, i) => {
        if (s.oneEyed && i === 1) return null;
        if (s.eyes === "sleepy") return <path key={x} d={`M${x - 4.5} ${y} Q${x} ${y + 4.5} ${x + 4.5} ${y}`} fill={INK} stroke={INK} strokeWidth="1.2" />;
        return (
          <g key={x}>
            <ellipse cx={x} cy={y} rx={4 * big} ry={5 * big} fill={INK} />
            <ellipse cx={x} cy={y + 1} rx={2.6 * big} ry={3.2 * big} fill={s.eyeColor} opacity="0.55" />
            <circle cx={x - 1.4} cy={y - 2} r={1.5 * big} fill="#fff" />
            {s.female && <path d={`M${x + (i ? 3 : -3)} ${y - 4} l${i ? 2.5 : -2.5} -2`} stroke={INK} strokeWidth="1.3" strokeLinecap="round" />}
          </g>
        );
      })}
      {(s.eyes === "fierce" || s.race === "orc") && (
        <path d={`M${xs[0] - 6} ${y - 8} L${xs[0] + 4} ${y - 5} M${xs[1] + 6} ${y - 8} L${xs[1] - 4} ${y - 5}`} stroke={s.race === "orc" ? INK : s.hairColor} strokeWidth="2.6" strokeLinecap="round" />
      )}
      {s.oneEyed && <Patch x={xs[1]} y={y} />}
    </g>
  );
}

function Patch({ x, y }: { x: number; y: number }) {
  return (
    <g>
      <path d={`M24 ${y - 9} L76 ${y + 3}`} stroke={INK} strokeWidth="1.4" />
      <ellipse cx={x} cy={y} rx="5.2" ry="5" fill="#2a1d16" {...ink} />
    </g>
  );
}

function Mouth({ style, y, orc }: { style: string; y: number; orc: boolean }) {
  const m = {
    line: <path d={`M46 ${y} L54 ${y}`} fill="none" {...ink} />,
    smile: <path d={`M45.5 ${y - 1} Q50 ${y + 3} 54.5 ${y - 1}`} fill="none" {...ink} />,
    frown: <path d={`M45.5 ${y + 1} Q50 ${y - 2.5} 54.5 ${y + 1}`} fill="none" {...ink} />,
    open: <ellipse cx="50" cy={y} rx="3" ry="2.6" fill="#5a1a14" {...ink} strokeWidth={1.2} />,
    grit: <g><rect x="45" y={y - 2} width="10" height="4" rx="1.5" fill="#f2ead8" {...ink} strokeWidth={1.2} /><path d={`M48.3 ${y - 2} V${y + 2} M51.7 ${y - 2} V${y + 2}`} stroke={INK} strokeWidth="0.8" /></g>,
  }[style];
  return (
    <g>
      {m}
      {orc && <g fill="#f4ecd8" {...ink} strokeWidth={1.1}><path d={`M43 ${y + 1} L44.5 ${y - 6} L46.5 ${y + 1} Z`} /><path d={`M53.5 ${y + 1} L55.5 ${y - 6} L57 ${y + 1} Z`} /></g>}
    </g>
  );
}

function Beard({ style, color }: { style: string; color: string }) {
  switch (style) {
    case "stubble":
      return <path d="M30 52 C32 64 40 68 50 68 C60 68 68 64 70 52 C64 60 58 62 50 62 C42 62 36 60 30 52 Z" fill={color} opacity="0.35" />;
    case "goatee":
      return <path d="M44 55 Q50 53 56 55 L54 60 L52 68 L48 68 L46 60 Z" fill={color} {...ink} strokeWidth={1.2} />;
    case "full":
      return <path d="M27 48 C27 72 38 82 50 82 C62 82 73 72 73 48 C66 58 60 60 50 60 C40 60 34 58 27 48 Z" fill={color} {...ink} />;
    case "braided":
      return (
        <g fill={color} {...ink}>
          <path d="M26 48 C26 70 36 78 50 78 C64 78 74 70 74 48 C66 58 60 60 50 60 C40 60 34 58 26 48 Z" />
          {[82, 88, 94].map((y) => <ellipse key={y} cx="50" cy={y} rx="4" ry="3.6" />)}
          <rect x="45.5" y="84" width="9" height="2.4" rx="1" fill="#c9a45a" />
        </g>
      );
    default:
      return <path d="M30 52 C31 66 40 72 50 72 C60 72 69 66 70 52 C64 60 58 62 50 62 C42 62 36 60 30 52 Z" fill={color} {...ink} />;
  }
}

function FrontHair({ style, color }: { style: string; color: string }) {
  const cap = "M27 44 C26 25 38 18 50 18 C62 18 74 25 73 44 C68 33 59 29 50 29 C41 29 32 33 27 44 Z";
  const p = { fill: color, ...ink };
  switch (style) {
    case "mohawk":
      return <path d="M45 30 L43 12 L47.5 17 L50 6 L52.5 17 L57 12 L55 30 Z" {...p} />;
    case "tuft":
      return <g {...p}><path d={cap} /><path d="M41 22 L44 10 L49 19 L54 9 L58 21 Z" /></g>;
    case "messy":
      return <path d="M26 46 L29 34 L33 39 L36 30 L41 37 L45 28 L50 36 L55 28 L59 37 L64 30 L67 39 L71 34 L74 46 C75 25 62 16 50 16 C38 16 25 25 26 46 Z" {...p} />;
    case "bun":
      return <g {...p}><circle cx="50" cy="15" r="7" /><path d={cap} /></g>;
    default:
      return <path d={cap} {...p} />;
  }
}

function Helmet({ style, cloth }: { style: string; cloth: string }) {
  switch (style) {
    case "crest":
      return (
        <g {...ink}>
          <path d="M28 24 C34 4 66 4 72 24 C64 14 36 14 28 24 Z" fill="#a3241d" />
          <path d="M24 42 C24 20 36 14 50 14 C64 14 76 20 76 42 L70 40 C68 28 60 24 50 24 C40 24 32 28 30 40 Z" fill="#b08a4f" />
          <path d="M20 42 L80 42" strokeWidth={3} stroke="#7a5c33" />
        </g>
      );
    case "horned":
      return (
        <g {...ink}>
          <path d="M28 30 C18 26 12 16 14 6 C20 16 26 20 34 22 Z M72 30 C82 26 88 16 86 6 C80 16 74 20 66 22 Z" fill="#e6d8b8" />
          <path d="M25 40 C25 20 37 14 50 14 C63 14 75 20 75 40 Z" fill="#6f7378" />
          <path d="M25 40 L75 40" stroke="#8a6a3a" strokeWidth={3} />
        </g>
      );
    case "cap":
      return (
        <g {...ink}>
          <path d="M25 40 C25 20 37 14 50 14 C63 14 75 20 75 40 Z" fill="#7d8185" />
          <rect x="47.5" y="38" width="5" height="12" rx="1.5" fill="#7d8185" />
          <circle cx="36" cy="30" r="1.2" fill={INK} /><circle cx="64" cy="30" r="1.2" fill={INK} />
        </g>
      );
    default:
      return <path d="M18 78 C12 40 26 10 50 10 C74 10 88 40 82 78 L70 76 C72 48 64 28 50 28 C36 28 28 48 30 76 Z" fill={cloth} {...ink} />;
  }
}
