export interface TocItem {
  id: string;
  text: string;
  level: 2 | 3;
  /** Dòng (từ 1) của tiêu đề trong tài liệu, để gắn đúng mã neo khi tiêu đề trùng chữ. */
  line: number;
}

/** Bỏ ký hiệu định dạng trong dòng tiêu đề để lấy chữ hiển thị. */
export function plainHeading(raw: string): string {
  return raw
    .replace(/\[([^\]]+)\]\([^)]*\)/g, "$1")
    .replace(/[*_`]/g, "")
    .trim();
}

// Dựng bằng RegExp vì tsconfig target thấp không cho viết cờ u trực tiếp.
const KY_TU_BO = new RegExp("[^\\p{L}\\p{N}\\p{M}\\s_-]", "gu");

/** Mã neo theo kiểu GitHub (giữ chữ có dấu), khớp với các liên kết `#...` trong tài liệu. */
export function slugify(text: string): string {
  return text
    .normalize("NFC")
    .toLowerCase()
    .replace(KY_TU_BO, "")
    .trim()
    .replace(/\s/g, "-");
}

/** Mục lục từ các tiêu đề cấp 2 và 3, bỏ qua khối mã. */
export function buildToc(markdown: string): TocItem[] {
  const items: TocItem[] = [];
  const seen = new Map<string, number>();
  let inFence = false;
  const lines = markdown.split(/\r?\n/);
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    if (/^\s*```/.test(line)) inFence = !inFence;
    if (inFence) continue;
    const m = /^(#{2,3})\s+(.+?)\s*#*\s*$/.exec(line);
    if (!m) continue;
    const text = plainHeading(m[2]);
    const base = slugify(text);
    const n = seen.get(base) ?? 0;
    seen.set(base, n + 1);
    items.push({ id: n === 0 ? base : `${base}-${n}`, text, level: m[1].length as 2 | 3, line: i + 1 });
  }
  return items;
}
