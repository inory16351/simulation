
const BASE = import.meta.env.BASE_URL;
// 단일 HTML 빌드에서는 이미지가 data URI로 들어 있다 (tools/build-single.mjs)
const INLINE = (globalThis as { __INLINE_ASSETS__?: Record<string, string> }).__INLINE_ASSETS__;
export const asset = (path: string) => INLINE?.[path] ?? `${BASE}assets/${path}`;

