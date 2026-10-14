namespace ArginTaxPayer.Infrastructure.Persistence.Customers;

/// <summary>Auditable customer-to-taxpayer membership. Never overrides TaxpayerProfile.CustomerUserId.</summary>
public sealed class CustomerTaxpayerMembership
{
    public Guid Id { get; set; }
    public Guid CustomerUserId { get; set; }
    public Guid TaxpayerId { get; set; }
    public Guid LinkedByUserId { get; set; }
    public DateTime LinkedAtUtc { get; set; }
    public Guid? UnlinkedByUserId { get; set; }
    public DateTime? UnlinkedAtUtc { get; set; }
    public long Revision { get; set; } = 1;
}
