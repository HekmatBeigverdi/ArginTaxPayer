using System.Text.Json;
using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

// Offline export from the compiled migration assembly. No database connection is opened.
if (args is not ["--export", var destination])
    throw new ArgumentException("Usage: --export <output-directory>");
var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
    .UseNpgsql("Host=127.0.0.1;Port=1;Database=offline_export;Username=offline;Timeout=1")
    .Options;
using var db = new ArginTaxPayerDbContext(options);
var migrations = db.Database.GetMigrations().ToArray();
if (migrations.Length == 0) throw new InvalidOperationException("Migration assembly is empty.");
Directory.CreateDirectory(destination);
File.WriteAllText(Path.Combine(destination, "migrations.json"), JsonSerializer.Serialize(migrations));
File.WriteAllText(Path.Combine(destination, "migrations.sql"), db.GetService<IMigrator>()
    .GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent));
Console.WriteLine($"Exported {migrations.Length} migrations; target {migrations[^1]}.");
