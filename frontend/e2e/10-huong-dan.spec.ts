import { test, expect } from './fixtures';

const DIEN_THOAI = { width: 390, height: 844 };

test.describe('Trang Hướng dẫn', () => {
  test('chưa đăng nhập thì chuyển về trang đăng nhập', async ({ page }) => {
    await page.goto('/help');
    await expect(page).toHaveURL(/\/login/);
  });

  test('cán bộ thường mở từ nút ? ở thanh trên: có mục lục, bảng và đúng tên mới', async ({ mo }) => {
    const page = await mo('trung.nb');
    await page.goto('/');
    await page.locator('header').getByRole('link', { name: 'Hướng dẫn' }).click();
    await expect(page).toHaveURL(/\/help$/);
    await expect(page.getByRole('heading', { name: 'Hướng dẫn sử dụng', level: 1 })).toBeVisible();
    await expect(page.getByText('Nút Hướng dẫn (dấu ?) ở góc trên bên phải hiển thị cùng nội dung này.')).toBeVisible();
    await expect(page.getByRole('navigation', { name: 'Mục lục hướng dẫn' }).getByRole('link', { name: /Xác nhận số liệu và duyệt kết quả/ })).toBeVisible();
    await expect(page.locator('article table').first()).toBeVisible();
    // Tên cũ không còn trong hướng dẫn.
    await expect(page.locator('article').getByText('Rà soát', { exact: true })).toHaveCount(0);
  });

  test('bấm mục lục cuộn tới đúng mục và neo # trên địa chỉ hoạt động', async ({ mo }) => {
    const page = await mo('loan.ctp');
    await page.goto('/help');
    await page.getByRole('navigation', { name: 'Mục lục hướng dẫn' }).getByRole('link', { name: /^10. Báo cáo/ }).click();
    await expect.poll(() => decodeURIComponent(page.url())).toContain('#10-báo-cáo');
    await expect(page.locator('article h2[id="10-báo-cáo"]')).toBeInViewport();

    await page.goto('/help#12-vì-sao-nút-bị-khóa');
    await expect(page.locator('article h2[id="12-vì-sao-nút-bị-khóa"]')).toBeInViewport();
  });

  test('liên kết sang tài liệu khác không dẫn tới trang chết', async ({ mo }) => {
    const page = await mo('trung.nb');
    await page.goto('/help');
    await expect(page.locator('article a[href$=".md"]')).toHaveCount(0);
    await expect(page.locator('article a[href]:not([href^="#"]):not([href^="http"])')).toHaveCount(0);
  });

  test('trên điện thoại không cuộn ngang, mục lục thu gọn mở được', async ({ mo }) => {
    const page = await mo('trung.nb', { viewport: DIEN_THOAI });
    await page.goto('/help');
    await page.waitForLoadState('networkidle');
    const [rong, vung] = await page.evaluate(() => [document.documentElement.scrollWidth, window.innerWidth]);
    expect(rong).toBeLessThanOrEqual(vung + 1);
    const muc = page.getByRole('navigation', { name: 'Mục lục hướng dẫn' });
    await expect(muc.getByRole('link', { name: /Báo cáo/ }).first()).toBeHidden();
    await muc.getByText('Mục lục', { exact: true }).click();
    await expect(muc.getByRole('link', { name: /Báo cáo/ }).first()).toBeVisible();
  });

  test('ô tìm kiếm lọc mục lục theo tiêu đề và nội dung; Esc và nút × xóa', async ({ mo }) => {
    const page = await mo('trung.nb');
    await page.goto('/help');
    const muc = page.getByRole('navigation', { name: 'Mục lục hướng dẫn' });
    const o = muc.getByRole('searchbox', { name: 'Tìm trong hướng dẫn' });
    const tong = await muc.getByRole('link').count();
    await o.fill('chuông');
    await expect(muc.getByRole('status')).toContainText(/d+ kết quả/);
    await expect(muc.getByRole('link', { name: /Nhắc việc (chuông)/ })).toBeVisible();
    expect(await muc.getByRole('link').count()).toBeLessThan(tong);
    await o.press('Escape');
    await expect(o).toHaveValue('');
    await expect.poll(() => muc.getByRole('link').count()).toBe(tong);
    await o.fill('khongcochuoinaoten');
    await expect(muc.getByRole('status')).toHaveText('Không có kết quả');
    await muc.getByRole('button', { name: 'Xóa tìm kiếm' }).click();
    await expect(o).toHaveValue('');
  });
});
