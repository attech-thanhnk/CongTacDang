import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

/**
 * Danh sách các đường dẫn công khai không yêu cầu phiên làm việc.
 */
const PUBLIC_PATHS = ['/login', '/favicon.ico'];

/**
 * Next.js Edge Middleware: Bảo vệ toàn bộ không gian làm việc ở cấp máy chủ.
 * Chặn hoàn toàn người dùng chưa xác thực trước khi mã nguồn React/UI được phân phối tới trình duyệt.
 */
export function middleware(request: NextRequest) {
  const { pathname, search } = request.nextUrl;

  // Bỏ qua tài nguyên tĩnh (images, fonts, static chunks) và endpoint API backend
  if (
    pathname.startsWith('/_next') ||
    pathname.startsWith('/static') ||
    pathname.startsWith('/api') ||
    PUBLIC_PATHS.includes(pathname)
  ) {
    if (pathname.startsWith('/api')) {
      const requestHeaders = new Headers(request.headers);
      if (request.ip) {
        requestHeaders.set('x-forwarded-for', request.ip);
      }
      return NextResponse.next({ request: { headers: requestHeaders } });
    }

    // Không tự chuyển /login -> hệ thống dựa trên cookie: cookie có thể đã hết hiệu lực (gây vòng lặp chuyển hướng).
    // AuthGuard chỉ chuyển hướng sau khi /api/auth/me xác nhận phiên hợp lệ.
    return NextResponse.next();
  }

  // Kiểm tra phiên xác thực. auth_token sống theo hạn refresh token; refresh_token (Path=/api/auth) không gửi tới route trang.
  const authToken = request.cookies.get('auth_token')?.value;

  if (!authToken) {
    // Nếu hoàn toàn không có token nào: Lập tức chuyển hướng tới trang /login kèm returnUrl
    const loginUrl = new URL('/login', request.url);
    if (pathname !== '/') {
      loginUrl.searchParams.set('returnUrl', `${pathname}${search}`);
    }
    return NextResponse.redirect(loginUrl);
  }

  return NextResponse.next();
}

/**
 * Áp dụng middleware cho toàn bộ các route ngoại trừ static assets
 */
export const config = {
  matcher: [
    /*
     * Match all request paths except for the ones starting with:
     * - _next/static (static files)
     * - _next/image (image optimization files)
     * - favicon.ico (favicon file)
     */
    '/((?!_next/static|_next/image|favicon.ico).*)',
  ],
};
