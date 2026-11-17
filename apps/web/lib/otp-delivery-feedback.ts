export function otpUnavailableCode(body: unknown): string {
 const error = body && typeof body === "object" && "error" in body ? body.error : null;
 return error === "maintenance_mode" || error === "recovery_mode" ? error : "otp_unavailable";
}

export function otpUnavailableMessage(error: unknown): string {
 if (error === "maintenance_mode") return "برنامه پس از بکاپ در حالت نگهداری است. ادمین سطح ۱ باید از بخش بکاپ، پایان نگهداری و فعال‌سازی برنامه را تأیید کند.";
 if (error === "recovery_mode") return "برنامه در حالت بررسی بازیابی است. ادمین سطح ۱ باید بررسی بازیابی و فعال‌سازی برنامه را تأیید کند.";
 if (error === "identity_service_unavailable") return "ارتباط با سرویس احراز هویت برقرار نشد. کمی بعد دوباره تلاش کنید.";
 return "ارسال کد تأیید انجام نشد. اتصال سرویس پیامک را بررسی کنید.";
}
