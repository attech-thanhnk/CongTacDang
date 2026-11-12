// Gộp hướng dẫn theo vai trò (nguồn duy nhất: docs/dung-thu) vào content/ và chép ảnh vào public/ để trang /help dùng lúc build.
import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const goc = join(dirname(fileURLToPath(import.meta.url)), "..");
const nguon = join(goc, "..", "docs", "dung-thu");
const dich = join(goc, "content", "huong-dan-su-dung.md");
const anhDich = join(goc, "public", "huong-dan", "anh");
const kichThuocDich = join(goc, "content", "kich-thuoc-anh.json");

// Thứ tự chương trên trang /help.
const TEP = [
  "huong-dan-chung",
  "can-bo",
  "lanh-dao-don-vi",
  "chi-bo",
  "thu-ky",
  "tham-dinh",
  "cap-truc-tiep-su-dung",
  "van-phong-dang-uy",
  "quan-tri",
];

const KY_TU_BO = new RegExp("[^\\p{L}\\p{N}\\p{M}\\s_-]", "gu");
const slug = (s) =>
  s.replace(/[*_`]/g, "").trim().normalize("NFC").toLowerCase().replace(KY_TU_BO, "").trim().replace(/\s/g, "-");

if (!existsSync(join(nguon, "can-bo.md"))) {
  if (existsSync(dich)) {
    console.warn("Không thấy docs/dung-thu, dùng bản đã chép trong content/.");
    process.exit(0);
  }
  console.error("Không có docs/dung-thu để chép vào content/ (build Docker cần COPY thư mục này).");
  process.exit(1);
}

const noiDung = TEP.map((ten) => ({ ten, md: readFileSync(join(nguon, `${ten}.md`), "utf-8") }));
// Mã neo chương theo tiêu đề cấp 1 của từng tệp, để liên kết giữa các tệp thành liên kết trong trang.
const neo = new Map(noiDung.map(({ ten, md }) => [ten, slug(/^#\s+(.+)$/m.exec(md)?.[1] ?? ten)]));

const chuong = noiDung.map(({ md }) => {
  let trongMa = false;
  return md
    .split(/\r?\n/)
    .map((dong) => {
      if (/^\s*```/.test(dong)) trongMa = !trongMa;
      if (trongMa) return dong;
      return dong
        .replace(/^(#{1,5})(\s)/, "#$1$2")
        .replace(/\]\(anh\//g, "](/huong-dan/anh/")
        .replace(/\]\(([a-z-]+)\.md(#[^)]*)?\)/g, (m, ten, hash) =>
          neo.has(ten) ? `](${hash ?? `#${neo.get(ten)}`})` : m,
        );
    })
    .join("\n");
});

mkdirSync(dirname(dich), { recursive: true });
writeFileSync(dich, `# Hướng dẫn sử dụng\n\n${chuong.join("\n\n")}\n`);
mkdirSync(anhDich, { recursive: true });
cpSync(join(nguon, "anh"), anhDich, { recursive: true, filter: (p) => !p.endsWith(".md") });
// Kích thước ảnh (đọc từ đầu tệp PNG) để trang giữ chỗ trước khi ảnh tải, neo # không bị lệch.
const kichThuoc = {};
for (const ten of readdirSync(join(nguon, "anh")).filter((t) => t.endsWith(".png"))) {
  const dau = readFileSync(join(nguon, "anh", ten)).subarray(16, 24);
  kichThuoc[`/huong-dan/anh/${ten}`] = [dau.readUInt32BE(0), dau.readUInt32BE(4)];
}
writeFileSync(kichThuocDich, JSON.stringify(kichThuoc));
console.log("Đã chép hướng dẫn sử dụng vào content/ và ảnh vào public/huong-dan/anh.");
