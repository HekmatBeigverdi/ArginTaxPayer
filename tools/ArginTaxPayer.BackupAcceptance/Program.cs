using System.Security.Cryptography;
using ArginTaxPayer.Workers.DatabaseBackup;
using ArginTaxPayer.Domain.Administration;
using Microsoft.Extensions.Configuration;
using Npgsql;

// This tool is deliberately pinned to a disposable loopback PostgreSQL instance.
var envPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".argin-backup-acceptance", "postgres.env");
var fields = File.ReadAllLines(envPath).Select(x => x.Split('=', 2)).ToDictionary(x => x[0], x => x[1]);
var admin = new NpgsqlConnectionStringBuilder { Host="127.0.0.1", Port=15438, Database="argin_backup_acceptance", Username="argin_acceptance_admin", Password=fields["POSTGRES_PASSWORD"], SslMode=SslMode.Disable, Timeout=5 };
if (fields["POSTGRES_DB"] != admin.Database || fields["POSTGRES_USER"] != admin.Username) throw new Exception("Disposable database identity required.");
var root = Path.Combine(Path.GetTempPath(), "argin-backup-acceptance-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var restoredName="recovery_"+Guid.NewGuid().ToString("N")[..12];
var roleSuffix=Guid.NewGuid().ToString("N")[..8];
var appRole="argin_acceptance_app_"+roleSuffix;
var restoreRole="argin_acceptance_restore_"+roleSuffix;
var rolePassword=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
await using(var connection=new NpgsqlConnection(admin.ConnectionString))
{
 await connection.OpenAsync();
 // Role password is freshly generated hexadecimal, never supplied by a caller.
 await using var create = new NpgsqlCommand($"CREATE ROLE {appRole} NOLOGIN; CREATE ROLE {restoreRole} LOGIN CREATEDB NOSUPERUSER NOCREATEROLE PASSWORD '{rolePassword}'; GRANT {appRole} TO {restoreRole}; GRANT USAGE,CREATE ON SCHEMA public TO {appRole}; DROP TABLE IF EXISTS backup_acceptance_rows; SET ROLE {appRole}; CREATE TABLE backup_acceptance_rows(id integer primary key, value text); INSERT INTO backup_acceptance_rows VALUES(1,'alpha'),(2,'beta'); RESET ROLE;",connection);
 await create.ExecuteNonQueryAsync();
}
var restore = new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Username=restoreRole, Password=rolePassword };
var keyPath=Path.Combine(root,"key");
await File.WriteAllTextAsync(keyPath,"acceptance-linux-1\n"+Convert.ToHexString(RandomNumberGenerator.GetBytes(32))+"\n");
File.SetUnixFileMode(keyPath,UnixFileMode.UserRead|UnixFileMode.UserWrite);
var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
 ["ConnectionStrings:ArginTaxPayerDb"]=restore.ConnectionString, ["BackupController:StateDirectory"]=Path.Combine(root,"state"),
 ["BackupController:EncryptionKeyPath"]=keyPath, ["BackupController:PgDumpPath"]="/usr/bin/pg_dump",["BackupController:PgRestorePath"]="/usr/bin/pg_restore"
}).Build();
var commands=new PostgresRestoreDatabaseCommands(restore.ConnectionString,appRole,"/usr/bin/pg_restore",root);
try
{
 var original=DatabaseBackupSettings.Default with {LocalPath=Path.Combine(root,"original"),RemoteEnabled=false};
 var artifact=await new PostgresBackupArtifactCreator(config).CreateAsync(original,CancellationToken.None);
 if(!artifact.ArchiveValidated||!artifact.ChecksumMatches)throw new Exception("Backup verification failed.");
 Console.WriteLine($"PASS encrypted backup: source={artifact.DatabaseName} bytes={artifact.EncryptedBytes}");
 var bundles=new BackupBundleService(config);
 string upload;
 await using(var zip=await bundles.DownloadAsync(original,artifact.ArtifactId,CancellationToken.None))
 {
  // Finish download before beginning upload: the service serializes transfers.
  var download=Path.Combine(root,"download.zip");
  await using(var file=File.Create(download))await zip.CopyToAsync(file);
 }
 await using(var zip=File.OpenRead(Path.Combine(root,"download.zip")))upload=await bundles.ReceiveAsync(zip,CancellationToken.None);
 var operations=new BackupOperationStore(Path.Combine(root,"state"));
 var operation=await operations.StartAsync("import-zip",Guid.NewGuid(),"validate-only",CancellationToken.None,"validate-bundle",3);
 var imported=original with {LocalPath=Path.Combine(root,"imported")};
 await bundles.ImportAsync(upload,imported,operations,operation.Id,CancellationToken.None);
 var importedArtifact=(await new LocalBackupStore(imported.LocalPath).ListAsync(CancellationToken.None)).Single();
 if(!importedArtifact.ChecksumMatches||(await operations.GetStatusAsync(CancellationToken.None))?.ProgressPercent!=100)throw new Exception("Import verification failed.");
 Console.WriteLine("PASS ZIP download, upload, checksum, key and full archive validation; progress=100");
 var key=await new FileBackupKeyProvider(keyPath).GetCurrentAsync(CancellationToken.None);
 try { await using var archive=File.OpenRead(importedArtifact.FilePath);await commands.RestoreIsolatedAsync(archive,key,restoredName,CancellationToken.None); }
 finally {CryptographicOperations.ZeroMemory(key.Material);}
 await using(var connection=new NpgsqlConnection(new NpgsqlConnectionStringBuilder(restore.ConnectionString){Database=restoredName}.ConnectionString))
 {
  await connection.OpenAsync();await using var query=new NpgsqlCommand("SELECT string_agg(value,',' ORDER BY id) FROM backup_acceptance_rows",connection);
  if((string?)await query.ExecuteScalarAsync()!="alpha,beta")throw new Exception("Restored rows do not match.");
 }
 await using(var connection=new NpgsqlConnection(admin.ConnectionString))
 {
  await connection.OpenAsync();await using var query=new NpgsqlCommand("SELECT count(*) FROM backup_acceptance_rows",connection);
  if(Convert.ToInt64(await query.ExecuteScalarAsync()) !=2)throw new Exception("Source rows changed.");
 }
 Console.WriteLine("PASS isolated PostgreSQL restore using non-superuser CREATEDB role; restored rows match and source remains unchanged.");
 Console.WriteLine("PASS no production database or application service was accessed.");
}
finally
{
 await commands.DropIsolatedAsync(restoredName,CancellationToken.None);
 Directory.Delete(root,true);
}
