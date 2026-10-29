import { test, expect } from './fixtures';
import type { Page } from '@playwright/test';
import { docTrangThai } from './du-lieu';

// Chữ trong ô bảng, ô chọn, ô nhập phải đọc được: không bị cắt, không bị bẻ giữa từ, không cao bất thường vì cột quá hẹp.
type Loi = { loai: string; chu: string; rong: number; can: number };

async function doChu(page: Page): Promise<Loi[]> {
  return page.evaluate(() => {
    const ra: { loai: string; chu: string; rong: number; can: number }[] = [];
    const canvas = document.createElement('canvas').getContext('2d')!;
    const doRong = (text: string, cs: CSSStyleDeclaration) => {
      canvas.font = `${cs.fontStyle} ${cs.fontWeight} ${cs.fontSize} ${cs.fontFamily}`;
      return canvas.measureText(text).width;
    };
    const thay = (el: Element) => {
      const e = el as HTMLElement;
      if (e.closest('aside, [aria-hidden=true], .visually-hidden, .modal:not(.show)')) return false;
      const r = e.getBoundingClientRect();
      return r.width > 0 && r.height > 0 && getComputedStyle(e).visibility !== 'hidden';
    };
    const ngan = (s: string) => s.replace(/\s+/g, ' ').trim().slice(0, 40);

    // Ô bảng: từ dài nhất phải vừa bề rộng; chữ không bị cắt; cột hẹp thì không được cao quá 5 dòng.
    for (const td of Array.from(document.querySelectorAll('table td, table th'))) {
      if (!thay(td) || td.querySelector('select, input, textarea, button, a.btn')) continue;
      const e = td as HTMLElement;
      const cs = getComputedStyle(e);
      const trong = e.clientWidth - parseFloat(cs.paddingLeft) - parseFloat(cs.paddingRight);
      const chu = (e.innerText || '').replace(/\s+/g, ' ').trim();
      if (!chu || trong <= 0) continue;
      const tuDai = chu.split(' ').reduce((a, b) => (doRong(b, cs) > doRong(a, cs) ? b : a), '');
      if (doRong(tuDai, cs) > trong + 1 && tuDai.length > 3) ra.push({ loai: 'từ bị bẻ hoặc tràn ô', chu: ngan(tuDai), rong: Math.round(trong), can: Math.round(doRong(tuDai, cs)) });
      else if (cs.overflow !== 'visible' && e.scrollWidth > e.clientWidth + 1) ra.push({ loai: 'chữ bị cắt', chu: ngan(chu), rong: e.clientWidth, can: e.scrollWidth });
      else {
        const dong = parseFloat(cs.lineHeight) || parseFloat(cs.fontSize) * 1.4;
        if (e.clientHeight / dong > 5 && trong < 130 && chu.length > 25) ra.push({ loai: 'cột hẹp, chữ xuống quá nhiều dòng', chu: ngan(chu), rong: Math.round(trong), can: Math.round(e.clientHeight / dong) });
      }
    }

    // Ô chọn và ô nhập: giá trị đang hiện phải vừa bề rộng ô.
    for (const el of Array.from(document.querySelectorAll('select, input[type=text], input:not([type])'))) {
      if (!thay(el)) continue;
      const e = el as HTMLSelectElement | HTMLInputElement;
      const cs = getComputedStyle(e);
      const khoang = parseFloat(cs.paddingLeft) + parseFloat(cs.paddingRight); // padding phải của ô chọn đã chừa chỗ cho mũi tên
      const chu = e.tagName === 'SELECT' ? ((e as HTMLSelectElement).selectedOptions[0]?.text ?? '') : (e as HTMLInputElement).value;
      if (!chu || e.clientWidth <= 0) continue;
      const can = doRong(chu, cs);
      if (can > e.clientWidth - khoang + 1) ra.push({ loai: e.tagName === 'SELECT' ? 'ô chọn bị cắt chữ' : 'ô nhập bị cắt chữ', chu: ngan(chu), rong: e.clientWidth, can: Math.round(can + khoang) });
    }
    return ra;
  });
}

async function kiemTraTrang(page: Page, duongDan: string, ds: string[]) {
  await page.goto(duongDan);
  await page.waitForLoadState('networkidle');
  const them = async (nhan: string) => {
    for (const l of await doChu(page)) ds.push(`${nhan}: ${l.loai} "${l.chu}" (ô ${l.rong}px, cần ${l.can}px)`);
  };
  await them(duongDan);
  const tabs = page.getByRole('tab');
  const dem = await tabs.count();
  for (let i = 0; i < dem; i++) {
    const tab = tabs.nth(i);
    const ten = ((await tab.innerText().catch(() => '')) || `tab ${i}`).trim().slice(0, 30);
    if (!(await tab.isVisible().catch(() => false))) continue;
    await tab.click().catch(() => undefined);
    await page.waitForLoadState('networkidle');
    await them(`${duongDan} › ${ten}`);
  }
}

const TRANG: [string, string[]][] = [
  ['loan.ctp', ['/work-queue', '/evaluations', '/periods', '/criteria', '/results', '/reports', '/quality-control', '/collective-evaluations', '/crosschecks']],
  ['quantri', ['/admin/users', '/admin/roles', '/admin/performers', '/admin/imports', '/catalog', '/audit', '/admin/templates', '/admin/settings']],
];

for (const [nguoi, duongDan] of TRANG) {
  test(`chữ đọc được trong bảng và ô nhập: ${nguoi}`, async ({ mo }) => {
    const page = await mo(nguoi, { viewport: { width: 1366, height: 900 } });
    const sai: string[] = [];
    for (const url of duongDan) await kiemTraTrang(page, url, sai);
    expect(sai, sai.join('\n')).toEqual([]);
  });
}

test('chữ đọc được: chi tiết kỳ và hồ sơ', async ({ mo }) => {
  const trangThai = docTrangThai();
  const sai: string[] = [];
  const page = await mo('loan.ctp', { viewport: { width: 1366, height: 900 } });
  const ky = trangThai['duongDanKy'];
  if (typeof ky === 'string') await kiemTraTrang(page, ky, sai);
  for (const khoa of ['hoSo_trung', 'hoSo_dao']) {
    const id = trangThai[khoa];
    if (typeof id === 'string') await kiemTraTrang(page, `/evaluations/${id}`, sai);
  }
  expect(sai, sai.join('\n')).toEqual([]);
});
