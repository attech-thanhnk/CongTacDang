// Trang liền trước trong phiên, để nút quay lại biết có nên dùng lịch sử trình duyệt hay không.
let current: string | null = null;
let previous: string | null = null;

export function recordPath(pathname: string) {
  if (current !== pathname) {
    previous = current;
    current = pathname;
  }
}

export function getPreviousPath(): string | null {
  return previous;
}
