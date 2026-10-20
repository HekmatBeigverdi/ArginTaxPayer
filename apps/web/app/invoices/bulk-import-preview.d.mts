export type BulkInvoiceLine={catalogItemId:null;unitId:null;description:string;quantity:number;unitPriceRial:number;discountPerUnitRial:number;vatRatePercent:number};
export type BulkInvoiceDraft={id:null;taxpayerId:string;buyerPartyId:string;expectedRevision:0;lastCompletedStep:2;issueDateUtc:string;type:number;pattern:number;settlementMethod:number;cashAmountRial:number;creditAmountRial:number;lines:BulkInvoiceLine[]};
export type BulkInvoicePreview={invoiceKey:string;lineCount:number;total:number;errors:string[];draft:BulkInvoiceDraft|null};
export function previewBulkInvoices(matrix:string[][]):BulkInvoicePreview[];
