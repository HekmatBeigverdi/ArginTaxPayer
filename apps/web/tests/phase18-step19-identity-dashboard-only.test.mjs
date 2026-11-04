import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';

const root = fileURLToPath(new URL('../../../', import.meta.url));
const read = path => readFileSync(resolve(root, path), 'utf8');

test('Production login and dashboard are explicitly separate from full operational endpoints', () => {
  const api = read('src/ArginTaxPayer.Api/Program.cs');
  assert.match(api, /EnableIdentityDashboardOnly/);
  assert.match(api, /!productionOperationalEnabled/);
  assert.match(api, /if \(identityDashboardOnlyEnabled\)[\s\S]*?app.MapPhase15DashboardEndpoints\(\)/);
  assert.match(api, /if \(identityDashboardOnlyEnabled\)[\s\S]*?else[\s\S]*?app.MapInvoiceDraftDevelopmentEndpoints/);
  assert.match(api, /if \(!allowed\)[\s\S]*?Status404NotFound/);
  assert.match(api, /path === "\/api\/dev\/identity\/authorization\/me"/);
  assert.match(api, /path.StartsWith\("\/api\/dev\/dashboard\/"/);
});

test('Public BFF blocks business and signing endpoints while keeping login and dashboard', () => {
  const middleware = read('apps/web/middleware.ts');
  assert.match(middleware, /ARGIN_IDENTITY_DASHBOARD_ONLY/);
  assert.match(middleware, /"login", "logout", "refresh", "mfa\/challenge", "mfa\/complete"/);
  assert.match(middleware, /path.startsWith\("\/api\/dashboard\/"/);
  assert.match(middleware, /return new NextResponse\(null, \{ status: 404 \}\)/);
  assert.doesNotMatch(middleware, /normal-enqueue/);
});

test('Restricted UI hides unsupported registration, OTP and module actions', () => {
  const login = read('apps/web/app/identity/identity-flow.tsx');
  const dashboard = read('apps/web/app/(console)/dashboard/page.tsx');
  const shell = read('apps/web/app/components/console-shell.tsx');
  assert.match(login, /restrictedLogin && \(tab !== "login" \|\| mode !== "password" \|\| forgot\)/);
  assert.match(login, /!restrictedLogin && step === "details"/);
  assert.match(dashboard, /!dashboardOnly && <details/);
  assert.match(shell, /dashboardOnly \? \[base\[0\]\]/);
  assert.match(shell, /!dashboardOnly && roles.includes\("Customer"\)/);
});

test('Deployment activation is exact-SHA, no fiscal dispatch, rollback on error', () => {
  const script = read('scripts/phase18-step19-activate-dashboard-only.sh');
  assert.match(script, /PHASE18_OWNER_APPROVED_DASHBOARD_ONLY/);
  assert.match(script, /Active deployed release is NOT source SHA/);
  assert.match(script, /ArginTaxPayer__EnableOperationalEndpoints=false/);
  assert.match(script, /SandboxInvoiceLegacy__Enabled=false/);
  assert.match(script, /Ghasedak__EnableLiveSending=false/);
  assert.match(script, /sudo nginx -t/);
  assert.match(script, /trap rollback ERR/);
  assert.match(script, /"\$auth" == 401 && "\$blocked" == 404/);
});
