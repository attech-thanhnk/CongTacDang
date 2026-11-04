import fs from 'node:fs';
import { expect } from './fixtures';
import type { Download, Locator, Page } from '@playwright/test';

/** Tệp thử: nội dung đầu tệp khớp phần mở rộng để máy chủ nhận. */
export function tepPdf(ten: string, noiDung = 'minh chung') {
  return { name: ten, mimeType: 'application/pdf', buffer: Buffer.from(`%PDF-1.4\n${noiDung}\n%%EOF`) };
}

/** Bấm nút mở hộp chọn tệp rồi chọn tệp. */
export async function chonTep(page: Page, nut: Locator, tep: { name: string; mimeType: string; buffer: Buffer }) {
  const cho = page.waitForEvent('filechooser');
  await nut.click();
  await (await cho).setFiles(tep);
}

/** Nút tải xuống của một tệp trong danh sách đính kèm. */
export function nutTai(vung: Page | Locator, ten: string) {
  return vung.getByRole('button', { name: `Tải xuống ${ten}`, exact: true });
}

/** Bấm tải xuống: tệp nhận về đúng tên và là PDF. */
export async function taiVaKiemTra(page: Page, nut: Locator, ten: string): Promise<Download> {
  const cho = page.waitForEvent('download');
  await nut.click();
  const tep = await cho;
  expect(tep.suggestedFilename()).toBe(ten);
  expect(fs.readFileSync(await tep.path()).subarray(0, 5).toString('latin1')).toBe('%PDF-');
  return tep;
}
