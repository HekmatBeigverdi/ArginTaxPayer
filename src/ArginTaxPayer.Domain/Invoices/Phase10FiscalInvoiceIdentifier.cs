using System.Globalization;
using System.Text;

namespace ArginTaxPayer.Domain.Invoices;

/// <summary>
/// Modern official SDK 2.0.32 TaxIdProvider + Verhoeff reference-aligned
/// pure function. Does NOT allocate/reserve an invoice serial or authorize
/// issuance. Caller MUST atomically reserve a serial per Sandbox memory first.
/// </summary>
public static class Phase10FiscalInvoiceIdentifier
{
    private static readonly int[,] D = {
        {0,1,2,3,4,5,6,7,8,9},{1,2,3,4,0,6,7,8,9,5},
        {2,3,4,0,1,7,8,9,5,6},{3,4,0,1,2,8,9,5,6,7},
        {4,0,1,2,3,9,5,6,7,8},{5,9,8,7,6,0,4,3,2,1},
        {6,5,9,8,7,1,0,4,3,2},{7,6,5,9,8,2,1,0,4,3},
        {8,7,6,5,9,3,2,1,0,4},{9,8,7,6,5,4,3,2,1,0}
    };
    private static readonly int[,] P = {
        {0,1,2,3,4,5,6,7,8,9},{1,5,7,6,2,8,3,0,9,4},
        {5,8,0,3,7,9,6,1,4,2},{8,9,1,6,0,4,3,5,2,7},
        {9,4,5,3,1,2,6,8,7,0},{4,2,8,6,5,7,3,9,0,1},
        {2,7,9,3,8,0,6,4,1,5},{7,0,4,6,9,1,3,2,5,8}
    };
    private static readonly int[] Inv = {0,4,3,2,1,5,6,7,8,9};

    public static string Generate(string sandboxMemoryCode,long serial,DateTime issueUtc)
    {
        if(sandboxMemoryCode is not {Length:6} ||
           sandboxMemoryCode.Any(ch=>!(ch is >= '0' and <= '9' or >= 'A' and <= 'Z')) ||
           serial < 1 || serial > 0xffffffffffL ||
           issueUtc.Kind!=DateTimeKind.Utc || issueUtc.Year<2020)
            throw new ArgumentException("Invalid fiscal ID input. An approved Sandbox memory, durable serial and UTC issue date are required.");

        long day= new DateTimeOffset(issueUtc).ToUnixTimeSeconds()/86400L;
        if(day<0 || day>0xfffff)
            throw new ArgumentOutOfRangeException(nameof(issueUtc));
        string dayHex=day.ToString("X5",CultureInfo.InvariantCulture);
        string serialHex=serial.ToString("X10",CultureInfo.InvariantCulture);
        var numeric=new StringBuilder();
        foreach(char ch in sandboxMemoryCode){
            if(ch is >= '0' and <= '9')numeric.Append(ch);
            else numeric.Append((int)ch);
        }
        numeric.Append(day.ToString(CultureInfo.InvariantCulture).PadLeft(6,'0'));
        numeric.Append(serial.ToString(CultureInfo.InvariantCulture).PadLeft(12,'0'));
        int c=0;
        string n=numeric.ToString();
        for(int i=0;i<n.Length;i++){
            int digit=n[n.Length-i-1]-'0';
            c=D[c,P[(i+1)%8,digit]];
        }
        return sandboxMemoryCode+dayHex+serialHex+Inv[c].ToString(CultureInfo.InvariantCulture);
    }
}
