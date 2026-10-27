import fs from 'node:fs';
import path from 'node:path';

// Xóa trạng thái chia sẻ của lần chạy trước để không lẫn kỳ/hồ sơ cũ.
export default function globalSetup() {
  fs.rmSync(path.join(__dirname, '..', 'e2e-ket-qua', 'trang-thai.json'), { force: true });
}
