import test from "node:test";import assert from "node:assert/strict";
import {readFileSync} from "node:fs";import {dirname,resolve} from "node:path";import {fileURLToPath} from "node:url";
const root=resolve(dirname(fileURLToPath(import.meta.url)),"../../..");const read=p=>readFileSync(resolve(root,p),"utf8");
test("snapshot preview reads PascalCase DTO line fields and scoped party names",()=>{
 const s=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10InvoicePrintPreviewService.cs");
 assert.ok(s.includes("char.ToUpperInvariant(key[0])+key[1..]"));
 assert.ok(s.includes("db.TaxpayerProfiles"));
 assert.ok(s.includes("db.Parties"));
 assert.ok(s.includes("OwnerCustomerUserId==owner.Value"));
});
test("compact formal invoice shows busy spinner and operations live inside invoice not table",()=>{
 const p=read("apps/web/app/invoices/invoice-formal-preview.tsx");
 const page=read("apps/web/app/invoices/workspace.tsx");
 assert.ok(p.includes("argin-loader-spinner"));
 assert.ok(/maxHeight:\s*300/.test(p));
 for(const label of [">اصلاح</button>",">ابطال</button>",">برگشت از فروش</button>"])assert.ok(p.includes(label));
 assert.ok(page.includes("authorityStatus={detail.authorityStatus}"));
 assert.ok(!page.includes(">ابطال</button>"));
});
