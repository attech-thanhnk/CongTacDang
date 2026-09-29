const isDev = process.env.NODE_ENV === 'development';

// Chế độ dev của Next.js cần eval (React Refresh) và WebSocket HMR; production giữ CSP chặt.
const contentSecurityPolicy = [
  "default-src 'self'",
  "base-uri 'self'",
  "object-src 'none'",
  "frame-ancestors 'none'",
  "frame-src 'self' blob:",
  "img-src 'self' data: blob:",
  "font-src 'self' data: https://fonts.gstatic.com",
  "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",
  `script-src 'self' 'unsafe-inline'${isDev ? " 'unsafe-eval'" : ''}`,
  `connect-src 'self'${isDev ? ' ws: wss:' : ''}`,
  "form-action 'self'",
].join('; ');

/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  // Tách cache dev khỏi production build để tránh mất chunk khi build lúc dev đang chạy.
  distDir: isDev ? '.next-dev' : '.next',
  output: 'standalone',
  // rewrites được tính LÚC BUILD và ghi cố định vào bản build: BACKEND_INTERNAL_URL phải có khi chạy
  // `npm run build` (Docker: build arg trong Dockerfile.frontend), đặt lúc chạy `next start` không có tác dụng.
  async rewrites() {
    const backendUrl = process.env.BACKEND_INTERNAL_URL || 'http://localhost:5000';
    return [
      {
        source: '/api/:path*',
        destination: `${backendUrl}/api/:path*`,
      },
    ];
  },
  async headers() {
    return [
      {
        source: '/(.*)',
        headers: [
          {
            key: 'Content-Security-Policy',
            value: contentSecurityPolicy,
          },
          {
            key: 'X-Frame-Options',
            value: 'DENY',
          },
          {
            key: 'X-Content-Type-Options',
            value: 'nosniff',
          },
          {
            key: 'Referrer-Policy',
            value: 'no-referrer',
          },
          {
            key: 'Permissions-Policy',
            value: 'camera=(), geolocation=(), microphone=(), payment=()',
          },
        ],
      },
    ];
  },
};

export default nextConfig;

