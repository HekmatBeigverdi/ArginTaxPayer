// Pure, deterministic base-10 preview. All arithmetic uses BigInt and the
// official V7.9 whole-rial truncation rule (p23, Tables 40-44).
export function parseDecimal(text) {
  const value=String(text??"").trim();
  if (!/^(?:0|[1-9][0-9]*)(?:\.[0-9]+)?$/.test(value)) throw Error("invalid_decimal");
  const [whole,fraction=""]=value.split(".");
  if(fraction.length>20) throw Error("decimal_precision_limit");
  return {n:BigInt(whole+fraction),scale:fraction.length};
}
function power(n){return 10n**BigInt(n);}
function truncateRial(n,scale){return scale===0?n:n/power(scale);}
function addValue(a,b){
 const scale=Math.max(a.scale,b.scale);
 return {n:a.n*power(scale-a.scale)+b.n*power(scale-b.scale),scale};
}
function subtract(a,b){
 const scale=Math.max(a.scale,b.scale);
 return {n:a.n*power(scale-a.scale)-b.n*power(scale-b.scale),scale};
}
function multiply(a,b){return {n:a.n*b.n,scale:a.scale+b.scale};}
export function invoicePreview(lines){
 let gross=0n,discount=0n,vat=0n;
 for(const line of lines){
  const qty=parseDecimal(line.quantity),price=parseDecimal(line.unitPrice),
    reduction=parseDecimal(line.discount),rate=parseDecimal(line.vatRate||"0");
  if(reduction.n*power(price.scale)>price.n*power(reduction.scale))throw Error("discount_exceeds_price");
  const rowGross=truncateRial(multiply(qty,price).n,qty.scale+price.scale);
  const rowDiscount=truncateRial(multiply(qty,reduction).n,qty.scale+reduction.scale);
  const net=rowGross-rowDiscount;
  const divisor=power(rate.scale+2);
  const rowVat=(net*rate.n)/divisor;
  gross+=rowGross;discount+=rowDiscount;vat+=rowVat;
 }
 return {base:Number(gross),discount:Number(discount),
  vat:Number(vat),total:Number(gross-discount+vat)};
}
