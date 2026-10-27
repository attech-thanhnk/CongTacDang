import { test, expect, thongBao } from './fixtures';
import { TRUNG, dongY, duongDanHoSo } from './luong';
import { docTrangThai, ghiTrangThai } from './du-lieu';

const TEN_NHOM = 'Phiếu kiểm soát Phòng Kế hoạch - Kinh doanh';

test.describe.serial('Kiểm soát (Mẫu 19)', () => {
  test('lập phiếu kiểm tra cho hồ sơ đã hiệu chuẩn', async ({ mo }) => {
    const page = await mo('loan.ttp');
    await page.goto('/quality-control?tab=control');
    await expect(page.getByRole('tab', { name: 'Kiểm tra' })).toHaveAttribute('aria-selected', 'true');
    await page.getByRole('button', { name: 'Lập phiếu' }).click();

    const hop = page.getByRole('dialog', { name: 'Lập phiếu' });
    await hop.getByLabel(/Nhóm chức danh \/ phạm vi/).fill(TEN_NHOM);
    await hop.getByRole('checkbox', { name: new RegExp(TRUNG.hoTen) }).check();
    await hop.getByRole('button', { name: 'Lập phiếu' }).click();

    await expect(thongBao(page, 'Đã lập phiếu.')).toBeVisible();
    await expect(page).toHaveURL(/\/quality-control\/control-checks\/[0-9a-f-]{36}$/);
    ghiTrangThai({ phieu19: new URL(page.url()).pathname });
    await expect(page.getByText('Đang soạn', { exact: true }).first()).toBeVisible();
  });

  test('điền 10 mục (một mục K/AD kèm lý do), kết luận Đủ điều kiện rồi gửi xác nhận', async ({ mo }) => {
    const { phieu19 } = docTrangThai();
    const page = await mo('loan.ttp');
    await page.goto(phieu19);

    const nhom = page.getByRole('radiogroup', { name: /^Kết quả — / });
    await expect(nhom).toHaveCount(10);
    for (let i = 0; i < 10; i++) {
      const lua = i === 3 ? 'K/AD' : 'Đạt';
      await nhom.nth(i).getByLabel(lua, { exact: true }).check();
      await page.getByLabel('Minh chứng / biện pháp').nth(i).fill(i === 3 ? 'Không áp dụng cho nhóm này do chưa có khiếu nại.' : 'Đã đối chiếu hồ sơ.');
    }
    await page.getByRole('radiogroup', { name: 'Kết luận' }).getByLabel('Đủ điều kiện trình phê duyệt').check();
    await page.getByRole('button', { name: 'Lưu', exact: true }).click();
    await expect(thongBao(page, 'Đã lưu phiếu kiểm tra.')).toBeVisible();

    await page.reload();
    await expect(page.getByRole('radiogroup', { name: 'Kết luận' }).getByLabel('Đủ điều kiện trình phê duyệt')).toBeChecked();
    await expect(page.getByRole('radiogroup', { name: /^Kết quả — / }).nth(3).getByLabel('K/AD', { exact: true })).toBeChecked();

    await page.getByRole('button', { name: 'Gửi xác nhận' }).click();
    await page.getByRole('dialog', { name: 'Gửi xác nhận' }).getByRole('button', { name: 'Gửi xác nhận' }).click();
    await expect(thongBao(page, /Đã gửi/)).toBeVisible();
    await expect(page.getByText('Chờ xác nhận', { exact: true }).first()).toBeVisible();
  });

  test('người lập tự xác nhận bị chặn: không có nút Xác nhận', async ({ mo }) => {
    const { phieu19 } = docTrangThai();
    const page = await mo('loan.ttp');
    await page.goto(phieu19);
    await expect(page.getByText('Chờ xác nhận', { exact: true }).first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'Xác nhận', exact: true })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Gửi xác nhận' })).toHaveCount(0);
  });

  test('hồ sơ vẫn khóa quyết định khi phiếu mới chờ xác nhận', async ({ mo }) => {
    const page = await mo('hung.dt');
    await page.goto(duongDanHoSo(TRUNG.khoa));
    await expect(page.getByRole('button', { name: 'Ghi nhận quyết định' })).toBeDisabled();
  });

  test('người khác xác nhận phiếu; nút quyết định của hồ sơ mở khóa', async ({ mo }) => {
    const { phieu19 } = docTrangThai();
    const page = await mo('hung.dt');
    await page.goto(phieu19);
    await page.getByRole('button', { name: 'Xác nhận', exact: true }).click();
    await page.getByRole('dialog', { name: 'Xác nhận phiếu' }).getByRole('button', { name: 'Xác nhận' }).click();
    await expect(thongBao(page, /Đã xác nhận/)).toBeVisible();
    await expect(page.getByText('Đã xác nhận', { exact: true }).first()).toBeVisible();

    await page.goto(duongDanHoSo(TRUNG.khoa));
    await expect(page.getByRole('button', { name: 'Ghi nhận quyết định' })).toBeEnabled();
    await expect(page.getByText(/Hồ sơ chưa thuộc/)).toHaveCount(0);
  });

  test('ghi nhận quyết định rồi công bố kết quả bằng giao diện', async ({ mo }) => {
    const page = await mo('hung.dt');
    await page.goto(duongDanHoSo(TRUNG.khoa));
    const vung = page.getByRole('region', { name: /Ghi nhận quyết định/ });
    await vung.locator('#B4_DECISION-grade').selectOption({ label: 'Hoàn thành nhiệm vụ' });
    await vung.locator('#B4_DECISION-docno').fill('01-QĐ/ĐU');
    const lyDo = vung.locator('#B4_DECISION-reason');
    if (await lyDo.isVisible()) await lyDo.fill('Theo biên bản họp thống nhất và phiếu kiểm tra.');
    await vung.getByRole('button', { name: 'Ghi nhận quyết định' }).click();
    await dongY(page);
    await expect(thongBao(page, 'Đã lưu thao tác trên hồ sơ.')).toBeVisible();

    await expect(page.getByRole('region', { name: /Công bố/ })).toBeVisible();
    await page.getByRole('region', { name: /Công bố/ }).getByRole('button', { name: 'Công bố' }).click();
    await dongY(page);
    await expect(thongBao(page, 'Đã lưu thao tác trên hồ sơ.')).toBeVisible();
    const duongDan = '/api/evaluations/records/' + duongDanHoSo(TRUNG.khoa).split('/').pop();
    await expect.poll(async () => (await (await page.request.get(duongDan)).json()).data.status).toBe('Published');
  });
});
