import { test, expect, thongBao } from './fixtures';
import { TRUNG, duongDanHoSo } from './luong';

test.describe('Ý kiến trước phê duyệt và theo dõi hồ sơ tập thể', () => {
  test('Cơ quan thẩm định ghi ý kiến, chủ hồ sơ không thấy khối này', async ({ mo }) => {
    const nguoiGhi = await mo('loan.ctp');
    await nguoiGhi.goto(duongDanHoSo(TRUNG.khoa));
    const khoi = nguoiGhi.locator('section.card', { has: nguoiGhi.getByRole('heading', { name: 'Ý kiến trước phê duyệt' }) });
    await khoi.getByRole('button', { name: 'Ghi ý kiến' }).click();
    await khoi.getByLabel('Loại ý kiến').selectOption('ListCheck');
    await khoi.getByLabel('Nội dung ý kiến').fill('Danh mục hợp lệ, đủ 5 sản phẩm');
    await khoi.getByRole('button', { name: 'Lưu' }).click();
    await thongBao(nguoiGhi, 'Đã ghi nhận ý kiến.');
    await expect(khoi.getByText('Kết quả rà soát hợp lệ')).toBeVisible();
    await expect(khoi.getByText('Danh mục hợp lệ, đủ 5 sản phẩm')).toBeVisible();

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
