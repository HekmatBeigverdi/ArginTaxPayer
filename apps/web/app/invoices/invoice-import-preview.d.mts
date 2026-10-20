export const MAX_IMPORT_BYTES: number;
export const MAX_IMPORT_ROWS: number;
export type ImportMapping = Record<string, number | null>;
export type ImportRow = {rowNumber:number;values:{description:string;quantity:string;unitPrice:string;discount:string;vatRate:string};errors:string[]};
export function parseCsv(text:string):string[][];
export function parseXlsx(buffer:ArrayBuffer):Promise<string[][]>;
export function previewRows(matrix:string[][],mapping:ImportMapping):ImportRow[];
export const importFields:string[];
