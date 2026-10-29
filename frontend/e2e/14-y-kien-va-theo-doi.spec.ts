import { test, expect } from './fixtures';
import { TRUNG, duongDanHoSo } from './luong';

test.describe('Ý kiến trước phê duyệt và theo dõi hồ sơ tập thể', () => {
  // Các kịch bản trước đã công bố hồ sơ này nên không còn ghi ý kiến; khối ẩn khi không có ý kiến và không ghi được.
  test('Hồ sơ đã công bố: không hiện khối ý kiến cho người ghi lẫn chủ hồ sơ', async ({ mo }) => {
    const nguoiGhi = await mo('loan.ctp');
    await nguoiGhi.goto(duongDanHoSo(TRUNG.khoa));
    await expect(nguoiGhi.getByRole('heading', { level: 1 })).toBeVisible();
    await expect(nguoiGhi.getByRole('heading', { name: 'Ý kiến trước phê duyệt' })).toHaveCount(0);

    const id = duongDanHoSo(TRUNG.khoa).split('/').pop();
    const ghi = await nguoiGhi.request.post(`/api/evaluations/records/${id}/opinions`, { data: { kind: 'ListCheck', content: 'x' }, failOnStatusCode: false });
    expect(ghi.status()).toBe(409);

    const chuHoSo = await mo(TRUNG.tenDangNhap);
    await chuHoSo.goto(duongDanHoSo(TRUNG.khoa));
    await expect(chuHoSo.getByRole('heading', { level: 1 })).toBeVisible();
    await expect(chuHoSo.getByRole('heading', { name: 'Ý kiến trước phê duyệt' })).toHaveCount(0);
  });

  test('Thẻ Theo dõi hiện bảng tổ chức Đảng của kỳ', async ({ mo }) => {
    const page = await mo('loan.ctp');
    await page.goto('/collective-evaluations');
    await page.getByRole('tab', { name: /^Theo dõi/ }).or(page.getByRole('button', { name: /^Theo dõi/ })).first().click();
    await expect(page.getByRole('columnheader', { name: 'Tổ chức Đảng' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Hạn' })).toBeVisible();
    await expect(page.getByText('Chưa lập').first()).toBeVisible();
  });
});
