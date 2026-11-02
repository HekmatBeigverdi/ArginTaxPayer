export const amount = (n:number) => new Intl.NumberFormat("fa-IR").format(n)+" ریال";
const temporalPlanNames:Record<number,string>={1:"یک‌ساله",3:"یک‌ماهه",4:"سه‌ماهه",5:"شش‌ماهه"};
export const planName = (kind:number,units:number|null) => kind===2
  ? `بسته ${new Intl.NumberFormat("fa-IR").format(units??0)} تایی`
  : (temporalPlanNames[kind]??"پلن نرم‌افزار");
