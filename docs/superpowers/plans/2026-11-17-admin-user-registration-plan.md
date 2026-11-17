# ثبت کاربر توسط مدیر و تشخیص حساب ثبت‌نام‌نشده Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** افزودن ثبت مشتری توسط مدیر سطح ۱/۲ با پروفایل کامل و هدایت ورود حساب ناشناخته به ثبت‌نام.

**Architecture:** مسیر موجود `IdentityAdministratorUserProvisioning` حساب، مالکیت و پروفایل را در یک تراکنش می‌سازد. endpoint ورود نبود حساب را از رمز اشتباه تفکیک می‌کند و وب‌اپ پیام و تب ثبت‌نام را تغییر می‌دهد؛ مودال مدیریت کاربران از همان BFF موجود استفاده می‌کند.

**Tech Stack:** ASP.NET Core Minimal APIs، EF Core/PostgreSQL، Next.js/React/TypeScript، تست‌های xUnit و Node assertions.

**Spec:** `docs/superpowers/specs/2026-11-17-admin-user-registration-design.md`

## Global Constraints

- کار فقط روی `feature/next-software-updates` انجام می‌شود و branch جدید ساخته نمی‌شود.
- فقط مدیر سطح ۱/۲ با نشست معتبر و MFA فعال مجاز است.
- موبایل، نام، نام خانوادگی و نشانی الزامی؛ شرکت اختیاری.
- رمز عبور در فرم دو بار دریافت و قبل از ارسال تطبیق داده می‌شود.

## Review Focus

- حساب موجود با رمز غلط همچنان پیام عمومی 401 بگیرد (تست endpoint/UI).
- شناسه ناشناخته پاسخ `account_not_registered` و هدایت ثبت‌نام بدهد.
- داده‌ی پروفایل در همان تراکنش همراه حساب ذخیره شود.
- مدیر سطح ۲ همان مجوز سطح ۱ را داشته باشد.
- خطای تعارض موبایل و ورودی ناقص در مودال قابل‌فهم باشد.

### Task 1: سرویس provisioning و endpoint ورود

**Files:**
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Identity/IdentityAdministratorUserProvisioning.cs`
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Identity/IdentityCredentialService.cs`
- Modify: `src/ArginTaxPayer.Api/Identity/IdentityDevelopmentEndpoints.cs`
- Modify: `tests/ArginTaxPayer.Tests/IdentityAdminProvisioningTests.cs`

- [ ] تست ایجاد حساب با پروفایل و رد ورودی ناقص را اضافه کن.
- [ ] سرویس را برای تولید رمز تصادفی امن در ثبت ادمینی و ایجاد `SoftwareUserProfile` توسعه بده.
- [ ] endpoint ورود را برای نبود حساب با `account_not_registered` توسعه بده.
- [ ] تست‌های هدفمند را اجرا کن.

### Task 2: جریان ورود و مودال UI

**Files:**
- Modify: `apps/web/app/identity/identity-flow.tsx`
- Modify: `apps/web/app/users/users-workspace.tsx`
- Modify: `apps/web/tests/identity-ui.test.mjs`
- Modify: `apps/web/tests/users-table-modal.test.mjs`

- [ ] هدایت ورود شناسه ناشناخته به تب ثبت‌نام را اضافه کن.
- [ ] مودال ثبت کاربر با پنج فیلد خواسته‌شده، ارسال، خطا و بازخوانی فهرست را اضافه کن.
- [ ] تست‌های متنی UI را به‌روزرسانی و اجرا کن.

### Task 3: بررسی نهایی و تحویل

- [ ] مستندات طراحی و برنامه را commit کن.
- [ ] تست‌های backend و frontend را اجرا کن.
- [ ] تغییرات را روی همین branch commit و push کن.
- [ ] PR از `feature/next-software-updates` به `main` ایجاد کن.
