import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  reactStrictMode: true,
  poweredByHeader: false,
  // Keep the long-running development server's cache separate from `next build`
  // so acceptance checks cannot invalidate a live local server.
  distDir: process.env.ARGIN_NEXT_DIST_DIR ?? ".next",
};

export default nextConfig;
