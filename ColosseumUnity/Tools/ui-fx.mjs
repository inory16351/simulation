// 로비 연출용 그림: 비네트, 로고 그림자, 버튼·패널 밑 부드러운 그림자.
//   vignette.png     가장자리로 갈수록 검게 (가운데 투명), 화면 전체에 늘려 깐다
//   logo_shadow.png  logo.png 모양을 검게 번지게 (로고 뒤에 깐다)
//   soft_shadow.png  번진 둥근 사각형 (9-slice 24, 버튼·패널 뒤에 깐다)
// 사용 (저장소 루트에서): node ColosseumUnity/Tools/ui-fx.mjs   (logo.png가 먼저 있어야 한다)
import sharp from "sharp";

const OUT = "ColosseumUnity/Assets/Resources/Art/ui";

// 비네트: 타원 거리로 알파를 올린다 (가운데 0, 모서리 ~0.85)
{
  const W = 512, H = 512, buf = Buffer.alloc(W * H * 4);
  for (let y = 0; y < H; y++)
    for (let x = 0; x < W; x++) {
      const dx = (x / (W - 1) - 0.5) * 2, dy = (y / (H - 1) - 0.5) * 2;
      const d = Math.sqrt(dx * dx * 0.8 + dy * dy);
      const a = Math.min(1, Math.max(0, (d - 0.45) / 0.95)) ** 1.6 * 0.85;
      const p = (y * W + x) * 4;
      buf[p] = buf[p + 1] = buf[p + 2] = 0;
      buf[p + 3] = Math.round(a * 255);
    }
  await sharp(buf, { raw: { width: W, height: H, channels: 4 } }).png().toFile(`${OUT}/vignette.png`);
}

// 로고 그림자: 로고 알파만 검게, 여백을 두고 크게 번지게
{
  const pad = 60;
  const { data, info } = await sharp(`${OUT}/logo.png`).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  for (let i = 0; i < data.length; i += 4) { data[i] = data[i + 1] = data[i + 2] = 0; data[i + 3] = Math.round(data[i + 3] * 0.9); }
  await sharp(data, { raw: { width: info.width, height: info.height, channels: 4 } })
    .extend({ top: pad, bottom: pad, left: pad, right: pad, background: { r: 0, g: 0, b: 0, alpha: 0 } })
    .blur(22).png().toFile(`${OUT}/logo_shadow.png`);
}

// 부드러운 그림자: 가운데 검은 사각형을 크게 번지게 (가장자리 24px가 번진 부분)
{
  const N = 96, inset = 24;
  const box = await sharp({ create: { width: N - inset * 2, height: N - inset * 2, channels: 4, background: { r: 0, g: 0, b: 0, alpha: 0.85 } } }).png().toBuffer();
  await sharp({ create: { width: N, height: N, channels: 4, background: { r: 0, g: 0, b: 0, alpha: 0 } } })
    .composite([{ input: box, left: inset, top: inset }]).blur(9).png().toFile(`${OUT}/soft_shadow.png`);
}
console.log(`${OUT}/vignette.png, logo_shadow.png, soft_shadow.png`);
