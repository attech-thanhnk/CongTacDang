// Chạy: npm i docx (thư mục tạm) rồi node build-phieu-xac-nhan.js docs/nghiep-vu/cau-hoi-xac-nhan.md docs/nghiep-vu/Phieu-xac-nhan-nghiep-vu.docx
// Dựng Phiếu xác nhận nghiệp vụ (.docx) từ docs/nghiep-vu/cau-hoi-xac-nhan.md
const fs = require("fs");
const {
  Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell, WidthType, BorderStyle,
  AlignmentType, HeadingLevel, ShadingType, Footer, PageNumber, LevelFormat, VerticalAlign,
} = require("docx");

const SRC = process.argv[2];
const OUT = process.argv[3];
const md = fs.readFileSync(SRC, "utf8").replace(/\r\n/g, "\n");

// ---------- Phân tích Markdown ----------
const lines = md.split("\n");
const intro = [];
const groups = [];
let group = null, q = null, field = null;
for (const raw of lines) {
  const line = raw.replace(/\s+$/, "");
  if (line.startsWith("# ")) continue;
  if (line.startsWith("## ")) { group = { title: line.slice(3).trim(), questions: [] }; groups.push(group); q = null; continue; }
  if (line.startsWith("### ")) {
    const t = line.slice(4).trim();
    const m = t.match(/^([A-Z]\d+)\.\s*(.*)$/);
    const priority = /\[Ưu tiên\]/.test(t);
    q = { id: m ? m[1] : "", title: (m ? m[2] : t).replace(/\[Ưu tiên\]\s*/, "").trim(), priority, fields: [] };
    group.questions.push(q); field = null; continue;
  }
  if (!group) { if (line.trim() !== "---") intro.push(line); continue; }
  if (!q) continue;
  const f = line.match(/^- \*\*(.+?):\*\*\s*(.*)$/);
  if (f) { field = { name: f[1].trim(), value: f[2].trim() }; q.fields.push(field); continue; }
  if (field && /^\s{2,}\S/.test(line)) { field.value += " " + line.trim(); continue; }
  if (field && /^\s*- /.test(line)) { field.value += "\n" + line.trim(); continue; }
}

// ---------- Tiện ích ----------
const FONT = "Times New Roman";
const runs = (text, base = {}) => {
  // **đậm** trong văn bản
  const parts = text.split(/(\*\*[^*]+\*\*)/g).filter(Boolean);
  return parts.map((p) => p.startsWith("**") && p.endsWith("**")
    ? new TextRun({ text: p.slice(2, -2), bold: true, font: FONT, ...base })
    : new TextRun({ text: p, font: FONT, ...base }));
};
const para = (text, opts = {}) => new Paragraph({ children: runs(text, opts.run || {}), spacing: { after: 80, line: 276 }, ...opts.p });
const border = { style: BorderStyle.SINGLE, size: 4, color: "808080" };
const cellBorders = { top: border, bottom: border, left: border, right: border };
const noBorder = { style: BorderStyle.NONE, size: 0, color: "FFFFFF" };
const noBorders = { top: noBorder, bottom: noBorder, left: noBorder, right: noBorder };

const PAGE_W = 11906, ML = 1701, MR = 1134;
const TW = PAGE_W - ML - MR; // 9071
const C1 = 2100, C2 = TW - C1;

const labelCell = (text) => new TableCell({
  width: { size: C1, type: WidthType.DXA }, borders: cellBorders,
  shading: { type: ShadingType.CLEAR, fill: "F2F2F2", color: "auto" },
  margins: { top: 60, bottom: 60, left: 100, right: 100 },
  children: [new Paragraph({ children: [new TextRun({ text, bold: true, font: FONT, size: 22 })] })],
});
const valueCell = (value) => {
  const paras = value.split("\n").map((v) => new Paragraph({ children: runs(v.replace(/^- /, "– "), { size: 22 }), spacing: { after: 40 } }));
  return new TableCell({ width: { size: C2, type: WidthType.DXA }, borders: cellBorders, margins: { top: 60, bottom: 60, left: 100, right: 100 }, children: paras });
};
const answerCell = () => new TableCell({
  width: { size: C2, type: WidthType.DXA }, borders: cellBorders, margins: { top: 80, bottom: 80, left: 100, right: 100 },
  children: [
    new Paragraph({ children: [new TextRun({ text: "☐  Đồng ý", font: FONT, size: 22 })], spacing: { after: 60 } }),
    new Paragraph({ children: [new TextRun({ text: "☐  Phương án khác / ý kiến:", font: FONT, size: 22 })], spacing: { after: 60 } }),
    ...[1, 2].map(() => new Paragraph({ children: [new TextRun({ text: " ", font: FONT, size: 22 })], border: { bottom: { style: BorderStyle.DOTTED, size: 6, color: "808080", space: 1 } }, spacing: { after: 120 } })),
  ],
});

