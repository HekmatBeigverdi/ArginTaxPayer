using System.Globalization;

namespace ArginTaxPayer.Domain.Subscriptions;

public sealed record CommercialReportPeriod(DateTime FromUtc,DateTime ThroughUtc)
{
    public static CommercialReportPeriod Resolve(string period,DateTime nowUtc)
    {
        if(nowUtc.Kind!=DateTimeKind.Utc)
            throw new ArgumentException("Report clock must be UTC.",nameof(nowUtc));
        if(period is not ("month" or "year"))
            throw new ArgumentException("Period must be month or year.",nameof(period));
        var zone=TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
        var local=TimeZoneInfo.ConvertTimeFromUtc(nowUtc,zone);
        var calendar=new PersianCalendar();
        var start=calendar.ToDateTime(calendar.GetYear(local),
            period=="year"?1:calendar.GetMonth(local),1,0,0,0,0);
        return new(TimeZoneInfo.ConvertTimeToUtc(start,zone),nowUtc);
    }
}
