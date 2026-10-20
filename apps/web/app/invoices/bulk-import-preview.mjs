// Phase08 Step18: pure, bounded, preview-only grouping. Does not perform any writes.
const columns=["invoiceKey","taxpayerId","buyerPartyId","issueDateUtc","type","pattern","settlementMethod",
 "cashAmountRial","description","quantity","unitPriceRial","discountPerUnitRial","vatRatePercent"];
const guid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const key=/^[A-Za-z0-9_-]{1,64}$/;
const numeric=/^(?:0|[1-9][0-9]*)(?:\.[0-9]{1,8})?$/;
export function previewBulkInvoices(matrix){
 if(!Array.isArray(matrix)||matrix.length<2||matrix.length>501)
  throw Error("فایل باید سربرگ و بین ۱ تا ۵۰۰ ردیف داده داشته باشد.");
 const headers=matrix[0].map(x=>String(x??"").trim());
 const indices=Object.fromEntries(columns.map(c=>[c,headers.indexOf(c)]));
 if(columns.some(c=>indices[c]<0)||new Set(headers).size!==headers.length)
  throw Error("ستون‌های فایل باید دقیقاً مطابق سربرگ قالب استاندارد و بدون تکرار باشند.");
 const map=new Map();
 for(let i=1;i<matrix.length;i++){
  const row=Object.fromEntries(columns.map(c=>[c,String(matrix[i][indices[c]]??"").trim()]));
  const invoiceKey=row.invoiceKey;
  if(!map.has(invoiceKey))map.set(invoiceKey,{invoiceKey,lines:[],errors:[],header:null,firstRow:i+1});
  const item=map.get(invoiceKey);
  const metadata=columns.slice(1,8).map(c=>row[c]);
  if(item.header===null)item.header=metadata;
  else if(metadata.some((v,k)=>v!==item.header[k]))item.errors.push("اطلاعات سربرگ سطر "+(i+1)+" با سایر اقلام صورتحساب یکسان نیست.");
  if(!key.test(invoiceKey))item.errors.push("شناسه invoiceKey باید انگلیسی، یکتا و حداکثر ۶۴ نویسه باشد.");
  if(!guid.test(row.taxpayerId)||!guid.test(row.buyerPartyId))item.errors.push("شناسه UUID مؤدی یا خریدار معتبر نیست.");
  if(!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$/.test(row.issueDateUtc)||
      !Number.isFinite(Date.parse(row.issueDateUtc)))item.errors.push("تاریخ صدور باید میلادی UTC در قالب ISO 8601 باشد.");
  if(row.type!=="1"&&row.type!=="2"||!/^[0-9]{1,2}$/.test(row.pattern))
   item.errors.push("نوع یا الگوی صورتحساب معتبر نیست.");
  if(!["1","2","3"].includes(row.settlementMethod))item.errors.push("روش تسویه باید ۱ نقدی، ۲ نسیه یا ۳ مختلط باشد.");
  if(!numeric.test(row.cashAmountRial))item.errors.push("مبلغ نقدی نامعتبر است.");
  if(!row.description||row.description.length>256)item.errors.push("شرح ردیف "+(i+1)+" الزامی و حداکثر ۲۵۶ نویسه است.");
  for(const c of ["quantity","unitPriceRial","discountPerUnitRial","vatRatePercent"]){
   if(!numeric.test(row[c]))item.errors.push("مقدار "+c+" در سطر "+(i+1)+" نامعتبر است.");
  }
  if(numeric.test(row.quantity)&&Number(row.quantity)<=0)item.errors.push("مقدار در سطر "+(i+1)+" باید مثبت باشد.");
  if(numeric.test(row.discountPerUnitRial)&&numeric.test(row.unitPriceRial)&&
    Number(row.discountPerUnitRial)>Number(row.unitPriceRial))item.errors.push("تخفیف سطر "+(i+1)+" بیش از قیمت واحد است.");
  if(numeric.test(row.vatRatePercent)&&Number(row.vatRatePercent)>=1000)item.errors.push("نرخ VAT سطر "+(i+1)+" معتبر نیست.");
  item.lines.push({catalogItemId:null,unitId:null,description:row.description,
    quantity:Number(row.quantity),unitPriceRial:Number(row.unitPriceRial),
    discountPerUnitRial:Number(row.discountPerUnitRial),vatRatePercent:Number(row.vatRatePercent)});
 }
 if(map.size>50)throw Error("در هر Batch حداکثر ۵۰ صورتحساب قابل ثبت است.");
 return [...map.values()].map(item=>{
  if(item.lines.length>150)item.errors.push("هر صورتحساب حداکثر ۱۵۰ قلم دارد.");
  const [taxpayerId,buyerPartyId,issueDateUtc,type,pattern,settlementMethod,cashAmountRial]=item.header;
  const money=v=>Math.round((v+Number.EPSILON)*100)/100;
  const total=money(item.lines.reduce((sum,line)=>{
   const base=money(line.quantity*line.unitPriceRial)-money(line.quantity*line.discountPerUnitRial);
   return sum+base+money(base*line.vatRatePercent/100);
  },0));
  const method=Number(settlementMethod);
  const cash=method===1?total:method===2?0:Number(cashAmountRial);
  const credit=money(total-cash);
  if(!Number.isFinite(total)||total<=0)item.errors.push("مبلغ نهایی صورتحساب باید مثبت باشد.");
  if(method===3&&(!Number.isFinite(cash)||cash<=0||credit<=0))
   item.errors.push("در تسویه مختلط، مبلغ نقدی باید بین صفر و جمع صورتحساب باشد.");
  if(item.errors.length)return {invoiceKey:item.invoiceKey,lineCount:item.lines.length,total,errors:[...new Set(item.errors)],draft:null};
  return {invoiceKey:item.invoiceKey,lineCount:item.lines.length,total,errors:[],draft:{
   id:null,taxpayerId,buyerPartyId,expectedRevision:0,lastCompletedStep:2,issueDateUtc,
   type:Number(type),pattern:Number(pattern),settlementMethod:method,
   cashAmountRial:cash,creditAmountRial:credit,lines:item.lines
  }};
 });
}
