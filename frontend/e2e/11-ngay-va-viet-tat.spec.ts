import { test, expect, thongBao } from './fixtures';

test.describe('Ô ngày dd/mm/yyyy và các ô mới của quản trị', () => {
  test('Thông tin đơn vị: số và ngày văn bản hướng dẫn lưu đúng ngày, tải lại vẫn đúng', async ({ mo }) => {
    const page = await mo('quantri');
    await page.goto('/admin/settings');
    const so = page.getByLabel('Số văn bản hướng dẫn');
    const ngay = page.getByLabel('Ngày văn bản hướng dẫn', { exact: true });
    await expect(so).toBeVisible();

    await so.fill('55-HD/TVĐU');
    await ngay.fill('05/11/2026');
    await page.getByRole('button', { name: 'Lưu', exact: true }).click();
    await expect(thongBao(page, 'Đã cập nhật thông tin đơn vị.')).toBeVisible();

    await page.reload();
    await expect(page.getByLabel('Số văn bản hướng dẫn')).toHaveValue('55-HD/TVĐU');
    await expect(page.getByLabel('Ngày văn bản hướng dẫn', { exact: true })).toHaveValue('05/11/2026');

    // Ngày không có thật bị chặn ở ô, không gửi đi.
    await page.getByLabel('Ngày văn bản hướng dẫn', { exact: true }).fill('31/02/2026');
    await page.getByLabel('Ngày văn bản hướng dẫn', { exact: true }).blur();
    await expect(page.getByText('Ngày theo dạng dd/mm/yyyy.')).toBeVisible();

    // Trả về giá trị mặc định.
    await page.getByLabel('Số văn bản hướng dẫn').fill('03-HD/TVĐU');
    await page.getByLabel('Ngày văn bản hướng dẫn', { exact: true }).fill('10/09/2026');
    await page.getByRole('button', { name: 'Lưu', exact: true }).click();
    await expect(thongBao(page, 'Đã cập nhật thông tin đơn vị.')).toBeVisible();
  });

  test('Danh mục tổ chức: nhập và xóa tên viết tắt của đơn vị', async ({ mo }) => {
    const page = await mo('quantri');
    await page.goto('/catalog');
    await page.getByRole('button', { name: 'Sửa', exact: true }).first().click();
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel('Tên viết tắt').fill('E2E-VT');
    await dialog.getByRole('button', { name: 'Lưu', exact: true }).click();
    await expect(thongBao(page, /Đã cập nhật đơn vị/)).toBeVisible();

    await page.getByRole('button', { name: 'Sửa', exact: true }).first().click();
    await expect(page.getByRole('dialog').getByLabel('Tên viết tắt')).toHaveValue('E2E-VT');
    await page.getByRole('dialog').getByLabel('Tên viết tắt').fill('');
    await page.getByRole('dialog').getByRole('button', { name: 'Lưu', exact: true }).click();
    await expect(thongBao(page, /Đã cập nhật đơn vị/).last()).toBeVisible();
  });

  test('Ngày nghỉ: thêm bằng ô dd/mm/yyyy, danh sách hiện đúng ngày', async ({ mo }) => {
    const page = await mo('quantri');
    await page.goto('/catalog');
    await page.getByRole('button', { name: /Ngày nghỉ/ }).click();
    await page.getByRole('button', { name: 'Thêm ngày nghỉ' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByPlaceholder('VD: Quốc khánh, Tết Nguyên đán').fill('Ngày thử e2e');
    await dialog.getByLabel('Từ ngày', { exact: true }).fill('05/11/2026');
    await dialog.getByRole('button', { name: 'Lưu', exact: true }).click();
    await expect(thongBao(page, 'Đã thêm 1 ngày nghỉ.')).toBeVisible();
    await expect(page.getByRole('row', { name: /05\/11\/2026.*Ngày thử e2e/ })).toBeVisible();
  });
});
