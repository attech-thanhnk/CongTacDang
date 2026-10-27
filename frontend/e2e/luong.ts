import { expect, thongBao } from './fixtures';
import type { Locator, Page } from '@playwright/test';
import { docTrangThai, ghiTrangThai } from './du-lieu';

export const TRUNG = { tenDangNhap: 'trung.nb', hoTen: 'Nguyễn Bình Trung', khoa: 'trung' };
export const DAO = { tenDangNhap: 'dao.nta', hoTen: 'Nguyễn Thị Anh Đào', khoa: 'dao' };

/** Đường dẫn hồ sơ đã lưu ở các kịch bản trước. */
export function duongDanHoSo(khoa: string): string {
  const id = docTrangThai()[`hoSo_${khoa}`];
  if (!id) throw new Error(`Chưa có hồ sơ "${khoa}": kịch bản dựng hồ sơ phải chạy trước.`);
  return `/evaluations/${id}`;
}

export function luuHoSo(khoa: string, page: Page) {
  ghiTrangThai({ [`hoSo_${khoa}`]: new URL(page.url()).pathname.split('/').pop() });
}

/** Mở hồ sơ từ danh sách "Việc cần xử lý" của người đang đăng nhập (đúng đường người dùng đi). */
export async function moTuViecCanXuLy(page: Page, hoTen: string, nhanNut: string | RegExp) {
  await page.goto('/work-queue');
  const dong = page.getByRole('row').filter({ hasText: hoTen });
  await expect(dong.first(), `"Việc cần xử lý" phải có hồ sơ của ${hoTen}`).toBeVisible();
  await dong.getByRole('link', { name: nhanNut }).click();
  await expect(page).toHaveURL(/\/evaluations\/[0-9a-f-]{36}/);
}

/** Bấm nút xác nhận của hộp thoại "Xác nhận thao tác". */
export async function dongY(page: Page) {
  await page.getByRole('dialog', { name: 'Xác nhận thao tác' }).getByRole('button', { name: 'Đồng ý' }).click();
}

export async function daLuuThaoTac(page: Page) {
  await expect(thongBao(page, 'Đã lưu thao tác trên hồ sơ.')).toBeVisible();
}

/** Nhập danh mục sản phẩm Mẫu 01 (3 sản phẩm, tổng 70 điểm) rồi nộp. */
export async function nopDanhMuc(page: Page, tiento: string) {
  const trongSo = [25, 25, 20];
  await expect(page.locator('#t0-name')).toBeVisible();
  for (let i = 0; i < 3; i++) {
    await page.locator(`#t${i}-name`).fill(`${tiento} sản phẩm ${i + 1}`);
    await page.locator(`#t${i}-kind`).selectOption({ index: 1 });
    await page.locator(`#t${i}-role`).selectOption({ index: 1 });
    await page.locator(`#t${i}-axis`).selectOption({ index: 1 });
    await page.locator(`#t${i}-weight`).fill(String(trongSo[i]));
    await page.locator(`#t${i}-standard`).fill(`Hoàn thành đúng chuẩn ${i + 1}`);
    await page.locator(`#t${i}-exceed`).fill(`Vượt chuẩn ${i + 1}`);
    await page.locator(`#t${i}-evidence`).fill('Báo cáo kết quả');
    await page.locator(`#t${i}-confirmer`).fill('Lãnh đạo Phòng');
  }
  await expect(page.getByText('Tổng trọng số: 70 / 70')).toBeVisible();
  await page.getByRole('button', { name: 'Nộp danh mục' }).click();
  await dongY(page);
  await daLuuThaoTac(page);
}

/** Bấm nút thao tác của hồ sơ, đồng ý ở hộp xác nhận và chờ thông báo đã lưu. */
export async function guiThaoTac(page: Page, nut: Locator) {
  await nut.click();
  await dongY(page);
  await daLuuThaoTac(page);
}
