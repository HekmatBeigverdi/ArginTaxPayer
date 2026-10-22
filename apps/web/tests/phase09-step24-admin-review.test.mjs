import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
const source=(file)=>readFileSync(new URL("../"+file,import.meta.url),"utf8");
test("Level1-only approvals route is linked only for Level1",()=>{
 const nav=source("app/components/console-shell.tsx");
 const page=source("app/(console)/signing-approvals/page.tsx");
 assert.match(nav,/roles.includes\("Level1"\).*signing-approvals/s);
 assert.match(page,/actor.roles.includes\("Level1"\)/);
 assert.match(page,/AdminSigningReviewWorkspace/);
});
test("admin decisions are role-and-MFA authorized on server and idempotency-gated",()=>{
 const service=readFileSync(new URL("../../src/ArginTaxPayer.Infrastructure/Signing/Review/EfSigningAdminReviewWorkflow.cs",import.meta.url),"utf8");
 assert.match(service,/IdentityRoleNames.Level1/);
 assert.match(service,/EnabledAtUtc != null/);
 assert.match(service,/req.RequestedByUserId == admin/);
 assert.match(service,/req.Revision != decision.ExpectedRevision/);
 assert.match(service,/key.Revision != req.ExpectedKeyRevision/);
 assert.match(service,/BeginTransactionAsync\(IsolationLevel.Serializable/);
 assert.match(service,/AddHours\(24\)/);
 assert.match(service,/SigningAuditEvents.Add/);
 assert.doesNotMatch(service,/PrivateMaterial|ExportPkcs8|RemoveRange|\.LifecycleState\s*=(?!=)/);
});
test("approval BFF prevents cross origin posts and does not expose credentials",()=>{
 const api=readFileSync(new URL("../../src/ArginTaxPayer.Api/Taxpayers/SigningAdminReviewEndpoints.cs",import.meta.url),"utf8");
 const post=source("app/api/admin/signing-change-requests/[requestId]/decision/route.ts");
 const list=source("app/api/admin/signing-change-requests/route.ts");
 assert.match(api,/IsLoopbackAddress/);
 assert.match(post,/isSameLocalOrigin/);
 assert.match(post,/ACCESS_COOKIE/);
 assert.match(list,/ACCESS_COOKIE/);
 assert.match(post,/expectedRevision/);
});

test("approved suspension is applied once and preserves the encrypted key",()=>{
 const service=readFileSync(new URL("../../src/ArginTaxPayer.Infrastructure/Signing/Review/EfSigningAdminReviewWorkflow.cs",import.meta.url),"utf8");
 assert.match(service,/immediateSuspension = decision.Approve && req.Kind == 1/);
 assert.match(service,/req.ConsumedByOperationRecordId = key.OriginOperationRecordId/);
 assert.match(service,/key.LifecycleState = 4/);
 assert.match(service,/change_request_suspension_applied/);
 assert.match(service,/immediateSuspension \? req.Revision - 1 : req.Revision/);
 assert.doesNotMatch(service,/Remove\(key\)|DeleteAsync|DeleteRange/);
});
