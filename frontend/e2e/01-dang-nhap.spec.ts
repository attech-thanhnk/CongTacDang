import { test, expect, MAT_KHAU } from './fixtures';
import type { Page } from '@playwright/test';

// PNG 1x1 hợp lệ để thử tải ảnh nền màn hình đăng nhập.
const PNG_1X1 = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
  'base64',
);

async function dangNhapBangGiaoDien(page: Page, ten: string, matKhau: string) {
  await page.goto('/login');
  await page.getByLabel('Tên đăng nhập').fill(ten);
  await page.getByLabel('Mật khẩu').fill(matKhau);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
}

async function menu(page: Page) {
  const nav = page.locator('aside');
  await expect(nav.getByRole('link', { name: 'Tổng quan' })).toBeVisible();
  // Nhóm Hệ thống gập sẵn với người làm nghiệp vụ; mở ra để đếm đủ mục.
  const he = nav.getByRole('button', { name: 'Hệ thống', exact: true });
  if ((await he.count()) && (await he.getAttribute('aria-expanded')) === 'false') await he.click();
  return (await nav.getByRole('link').allInnerTexts()).map((t) => t.trim());
}

test.describe('Đăng nhập và menu', () => {
  test('trang đăng nhập hiển thị đủ ô nhập và tên đơn vị', async ({ page }) => {
    await page.goto('/login');
    await expect(page.getByRole('heading', { name: 'Đăng nhập' }).or(page.getByText('Đăng nhập', { exact: true }).first())).toBeVisible();
    await expect(page.getByLabel('Tên đăng nhập')).toBeVisible();
    await expect(page.getByLabel('Mật khẩu')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Đăng nhập', exact: true })).toBeEnabled();
  });

  test('chưa đăng nhập vào trang nghiệp vụ thì chuyển về trang đăng nhập kèm đường dẫn quay lại', async ({ page }) => {
    await page.goto('/periods');
    await expect(page).toHaveURL(/\/login\?returnUrl=%2Fperiods/);
  });

  test('mật khẩu sai báo lỗi và ở lại trang đăng nhập', async ({ page }) => {
    await dangNhapBangGiaoDien(page, 'loan.ctp', 'SaiMatKhau1');
    await expect(page.getByRole('alert').filter({ hasText: 'Đăng nhập không thành công' })).toBeVisible();
    await expect(page).toHaveURL(/\/login/);
  });

  test('đăng nhập đúng rồi đăng xuất quay về trang đăng nhập', async ({ page }) => {
    await dangNhapBangGiaoDien(page, 'loan.ctp', MAT_KHAU);
    await expect(page).not.toHaveURL(/\/login/);
    await expect(page.getByText('Chu Thị Phương Loan').first()).toBeVisible();

    await page.getByRole('button', { name: /Chu Thị Phương Loan/ }).click();
    await page.getByRole('menuitem', { name: 'Đăng xuất' }).click();
    await expect(page).toHaveURL(/\/login/);

    // Sau khi đăng xuất không vào lại được trang nghiệp vụ.
    await page.goto('/periods');
    await expect(page).toHaveURL(/\/login/);
  });

  test('quantri thấy menu quản trị nhưng không thấy mục nghiệp vụ', async ({ page }) => {
    await dangNhapBangGiaoDien(page, 'quantri', MAT_KHAU);
    await expect(page.locator('aside').getByRole('link', { name: 'Tài khoản' })).toBeVisible();
    const muc = await menu(page);
    for (const can of ['Tài khoản', 'Vai trò', 'Người thực hiện', 'Nhật ký hệ thống', 'Thông tin đơn vị', 'Biểu mẫu Word', 'Danh mục tổ chức']) {
      expect(muc, `quantri phải thấy "${can}"`).toContain(can);
    }
    for (const khong of ['Việc cần xử lý', 'Hồ sơ đánh giá', 'Đánh giá tập thể', 'Hiệu chuẩn và kiểm soát', 'Kỳ đánh giá', 'Kết quả đánh giá', 'Báo cáo']) {
      expect(muc, `quantri không được thấy "${khong}"`).not.toContain(khong);
    }
  });

  const THEO_VAI: { ten: string; mo_ta: string; co: string[]; khong: string[] }[] = [
    {
      ten: 'loan.ctp', mo_ta: 'Phòng TCCB-LĐ (cơ quan thẩm định)',
      co: ['Việc cần xử lý', 'Hồ sơ đánh giá', 'Đối soát chuyên ngành', 'Hiệu chuẩn và kiểm soát', 'Kỳ đánh giá', 'Bộ tiêu chí', 'Báo cáo'],
      khong: ['Vai trò', 'Nhập dữ liệu', 'Nhật ký hệ thống'],
    },
    {
      ten: 'hung.dt', mo_ta: 'Văn phòng Đảng ủy',
      co: ['Việc cần xử lý', 'Hồ sơ đánh giá', 'Hiệu chuẩn và kiểm soát', 'Kết quả đánh giá', 'Báo cáo'],
      khong: ['Kỳ đánh giá', 'Bộ tiêu chí', 'Vai trò'],
    },
    {
      ten: 'giang.nh', mo_ta: 'cấp trực tiếp sử dụng',
      co: ['Việc cần xử lý', 'Hồ sơ đánh giá', 'Đánh giá tập thể', 'Kết quả đánh giá'],
      khong: ['Hiệu chuẩn và kiểm soát', 'Kỳ đánh giá', 'Vai trò'],
    },
    {
      ten: 'trung.nb', mo_ta: 'cán bộ được đánh giá',
      co: ['Việc cần xử lý', 'Kết quả đánh giá'],
      khong: ['Hồ sơ đánh giá', 'Hiệu chuẩn và kiểm soát', 'Kỳ đánh giá', 'Báo cáo', 'Vai trò'],
    },
  ];
  for (const vai of THEO_VAI) {
    test(`menu đúng theo quyền: ${vai.ten} (${vai.mo_ta})`, async ({ mo }) => {
      const page = await mo(vai.ten);
      await page.goto('/');
      const muc = await menu(page);
      for (const can of vai.co) expect(muc, `${vai.ten} phải thấy "${can}"`).toContain(can);
      for (const khong of vai.khong) expect(muc, `${vai.ten} không được thấy "${khong}"`).not.toContain(khong);
    });
  }

  test('cán bộ thường mở thẳng trang quản trị bị chặn, không thấy nội dung', async ({ mo }) => {
    const page = await mo('trung.nb');
    await page.goto('/admin/roles');
    await expect(page.getByText(/không có quyền/i).first()).toBeVisible();
    await expect(page.getByRole('button', { name: /Thêm vai trò|Tạo vai trò/ })).toHaveCount(0);
  });

  test('ảnh nền màn hình đăng nhập: quản trị tải lên thì trang đăng nhập hiển thị, bỏ ảnh thì trở về mặc định', async ({ mo, browser }) => {
    const admin = await mo('quantri');
    await admin.goto('/admin/settings');
    await expect(admin.getByRole('heading', { name: 'Màn hình đăng nhập' })).toBeVisible();

    await admin.locator('input[type="file"]').setInputFiles({ name: 'nen.png', mimeType: 'image/png', buffer: PNG_1X1 });
    await expect(admin.getByRole('status').filter({ hasText: 'Đã cập nhật ảnh nền' })).toBeVisible();

    // Người chưa đăng nhập thấy ảnh nền.
    const khach = await browser.newContext({ baseURL: process.env.E2E_BASE_URL || 'http://localhost:3400' });
    const trang = await khach.newPage();
    await trang.goto('/login');
    const anh = trang.locator('img[src*="login-background"], img[src*="background"]').first();
    await expect(anh).toBeAttached();
    expect(await anh.evaluate((i: HTMLImageElement) => i.complete && i.naturalWidth > 0)).toBeTruthy();
    await expect(trang.getByLabel('Tên đăng nhập')).toBeVisible();

    await admin.getByRole('button', { name: 'Bỏ ảnh' }).click();
    await admin.getByRole('button', { name: 'Bỏ ảnh' }).last().click();
    await expect(admin.getByRole('status').filter({ hasText: 'Đã bỏ ảnh nền' })).toBeVisible();

    await trang.evaluate(() => localStorage.removeItem('login-look'));
    await trang.reload();
    await expect(trang.getByLabel('Tên đăng nhập')).toBeVisible();
    await expect(trang.locator('img[src*="background"]')).toHaveCount(0);
    await khach.close();
  });
});
