// Chép hướng dẫn sử dụng (nguồn duy nhất: docs/huong-dan-su-dung.md) vào content/ để trang /help đọc lúc build.
import { copyFileSync, existsSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const goc = join(dirname(fileURLToPath(import.meta.url)), "..");
const nguon = join(goc, "..", "docs", "huong-dan-su-dung.md");
const dich = join(goc, "content", "huong-dan-su-dung.md");

if (existsSync(nguon)) {
  mkdirSync(dirname(dich), { recursive: true });
  copyFileSync(nguon, dich);
  console.log("Đã chép hướng dẫn sử dụng vào content/.");
} else if (existsSync(dich)) {
  console.warn("Không thấy docs/huong-dan-su-dung.md, dùng bản đã chép trong content/.");
} else {
  console.error("Không có docs/huong-dan-su-dung.md để chép vào content/ (build Docker cần COPY file này).");
  process.exit(1);
}
