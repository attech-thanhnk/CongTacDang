import { test as base, expect, type Browser, type BrowserContext, type Page } from '@playwright/test';

export const MAT_KHAU = process.env.E2E_PASSWORD || 'Thu12345a';

/** Thông điệp console được bỏ qua: trình duyệt tự ghi khi API trả 4xx hợp lệ (mật khẩu sai, bị chặn…). */
const CONSOLE_BO_QUA = [/Failed to load resource: the server responded with a status of 4\d\d/];

export interface LoiTrang {
  loai: 'console' | 'pageerror' | 'api5xx';
  noiDung: string;
}

/** Gắn bộ thu lỗi JS và phản hồi 5xx của /api vào một trang. */
export function thuLoi(page: Page, ds: LoiTrang[]) {
  page.on('console', (m) => {
    if (m.type() !== 'error') return;
    const text = m.text();
    if (CONSOLE_BO_QUA.some((re) => re.test(text))) return;
    ds.push({ loai: 'console', noiDung: `${text} @ ${page.url()}` });
  });
  page.on('pageerror', (e) => ds.push({ loai: 'pageerror', noiDung: `${e.message} @ ${page.url()}` }));
  page.on('response', (r) => {
    if (r.status() >= 500 && new URL(r.url()).pathname.startsWith('/api/')) {
      ds.push({ loai: 'api5xx', noiDung: `${r.status()} ${r.request().method()} ${new URL(r.url()).pathname}` });
    }
  });
}

interface Fixtures {
  loi: LoiTrang[];
  /** Mở trang đã đăng nhập sẵn bằng tài khoản thật (cùng bộ thu lỗi). */
  mo: (tenDangNhap: string, tuyChon?: { viewport?: { width: number; height: number } }) => Promise<Page>;
}

// Dùng chung cho mọi kịch bản: sau mỗi kịch bản không được còn lỗi JS hoặc 5xx từ /api trên bất kỳ trang nào đã mở.
export const test = base.extend<Fixtures>({
  loi: async ({}, use) => {
    const ds: LoiTrang[] = [];
    await use(ds);
    expect(ds, 'Lỗi JavaScript hoặc phản hồi 5xx từ /api').toEqual([]);
  },
  page: async ({ page, loi }, use) => {
    thuLoi(page, loi);
    await use(page);
  },
  mo: async ({ browser, loi }, use) => {
    const contexts: BrowserContext[] = [];
    await use(async (tenDangNhap, tuyChon) => {
      const ctx = await taoNguoiDung(browser, tenDangNhap, tuyChon?.viewport);
      contexts.push(ctx);
      const page = await ctx.newPage();
      thuLoi(page, loi);
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
