import type { NationId } from "../core/types";

const BASE = import.meta.env.BASE_URL;
export const asset = (path: string) => `${BASE}assets/${path}`;

export const crestUrl = (n: NationId) => asset(`crest/${n}.webp`);
export const menuIconUrl = (id: string) => asset(`icon/menu/${id}.webp`);
