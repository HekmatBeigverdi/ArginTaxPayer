using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

if (args.Length != 1 || (args[0] != "enroll-level1-test" && args[0] != "bootstrap-level1-test" && args[0] != "inspect-level1-test"))
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/ArginTaxPayer.IdentityOperator -- enroll-level1-test|bootstrap-level1-test|inspect-level1-test");
    return 2;
}

var cs = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb");
if (string.IsNullOrWhiteSpace(cs) ||
    !string.Equals(new NpgsqlConnectionStringBuilder(cs).Database,
        "argintaxpayer_test", StringComparison.Ordinal))
{
    Console.Error.WriteLine("Refusing operation: only explicit argintaxpayer_test is permitted.");
    return 2;
}
await using var db = new ArginTaxPayerDbContext(
    new DbContextOptionsBuilder<ArginTaxPayerDbContext>().UseNpgsql(cs).Options);
try
{
    if (!(await db.Database.GetAppliedMigrationsAsync()).Contains("20261012040000_AddIdentityMfa"))
    {
        Console.Error.WriteLine("MFA migration must be applied first.");
        return 2;
    }
}
catch (NpgsqlException)
{
    Console.Error.WriteLine("PostgreSQL connection failed. Check the macOS SSH tunnel at 127.0.0.1:15432 and verify the test database is reachable.");
    return 2;
}

if (args[0] == "inspect-level1-test")
{
    // Read-only operator diagnostic: never display password hashes or MFA secrets.
    var adminRows = await db.IdentityUserRoles.AsNoTracking()
        .Where(x => x.Role == IdentityRoleNames.Level1)
        .Join(db.IdentityUsers.AsNoTracking(), r => r.UserId, u => u.Id,
            (r, u) => new { u.NormalizedEmail, u.IsActive })
        .ToListAsync();
    Console.WriteLine($"Level 1 account count: {adminRows.Count}");
    foreach (var admin in adminRows)
        Console.WriteLine($"Level 1 email: {admin.NormalizedEmail}; active: {admin.IsActive}");
    return 0;
}
var key = Environment.GetEnvironmentVariable("Identity__MfaEncryptionKey");
if (string.IsNullOrWhiteSpace(key))
{
    Console.Error.WriteLine("Identity MFA encryption key required.");
    return 2;
}
var crypto = new IdentityMfaCrypto(key);

static string Hidden(string label)
{
    Console.Write(label);
    var chars = new List<char>();
    while (true)
    {
        var k = Console.ReadKey(intercept: true);
        if (k.Key == ConsoleKey.Enter) break;
        if (k.Key == ConsoleKey.Backspace)
        {
            if (chars.Count > 0) chars.RemoveAt(chars.Count - 1);
        }
        else if (!char.IsControl(k.KeyChar)) chars.Add(k.KeyChar);
    }
    Console.WriteLine();
    var value = new string(chars.ToArray());
    chars.Clear();
    return value;
}

Console.Write("Existing Level 1 administrator email: ");
string email;
try { email = InitialAdministratorBootstrap.NormalizeEmail(Console.ReadLine() ?? ""); }
catch (ArgumentException) { Console.Error.WriteLine("Invalid email."); return 2; }
var password = Hidden("Administrator password (hidden): ");
var user = await db.IdentityUsers.AsNoTracking()
    .SingleOrDefaultAsync(x => x.NormalizedEmail == email && x.IsActive);
if (user is null && args[0] == "bootstrap-level1-test")
{
    Console.Write("Create first Level 1 admin in TEST database? Type CREATE: ");
    if (Console.ReadLine() != "CREATE") return 2;
    try { await InitialAdministratorBootstrap.ProvisionAsync(db, email, password); }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
    {
        Console.Error.WriteLine("Initial Level 1 bootstrap rejected: " + ex.Message);
        return 2;
    }
    user = await db.IdentityUsers.AsNoTracking()
        .SingleAsync(x => x.NormalizedEmail == email && x.IsActive);
}
if (user is null || !IdentityPasswordHasher.Verify(password, user.PasswordHash) ||
    !await db.IdentityUserRoles.AsNoTracking()
        .AnyAsync(x => x.UserId == user.Id && x.Role == IdentityRoleNames.Level1))
{
    Console.Error.WriteLine("Invalid administrator identity, password, or role.");
    return 2;
}

var mfa = new IdentityMfaService(db, crypto, new IdentitySessionService(db));
var enrollment = await mfa.BeginEnrollmentAsync(user.Id);
if (enrollment is null)
{
    Console.WriteLine("MFA already enabled. Enrollment refused.");
    return 0;
}

Console.WriteLine("Private enrollment URI — never log or share:");
Console.WriteLine(enrollment.ProvisioningUri);
var code = Hidden("Enter the authenticator's six-digit code (hidden): ");
var recovery = await mfa.ConfirmEnrollmentAsync(user.Id, code);
if (recovery is null)
{
    Console.Error.WriteLine("MFA confirmation rejected.");
    return 1;
}
Console.WriteLine("Recovery codes — store OFFLINE; shown only once:");
foreach (var item in recovery.Codes) Console.WriteLine(item);
return 0;
