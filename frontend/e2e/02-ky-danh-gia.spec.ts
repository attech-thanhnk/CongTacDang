import { test, expect, thongBao } from './fixtures';
import type { Page } from '@playwright/test';
import { TEN_KY, luuDuongDanKy } from './du-lieu';

// Người được đánh giá của kịch bản: Phòng Kế hoạch - Kinh doanh (Trung và Đào; Duy bị bỏ nếu thiếu người duyệt).
const PHONG = 'Phòng Kế hoạch - Kinh doanh';

async function moKy(page: Page) {
  await page.goto('/periods');
  await page.getByRole('row', { name: new RegExp(TEN_KY) }).click();
  await expect(page.getByRole('heading', { name: TEN_KY })).toBeVisible();
  await expect(page.getByRole('tab', { name: 'Diện quyết định' })).toBeVisible();
}

// Mặc định của kỳ mới là chỉ ghi nhận Mẫu 19, 20; các kịch bản sau kiểm tra hồ sơ bị khóa nên đặt hai cổng bắt buộc.
async function datCongBatBuoc(page: Page) {
  const id = new URL(page.url()).pathname.split('/').pop();
  const ky = (await (await page.request.get(`/api/evaluations/periods/${id}`)).json()).data;
  for (const profile of ky.settings.profiles) {
    profile.gates.CALIBRATION = { mode: 'Required' };
    profile.gates.CONTROL = { mode: 'Required' };
  }
  const luu = await page.request.put(`/api/evaluations/periods/${id}`, { data: { version: ky.version, settings: ky.settings } });
  expect(luu.ok()).toBeTruthy();
}

