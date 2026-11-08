import test from "node:test";import assert from "node:assert/strict";import {readFileSync} from "node:fs";import {dirname,resolve} from "node:path";import {fileURLToPath} from "node:url";const root=resolve(dirname(fileURLToPath(import.meta.url)),"../../..");const read=p=>readFileSync(resolve(root,p),"utf8");
test("each lineage node embeds its own read-only document preview in a collapsed accordion",()=>{const s=read("apps/web/app/invoices/invoice-lineage-modal.tsx");assert.ok(s.includes("<details className=\"fiscal-section\""));assert.ok(s.includes("مشاهده صورتحساب در همین مرحله"));assert.ok(s.includes("invoiceId={node.invoiceId}"));assert.ok(s.includes("readOnly"));});
test("each lineage invoice can print as A4 landscape with its tax ID and authority reference",()=>{
 const modal=read("apps/web/app/invoices/invoice-lineage-modal.tsx");
 const preview=read("apps/web/app/invoices/invoice-formal-preview.tsx");
 const css=read("apps/web/app/globals.css");
 assert.ok(modal.includes("چاپ صورتحساب"));
 assert.ok(modal.includes("invoice-lineage-print-sheet"));
 assert.ok(preview.includes("authorityReference"));
 assert.ok(preview.includes("شماره مرجع"));
 assert.ok(css.includes("@page{size:A4 landscape"));
});
test("lineage print preview follows the paper invoice seller, buyer, items, totals and signatures",()=>{
 const preview=read("apps/web/app/invoices/invoice-formal-preview.tsx");
 const service=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10InvoicePrintPreviewService.cs");
 for(const text of ["مشخصات فروشنده","مشخصات خریدار","مشخصات کالا / خدمات",
  "جمع مبلغ کل","جمع تخفیف","جمع مالیات و عوارض","مبلغ قابل پرداخت",
  "روش تسویه","مهر و امضاء فروشنده","مهر و امضاء خریدار"])
  assert.ok(preview.includes(text),text);
 for(const text of ["InvoiceParticipantDetails","PartyAddresses","PartyContacts","cashAmountRial","creditAmountRial"])
  assert.ok(service.includes(text),text);
});
test("historical preview cannot expose mutation controls",()=>{const s=read("apps/web/app/invoices/invoice-formal-preview.tsx");assert.ok(s.includes("readOnly?: boolean"));assert.ok(s.includes("!readOnly && <AuthorityActions"));});
test("step25 mandates effective latest subject only after verified legal linkage",()=>{const s=read("docs/phase10-step25-invoice-lineage-visualization.md");assert.ok(s.includes("latest authoritative legal subject"));assert.ok(s.includes("pending or rejected successor"));assert.ok(s.includes("100,000"));assert.ok(s.includes("120,000"));});
