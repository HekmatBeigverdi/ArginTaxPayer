import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "آرگین | سامانه مؤدیان",
  description: "زیرساخت سامانه یکپارچه مؤدیان آرگین",
  robots: { index: false, follow: false },
  icons: {
    icon: [{ url: "/brand/argin-logo.png?v=20261017transparent", type: "image/png", sizes: "128x128" }],
    shortcut: "/brand/argin-logo.png?v=20261017transparent",
    apple: [{ url: "/brand/argin-logo.png?v=20261017transparent", sizes: "128x128", type: "image/png" }],
  },
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="fa" dir="rtl">
      <body>{children}</body>
    </html>
  );
}
