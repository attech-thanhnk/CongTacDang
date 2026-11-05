import { test, expect } from './fixtures';

const DIEN_THOAI = { width: 390, height: 844 };

test.describe('Trang Hướng dẫn', () => {
  test('chưa đăng nhập thì chuyển về trang đăng nhập', async ({ page }) => {
    await page.goto('/help');
    await expect(page).toHaveURL(/\/login/);
  });

  test('cán bộ thường mở từ nút ? ở thanh trên: có chương theo vai trò, bảng và ảnh', async ({ mo }) => {
    const page = await mo('trung.nb');
    await page.goto('/');
    await page.locator('header').getByRole('link', { name: 'Hướng dẫn' }).click();
    await expect(page).toHaveURL(/\/help$/);
    await expect(page.getByRole('heading', { name: 'Hướng dẫn sử dụng', level: 1 })).toBeVisible();
    const muc = page.getByRole('navigation', { name: 'Mục lục hướng dẫn' });
    await expect(muc.getByRole('link', { name: /^Hướng dẫn cho người được đánh giá/ })).toBeVisible();
    await expect(muc.getByRole('link', { name: /^Hướng dẫn cho Văn phòng Đảng ủy/ })).toBeVisible();
    await expect(page.locator('article table').first()).toBeVisible();
    const anh = page.locator('article img').first();
    await expect(anh).toHaveAttribute('src', /^\/huong-dan\/anh\/.+\.png$/);
    const tai = await page.request.get((await anh.getAttribute('src'))!);
    expect(tai.status()).toBe(200);
  });

  test('bấm mục lục cuộn tới đúng chương và neo # trên địa chỉ hoạt động', async ({ mo }) => {
    const page = await mo('loan.ctp');
    await page.goto('/help');
    await page.getByRole('navigation', { name: 'Mục lục hướng dẫn' }).getByRole('link', { name: /^Hướng dẫn cho Phòng TCCB-LĐ/ }).click();
    await expect.poll(() => decodeURIComponent(page.url())).toContain('#hướng-dẫn-cho-phòng-tccb-lđ');
    await expect(page.locator('article h2[id="hướng-dẫn-cho-phòng-tccb-lđ"]')).toBeInViewport();

    await page.goto('/help#hướng-dẫn-cho-văn-phòng-đảng-ủy');
    await expect(page.locator('article h2[id="hướng-dẫn-cho-văn-phòng-đảng-ủy"]')).toBeInViewport();
  });

  test('liên kết giữa các hướng dẫn thành liên kết trong trang, không dẫn tới trang chết', async ({ mo }) => {
    const page = await mo('trung.nb');
    await page.goto('/help');
    await expect(page.locator('article a[href$=".md"]')).toHaveCount(0);
    await expect(page.locator('article a[href]:not([href^="#"]):not([href^="http"])')).toHaveCount(0);
    const neo = await page.locator('article a[href^="#"]').evaluateAll((ds) => ds.map((d) => decodeURIComponent(d.getAttribute('href') ?? '')));
    expect(neo).toContain('#hướng-dẫn-cho-người-được-đánh-giá');
  });

  test('trên điện thoại không cuộn ngang, mục lục thu gọn mở được', async ({ mo }) => {
    const page = await mo('trung.nb', { viewport: DIEN_THOAI });
    await page.goto('/help');
    await page.waitForLoadState('networkidle');
    const [rong, vung] = await page.evaluate(() => [document.documentElement.scrollWidth, window.innerWidth]);
    expect(rong).toBeLessThanOrEqual(vung + 1);
    const muc = page.getByRole('navigation', { name: 'Mục lục hướng dẫn' });
    await expect(muc.getByRole('link', { name: /^Hướng dẫn cho Quản trị/ })).toBeHidden();
    await muc.getByText('Mục lục', { exact: true }).click();
    await expect(muc.getByRole('link', { name: /^Hướng dẫn cho Quản trị/ })).toBeVisible();
  });

  test('ô tìm kiếm lọc mục lục theo tiêu đề và nội dung; Esc và nút × xóa', async ({ mo }) => {
    const page = await mo('trung.nb');
    await page.goto('/help');
    const muc = page.getByRole('navigation', { name: 'Mục lục hướng dẫn' });
    const o = muc.getByRole('searchbox', { name: 'Tìm trong hướng dẫn' });
    await expect(muc.getByRole('link').first()).toBeVisible();
    const tong = await muc.getByRole('link').count();
    await o.fill('chuông');
    await expect(muc.getByRole('status')).toContainText(/[0-9]+ kết quả/);
    await expect(muc.getByRole('link', { name: /Gửi kiến nghị/ })).toBeVisible();
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
