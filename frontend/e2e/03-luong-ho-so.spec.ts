import { test, expect } from './fixtures';
import type { Page } from '@playwright/test';
import { DAO, TRUNG, daLuuThaoTac, guiThaoTac, duongDanHoSo, luuHoSo, moTuViecCanXuLy, nopDanhMuc } from './luong';

// Đưa hai hồ sơ qua luồng bằng giao diện: Trung tới "Chờ quyết định"; Đào dừng ở "Chờ thẩm định" (dùng cho phiếu đối soát).

async function chonVung(page: Page, ten: string | RegExp) {
  const vung = page.getByRole('region', { name: ten });
  await expect(vung, `Thiếu khu thao tác "${ten}"`).toBeVisible();
  return vung;
}

test.describe.serial('Luồng hồ sơ qua giao diện', () => {
  for (const ho of [TRUNG, DAO]) {
    test(`B1 ${ho.hoTen} đăng ký danh mục sản phẩm`, async ({ mo }) => {
      const page = await mo(ho.tenDangNhap);
      await moTuViecCanXuLy(page, ho.hoTen, 'Đăng ký');
      luuHoSo(ho.khoa, page);
      await nopDanhMuc(page, ho.khoa === 'trung' ? 'Trung' : 'Dao');
      await expect(page.getByText(/Chờ: .*Lãnh đạo Phòng/).first()).toBeVisible();
    });
  }

  test('B1 Lãnh đạo Phòng duyệt danh mục của hai hồ sơ', async ({ mo }) => {
    const page = await mo('duy.lm');
    for (const ho of [TRUNG, DAO]) {
      await moTuViecCanXuLy(page, ho.hoTen, 'Duyệt');
      const vung = await chonVung(page, 'Duyệt danh mục');
      await guiThaoTac(page, vung.getByRole('button', { name: 'Duyệt', exact: true }));
    }
    await page.goto('/work-queue');
    await expect(page.getByRole('row').filter({ hasText: TRUNG.hoTen })).toHaveCount(0);
  });

  for (const ho of [TRUNG, DAO]) {
    test(`B2 ${ho.hoTen} tự chấm và nộp phiếu`, async ({ mo }) => {
      const page = await mo(ho.tenDangNhap);
      await moTuViecCanXuLy(page, ho.hoTen, /Tự chấm|Nộp/);
      const ten = ho.khoa === 'trung' ? 'Trung' : 'Dao';
      await expect(page.locator('#self-grade')).toBeVisible();
      for (let i = 1; i <= 3; i++) {
        for (const k of ['A', 'B', 'C', 'D']) await page.getByLabel(`Thành phần ${k} — ${ten} sản phẩm ${i}`).fill('90');
      }
      await page.locator('#self-grade').selectOption({ label: 'Hoàn thành tốt nhiệm vụ' });
      await page.getByRole('button', { name: 'Nộp phiếu', exact: true }).click();
      await page.getByRole('dialog', { name: 'Nộp phiếu tự chấm' }).getByRole('button', { name: 'Nộp phiếu' }).click();
      await daLuuThaoTac(page);
      await expect(page.getByText(/Chờ: .*Chi bộ|Chờ Chi bộ/i).first()).toBeVisible();
    });
  }

  test('B2 Chi bộ xác nhận phiếu tự chấm (người khác chủ hồ sơ)', async ({ mo }) => {
    const daoXacNhan = await mo(DAO.tenDangNhap);
    await moTuViecCanXuLy(daoXacNhan, TRUNG.hoTen, 'Xác nhận');
    await guiThaoTac(daoXacNhan, (await chonVung(daoXacNhan, /Chi bộ xác nhận/)).getByRole('button', { name: 'Xác nhận', exact: true }));

    // Chủ hồ sơ không tự xác nhận phiếu của mình: việc của Đào chỉ hiện ở người khác của Chi bộ.
    const duy = await mo('duy.lm');
    await moTuViecCanXuLy(duy, DAO.hoTen, 'Xác nhận');
    await guiThaoTac(duy, (await chonVung(duy, /Chi bộ xác nhận/)).getByRole('button', { name: 'Xác nhận', exact: true }));
  });

  test('B3a tập thể lãnh đạo đề xuất: thư ký ghi nhận', async ({ mo }) => {
    const thuKy = await mo('dao.nta');
    await moTuViecCanXuLy(thuKy, TRUNG.hoTen, 'Đề xuất');
    let vung = await chonVung(thuKy, /đề xuất/i);
    await vung.locator('#B3A_COLLECTIVE-grade').selectOption({ label: 'Hoàn thành tốt nhiệm vụ' });
    await guiThaoTac(thuKy, vung.getByRole('button', { name: 'Ghi nhận đề xuất' }));

    // Hồ sơ của chính thư ký do người khác ghi nhận.
    const khac = await mo('loan.ttp');
    await moTuViecCanXuLy(khac, DAO.hoTen, 'Đề xuất');
    vung = await chonVung(khac, /đề xuất/i);
    await vung.locator('#B3A_COLLECTIVE-grade').selectOption({ label: 'Hoàn thành tốt nhiệm vụ' });
    await guiThaoTac(khac, vung.getByRole('button', { name: 'Ghi nhận đề xuất' }));
  });

  test('B3b cơ quan thẩm định thẩm định hồ sơ của Trung', async ({ mo }) => {
    const page = await mo('loan.ctp');
    await moTuViecCanXuLy(page, TRUNG.hoTen, 'Thẩm định');
    const vung = await chonVung(page, /Thẩm định/);
    const mucThamDinh = vung.getByRole('combobox', { name: /^Mức thẩm định:/ });
    for (let i = 0; i < (await mucThamDinh.count()); i++) await mucThamDinh.nth(i).selectOption({ index: 2 });
    await vung.locator('#ap-grade').selectOption({ label: 'Hoàn thành tốt nhiệm vụ' });
    await vung.getByLabel('Ý kiến thẩm định').fill('Hồ sơ đầy đủ, kết quả phù hợp.');
    const giaiTrinh = vung.getByLabel(/^Giải trình/);
    if (await giaiTrinh.isVisible()) await giaiTrinh.fill('Điều chỉnh theo minh chứng đã đối chiếu.');
    await guiThaoTac(page, vung.getByRole('button', { name: 'Ghi nhận thẩm định' }));
  });

  test('B3c cấp trực tiếp sử dụng nhận xét, hồ sơ chuyển sang chờ quyết định', async ({ mo }) => {
    const page = await mo('giang.nh');
    await moTuViecCanXuLy(page, TRUNG.hoTen, 'Nhận xét');
    const vung = await chonVung(page, /Nhận xét/);
    await vung.locator('#gc-grade').selectOption({ label: 'Hoàn thành tốt nhiệm vụ' });
    await guiThaoTac(page, vung.getByRole('button', { name: 'Ghi nhận nhận xét' }));

    await page.goto(duongDanHoSo('trung'));
    await expect(page.getByRole('button', { name: /Quyết định mức xếp loại: Chờ/ })).toBeVisible();
    const ban = await page.request.get('/api/evaluations/records/' + duongDanHoSo('trung').split('/').pop());
    expect((await ban.json()).data.status).toBe('AwaitingDecision');
  });
});
