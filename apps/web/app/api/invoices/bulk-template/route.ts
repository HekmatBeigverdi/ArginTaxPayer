import {NextResponse} from "next/server";
import {bulkInvoiceTemplateXlsx} from "../../../invoices/invoice-template.mjs";
export function GET(){
 return new NextResponse(new Uint8Array(bulkInvoiceTemplateXlsx()),{
  headers:{
   "Content-Type":"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
   "Content-Disposition":'attachment; filename="ArginTaxPayer_Bulk_Invoice_Template.xlsx"',
   "Cache-Control":"private, no-store"
  }
 });
}
