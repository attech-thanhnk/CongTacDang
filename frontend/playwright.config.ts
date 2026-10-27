import { defineConfig, devices } from '@playwright/test';

// baseURL và mật khẩu thử do e2e/chay-e2e.ps1 đặt; có thể tự đặt để chạy với môi trường thử khác.
const baseURL = process.env.E2E_BASE_URL || 'http://localhost:3400';

export default defineConfig({
  testDir: './e2e',
  globalSetup: './e2e/global-setup.ts',
  // Các kịch bản nối tiếp nhau trên cùng một CSDL (kỳ, hồ sơ, phiếu) nên chạy tuần tự theo tên tệp.
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 120_000,
  expect: { timeout: 15_000 },
  outputDir: 'e2e-ket-qua/vet',
  reporter: [['list'], ['html', { outputFolder: 'e2e-ket-qua/bao-cao', open: 'never' }]],
  use: {
    baseURL,
    locale: 'vi-VN',
    timezoneId: 'Asia/Ho_Chi_Minh',
    acceptDownloads: true,
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
    video: 'retain-on-failure',
    actionTimeout: 20_000,
    navigationTimeout: 60_000,
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'], viewport: { width: 1366, height: 800 } } }],
});
