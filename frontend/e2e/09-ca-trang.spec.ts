import { test, expect } from './fixtures';
import { DAO, TRUNG, duongDanHoSo } from './luong';
import { docTrangThai } from './du-lieu';

// Mở lần lượt các trang theo từng vai: trang phải có tiêu đề, không lỗi JavaScript, không 5xx và không 403/404 từ /api (bộ thu lỗi chung).
const THEO_VAI: Record<string, string[]> = {
  'loan.ctp': ['/', '/work-queue', '/evaluations', '/collective-evaluations', '/crosschecks', '/quality-control', '/quality-control?tab=control', '/results', '/reports', '/periods', '/criteria', '/account'],
  'hung.dt': ['/', '/work-queue', '/evaluations', '/quality-control', '/results', '/reports'],
  'giang.nh': ['/', '/work-queue', '/evaluations', '/collective-evaluations', '/results'],
  'dao.nta': ['/', '/work-queue', '/collective-evaluations', '/results'],
  'trung.nb': ['/', '/work-queue', '/results', '/account'],
  quantri: ['/', '/catalog', '/admin/users', '/admin/roles', '/admin/performers', '/admin/imports', '/admin/settings', '/admin/templates', '/audit', '/account'],
};

test.describe('Mở các trang chính theo từng vai', () => {
  for (const [nguoi, trang] of Object.entries(THEO_VAI)) {
    test(`${nguoi} mở ${trang.length} trang`, async ({ mo }) => {
      const page = await mo(nguoi);
      for (const duongDan of trang) {
        await page.goto(duongDan);
        await page.waitForLoadState('networkidle');
        await expect(page.locator('h1, h2').first(), `${duongDan} phải có tiêu đề`).toBeVisible();
        await expect(page.getByText(/Đang tải\.\.\./), `${duongDan} còn vòng tải`).toHaveCount(0);
      }
    });
  }

  test('trang chi tiết: kỳ, hồ sơ, biên bản, phiếu, quản trị người dùng và vai trò', async ({ mo }) => {
    const tt = docTrangThai();
    const page = await mo('loan.ctp');
    for (const duongDan of [tt.duongDanKy, duongDanHoSo(TRUNG.khoa), duongDanHoSo(DAO.khoa), tt.bienBan20, tt.phieu19, tt.phieu03]) {
      await page.goto(duongDan);
      await page.waitForLoadState('networkidle');
      await expect(page.locator('h1').first(), `${duongDan} phải có tiêu đề`).toBeVisible();
    }

    const quanTri = await mo('quantri');
    await quanTri.goto('/admin/users');
    await quanTri.getByRole('link', { name: /trung\.nb|Nguyễn Bình Trung/ }).first().click();
    await expect(quanTri).toHaveURL(/\/admin\/users\/[0-9a-f-]{36}/);
    await expect(quanTri.locator('h1').first()).toBeVisible();
    await quanTri.waitForLoadState('networkidle');

    await quanTri.goto('/admin/roles');
    await quanTri.getByRole('link', { name: /Lãnh đạo Phòng/ }).first().click();
    await expect(quanTri).toHaveURL(/\/admin\/roles\/[0-9a-f-]{36}/);
    await expect(quanTri.locator('h1').first()).toBeVisible();
    await quanTri.waitForLoadState('networkidle');
  });
});