test.describe.serial('Kỳ đánh giá', () => {
  test('tạo kỳ ở trạng thái dự thảo', async ({ mo }) => {
    const page = await mo('loan.ctp');
    await page.goto('/periods');
    await expect(page.getByText('Chưa có kỳ đánh giá')).toBeVisible();

    await page.getByRole('button', { name: 'Tạo kỳ đánh giá' }).click();
    const hop = page.getByRole('dialog', { name: 'Tạo kỳ đánh giá' });
    await hop.getByLabel('Năm').fill('2026');
    await hop.getByLabel('Quý').selectOption('4');
    await expect(hop.getByRole('button', { name: 'Tạo kỳ', exact: true })).toBeEnabled();
    await hop.getByRole('button', { name: 'Tạo kỳ', exact: true }).click();

    await expect(thongBao(page, 'Đã tạo kỳ đánh giá (dự thảo).')).toBeVisible();
    await expect(page).toHaveURL(/\/periods\/[0-9a-f-]{36}$/);
    luuDuongDanKy(new URL(page.url()).pathname);
    await datCongBatBuoc(page);
    await page.reload();
    await expect(page.getByRole('heading', { name: TEN_KY })).toBeVisible();
    await expect(page.getByText('Dự thảo', { exact: true }).first()).toBeVisible();
    // Chưa có người được đánh giá nên chưa mở được kỳ.
    await expect(page.getByRole('button', { name: 'Mở kỳ' })).toBeDisabled();
  });

  test('thêm người theo đơn vị, đặt thời hạn và kiểm tra sẵn sàng', async ({ mo }) => {
    const page = await mo('loan.ctp');
    await moKy(page);

    await page.getByLabel('Phòng', { exact: true }).selectOption({ label: PHONG });
    await page.getByRole('button', { name: 'Thêm cả đơn vị' }).click();
    await expect(thongBao(page, 'Đã thêm người được đánh giá theo đơn vị.')).toBeVisible();
    await expect(page.getByRole('link', { name: 'Nguyễn Bình Trung' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Nguyễn Thị Anh Đào' })).toBeVisible();
    await expect(page.getByRole('tab', { name: /Người được đánh giá/ })).toBeVisible();

    // Bước cuối chưa có thời hạn thì chưa mở được kỳ; đặt thời hạn rồi lưu.
    const datThoiHan = page.getByRole('button', { name: 'Đặt thời hạn' });
    if (await datThoiHan.isVisible()) {
      await datThoiHan.click();
      await page.getByLabel('Thời hạn — Công bố và lưu trữ kết quả', { exact: true }).fill('31/12/2026');
      await page.getByRole('button', { name: 'Lưu', exact: true }).click();
      await expect(thongBao(page, 'Đã lưu cấu hình kỳ.')).toBeVisible();
    }
    await expect(page.getByText('Các bước đã có thời hạn')).toBeVisible();
  });

  test('còn bước chưa có người phụ trách thì mở kỳ bị từ chối cho tới khi bỏ người đó khỏi kỳ', async ({ mo }) => {
    const page = await mo('loan.ctp');
    await moKy(page);
    // Kết quả kiểm tra tải bất đồng bộ: chờ có dòng kết luận về người thực hiện.
    await expect(page.getByText(/Mọi bước đều có người thực hiện|bước chưa có người thực hiện/)).toBeVisible();

    if (await page.getByText(/bước chưa có người thực hiện/).isVisible()) {
      // Mở kỳ khi còn bước kẹt: máy chủ từ chối, giao diện chuyển sang bảng người cần phân công và đề nghị mở bắt buộc.
      await page.getByRole('button', { name: 'Mở kỳ' }).click();
      await page.getByRole('dialog', { name: 'Mở kỳ đánh giá' }).getByRole('button', { name: 'Mở kỳ' }).click();
      await expect(thongBao(page, /Chưa mở được kỳ|chưa có người/)).toBeVisible();
      const batBuoc = page.getByRole('dialog', { name: 'Mở kỳ bắt buộc' });
      await expect(batBuoc).toBeVisible();
      await batBuoc.getByRole('button', { name: 'Hủy' }).click();
      await expect(batBuoc).toHaveCount(0);

      const dong = page.getByRole('row', { name: /Duyệt danh mục sản phẩm/ }).first();
      await expect(dong).toBeVisible();
      const ten = (await dong.getByRole('link').first().innerText()).trim();

      await page.getByRole('tab', { name: /Người được đánh giá/ }).click();
      await page.getByRole('button', { name: `Thao tác khác — ${ten}` }).click();
      await page.getByRole('menuitem', { name: 'Bỏ khỏi kỳ' }).click();
      await page.getByRole('dialog').getByRole('button', { name: 'Bỏ khỏi kỳ' }).click();
      await expect(thongBao(page, 'Đã bỏ khỏi danh sách.')).toBeVisible();
    }
    await expect(page.getByText('Mọi bước đều có người thực hiện')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Mở kỳ' })).toBeEnabled();
  });

  test('chỉnh chế độ cổng khi kỳ dự thảo, lưu và tải lại vẫn giữ nguyên', async ({ mo }) => {
    const page = await mo('loan.ctp');
    await moKy(page);
    await page.getByRole('tab', { name: 'Diện quyết định' }).click();

    const kiemSoat = page.locator('#gate-CONTROL');
    const hieuChuan = page.locator('#gate-CALIBRATION');
    const doiSoat = page.locator('#gate-CROSSCHECK');
    await expect(kiemSoat).toHaveValue(/.+/);
    const goc = { kiemSoat: await kiemSoat.inputValue(), hieuChuan: await hieuChuan.inputValue(), doiSoat: await doiSoat.inputValue() };

    await kiemSoat.selectOption({ label: 'Chỉ ghi nhận' });
    await hieuChuan.selectOption({ label: 'Chỉ ghi nhận' });
    await doiSoat.selectOption({ label: 'Tắt' });
    await page.getByRole('button', { name: 'Lưu', exact: true }).click();
    await expect(thongBao(page, 'Đã lưu cấu hình kỳ.')).toBeVisible();

    await page.reload();
    await page.getByRole('tab', { name: 'Diện quyết định' }).click();
    await expect(page.locator('#gate-CONTROL').locator('option:checked')).toHaveText('Chỉ ghi nhận');
    await expect(page.locator('#gate-CALIBRATION').locator('option:checked')).toHaveText('Chỉ ghi nhận');
    await expect(page.locator('#gate-CROSSCHECK').locator('option:checked')).toHaveText('Tắt');

    // Trả về chế độ ban đầu (Bắt buộc, Bắt buộc, Tùy chọn) để các kịch bản sau chạy đúng luồng chuẩn.
    await page.locator('#gate-CONTROL').selectOption(goc.kiemSoat);
    await page.locator('#gate-CALIBRATION').selectOption(goc.hieuChuan);
    await page.locator('#gate-CROSSCHECK').selectOption(goc.doiSoat);
    await page.getByRole('button', { name: 'Lưu', exact: true }).click();
    await expect(thongBao(page, 'Đã lưu cấu hình kỳ.')).toBeVisible();
    await page.reload();
    await page.getByRole('tab', { name: 'Diện quyết định' }).click();
    await expect(page.locator('#gate-CONTROL')).toHaveValue(goc.kiemSoat);
    await expect(page.locator('#gate-CALIBRATION')).toHaveValue(goc.hieuChuan);
    await expect(page.locator('#gate-CROSSCHECK')).toHaveValue(goc.doiSoat);
  });

  test('mở kỳ: trạng thái chuyển Đang mở và cán bộ thấy việc đăng ký sản phẩm', async ({ mo }) => {
    const page = await mo('loan.ctp');
    await moKy(page);
    await page.getByRole('button', { name: 'Mở kỳ' }).click();
    await page.getByRole('dialog', { name: 'Mở kỳ đánh giá' }).getByRole('button', { name: 'Mở kỳ' }).click();
    await expect(thongBao(page, 'Đã mở kỳ đánh giá.')).toBeVisible();
    await expect(page.getByText('Đang mở', { exact: true }).first()).toBeVisible();

    await page.reload();
    await expect(page.getByText('Đang mở', { exact: true }).first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'Mở kỳ' })).toHaveCount(0);

    const canBo = await mo('trung.nb');
    await canBo.goto('/work-queue');
    await expect(canBo.getByText('Đăng ký sản phẩm, nhiệm vụ').first()).toBeVisible();
  });
});
