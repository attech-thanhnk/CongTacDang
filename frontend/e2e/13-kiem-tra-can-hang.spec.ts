import { test, expect } from './fixtures';
import type { Page } from '@playwright/test';

// Mọi cặp điều khiển (nút, ô nhập, ô chọn, tab) nằm cùng một hàng phải cùng tâm dọc, và cùng loại thì cùng chiều cao.
const NGUONG_TAM = 2.5;
const NGUONG_CAO = 4;

type Phan = { ten: string; loai: string; x: number; y: number; w: number; h: number };

async function doHang(page: Page): Promise<string[]> {
  const phan: Phan[] = await page.evaluate(() => {
    const chon = 'button, a.btn, input, select, textarea, [role=tab], .nav-tabs .nav-link';
    const ra: Phan[] = [];
    for (const el of Array.from(document.querySelectorAll(chon))) {
      const e = el as HTMLElement;
      if (e.closest('aside, table, ul.list-group, [role=dialog], .modal, .bell-panel, .visually-hidden, [aria-hidden=true]')) continue;
      if (e instanceof HTMLInputElement && ['checkbox', 'radio', 'hidden', 'file'].includes(e.type)) continue;
      const cs = getComputedStyle(e);
      if (cs.visibility === 'hidden' || cs.display === 'none' || cs.position === 'sticky' || cs.position === 'fixed') continue;
      const r = e.getBoundingClientRect();
      if (r.width < 8 || r.height < 8) continue;
      if (r.bottom < 0 || r.top > window.innerHeight * 2) continue;
      const loai = e.tagName === 'SELECT' ? 'select' : e.tagName === 'INPUT' || e.tagName === 'TEXTAREA' ? 'input' : e.getAttribute('role') === 'tab' || e.classList.contains('nav-link') ? 'tab' : 'nut';
      const ten = (e.getAttribute('aria-label') || e.textContent || e.getAttribute('placeholder') || e.id || e.tagName).trim().slice(0, 24);
      ra.push({ ten, loai, x: r.left, y: r.top, w: r.width, h: r.height });
    }
    return ra;
  });
  const loi: string[] = [];
  for (let i = 0; i < phan.length; i++) {
    for (let j = i + 1; j < phan.length; j++) {
      const a = phan[i];
      const b = phan[j];
      const chong = Math.min(a.y + a.h, b.y + b.h) - Math.max(a.y, b.y);
      if (chong < 0.4 * Math.min(a.h, b.h)) continue;
      if (a.x < b.x + b.w && b.x < a.x + a.w) continue; // chồng ngang: lồng nhau hoặc phủ lên nhau
      const lech = Math.abs(a.y + a.h / 2 - (b.y + b.h / 2));
      if (lech > NGUONG_TAM) loi.push(`"${a.ten}" (${a.loai}) và "${b.ten}" (${b.loai}) lệch tâm ${lech.toFixed(1)}px`);
      else if (a.loai === b.loai && Math.abs(a.h - b.h) > NGUONG_CAO) loi.push(`"${a.ten}" và "${b.ten}" (${a.loai}) cao ${a.h.toFixed(0)} và ${b.h.toFixed(0)}px`);
    }
  }
  return loi;
}

const TRANG: [string, string[]][] = [
  ['loan.ctp', ['/work-queue', '/evaluations', '/periods', '/criteria', '/results', '/reports', '/quality-control', '/collective-evaluations', '/crosschecks']],
  ['quantri', ['/admin/users', '/admin/roles', '/admin/performers', '/admin/imports', '/catalog', '/audit', '/admin/templates', '/admin/settings']],
];

for (const rong of [1366]) {
  for (const [nguoi, duongDan] of TRANG) {
    test(`căn hàng ${rong}px: ${nguoi}`, async ({ mo }) => {
      const page = await mo(nguoi, { viewport: { width: rong, height: 900 } });
      const sai: string[] = [];
      for (const url of duongDan) {
        await page.goto(url);
        await page.waitForLoadState('networkidle');
        for (const l of await doHang(page)) sai.push(`${url}: ${l}`);
      }
      expect(sai, sai.join('\n')).toEqual([]);
    });
  }
}
