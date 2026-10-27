import { test as base, expect, type Browser, type BrowserContext, type Page } from '@playwright/test';

export const MAT_KHAU = process.env.E2E_PASSWORD || 'Thu12345a';

/** Thông điệp console được bỏ qua: trình duyệt tự ghi khi API trả 4xx hợp lệ (mật khẩu sai, bị chặn…). */
const CONSOLE_BO_QUA = [/Failed to load resource: the server responded with a status of 4\d\d/];

export interface LoiTrang {
  loai: 'console' | 'pageerror' | 'api5xx' | 'api403404';
  noiDung: string;
}

/** Gắn bộ thu lỗi JS và phản hồi 5xx/403/404 của /api vào một trang. `phep`: mẫu đường dẫn /api được phép trả 403/404 trong kịch bản (kiểm thử chủ ý bị chặn). */
export function thuLoi(page: Page, ds: LoiTrang[], phep: RegExp[] = []) {
  page.on('console', (m) => {
    if (m.type() !== 'error') return;
    const text = m.text();
    if (CONSOLE_BO_QUA.some((re) => re.test(text))) return;
    ds.push({ loai: 'console', noiDung: `${text} @ ${page.url()}` });
  });
  page.on('pageerror', (e) => ds.push({ loai: 'pageerror', noiDung: `${e.message} @ ${page.url()}` }));
  page.on('response', (r) => {
    const duongDan = new URL(r.url()).pathname;
    if (!duongDan.startsWith('/api/')) return;
    const mo = `${r.status()} ${r.request().method()} ${duongDan}`;
    if (r.status() >= 500) ds.push({ loai: 'api5xx', noiDung: mo });
    // Giao diện gọi API mà người dùng không có quyền hoặc đối tượng không tồn tại là lỗi nối giao diện - API.
    else if ((r.status() === 403 || r.status() === 404) && r.request().method() === 'GET' && !phep.some((re) => re.test(duongDan))) {
      ds.push({ loai: 'api403404', noiDung: mo });
    }
  });
}

interface Fixtures {
  loi: LoiTrang[];
  /** Đường dẫn /api được phép trả 403/404 ở kịch bản hiện tại. */
  cho4xx: RegExp[];
  /** Mở trang đã đăng nhập sẵn bằng tài khoản thật (cùng bộ thu lỗi). */
  mo: (tenDangNhap: string, tuyChon?: { viewport?: { width: number; height: number } }) => Promise<Page>;
}

// Dùng chung cho mọi kịch bản: sau mỗi kịch bản không được còn lỗi JS hoặc 5xx từ /api trên bất kỳ trang nào đã mở.
export const test = base.extend<Fixtures>({
  loi: async ({}, use) => {
    const ds: LoiTrang[] = [];
    await use(ds);
    expect(ds, 'Lỗi JavaScript, phản hồi 5xx hoặc 403/404 không chủ ý từ /api').toEqual([]);
  },
  cho4xx: async ({}, use) => {
    await use([]);
  },
  page: async ({ page, loi, cho4xx }, use) => {
    thuLoi(page, loi, cho4xx);
    await use(page);
  },
  mo: async ({ browser, loi, cho4xx }, use) => {
    const contexts: BrowserContext[] = [];
    await use(async (tenDangNhap, tuyChon) => {
      const ctx = await taoNguoiDung(browser, tenDangNhap, tuyChon?.viewport);
      contexts.push(ctx);
      const page = await ctx.newPage();
      thuLoi(page, loi, cho4xx);
      return page;
    });
    for (const c of contexts) await c.close();
  },
});

export { expect };

const trangThaiDangNhap = new Map<string, Awaited<ReturnType<BrowserContext['storageState']>>>();

/** Đăng nhập qua API (nhanh) rồi dùng lại cookie cho mọi kịch bản; kịch bản đăng nhập bằng giao diện nằm riêng ở 01. */
export async function taoNguoiDung(browser: Browser, tenDangNhap: string, viewport?: { width: number; height: number }) {
  const baseURL = process.env.E2E_BASE_URL || 'http://localhost:3400';
  let state = trangThaiDangNhap.get(tenDangNhap);
  if (!state) {
    const tam = await browser.newContext({ baseURL });
    const res = await tam.request.post('/api/auth/login', { data: { username: tenDangNhap, password: MAT_KHAU } });
    expect(res.ok(), `Đăng nhập ${tenDangNhap} bằng API không thành công (${res.status()})`).toBeTruthy();
    state = await tam.storageState();
    await tam.close();
    trangThaiDangNhap.set(tenDangNhap, state);
  }
  return browser.newContext({ baseURL, storageState: state, locale: 'vi-VN', timezoneId: 'Asia/Ho_Chi_Minh', acceptDownloads: true, viewport });
}

/** Chờ trang nghiệp vụ tải xong: không còn vòng quay tải. */
export async function choTaiXong(page: Page) {
  await page.waitForLoadState('networkidle').catch(() => undefined);
}

/** Thông báo (toast) đang hiện, theo tiêu đề hoặc nội dung. */
export function thongBao(page: Page, noiDung: string | RegExp) {
  return page.locator('[role="status"], [role="alert"]').filter({ hasText: noiDung });
}
