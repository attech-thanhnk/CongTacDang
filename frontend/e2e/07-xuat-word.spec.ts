import { test, expect } from './fixtures';
import { DAO, TRUNG, duongDanHoSo } from './luong';
import { docTrangThai } from './du-lieu';
import { kiemTraDocx, tai } from './xuat-tep';

// Xuất Word bằng nút trên giao diện: tệp tải về phải là .docx hợp lệ, có nội dung và không còn thẻ chưa điền.
test.describe('Xuất Word từ giao diện', () => {
  for (const mau of [
    { ten: 'biên bản hiệu chuẩn (Mẫu 20)', khoa: 'bienBan20', nguoi: 'loan.ctp', can: [TRUNG.hoTen] },
    { ten: 'phiếu kiểm soát (Mẫu 19)', khoa: 'phieu19', nguoi: 'loan.ttp', can: [TRUNG.hoTen] },
    { ten: 'phiếu đối soát (Mẫu 03)', khoa: 'phieu03', nguoi: 'loan.ctp', can: [DAO.hoTen] },
  ]) {
    test(`xuất ${mau.ten}`, async ({ mo }) => {
      const duongDan = docTrangThai()[mau.khoa];
      const page = await mo(mau.nguoi);
      await page.goto(duongDan);
      await expect(page.getByRole('button', { name: /^Thao tác khác/ })).toBeVisible();
      const tep = await tai(page, async () => {
        await page.getByRole('button', { name: /^Thao tác khác/ }).click();
        await page.getByRole('menuitem', { name: /Xuất Word/ }).click();
      });
      await kiemTraDocx(tep, { chua: mau.can });
    });
  }

  test('xuất Mẫu 01 và Mẫu 02 từ trang hồ sơ', async ({ mo }) => {
    const page = await mo('loan.ctp');
    await page.goto(duongDanHoSo(TRUNG.khoa));
    for (const [ma, noiDung] of [['Mẫu 01', 'Trung sản phẩm 1'], ['Mẫu 02', 'Trung sản phẩm 1']]) {
      const dong = page.getByRole('listitem').filter({ hasText: new RegExp(`^${ma} ·`) });
      await expect(dong).toBeVisible();
      const tep = await tai(page, () => dong.getByRole('button', { name: 'Xuất Word' }).click());
      await kiemTraDocx(tep, { chua: [TRUNG.hoTen, noiDung] });
    }
  });
});
