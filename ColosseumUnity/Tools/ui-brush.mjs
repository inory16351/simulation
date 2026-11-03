// UI 패널·버튼용 9-slice 붓 그림 (Unity판). 밝은 회색으로 그려 Image.color(테마 색)가 그 위에 칠해진다.
//   brush_frame.png  테두리: 바깥 먹선 + 붓결 띠, 안쪽은 투명 (가장자리 12px 9-slice)
//   brush_fill.png   바탕: 붓결 있는 면, 가장자리로 갈수록 살짝 어둡게 (가장자리 16px 9-slice)
//   brush_head.png   제목 줄: 가로 붓질 띠 (가장자리 12px 9-slice)
// 사용 (저장소 루트에서): node ColosseumUnity/Tools/ui-brush.mjs   (9-slice 경계는 Editor/ArtImporter.cs가 넣는다)
import sharp from "sharp";

const OUT = "ColosseumUnity/Assets/Resources/Art/ui";
const N = 64;

const hash = (x, y, s = 0) => {
  let h = (x * 374761393 + y * 668265263 + s * 2246822519) | 0;
  h = (h ^ (h >>> 13)) * 1274126177;
  return ((h ^ (h >>> 16)) >>> 0) / 4294967295;
};
// 가로로 긴 붓결: 몇 픽셀씩 같은 값이 이어진다 (9-slice로 늘려도 결이 자연스럽게)
const stroke = (x, y, s) => hash(Math.floor(x / 9), y, s) * 0.65 + hash(x, Math.floor(y / 2), s + 1) * 0.35;
function sdBox(x, y, half, r) {
  const qx = Math.abs(x) - half + r, qy = Math.abs(y) - half + r;
  return Math.hypot(Math.max(qx, 0), Math.max(qy, 0)) + Math.min(Math.max(qx, qy), 0) - r;
}

async function draw(name, fn) {
  const buf = Buffer.alloc(N * N * 4);
  for (let y = 0; y < N; y++)
    for (let x = 0; x < N; x++) {
      const [v, a] = fn(x, y);
      const p = (y * N + x) * 4;
      buf[p] = buf[p + 1] = buf[p + 2] = Math.max(0, Math.min(255, Math.round(v * 255)));
      buf[p + 3] = Math.max(0, Math.min(255, Math.round(a * 255)));
    }
  await sharp(buf, { raw: { width: N, height: N, channels: 4 } }).png().toFile(`${OUT}/${name}.png`);
}

// 테두리: 바깥 1.5px 먹선, 안쪽 3px 붓결 띠(밝게, 왼쪽 위가 더 밝음), 그 안쪽은 투명
await draw("brush_frame", (x, y) => {
  const cx = x - N / 2 + 0.5, cy = y - N / 2 + 0.5;
  const d = sdBox(cx, cy, N / 2 - 1, 5) + (hash(Math.floor((x + y) / 4), 3) - 0.5) * 1.2;
  if (d > 0) return [0, Math.max(0, 1 - d)];
  if (d > -1.6) return [0.08, 1];
  if (d > -4.6) {
    const lit = (-cx - cy) / N;
    return [0.78 + lit * 0.25 + (stroke(x, y, 5) - 0.5) * 0.22, 1];
  }
  if (d > -5.6) return [0.15, 0.55];
  return [0, 0];
});

// 바탕: 0.86~1 사이 붓결, 가장자리로 어두워짐, 모서리는 둥글게 (테두리 안쪽에 깔린다)
await draw("brush_fill", (x, y) => {
  const cx = x - N / 2 + 0.5, cy = y - N / 2 + 0.5;
  const d = sdBox(cx, cy, N / 2 - 0.5, 4);
  if (d > 0) return [0, Math.max(0, 1 - d)];
  const edge = Math.max(0, 1 + d / 14);
  return [0.93 + (stroke(x, y, 9) - 0.5) * 0.14 - edge * 0.12, 1];
});

// 제목 줄: 가로 붓질 띠, 위쪽이 조금 밝다
await draw("brush_head", (x, y) => {
  const cx = x - N / 2 + 0.5, cy = y - N / 2 + 0.5;
  const d = sdBox(cx, cy, N / 2 - 0.5, 3) + (hash(Math.floor(x / 5), 11) - 0.5) * 1.4;
  if (d > 0) return [0, Math.max(0, 1 - d)];
  return [0.88 + (stroke(x, y, 13) - 0.5) * 0.2 - (cy / N) * 0.12, 1];
});
console.log(`${OUT}/brush_frame.png, brush_fill.png, brush_head.png`);
