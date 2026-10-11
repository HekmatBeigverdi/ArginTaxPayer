import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "آرگین | سامانه مؤدیان",
  description: "زیرساخت سامانه یکپارچه مؤدیان آرگین",
  robots: { index: false, follow: false },
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="fa" dir="rtl">
      <body>{children}</body>
    </html>
  );
}
