import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

const PUBLIC_PATHS = ['/login', '/favicon.ico'];

/** Chặn người chưa đăng nhập trước khi tải trang. */
export function middleware(request: NextRequest) {
  const { pathname, search } = request.nextUrl;

  // Bỏ qua tài nguyên tĩnh và API backend
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

    // Không tự chuyển khỏi /login theo cookie: cookie có thể đã hết hiệu lực (gây vòng lặp chuyển hướng).
    return NextResponse.next();
  }

  // auth_token sống theo hạn refresh token; refresh_token (Path=/api/auth) không gửi tới route trang.
  const authToken = request.cookies.get('auth_token')?.value;

  if (!authToken) {
    const loginUrl = new URL('/login', request.url);
    if (pathname !== '/') {
      loginUrl.searchParams.set('returnUrl', `${pathname}${search}`);
    }
    return NextResponse.redirect(loginUrl);
  }

  return NextResponse.next();
}

export const config = {
  matcher: [
    // Trừ _next/static, _next/image, favicon.ico
    '/((?!_next/static|_next/image|favicon.ico).*)',
  ],
};
