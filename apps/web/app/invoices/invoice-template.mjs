// Small deterministic XLSX template without a third-party workbook dependency.
// XLSX is an OOXML ZIP archive. Stored entries ensure browser importer support.
const encoder=new TextEncoder();
const xmlEscape=value=>String(value).replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;");
const table=[
 ["description","quantity","unitPrice","discount","vatRate"],
 ["کاغذ A4",2,5000,0,10],["خودکار آبی",5,2000,100,10],
 ["پوشه اداری",3,1500,0,10]
];
function worksheet(data=table){
 const rows=data.map((row,i)=>'<row r="'+(i+1)+'">'+row.map((v,j)=>{
  const ref=String.fromCharCode(65+j)+(i+1);
  return typeof v==="number"?'<c r="'+ref+'"><v>'+v+'</v></c>':
   '<c r="'+ref+'" t="inlineStr"'+(i===0?' s="1"':'')+'><is><t>'+xmlEscape(v)+'</t></is></c>';
 }).join("")+'</row>').join("");
 return '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'+
 '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">'+
 '<cols><col min="1" max="1" width="32" customWidth="1"/><col min="2" max="5" width="22" customWidth="1"/></cols>'+
 '<sheetData>'+rows+'</sheetData></worksheet>';
}
function crc32(data){
 let value=0xffffffff;
 for(const byte of data){value^=byte;for(let i=0;i<8;i++)value=(value>>>1)^((value&1)?0xedb88320:0);}
 return (value^0xffffffff)>>>0;
}
function zip(entries){
 const parts=[],directory=[];let offset=0;
 const u16=(d,p,n)=>{d[p]=n&255;d[p+1]=(n>>>8)&255;};
 const u32=(d,p,n)=>{u16(d,p,n);u16(d,p+2,n>>>16);};
 for(const [name,body] of entries){
  const filename=encoder.encode(name),data=encoder.encode(body),crc=crc32(data);
  const local=new Uint8Array(30+filename.length);
  u32(local,0,0x04034b50);u16(local,4,20);u32(local,14,crc);
  u32(local,18,data.length);u32(local,22,data.length);u16(local,26,filename.length);
  local.set(filename,30);
  const central=new Uint8Array(46+filename.length);
  u32(central,0,0x02014b50);u16(central,4,20);u16(central,6,20);
  u32(central,16,crc);u32(central,20,data.length);u32(central,24,data.length);
  u16(central,28,filename.length);u32(central,42,offset);central.set(filename,46);
  directory.push(central);parts.push(local,data);offset+=local.length+data.length;
 }
 const size=directory.reduce((sum,item)=>sum+item.length,0);
 const footer=new Uint8Array(22);u32(footer,0,0x06054b50);
 u16(footer,8,entries.length);u16(footer,10,entries.length);
 u32(footer,12,size);u32(footer,16,offset);
 const bytes=new Uint8Array(offset+size+footer.length);
 let p=0;for(const part of [...parts,...directory,footer]){bytes.set(part,p);p+=part.length;}
 return bytes;
}
export function invoiceTemplateXlsx(data=table){
 return zip([
  ["[Content_Types].xml",'<?xml version="1.0" encoding="UTF-8"?>'+
   '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'+
   '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>'+
   '<Default Extension="xml" ContentType="application/xml"/>'+
   '<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>'+
   '<Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>'+
   '<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>'+
   '</Types>'],
  ["_rels/.rels",'<?xml version="1.0" encoding="UTF-8"?>'+
   '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'+
   '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>'+
   '</Relationships>'],
  ["xl/workbook.xml",'<?xml version="1.0" encoding="UTF-8"?>'+
   '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">'+
   '<sheets><sheet name="InvoiceLines" sheetId="1" r:id="rId1"/></sheets></workbook>'],
  ["xl/_rels/workbook.xml.rels",'<?xml version="1.0" encoding="UTF-8"?>'+
   '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'+
   '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>'+
   '<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>'+
   '</Relationships>'],
  ["xl/styles.xml",'<?xml version="1.0" encoding="UTF-8"?>'+
   '<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">'+
   '<fonts count="2"><font/><font><b/><color rgb="FF173C32"/></font></fonts>'+
   '<fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FFE4F2EB"/><bgColor indexed="64"/></patternFill></fill></fills>'+
   '<borders count="1"><border/></borders>'+
   '<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>'+
   '<cellXfs count="2"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>'+
   '<xf numFmtId="0" fontId="1" fillId="1" borderId="0" xfId="0"/></cellXfs></styleSheet>'],
  ["xl/worksheets/sheet1.xml",worksheet(data)]
 ]);
}

export function bulkInvoiceTemplateXlsx(){
 return invoiceTemplateXlsx([
  ["invoiceKey","taxpayerId","buyerPartyId","issueDateUtc","type","pattern",
   "settlementMethod","cashAmountRial","description","quantity","unitPriceRial",
   "discountPerUnitRial","vatRatePercent"],
  ["INV-001","REPLACE_WITH_TAXPAYER_UUID","REPLACE_WITH_BUYER_UUID",
   "2026-10-20T09:00:00Z",1,1,1,0,"کاغذ A4",2,5000,0,10],
  ["INV-001","REPLACE_WITH_TAXPAYER_UUID","REPLACE_WITH_BUYER_UUID",
   "2026-10-20T09:00:00Z",1,1,1,0,"خودکار آبی",5,2000,100,10],
  ["INV-002","REPLACE_WITH_TAXPAYER_UUID","REPLACE_WITH_BUYER_UUID",
   "2026-10-20T10:00:00Z",1,1,2,0,"خدمات نصب",1,200000,0,10]
 ]);
}
