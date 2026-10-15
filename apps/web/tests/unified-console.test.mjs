import assert from "node:assert/strict";
import { readFileSync, existsSync } from "node:fs";
import { test } from "node:test";
const read = path => readFileSync(new URL("../" + path, import.meta.url), "utf8");
const exists = path => existsSync(new URL("../" + path, import.meta.url));

test("all protected pages share the same persistent route-group layout", () => {
  const layout = read("app/(console)/layout.tsx");
  for (const name of ["dashboard", "users", "parties", "account"])
    assert.ok(exists(`app/(console)/${name}/page.tsx`), name);
  assert.match(layout, /ConsoleShell/);
  assert.match(layout, /currentIdentity/);
  assert.match(layout, /redirect\("\/identity"\)/);
  for (const name of ["dashboard", "users", "parties", "account"])
    assert.equal(exists(`app/${name}/page.tsx`), false, name + " duplicate route");
});

test("sidebar navigation and account menu are shared, with secure logout", () => {
  const shell = read("app/components/console-shell.tsx");
  assert.match(shell, /usePathname/);
  assert.match(shell, /aria-current/);
  assert.match(shell, /Level1/);
  assert.match(shell, /Level2/);
  assert.match(shell, /کاربر پنل/);
  assert.match(shell, /\/api\/identity\/logout/);
  assert.match(shell, /role="menu"/);
  assert.match(shell, /router\.replace\("\/identity"\)/);
});

test("buyer and taxpayer list has separate create and fiscal connection actions", () => {
  const directory = read("app/parties/buyer-taxpayer-directory.tsx");
  assert.match(directory, /افزودن خریدار/);
  assert.match(directory, /افزودن مؤدی/);
  assert.match(read("app/parties/participant-registration-modal.tsx"), /شخص حقیقی/);
  assert.match(read("app/parties/participant-registration-modal.tsx"), /شخص حقوقی/);
  assert.match(directory, /console-table/);
  assert.match(directory, /TaxpayerWorkspace/);
  assert.match(directory, /memoryIdentifier|حافظه مالیاتی/);
  assert.match(directory, /ParticipantRegistrationModal/);
  assert.doesNotMatch(directory, /AdminCreateCustomer/);
});

test("fiscal details can be opened for a selected taxpayer without creating another", () => {
  const taxpayer = read("app/taxpayers/taxpayer-workspace.tsx");
  assert.match(taxpayer, /initialTaxpayerId/);
  assert.match(taxpayer, /showCreateForm/);
  assert.match(taxpayer, /show\(initialTaxpayerId\)/);
});

test("shared teal design and accounting-style condensed tables are responsive", () => {
  const css = read("app/globals.css");
  assert.match(css, /--argin-primary/);
  assert.match(css, /Vazirmatn/);
  assert.match(css, /\.console-table/);
  assert.match(css, /\.console-directory/);
  assert.match(css, /\.console-account-dropdown/);
  assert.match(css, /max-width:780px/);
});
