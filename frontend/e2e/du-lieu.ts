import fs from 'node:fs';
import path from 'node:path';

/** Tên kỳ do kịch bản 02 tạo; các kịch bản sau tìm kỳ theo tên này. */
export const TEN_KY = 'Đánh giá, xếp loại cán bộ Quý IV/2026';

// Trạng thái chia sẻ giữa các tệp kịch bản (chạy tuần tự trên cùng một CSDL thử).
const TEP = path.join(__dirname, '..', 'e2e-ket-qua', 'trang-thai.json');

export function docTrangThai<T extends Record<string, unknown> = Record<string, string>>(): T {
  try {
    return JSON.parse(fs.readFileSync(TEP, 'utf8')) as T;
  } catch {
    return {} as T;
  }
}

export function ghiTrangThai(phan: Record<string, unknown>) {
  fs.mkdirSync(path.dirname(TEP), { recursive: true });
  fs.writeFileSync(TEP, JSON.stringify({ ...docTrangThai(), ...phan }, null, 2));
}

export function luuDuongDanKy(duongDan: string) {
  ghiTrangThai({ duongDanKy: duongDan });
}