// ---------- Nội dung ----------
const children = [];
children.push(new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 60 }, children: [new TextRun({ text: "PHIẾU XÁC NHẬN NGHIỆP VỤ", bold: true, size: 30, font: FONT })] }));
children.push(new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 60 }, children: [new TextRun({ text: "Hệ thống số hóa đánh giá, xếp loại chất lượng cán bộ hằng quý", bold: true, size: 26, font: FONT })] }));
children.push(new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 240 }, children: [new TextRun({ text: "(theo Hướng dẫn số 03-HD/TVĐU)", italics: true, size: 24, font: FONT })] }));

// Mở đầu
for (const l of intro) {
  if (!l.trim()) continue;
  if (/^\s*- /.test(l)) children.push(new Paragraph({ numbering: { reference: "bul", level: /^\s{2,}/.test(l) ? 1 : 0 }, children: runs(l.replace(/^\s*- /, ""), { size: 24 }), spacing: { after: 60 } }));
  else if (/^\s{2,}\S/.test(l) && children.length) {
    const last = children[children.length - 1];
    last.addChildElement(new TextRun({ text: " " + l.trim(), font: FONT, size: 24 }));
  } else children.push(new Paragraph({ children: runs(l.trim(), { size: 24 }), spacing: { after: 100 }, alignment: AlignmentType.JUSTIFIED }));
}

// Bảng tổng hợp
const total = groups.reduce((s, g) => s + g.questions.length, 0);
const totalP = groups.reduce((s, g) => s + g.questions.filter((x) => x.priority).length, 0);
children.push(new Paragraph({ spacing: { before: 200, after: 100 }, children: [new TextRun({ text: `Tổng hợp: ${total} câu, trong đó ${totalP} câu [Ưu tiên]`, bold: true, font: FONT, size: 24 })] }));
const SW = [6271, 1400, 1400];
const hdr = (t, w) => new TableCell({ width: { size: w, type: WidthType.DXA }, borders: cellBorders, shading: { type: ShadingType.CLEAR, fill: "D9E2F3", color: "auto" }, margins: { top: 60, bottom: 60, left: 100, right: 100 }, children: [new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ text: t, bold: true, font: FONT, size: 22 })] })] });
const cell = (t, w, center) => new TableCell({ width: { size: w, type: WidthType.DXA }, borders: cellBorders, margins: { top: 60, bottom: 60, left: 100, right: 100 }, children: [new Paragraph({ alignment: center ? AlignmentType.CENTER : AlignmentType.LEFT, children: [new TextRun({ text: String(t), font: FONT, size: 22 })] })] });
children.push(new Table({
  width: { size: TW, type: WidthType.DXA }, columnWidths: SW,
  rows: [
    new TableRow({ tableHeader: true, children: [hdr("Nhóm", SW[0]), hdr("Số câu", SW[1]), hdr("[Ưu tiên]", SW[2])] }),
    ...groups.map((g) => new TableRow({ children: [cell(g.title, SW[0]), cell(g.questions.length, SW[1], true), cell(g.questions.filter((x) => x.priority).length, SW[2], true)] })),
  ],
}));

