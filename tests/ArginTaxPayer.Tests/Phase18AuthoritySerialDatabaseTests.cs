using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

/// <summary>
/// Executes actual migration and reservation SQL in a fresh schema. No shared
/// domain rows are changed; only this fixture's random schema is dropped.
/// Opt in using PHASE18_PRODUCER_TEST_CONNECTION (loopback test DB only).
/// </summary>
public sealed class Phase18AuthoritySerialDatabaseTests
{
    [Fact]
    public async Task Classified_serials_are_atomic_monotonic_immutable_and_keep_legacy_FK_protection()
    {
        var raw=Environment.GetEnvironmentVariable("PHASE18_PRODUCER_TEST_CONNECTION");
        if(string.IsNullOrEmpty(raw))return;
        var settings=new NpgsqlConnectionStringBuilder(raw);
        if(settings.Host is not ("localhost" or "127.0.0.1") || settings.Database!="argintaxpayer_test")
            throw new InvalidOperationException("Only the loopback test database is permitted.");
        var schema="p18_serial_"+Guid.NewGuid().ToString("N");
        await using var connection=new NpgsqlConnection(raw);
        await connection.OpenAsync();
        await Exec(connection,$"CREATE SCHEMA {schema}");
        try
        {
            await Exec(connection,$"SET search_path TO {schema}");
            await Exec(connection,"""
CREATE TABLE identity_users(id uuid PRIMARY KEY);
CREATE TABLE taxpayer_profiles(id uuid PRIMARY KEY,customer_user_id uuid NOT NULL);
CREATE TABLE fiscal_memories(id uuid PRIMARY KEY,taxpayer_id uuid NOT NULL,memory_identifier text NOT NULL,authority_environment integer,status integer NOT NULL);
CREATE TABLE sandbox_fiscal_memories(id uuid PRIMARY KEY,customer_user_id uuid NOT NULL,taxpayer_id uuid NOT NULL);
CREATE TABLE invoice_documents(id uuid PRIMARY KEY,customer_user_id uuid NOT NULL,taxpayer_id uuid NOT NULL,
 revision bigint NOT NULL,state integer NOT NULL,subject integer NOT NULL,snapshot_sha256 text NOT NULL,snapshot_json jsonb NOT NULL,
 UNIQUE(customer_user_id,taxpayer_id,id));
CREATE TABLE invoice_document_revisions(invoice_id uuid NOT NULL,customer_user_id uuid NOT NULL,taxpayer_id uuid NOT NULL,
 revision bigint NOT NULL,snapshot_sha256 text NOT NULL,snapshot_json jsonb NOT NULL);
CREATE TABLE customer_taxpayer_memberships(customer_user_id uuid NOT NULL,taxpayer_id uuid NOT NULL,unlinked_at_utc timestamptz);
CREATE TABLE invoice_reviews(invoice_id uuid NOT NULL,customer_user_id uuid NOT NULL,taxpayer_id uuid NOT NULL,state integer NOT NULL);
CREATE TABLE invoice_dispatch_operations(id uuid PRIMARY KEY,customer_user_id uuid NOT NULL,taxpayer_id uuid NOT NULL,
 fiscal_memory_id uuid NOT NULL,invoice_id uuid NOT NULL,invoice_revision bigint NOT NULL,authority_environment integer,
 CONSTRAINT fk_phase10_dispatch_sandbox_memory FOREIGN KEY(fiscal_memory_id) REFERENCES sandbox_fiscal_memories(id));
""");
            // Exercise migration backfill with an actual historical unclassified operation.
            var owner=Guid.NewGuid();var taxpayer=Guid.NewGuid();var legacy=Guid.NewGuid();
            await Exec(connection,"INSERT INTO identity_users VALUES(@o); INSERT INTO taxpayer_profiles VALUES(@t,@o); INSERT INTO sandbox_fiscal_memories VALUES(@m,@o,@t); INSERT INTO invoice_dispatch_operations VALUES(@op,@o,@t,@m,@i,1,NULL)",
                ("o",owner),("t",taxpayer),("m",legacy),("op",Guid.NewGuid()),("i",Guid.NewGuid()));
            foreach(var operation in new AddAuthorityInvoiceFiscalReservations().UpOperations.OfType<SqlOperation>())
                await Exec(connection,operation.Sql);
            Assert.Equal(legacy,await Scalar(connection,"SELECT legacy_sandbox_memory_id FROM invoice_dispatch_memory_scopes"));
            await Assert.ThrowsAsync<PostgresException>(()=>Exec(connection,"DELETE FROM sandbox_fiscal_memories WHERE id=@id",("id",legacy)));
            await Assert.ThrowsAsync<PostgresException>(()=>Exec(connection,"INSERT INTO invoice_dispatch_memory_scopes(id) VALUES(@id)",("id",Guid.NewGuid())));

            var production=Guid.NewGuid();var sandbox=Guid.NewGuid();
            await Exec(connection,"INSERT INTO fiscal_memories VALUES(@p,@t,'ABC123',1,2),(@s,@t,'XYZ999',2,2); INSERT INTO customer_taxpayer_memberships VALUES(@o,@t,NULL)",
                ("p",production),("s",sandbox),("t",taxpayer),("o",owner));
            var first=await SeedInvoice(connection,owner,taxpayer,production,"Production");
            ProductionFiscalReservation reserved;
            await using(var tx=await connection.BeginTransactionAsync(IsolationLevel.Serializable))
            {
                await Exec(connection,"SELECT id FROM fiscal_memories WHERE id=@m FOR UPDATE",tx,("m",production));
                reserved=await ProductionInvoiceFiscalSerialRepository.ReserveAsync(connection,tx,first,production,"ABC123",1,"9",[],default);
                Assert.Equal(10,reserved.Serial);
                var replay=await ProductionInvoiceFiscalSerialRepository.ReserveAsync(connection,tx,first,production,"ABC123",1,"999",[],default);
                Assert.Equal(reserved,replay);
                await tx.CommitAsync();
            }
            await Assert.ThrowsAsync<PostgresException>(()=>Exec(connection,"UPDATE authority_invoice_fiscal_reservations SET serial=11 WHERE id=@id",("id",reserved.Id)));
            await Assert.ThrowsAsync<PostgresException>(()=>Exec(connection,"DELETE FROM authority_invoice_fiscal_reservations WHERE id=@id",("id",reserved.Id)));
            await Exec(connection,"INSERT INTO invoice_dispatch_operations VALUES(@op,@o,@t,@m,@i,1,1)",("op",Guid.NewGuid()),("o",owner),("t",taxpayer),("m",production),("i",first.Id));
            Assert.Equal(production,await Scalar(connection,"SELECT classified_fiscal_memory_id FROM invoice_dispatch_memory_scopes WHERE id=@m",("m",production)));
            await Assert.ThrowsAsync<PostgresException>(()=>Exec(connection,"DELETE FROM fiscal_memories WHERE id=@m",("m",production)));

            var second=await SeedInvoice(connection,owner,taxpayer,production,"Production");
            await using(var tx=await connection.BeginTransactionAsync())
            {
                var next=await ProductionInvoiceFiscalSerialRepository.ReserveAsync(connection,tx,second,production,"ABC123",1,null,[],default);
                Assert.Equal(11,next.Serial);
                await tx.RollbackAsync();
            }
            Assert.Equal(1L,await Scalar(connection,"SELECT count(*) FROM authority_invoice_fiscal_reservations"));
            await using(var tx=await connection.BeginTransactionAsync())
            {
                var next=await ProductionInvoiceFiscalSerialRepository.ReserveAsync(connection,tx,second,production,"ABC123",1,null,[],default);
                Assert.Equal(11,next.Serial);
                await tx.CommitAsync();
            }
            var sandboxInvoice=await SeedInvoice(connection,owner,taxpayer,sandbox,"Sandbox");
            await using(var tx=await connection.BeginTransactionAsync())
            {
                var own=await ProductionInvoiceFiscalSerialRepository.ReserveAsync(connection,tx,sandboxInvoice,sandbox,"XYZ999",2,null,[],default);
                Assert.Equal(1,own.Serial);
                await tx.CommitAsync();
            }
            await Exec(connection,"INSERT INTO invoice_dispatch_operations VALUES(@op,@o,@t,@m,@i,1,2)",("op",Guid.NewGuid()),("o",owner),("t",taxpayer),("m",sandbox),("i",sandboxInvoice.Id));
            // An operation cannot classify a Sandbox reservation as Production.
            await Assert.ThrowsAsync<PostgresException>(()=>Exec(connection,"INSERT INTO invoice_dispatch_operations VALUES(@op,@o,@t,@m,@i,1,1)",("op",Guid.NewGuid()),("o",owner),("t",taxpayer),("m",sandbox),("i",sandboxInvoice.Id)));
            var mismatch=await SeedInvoice(connection,owner,taxpayer,sandbox,"Production");
            await using(var tx=await connection.BeginTransactionAsync())
            {
                await Assert.ThrowsAsync<PostgresException>(()=>ProductionInvoiceFiscalSerialRepository.ReserveAsync(connection,tx,mismatch,sandbox,"XYZ999",1,null,[],default));
                await tx.RollbackAsync();
            }
            var otherOwner=Guid.NewGuid();await Exec(connection,"INSERT INTO identity_users VALUES(@o)",("o",otherOwner));
            var wrongTenant=await SeedInvoice(connection,otherOwner,taxpayer,production,"Production");
            await using(var tx=await connection.BeginTransactionAsync())
            {
                await Assert.ThrowsAsync<PostgresException>(()=>ProductionInvoiceFiscalSerialRepository.ReserveAsync(connection,tx,wrongTenant,production,"ABC123",1,null,[],default));
                await tx.RollbackAsync();
            }
        }
        finally
        {
            await Exec(connection,"SET search_path TO public");
            await Exec(connection,$"DROP SCHEMA {schema} CASCADE");
        }
    }

