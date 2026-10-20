import {NextResponse} from "next/server";
import {invoiceTemplateXlsx} from "../../../invoices/invoice-template.mjs";
export const dynamic="force-dynamic";
export function GET(){
 const bytes=invoiceTemplateXlsx();
 return new NextResponse(new Uint8Array(bytes),{
  headers:{
   "Content-Type":"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
   "Content-Disposition":'attachment; filename="ArginTaxPayer_Invoice_Import_Template.xlsx"',
   "Cache-Control":"private, no-store"
  }
 });
}
