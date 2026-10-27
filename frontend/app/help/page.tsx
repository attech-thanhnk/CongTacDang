import { readFileSync } from "node:fs";
import { join } from "node:path";
import { HelpView } from "@/components/help/HelpView";

// Nội dung đọc lúc build từ bản chép của docs/huong-dan-su-dung.md (xem scripts/chep-huong-dan.mjs).
export const dynamic = "force-static";

export const metadata = { title: "Hướng dẫn sử dụng" };

export default function HelpPage() {
  const markdown = readFileSync(join(process.cwd(), "content", "huong-dan-su-dung.md"), "utf-8");
  return <HelpView markdown={markdown} />;
}