    private static async Task<StoredInvoice> SeedInvoice(NpgsqlConnection c,Guid owner,Guid taxpayer,Guid memory,string environment)
    {
        var id=Guid.NewGuid();var snapshot=JsonSerializer.Serialize(new {fiscalMemoryId=memory,environment});
        var hash=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)));
        var row=new StoredInvoice{Id=id,CustomerUserId=owner,TaxpayerId=taxpayer,Revision=1,State=InvoiceLifecycleState.Finalized,
            Subject=InvoiceSubject.Original,SnapshotJson=snapshot,SnapshotSha256=hash,IssueDateUtc=new DateTime(2026,11,1,12,0,0,DateTimeKind.Utc)};
        await Exec(c,"INSERT INTO invoice_documents VALUES(@i,@o,@t,1,3,1,@h,CAST(@j AS jsonb)); INSERT INTO invoice_document_revisions VALUES(@i,@o,@t,1,@h,CAST(@j AS jsonb)); INSERT INTO invoice_reviews VALUES(@i,@o,@t,4)",
            ("i",id),("o",owner),("t",taxpayer),("h",hash),("j",snapshot));
        return row;
    }
    private static Task Exec(NpgsqlConnection c,string sql,params (string,object)[] args)=>Exec(c,sql,null,args);
    private static async Task Exec(NpgsqlConnection c,string sql,NpgsqlTransaction? tx,params (string,object)[] args)
    {
        await using var command=new NpgsqlCommand(sql,c,tx);
        foreach(var(name,value) in args)command.Parameters.AddWithValue(name,value);
        await command.ExecuteNonQueryAsync();
    }
    private static async Task<object?> Scalar(NpgsqlConnection c,string sql,params (string,object)[] args)
    {
        await using var command=new NpgsqlCommand(sql,c);
        foreach(var(name,value) in args)command.Parameters.AddWithValue(name,value);
        return await command.ExecuteScalarAsync();
    }
}
