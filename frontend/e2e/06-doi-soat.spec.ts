import { test, expect, thongBao } from './fixtures';
import type { Page } from '@playwright/test';
import { DAO, duongDanHoSo } from './luong';
import { docTrangThai, ghiTrangThai } from './du-lieu';

// Lý do hồ sơ hiện ở dải thông báo khi còn phiếu đối soát chưa xác nhận hoặc hủy.
const CHAN_THAM_DINH = /phiếu đối soát chuyên ngành chờ lập hoặc xác nhận/;
const CO_QUAN = 'Cục Quản lý hoạt động bay (thử)';

/** Gửi yêu cầu đối soát từ khối "Đối soát chuyên ngành" của hồ sơ Đào (cơ quan ngoài hệ thống: người yêu cầu nhập hộ kết quả). */
async function yeuCau(page: Page, noiDung: string) {
  await page.goto(duongDanHoSo(DAO.khoa));
  const khoi = page.locator('section.card').filter({ has: page.getByRole('heading', { name: 'Đối soát chuyên ngành' }) });
  await khoi.getByRole('button', { name: 'Yêu cầu đối soát' }).click();
  await khoi.getByLabel(/Cơ quan ngoài hệ thống/).check();
  await khoi.getByLabel('Đơn vị đối soát').fill(CO_QUAN);
  await khoi.getByRole('group', { name: 'Lĩnh vực' }).getByRole('checkbox').first().check();
  await khoi.getByLabel('Nội dung yêu cầu').fill(noiDung);
  await khoi.getByRole('combobox', { name: 'Thêm từ sản phẩm đăng ký' }).selectOption({ index: 1 });
  await khoi.getByRole('button', { name: 'Gửi yêu cầu' }).click();
  await expect(thongBao(page, 'Đã gửi yêu cầu đối soát.')).toBeVisible();
  const the = khoi.getByRole('link').filter({ hasText: CO_QUAN }).filter({ hasText: 'Chờ lập phiếu' });
  await expect(the).toBeVisible();
  return the;
}

test.describe.serial('Đối soát chuyên ngành (Mẫu 03)', () => {
  test('yêu cầu đối soát từ hồ sơ ở bước thẩm định: nút thẩm định bị khóa khi còn phiếu mở', async ({ mo }) => {
    const page = await mo('loan.ctp');
    const the = await yeuCau(page, 'Đối chiếu số liệu sản phẩm với dữ liệu gốc của cơ quan quản lý.');
    await expect(the).toContainText('Chờ lập phiếu');
    await the.click();
    await expect(page).toHaveURL(/\/crosschecks\/[0-9a-f-]{36}$/);
    ghiTrangThai({ phieu03: new URL(page.url()).pathname });

    await page.goto(duongDanHoSo(DAO.khoa));
    await expect(page.getByText(CHAN_THAM_DINH)).toBeVisible();
  });

  test('lập phiếu: nhập kết quả, lưu rồi gửi xác nhận; người lập không tự xác nhận', async ({ mo }) => {
    const { phieu03 } = docTrangThai();
    const page = await mo('loan.ctp');
    await page.goto(phieu03);
    await expect(page.getByRole('heading', { name: 'Kết quả đối soát' })).toBeVisible();

    await page.getByLabel('Kết quả đối soát dữ liệu gốc').first().fill('Số liệu khớp với dữ liệu gốc tại cơ quan quản lý.');
    await page.getByLabel('Kết quả', { exact: true }).first().selectOption({ index: 1 });
    await page.getByLabel('Căn cứ / xác nhận chuyên môn').first().fill('Công văn trả lời của cơ quan quản lý.');
    await page.getByLabel('Văn bản số').first().fill('15/CV-TEST');
    await page.getByLabel('Cơ quan ban hành').first().fill(CO_QUAN);
    await page.getByRole('button', { name: 'Lưu kết quả' }).click();
    await expect(thongBao(page, /Đã lưu/)).toBeVisible();

    await page.getByRole('button', { name: 'Gửi xác nhận' }).click();
    await page.getByRole('dialog', { name: 'Gửi xác nhận' }).getByRole('button', { name: 'Gửi xác nhận' }).click();
    await expect(thongBao(page, /Đã gửi/)).toBeVisible();
    await expect(page.getByText('Chờ xác nhận', { exact: true }).first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'Xác nhận', exact: true })).toHaveCount(0);
  });

  test('người xác nhận thấy việc ở "Việc cần xử lý" và danh sách phiếu rồi xác nhận', async ({ mo }) => {
    const { phieu03 } = docTrangThai();
    const page = await mo('hung.dt');
    await page.goto('/work-queue');
    await expect(page.getByRole('row').filter({ hasText: DAO.hoTen }).getByRole('link', { name: 'Xác nhận' })).toBeVisible();

    await page.getByRole('link', { name: 'Đối soát chuyên ngành' }).click();
    await expect(page).toHaveURL(/\/crosschecks$/);
    await expect(page.locator('tr', { hasText: DAO.hoTen })).toContainText('Chờ xác nhận');

    await page.goto(phieu03);
    await page.getByRole('button', { name: 'Xác nhận', exact: true }).click();
    await page.getByRole('dialog', { name: 'Xác nhận phiếu' }).getByRole('button', { name: 'Xác nhận' }).click();
    await expect(thongBao(page, /Đã xác nhận/)).toBeVisible();
    await expect(page.getByText('Đã xác nhận', { exact: true }).first()).toBeVisible();
  });

  test('phiếu đã xác nhận không còn chặn thẩm định', async ({ mo }) => {
    const page = await mo('loan.ctp');
    await page.goto(duongDanHoSo(DAO.khoa));
    await expect(page.getByRole('region', { name: /Thẩm định/ })).toBeVisible();
    await expect(page.getByText(CHAN_THAM_DINH)).toHaveCount(0);
  });

  test('yêu cầu thứ hai rồi hủy kèm lý do: phiếu chuyển Đã hủy, thẩm định vẫn mở', async ({ mo }) => {
    const page = await mo('loan.ctp');
    const the = await yeuCau(page, 'Yêu cầu bổ sung, sau đó hủy vì không còn cần thiết.');
    await the.click();
    await expect(page).toHaveURL(/\/crosschecks\/[0-9a-f-]{36}$/);

    await page.getByRole('button', { name: /Thao tác khác/ }).click();
    await page.getByRole('menuitem', { name: 'Hủy yêu cầu' }).click();
    const hop = page.getByRole('dialog', { name: 'Hủy phiếu đối soát' }).or(page.getByRole('dialog').filter({ hasText: 'Hủy yêu cầu' }));
    await hop.getByRole('textbox').fill('Không còn cần đối soát thêm.');
    await hop.getByRole('button', { name: 'Hủy yêu cầu' }).click();
    await expect(thongBao(page, /Đã hủy/)).toBeVisible();
    await expect(page.getByText('Đã hủy', { exact: true }).first()).toBeVisible();

    await page.goto(duongDanHoSo(DAO.khoa));
    await expect(page.getByRole('region', { name: /Thẩm định/ })).toBeVisible();
    await expect(page.getByText(CHAN_THAM_DINH)).toHaveCount(0);
  });
});
