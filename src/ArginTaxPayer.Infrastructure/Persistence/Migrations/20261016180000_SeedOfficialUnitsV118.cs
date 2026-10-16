using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261016180000_SeedOfficialUnitsV118")]
public sealed class SeedOfficialUnitsV118 : Migration
{
    // Source: owner-provided RC_UMGS.ST_V1.18, Ordibehesht 1404, pages 3-4.
    // SHA256 is of this exact normalized UTF-8 *transcription*, NOT the PDF bytes.
    // This version is staged; operator must verify it is current before activation.
    internal const string SourceRows = """
1|1611|لنگه
2|1612|عدل
3|1613|جعبه
4|1618|توپ
5|1619|ست
6|1620|دست
7|1624|کارتن
8|1627|عدد
9|1628|بسته
10|1629|پاکت
11|1631|دستگاه
12|1640|تخته
13|1641|رول
14|1642|طاقه
15|1643|جفت
16|1645|متر مربع
17|1649|پالت
18|1661|دوجین
19|1668|حلقه (رینگ)
20|1673|قراص
21|1694|قراصه (bundle)
22|1637|لیتر
23|1650|ساشه
24|1683|کپسول
25|1656|بندیل
26|1630|حلقه (رول)
27|163|قالب
28|1660|شانه
29|1647|متر مکعب
30|1689|ثوب
31|1690|نیم دوجین
32|1635|قرقره
33|164|کیلوگرم
34|1638|بطری
35|161|برگ
36|1625|سطل
37|1654|ورق
38|1646|شاخه
39|1644|قوطی
40|1617|جلد
41|162|تیوب
42|165|متر
43|1610|کلاف
44|1615|کیسه
45|1680|طغرا
46|1639|بشکه
47|1614|گالن
48|1687|فاقد بسته بندی
49|1693|کارتن (master case)
50|166|صفحه
51|1666|مخزن
52|1626|تانکر
53|1648|دبه
54|1684|سبد
55|169|تن
56|1651|بانکه
57|1633|سیلندر
58|1679|فوت مربع
59|168|حلب
60|1665|شیت
61|1659|چلیک
62|1636|جام
63|1622|گرم
64|1616|نخ
65|1652|شعله
66|1678|قیراط
67|16100|میلی لیتر
68|16101|میلی متر
69|16102|میلی گرم
70|16103|ساعت
71|16104|روز
72|16105|تن کیلومتر
73|1669|کیلووات ساعت
74|1676|نفر
75|16110|ثانیه
76|16111|دقیقه
77|16112|ماه
78|16113|سال
79|16114|قطعه
80|16115|سانتی متر
81|16116|سانتی متر مربع
82|1632|فروند
83|1653|واحد
84|16108|لیوان
85|16117|نوبت
86|16118|مگا وات ساعت
87|16119|گیگا بایت بر ثانیه
88|1681|ویال
89|1667|حلقه (دیسک)
90|16120|نسخه (جلد)
91|16121|نفر-ساعت
92|16122|کیلومتر
93|16125|آمپر
94|16126|میلی آمپر
95|16127|مثقال
96|16128|سیر
97|16129|دفعه (time)
98|16130|مگا یونیت
99|16131|کادر
100|16132|پرس
101|16133|بلوک
102|16134|نفر-ماه
""";
    internal static readonly Guid ReleaseId = Guid.Parse("14051800-0000-4000-8000-000000000118");

    private static string Quoted(string value) => "'" + value.Replace("'", "''") + "'";

    private static (string Code, string Title)[] GetEntries()
    {
        var lines = SourceRows.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length != 102) throw new InvalidOperationException("Expected exactly 102 source unit rows.");
        var result = new (string Code, string Title)[lines.Length];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < lines.Length; index++)
        {
            var parts = lines[index].Split('|');
            if (parts.Length != 3 || parts[0] != (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) ||
                parts[1].Length is < 1 or > 8 || parts[1].Any(c => c is < '0' or > '9') ||
                string.IsNullOrWhiteSpace(parts[2]) || !seen.Add(parts[1]))
                throw new InvalidOperationException($"Invalid RC_UMGS.ST row {index + 1}");
            result[index] = (parts[1], parts[2]);
        }
        return result;
    }

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var entries = GetEntries();
        var sha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SourceRows.TrimEnd('\n', '\r') + "\n"))).ToLowerInvariant();
        var release = ReleaseId.ToString("D");
        migrationBuilder.Sql($"""
INSERT INTO catalog_reference_releases
    (id,source_document,edition,source_sha256,source_reference,state,imported_at_utc)
VALUES
    ('{release}','RC_UMGS.ST','V1.18','{sha}',
     'Owner-provided RC_UMGS.ST_V1.18, Ordibehesht 1404; SHA256 of canonical transcription, not PDF',1,NOW())
ON CONFLICT (id) DO NOTHING;
DO $$
BEGIN
    IF NOT EXISTS (
      SELECT 1 FROM catalog_reference_releases
       WHERE id = '{release}'
         AND source_document = 'RC_UMGS.ST' AND edition = 'V1.18'
         AND source_sha256 = '{sha}' AND state = 1
    ) THEN
      RAISE EXCEPTION 'Official-unit seed release exists with conflicting metadata/state';
    END IF;
END $$;
""");
        foreach (var (code, title) in entries)
        {
            // Stable UUID for each (release,code), not a randomly generated ID on replay.
            var digest = SHA256.HashData(Encoding.UTF8.GetBytes(release + ":" + code));
            var id = new Guid(digest.AsSpan(0, 16)).ToString("D");
            migrationBuilder.Sql($"""
INSERT INTO official_measurement_units(id,release_id,code,title,status)
VALUES ('{id}','{release}',{Quoted(code)},{Quoted(title)},1)
ON CONFLICT (release_id, code) DO NOTHING;
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM official_measurement_units
                    WHERE release_id = '{release}' AND id = '{id}'
                      AND code = {Quoted(code)} AND title = {Quoted(title)} AND status = 1)
    THEN RAISE EXCEPTION 'Conflicting official unit row for code {code}'; END IF;
END $$;
""");
        }
        migrationBuilder.Sql($"""
DO $$
BEGIN
    IF (SELECT count(*) FROM official_measurement_units WHERE release_id = '{release}') <> 102
    THEN RAISE EXCEPTION 'RC_UMGS.ST_V1.18 seed row count must be exactly 102'; END IF;
END $$;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally preserve versioned seed rows and referenced history.
        // Rollback of reference data requires an explicitly reviewed operator procedure.
    }
}
