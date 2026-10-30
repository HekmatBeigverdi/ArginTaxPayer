const labels:Record<string,string>={
 Original:"اصلی",Correction:"اصلاحیه",Corrective:"اصلاحیه",Cancellation:"ابطالی",Cancel:"ابطالی",
 SalesReturn:"برگشت",ReturnFromSale:"برگشت",Return:"برگشت"
};
const tones:Record<string,string>={
 Original:"original",Correction:"correction",Corrective:"correction",Cancellation:"cancellation",Cancel:"cancellation",
 SalesReturn:"return",ReturnFromSale:"return",Return:"return"
};
export default function InvoiceSubjectBadge({subject}:{subject:string}){
 const tone=tones[subject];
 return <span className={tone?"invoice-subject-badge invoice-subject-badge--"+tone:undefined}>{labels[subject]??subject}</span>;
}