// Câu hỏi
const ORDER = ["Câu hỏi", "Căn cứ", "Hệ thống đang làm (mặc định)", "Phương án khác", "Ảnh hưởng nếu đổi"];
for (const g of groups) {
  children.push(new Paragraph({ heading: HeadingLevel.HEADING_1, pageBreakBefore: true, spacing: { after: 160 }, children: [new TextRun({ text: g.title, bold: true, font: FONT, size: 28, color: "1F3864" })] }));
  for (const x of g.questions) {
    const head = [new TextRun({ text: `${x.id}. ${x.title}`, bold: true, font: FONT, size: 24 })];
    if (x.priority) head.push(new TextRun({ text: "   [Ưu tiên]", bold: true, font: FONT, size: 22, color: "C00000" }));
    children.push(new Paragraph({ heading: HeadingLevel.HEADING_2, keepNext: true, spacing: { before: 240, after: 100 }, children: head }));
    const byName = Object.fromEntries(x.fields.map((f) => [f.name, f.value]));
    const rows = ORDER.filter((n) => byName[n]).map((n) => new TableRow({ cantSplit: true, children: [labelCell(n === "Hệ thống đang làm (mặc định)" ? "Hệ thống đang làm" : n), valueCell(byName[n])] }));
    for (const f of x.fields) if (!ORDER.includes(f.name)) rows.push(new TableRow({ cantSplit: true, children: [labelCell(f.name), valueCell(f.value)] }));
    rows.push(new TableRow({ cantSplit: true, children: [labelCell("Ý kiến trả lời"), answerCell()] }));
    children.push(new Table({ width: { size: TW, type: WidthType.DXA }, columnWidths: [C1, C2], rows }));
  }
}

// Ký xác nhận
const sig = (a, b) => new TableCell({ width: { size: TW / 2, type: WidthType.DXA }, borders: noBorders, children: [
  new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ text: a, bold: true, font: FONT, size: 24 })] }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 1400 }, children: [new TextRun({ text: b, italics: true, font: FONT, size: 22 })] }),
] });
children.push(new Paragraph({ pageBreakBefore: true, spacing: { after: 200 }, children: [new TextRun({ text: "Ý kiến chung (nếu có):", bold: true, font: FONT, size: 24 })] }));
for (let i = 0; i < 5; i++) children.push(new Paragraph({ children: [new TextRun({ text: " ", font: FONT })], border: { bottom: { style: BorderStyle.DOTTED, size: 6, color: "808080", space: 1 } }, spacing: { after: 160 } }));
children.push(new Paragraph({ alignment: AlignmentType.RIGHT, spacing: { before: 300, after: 200 }, children: [new TextRun({ text: "……………, ngày …… tháng …… năm 2026", italics: true, font: FONT, size: 24 })] }));
children.push(new Table({ width: { size: TW, type: WidthType.DXA }, columnWidths: [TW / 2, TW / 2], borders: noBorders, rows: [
  new TableRow({ children: [sig("NGƯỜI TRẢ LỜI", "(Ký, ghi rõ họ tên)"), sig("XÁC NHẬN CỦA ĐƠN VỊ", "(Ký, ghi rõ họ tên)")] }),
] }));

const doc = new Document({
  creator: "CongTacDang",
  title: "Phiếu xác nhận nghiệp vụ — Hệ thống đánh giá cán bộ",
  styles: { default: { document: { run: { font: FONT, size: 24 } } } },
  numbering: { config: [{ reference: "bul", levels: [
    { level: 0, format: LevelFormat.BULLET, text: "–", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 360, hanging: 260 } } } },
    { level: 1, format: LevelFormat.BULLET, text: "+", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 720, hanging: 260 } } } },
  ] }] },
  sections: [{
    properties: { page: { size: { width: PAGE_W, height: 16838 }, margin: { top: 1134, bottom: 1134, left: ML, right: MR } } },
    footers: { default: new Footer({ children: [new Paragraph({ alignment: AlignmentType.CENTER, children: [
      new TextRun({ text: "Trang ", font: FONT, size: 20 }), new TextRun({ children: [PageNumber.CURRENT], font: FONT, size: 20 }),
      new TextRun({ text: " / ", font: FONT, size: 20 }), new TextRun({ children: [PageNumber.TOTAL_PAGES], font: FONT, size: 20 }),
    ] })] }) },
    children,
  }],
});
Packer.toBuffer(doc).then((b) => { fs.writeFileSync(OUT, b); console.log(`OK ${OUT} — ${groups.length} nhóm, ${total} câu, ${totalP} ưu tiên`); });
