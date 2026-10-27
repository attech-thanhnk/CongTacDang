import { test, expect } from './fixtures';
import type { Page } from '@playwright/test';
import { DAO, TRUNG, duongDanHoSo } from './luong';
import { docTrangThai } from './du-lieu';

const DIEN_THOAI = { width: 390, height: 844 };

/** Trang không được cuộn ngang (nội dung rộng chỉ cuộn trong khung bảng). */
async function khongCuonNgang(page: Page, trang: string) {
  const [rong, vung] = await page.evaluate(() => [document.documentElement.scrollWidth, window.innerWidth]);
  expect(vung, 'Cửa sổ phải đúng cỡ điện thoại').toBe(DIEN_THOAI.width);
  expect(rong, `${trang}: trang cuộn ngang (${rong}px > ${vung}px)`).toBeLessThanOrEqual(vung + 1);
}

const TRANG_CAN_BO: [string, string][] = [
  ['loan.ctp', '/'],
  ['loan.ctp', '/work-queue'],
  ['loan.ctp', '/evaluations'],
  ['loan.ctp', '/collective-evaluations'],
  ['loan.ctp', '/crosschecks'],
  ['loan.ctp', '/quality-control'],
  ['loan.ctp', '/results'],
  ['loan.ctp', '/reports'],
  ['loan.ctp', '/periods'],
  ['loan.ctp', '/criteria'],
  ['loan.ctp', '/account'],
  ['hung.dt', '/work-queue'],
  ['giang.nh', '/'],
  ['trung.nb', '/'],
];

const TRANG_QUAN_TRI = ['/', '/catalog', '/admin/users', '/admin/roles', '/admin/performers', '/admin/imports', '/admin/settings', '/admin/templates', '/audit'];

test.describe('Giao diện điện thoại 390px', () => {
  for (const [nguoi, duongDan] of TRANG_CAN_BO) {
    test(`${nguoi} mở ${duongDan} không cuộn ngang`, async ({ mo }) => {
      const page = await mo(nguoi, { viewport: DIEN_THOAI });
      await page.goto(duongDan);
      await page.waitForLoadState('networkidle');
      await expect(page.getByRole('button', { name: 'Mở menu' })).toBeVisible();
      await khongCuonNgang(page, duongDan);
    });
  }

  for (const duongDan of TRANG_QUAN_TRI) {
    test(`quantri mở ${duongDan} không cuộn ngang`, async ({ mo }) => {
      const page = await mo('quantri', { viewport: DIEN_THOAI });
      await page.goto(duongDan);
      await page.waitForLoadState('networkidle');
      await khongCuonNgang(page, duongDan);
    });
  }

  test('trang chi tiết kỳ, hồ sơ, biên bản, phiếu mở được và không cuộn ngang', async ({ mo }) => {
    const tt = docTrangThai();
    const page = await mo('loan.ctp', { viewport: DIEN_THOAI });
    for (const duongDan of [duongDanHoSo(TRUNG.khoa), duongDanHoSo(DAO.khoa), tt.bienBan20, tt.phieu19, tt.phieu03, tt.duongDanKy]) {
      await page.goto(duongDan);
      await page.waitForLoadState('networkidle');
      await expect(page.locator('h1').first()).toBeVisible();
      await khongCuonNgang(page, duongDan);
    }
  });

  test('danh sách chuyển sang dạng thẻ: không còn bảng hiển thị', async ({ mo }) => {
    const page = await mo('loan.ctp', { viewport: DIEN_THOAI });
    for (const duongDan of ['/quality-control', '/quality-control?tab=control', '/crosschecks']) {
      await page.goto(duongDan);
      await page.waitForLoadState('networkidle');
      const the = page.locator('a.card');
      await expect(the.first(), `${duongDan}: phải có thẻ`).toBeVisible();
      await expect(page.locator('table:visible'), `${duongDan}: không được còn bảng`).toHaveCount(0);
      await khongCuonNgang(page, duongDan);
    }
    const quanTri = await mo('quantri', { viewport: DIEN_THOAI });
    await quanTri.goto('/admin/users');
    await quanTri.waitForLoadState('networkidle');
    await expect(quanTri.locator('table:visible')).toHaveCount(0);
  });

  test('menu mở bằng nút và chuyển trang được', async ({ mo }) => {
    const page = await mo('loan.ctp', { viewport: DIEN_THOAI });
    await page.goto('/');
    await expect(page.locator('aside').getByRole('link', { name: 'Kỳ đánh giá' })).not.toBeInViewport();
    await page.getByRole('button', { name: 'Mở menu' }).click();
    await page.locator('aside').getByRole('link', { name: 'Duyệt kết quả' }).click();
    await expect(page).toHaveURL(/\/quality-control/);
    await expect(page.locator('aside').getByRole('link', { name: 'Kỳ đánh giá' })).not.toBeInViewport();
  });
});
