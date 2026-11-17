export function backupFilePage<T>(items: T[], dateOf: (item: T) => string, from: string, to: string, page: number) {
 const day = (value: string) => new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Tehran", year: "numeric", month: "2-digit", day: "2-digit" }).format(new Date(value));
 const filtered = items.filter(item => (!from || day(dateOf(item)) >= from) && (!to || day(dateOf(item)) <= to))
  .sort((a, b) => Date.parse(dateOf(b)) - Date.parse(dateOf(a)));
 const pages = Math.max(1, Math.ceil(filtered.length / 15));
 const current = Math.max(1, Math.min(page, pages));
 return { items: filtered.slice((current - 1) * 15, current * 15), current, pages, total: filtered.length };
}

