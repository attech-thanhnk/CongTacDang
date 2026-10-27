import { test, expect, thongBao } from './fixtures';
import { TRUNG, duongDanHoSo } from './luong';
import { docTrangThai, ghiTrangThai } from './du-lieu';

const TEN_NHOM = 'Nhóm Phòng Kế hoạch - Kinh doanh';

test.describe.serial('Hiệu chuẩn kết quả (Mẫu 20)', () => {
  test('trước hiệu chuẩn nút ghi nhận quyết định bị khóa và nêu lý do', async ({ mo }) => {
    const page = await mo('hung.dt');
    await page.goto(duongDanHoSo(TRUNG.khoa));
    await expect(page.getByText(/chưa thuộc biên bản hiệu chuẩn nào còn hiệu lực/)).toBeVisible();
    await expect(page.getByRole('button', { name: 'Ghi nhận quyết định' })).toBeDisabled();
  });

  test('lập biên bản: chọn hồ sơ, nhập tên nhóm', async ({ mo }) => {
    const page = await mo('loan.ctp');
    await page.goto('/quality-control');
    await expect(page.getByRole('tab', { name: 'Họp thống nhất' })).toHaveAttribute('aria-selected', 'true');
    await page.getByRole('button', { name: 'Lập biên bản' }).click();

    const hop = page.getByRole('dialog', { name: 'Lập biên bản' });
    const lap = hop.getByRole('button', { name: 'Lập biên bản' });
    // Chưa nhập tên nhóm và chưa chọn hồ sơ thì chưa lập được.
    await expect(lap).toBeDisabled();
    await hop.getByLabel(/Nhóm chức danh \/ phạm vi/).fill(TEN_NHOM);
    await hop.getByRole('checkbox', { name: new RegExp(TRUNG.hoTen) }).check();
    await expect(lap).toBeEnabled();
    await lap.click();

    await expect(thongBao(page, 'Đã lập biên bản.')).toBeVisible();
    await expect(page).toHaveURL(/\/quality-control\/calibrations\/[0-9a-f-]{36}$/);
    ghiTrangThai({ bienBan20: new URL(page.url()).pathname });
    await expect(page.getByRole('heading', { name: 'Biên bản hiệu chuẩn' })).toBeVisible();
    await expect(page.getByText('Đang soạn', { exact: true }).first()).toBeVisible();
  });

  test('điền chủ trì, thư ký, thành phần và một ca điều chỉnh rồi lưu; tải lại giữ nguyên', async ({ mo }) => {
    const { bienBan20 } = docTrangThai();
    const page = await mo('loan.ctp');
    await page.goto(bienBan20);
    await expect(page.getByLabel('Chủ trì')).toBeVisible();

    await page.getByLabel('Chủ trì').fill('Chu Thị Phương Loan');
    await page.getByLabel('Thư ký').fill('Lê Thị Thu');
    await page.getByLabel('Địa điểm').fill('Phòng họp Đảng ủy');
    await page.locator('#cal-held').fill('26/10/2026');
    await page.locator('#cal-extra').fill('Biên bản chỉ có một hồ sơ nên không có nhóm tương đồng để đối chiếu.');

    await page.getByRole('button', { name: 'Thêm người' }).click();
    await page.locator('#cal-att-0').fill('Chu Thị Phương Loan');
    await page.locator('#cal-cap-0').fill('Chủ trì');

    await page.getByRole('combobox', { name: 'Thêm trường hợp thảo luận' }).selectOption({ label: TRUNG.hoTen });
    await page.locator('#cal-case-0-cl').fill('Đối chiếu minh chứng thấy mức tự chấm cao hơn kết quả thực tế.');
    await page.locator('#cal-case-0-gr').selectOption({ label: 'Hoàn thành nhiệm vụ' });
    await page.locator('#cal-case-0-sc').fill('65');
    await page.locator('#cal-case-0-rp').fill('Chu Thị Phương Loan');
    await page.locator('#cal-case-0-ba').fill('Biên bản đối chiếu minh chứng ngày họp.');

    await page.getByRole('button', { name: 'Lưu', exact: true }).click();
    await expect(thongBao(page, 'Đã lưu biên bản hiệu chuẩn.')).toBeVisible();

    await page.reload();
    await expect(page.getByLabel('Chủ trì')).toHaveValue('Chu Thị Phương Loan');
    await expect(page.getByLabel('Thư ký')).toHaveValue('Lê Thị Thu');
    await expect(page.locator('#cal-held')).toHaveValue('26/10/2026');
    await expect(page.locator('#cal-att-0')).toHaveValue('Chu Thị Phương Loan');
    await expect(page.locator('#cal-case-0-sc')).toHaveValue('65');
    await expect(page.locator('#cal-case-0-gr').locator('option:checked')).toHaveText('Hoàn thành nhiệm vụ');
  });

  test('người lập biên bản không tự chốt được; người khác chốt thành công', async ({ mo }) => {
    const { bienBan20 } = docTrangThai();
    const nguoiLap = await mo('loan.ctp');
    await nguoiLap.goto(bienBan20);
    await expect(nguoiLap.getByRole('heading', { name: 'Biên bản hiệu chuẩn' })).toBeVisible();
    await expect(nguoiLap.getByRole('button', { name: 'Chốt biên bản' })).toHaveCount(0);

    const khac = await mo('long.nc');
    await khac.goto(bienBan20);
    await khac.getByRole('button', { name: 'Chốt biên bản' }).click();
    await khac.getByRole('dialog', { name: 'Chốt biên bản' }).getByRole('button', { name: 'Chốt biên bản' }).click();
    await expect(thongBao(khac, 'Đã chốt biên bản hiệu chuẩn.')).toBeVisible();
    await expect(khac.getByText('Đã chốt').first()).toBeVisible();
  });

  test('sau khi chốt: hồ sơ hiện kết quả sau hiệu chuẩn, nút quyết định vẫn khóa chờ phiếu kiểm soát', async ({ mo }) => {
    const page = await mo('hung.dt');
    await page.goto(duongDanHoSo(TRUNG.khoa));
    const khoiThamDinh = page.locator('button[aria-expanded]').filter({ hasText: /^Thẩm định/ });
    if ((await khoiThamDinh.getAttribute('aria-expanded')) === 'false') await khoiThamDinh.click();
    await expect(page.getByText('Mức sau hiệu chuẩn')).toBeVisible();
    await expect(page.getByRole('link', { name: /Xem biên bản/ })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Ghi nhận quyết định' })).toBeDisabled();
    await expect(page.getByText(/phiếu kiểm soát/i).first()).toBeVisible();
    await expect(page.getByText(/chưa thuộc biên bản hiệu chuẩn nào/)).toHaveCount(0);
  });

  test('danh sách biên bản hiện biên bản đã chốt', async ({ mo }) => {
    const page = await mo('hung.dt');
    await page.goto('/quality-control');
    await expect(page.locator('tr', { hasText: TEN_NHOM })).toContainText('Đã chốt');
  });
});
