import fs from 'node:fs';
import zlib from 'node:zlib';
import { expect } from '@playwright/test';
import type { Download, Page } from '@playwright/test';

/** Đọc một tệp trong gói .docx (zip) bằng zlib, không cần thư viện ngoài. */
export function docTrongZip(duongDan: string, tenMuc: string): string | null {
  const buf = fs.readFileSync(duongDan);
  // Bản ghi kết thúc thư mục trung tâm nằm ở cuối tệp.
  let eocd = -1;
  for (let i = buf.length - 22; i >= Math.max(0, buf.length - 65_557); i--) {
    if (buf.readUInt32LE(i) === 0x06054b50) {
      eocd = i;
      break;
    }
  }
  if (eocd < 0) return null;
  const soMuc = buf.readUInt16LE(eocd + 10);
  let p = buf.readUInt32LE(eocd + 16);
  for (let n = 0; n < soMuc; n++) {
    if (buf.readUInt32LE(p) !== 0x02014b50) return null;
    const phuongThuc = buf.readUInt16LE(p + 10);
    const kichThuocNen = buf.readUInt32LE(p + 20);
    const doDaiTen = buf.readUInt16LE(p + 28);
    const doDaiThem = buf.readUInt16LE(p + 30);
    const doDaiGhiChu = buf.readUInt16LE(p + 32);
    const viTriCucBo = buf.readUInt32LE(p + 42);
    const ten = buf.toString('utf8', p + 46, p + 46 + doDaiTen);
    if (ten === tenMuc) {
      const tenCucBo = buf.readUInt16LE(viTriCucBo + 26);
      const themCucBo = buf.readUInt16LE(viTriCucBo + 28);
      const batDau = viTriCucBo + 30 + tenCucBo + themCucBo;
      const du = buf.subarray(batDau, batDau + kichThuocNen);
      return (phuongThuc === 0 ? du : zlib.inflateRawSync(du)).toString('utf8');
    }
    p += 46 + doDaiTen + doDaiThem + doDaiGhiChu;
  }
  return null;
}

/** Kiểm tệp tải về là .docx hợp lệ: đủ lớn, đầu tệp "PK", có nội dung văn bản và không còn thẻ chưa điền. */
export async function kiemTraDocx(tep: Download, can: { chua?: string[] } = {}) {
  expect(tep.suggestedFilename()).toMatch(/\.docx$/i);
  const duongDan = await tep.path();
  const buf = fs.readFileSync(duongDan);
  expect(buf.length, 'Tệp .docx quá nhỏ').toBeGreaterThan(4_000);
  expect(buf.subarray(0, 2).toString('latin1'), 'Đầu tệp phải là "PK"').toBe('PK');
  const noiDung = docTrongZip(duongDan, 'word/document.xml');
  expect(noiDung, 'Thiếu word/document.xml').not.toBeNull();
  const chu = (noiDung as string).replace(/<[^>]+>/g, '');
  expect(chu, 'Còn thẻ chưa điền kiểu {{…}} hoặc «…»').not.toMatch(/\{\{|\}\}|«|»/);
  for (const dong of can.chua ?? []) expect(chu, `Tệp phải có "${dong}"`).toContain(dong);
}

/** Chạy thao tác kích hoạt xuất (bấm nút hoặc mục menu) và nhận tệp tải về. */
export async function tai(page: Page, kichHoat: () => Promise<unknown>): Promise<Download> {
  const cho = page.waitForEvent('download', { timeout: 60_000 });
  await kichHoat();
  return cho;
}
