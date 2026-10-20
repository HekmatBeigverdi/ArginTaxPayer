export type PreviewLine = {quantity:string;unitPrice:string;discount:string;vatRate:string};
export function invoicePreview(lines: PreviewLine[]): {base:number;discount:number;vat:number;total:number};
export function parseDecimal(text:string): {n:bigint;scale:number};
