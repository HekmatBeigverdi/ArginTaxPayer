// Bounded, preview-only import. No persistence or invoice submission.
export const MAX_IMPORT_BYTES=2_000_000;
export const MAX_IMPORT_ROWS=500;
const number=/^(?:0|[1-9][0-9]*)(?:\.[0-9]{1,8})?$/;
export function parseCsv(text){
 const rows=[];let row=[],cell="",quoted=false;
 text=text.replace(/^\uFEFF/,"");
 for(let i=0;i<text.length;i++){
  const c=text[i];
  if(quoted){if(c==='"'&&text[i+1]==='"'){cell+='"';i++;}else if(c==='"')quoted=false;else cell+=c;}
  else if(c==='"'){if(cell!=="")throw Error("قالب نقل‌قول CSV معتبر نیست.");quoted=true;}
  else if(c===","){row.push(cell);cell="";}
  else if(c==="\n"||c==="\r"){if(c==="\r"&&text[i+1]==="\n")i++;row.push(cell);if(row.some(x=>x.trim()))rows.push(row);row=[];cell="";if(rows.length>MAX_IMPORT_ROWS+1)throw Error("تعداد ردیف‌های فایل بیش از حد مجاز است.");}
  else cell+=c;
 }
 if(quoted)throw Error("نقل‌قول CSV بسته نشده است.");
 row.push(cell);if(row.some(x=>x.trim()))rows.push(row);
 if(rows.length>MAX_IMPORT_ROWS+1)throw Error("تعداد ردیف‌های فایل بیش از حد مجاز است.");
 return rows;
}
export const importFields=["description","quantity","unitPrice","discount","vatRate"];
export function previewRows(matrix,mapping){
 if(matrix.length<2)throw Error("فایل باید سربرگ و حداقل یک ردیف داشته باشد.");
 if(matrix.length>MAX_IMPORT_ROWS+1)throw Error("حداکثر ۵۰۰ ردیف مجاز است.");
 if(["description","quantity","unitPrice"].some(k=>mapping[k]==null))throw Error("ستون‌های شرح، مقدار و قیمت واحد الزامی هستند.");
 const used=Object.values(mapping).filter(v=>v!==null&&v!==undefined);
 if(new Set(used).size!==used.length)throw Error("هر ستون را فقط به یک فیلد نگاشت کنید.");
 return matrix.slice(1).map((cells,index)=>{
  const get=(key,fallback="")=>mapping[key]==null?fallback:String(cells[mapping[key]]??"").trim();
  const result={description:get("description"),quantity:get("quantity"),unitPrice:get("unitPrice"),
    discount:get("discount","0")||"0",vatRate:get("vatRate","0")||"0"};
  const errors=[];
  if(!result.description||result.description.length>256)errors.push("شرح الزامی و حداکثر ۲۵۶ نویسه است");
  for(const field of ["quantity","unitPrice","discount","vatRate"]){
   if(!number.test(result[field]))errors.push("عدد "+field+" معتبر نیست");
  }
  if(errors.length===0){
   if(Number(result.quantity)<=0)errors.push("مقدار باید مثبت باشد");
   if(Number(result.discount)>Number(result.unitPrice))errors.push("تخفیف از قیمت واحد بیشتر است");
   if(Number(result.vatRate)>=1000)errors.push("نرخ VAT نامعتبر است");
  }
  return {rowNumber:index+2,values:result,errors};
 });
}
function bytes2(bytes,offset){return bytes[offset]|(bytes[offset+1]<<8);}
function bytes4(bytes,offset){return (bytes[offset]|bytes[offset+1]<<8|bytes[offset+2]<<16|bytes[offset+3]<<24)>>>0;}
async function inflate(data,method,limit){
 if(method===0){if(data.length>limit)throw Error("حجم XML بیش از حد مجاز است.");return data;}
 if(method!==8||typeof DecompressionStream==="undefined")throw Error("فشرده‌سازی فایل XLSX پشتیبانی نمی‌شود.");
 const stream=new Blob([data]).stream().pipeThrough(new DecompressionStream("deflate-raw"));
 const reader=stream.getReader();let size=0;const chunks=[];
 try{
  while(true){const {value,done}=await reader.read();if(done)break;
   size+=value.length;if(size>limit){await reader.cancel();throw Error("حجم XML بیش از حد مجاز است.");}
   chunks.push(value);
  }
 }finally{reader.releaseLock();}
 const combined=new Uint8Array(size);let offset=0;for(const chunk of chunks){combined.set(chunk,offset);offset+=chunk.length;}
 return combined;
}
function parseXml(text){
 const doc=new DOMParser().parseFromString(text,"application/xml");
 if(doc.getElementsByTagName("parsererror").length)throw Error("ساختار XML فایل XLSX معتبر نیست.");
 return doc;
}
function nodes(parent,name){return [...parent.getElementsByTagNameNS("*",name)];}
export async function parseXlsx(buffer){
 const bytes=new Uint8Array(buffer);if(bytes.length>MAX_IMPORT_BYTES)throw Error("حجم فایل بیش از دو مگابایت است.");
 const decoder=new TextDecoder();const entries=new Map();
 // Central directory only: ignore arbitrary ZIP paths, encrypted archives and
 // entries with suspicious decompression ratios.
 let eocd=-1;for(let i=bytes.length-22;i>=Math.max(0,bytes.length-65557);i--){if(bytes4(bytes,i)===0x06054b50){eocd=i;break;}}
 if(eocd<0)throw Error("ساختار ZIP معتبر نیست.");
 const count=bytes2(bytes,eocd+10),start=bytes4(bytes,eocd+16);
 if(count>128||start>=bytes.length)throw Error("فایل XLSX بیش از حد پیچیده است.");
 let position=start;for(let n=0;n<count;n++){
  if(bytes4(bytes,position)!==0x02014b50)throw Error("فهرست ZIP نامعتبر است.");
  const method=bytes2(bytes,position+10),compressed=bytes4(bytes,position+20),uncompressed=bytes4(bytes,position+24);
  const nameLen=bytes2(bytes,position+28),extra=bytes2(bytes,position+30),comment=bytes2(bytes,position+32);
  const filename=decoder.decode(bytes.slice(position+46,position+46+nameLen));
  if(compressed>MAX_IMPORT_BYTES||uncompressed>MAX_IMPORT_BYTES||filename.includes(".."))throw Error("فایل XLSX نامعتبر یا بیش از حد بزرگ است.");
  if(filename==="xl/sharedStrings.xml"||filename==="xl/worksheets/sheet1.xml"){
   const local=bytes4(bytes,position+42);if(bytes4(bytes,local)!==0x04034b50)throw Error("فایل XLSX نامعتبر است.");
   const dataOffset=local+30+bytes2(bytes,local+26)+bytes2(bytes,local+28);
   if(dataOffset+compressed>bytes.length)throw Error("فایل XLSX ناقص است.");
   entries.set(filename,await inflate(bytes.slice(dataOffset,dataOffset+compressed),method,MAX_IMPORT_BYTES));
  }
  position+=46+nameLen+extra+comment;
 }
 const sheet=entries.get("xl/worksheets/sheet1.xml");if(!sheet)throw Error("برگه اول XLSX پیدا نشد.");
 const shared=entries.get("xl/sharedStrings.xml");
 const strings=shared?nodes(parseXml(decoder.decode(shared)),"si").map(node=>nodes(node,"t").map(t=>t.textContent??"").join("")):[];
 const xml=parseXml(decoder.decode(sheet));
 const result=[];
 for(const row of nodes(xml,"row")){
  const cells=[];for(const cell of nodes(row,"c")){
   const ref=cell.getAttribute("r")??"";const match=/^[A-Z]+/.exec(ref);if(!match)continue;
   let index=0;for(const ch of match[0])index=index*26+ch.charCodeAt(0)-64;index--;
   if(index>=40)throw Error("حداکثر ۴۰ ستون قابل پیش‌نمایش است.");
   const raw=nodes(cell,"v")[0]?.textContent??"";
   const inline=nodes(cell,"is")[0];
   cells[index]=cell.getAttribute("t")==="s"?strings[Number(raw)]??"":
    inline?nodes(inline,"t").map(t=>t.textContent??"").join(""):raw;
  }
  if(cells.some(x=>String(x??"").trim()))result.push(cells.map(x=>x??""));
  if(result.length>MAX_IMPORT_ROWS+1)throw Error("تعداد ردیف‌ها بیش از حد مجاز است.");
 }
 return result;
}
