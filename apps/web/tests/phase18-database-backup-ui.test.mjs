import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
const panel=await readFile(new URL('../app/backup-control-panel.tsx',import.meta.url),'utf8');
const proxy=await readFile(new URL('../app/api/backup-control/[...parts]/route.ts',import.meta.url),'utf8');
const page=await readFile(new URL('../app/(console)/backup-control/page.tsx',import.meta.url),'utf8');
test('backup panel displays retention, local paths and explicit stopped-service control',()=>{
 assert.match(panel,/نگهداری محلی: ۷ روز/);assert.match(panel,/localPath/);assert.match(panel,/گرفتن بکاپ دستی و توقف API/);assert.match(panel,/فعال‌کردن سرویس‌های برنامه/);assert.match(panel,/checksumMatches/);
});
test('backup controls require same-origin BFF and mask secrets',()=>{
 assert.match(proxy,/isSameLocalOrigin/);assert.match(proxy,/ACCESS_COOKIE/);assert.match(proxy,/allowed=new Set/);assert.match(panel,/type="password"/);assert.doesNotMatch(panel,/console\.log/);
});
test('backup page gates access to Level 1 and exposes restore review state',()=>{
 assert.match(page,/roles\.includes\("Level1"\)/);assert.match(panel,/restore\/stage-local/);assert.match(panel,/restore\/stage-remote/);assert.match(panel,/restore\/activate/);assert.match(panel,/recovery\/enable-business/);
});
test('completed operation progress is shown only after a live operation and expires after ten seconds',()=>{
 assert.match(panel,/useRef/);
 assert.match(panel,/setTimeout\([^,]+,\s*10_000\)/);
 assert.match(panel,/operation\.status===\"succeeded\"&&!showSuccess/);
});
