using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Catalog;

public static class CatalogModelConfiguration
{
    public static void Apply(ModelBuilder b)
    {
        var rel = b.Entity<CatalogReferenceRelease>();
        rel.ToTable("catalog_reference_releases", t => {
            t.HasCheckConstraint("ck_catalog_release_state", "state BETWEEN 1 AND 4");
            t.HasCheckConstraint("ck_catalog_release_hash", "length(source_sha256) = 64");
        });
        rel.HasKey(x => x.Id);
        rel.Property(x => x.Id).HasColumnName("id");
        rel.Property(x => x.SourceDocument).HasColumnName("source_document").HasMaxLength(80).IsRequired();
        rel.Property(x => x.Edition).HasColumnName("edition").HasMaxLength(80).IsRequired();
        rel.Property(x => x.SourceSha256).HasColumnName("source_sha256").HasMaxLength(64).IsRequired();
        rel.Property(x => x.SourceReference).HasColumnName("source_reference").HasMaxLength(1000).IsRequired();
        rel.Property(x => x.State).HasColumnName("state");
        rel.Property(x => x.ImportedAtUtc).HasColumnName("imported_at_utc").HasColumnType("timestamp with time zone");
        rel.Property(x => x.ActivatedAtUtc).HasColumnName("activated_at_utc").HasColumnType("timestamp with time zone");
        rel.HasIndex(x => new { x.SourceDocument, x.SourceSha256 }).IsUnique().HasDatabaseName("ux_catalog_release_source_hash");

        var units = b.Entity<OfficialMeasurementUnit>();
        units.ToTable("official_measurement_units", t => t.HasCheckConstraint("ck_official_unit_code", "code ~ '^[0-9]{1,8}$'"));
        units.HasKey(x => x.Id);
        units.Property(x => x.Id).HasColumnName("id");
        units.Property(x => x.ReleaseId).HasColumnName("release_id");
        units.Property(x => x.Code).HasColumnName("code").HasMaxLength(8).IsRequired();
        units.Property(x => x.Title).HasColumnName("title").HasMaxLength(256).IsRequired();
        units.Property(x => x.Status).HasColumnName("status");
        units.HasOne<CatalogReferenceRelease>().WithMany().HasForeignKey(x => x.ReleaseId).OnDelete(DeleteBehavior.Restrict);
        units.HasIndex(x => new { x.ReleaseId, x.Code }).IsUnique().HasDatabaseName("ux_official_units_release_code");

        var goods = b.Entity<OfficialGoodsServiceEntry>();
        goods.ToTable("official_goods_service_entries", t => {
            t.HasCheckConstraint("ck_official_goods_kind", "kind IN (1,2)");
            t.HasCheckConstraint("ck_official_goods_code_format", "code ~ '^[0-9]{13}
            t.HasCheckConstraint("ck_official_goods_verification", "verification_status IN (1,2)");
        });
        goods.HasKey(x => x.Id);
        goods.Property(x => x.Id).HasColumnName("id");
        goods.Property(x => x.ReleaseId).HasColumnName("release_id");
        goods.Property(x => x.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
        goods.Property(x => x.Title).HasColumnName("title").HasMaxLength(256).IsRequired();
        goods.Property(x => x.Kind).HasColumnName("kind");
        goods.Property(x => x.VerificationStatus).HasColumnName("verification_status");
        goods.HasOne<CatalogReferenceRelease>().WithMany().HasForeignKey(x => x.ReleaseId).OnDelete(DeleteBehavior.Restrict);
        goods.HasIndex(x => new { x.ReleaseId, x.Kind, x.Code }).IsUnique().HasDatabaseName("ux_official_goods_release_kind_code");

        var vat = b.Entity<VatClassificationEntry>();
        vat.ToTable("vat_classification_entries", t => {
            t.HasCheckConstraint("ck_vat_interval", "effective_to_utc IS NULL OR effective_to_utc > effective_from_utc");
            t.HasCheckConstraint("ck_vat_percentage", "rate_percent IS NULL OR (rate_percent >= 0 AND rate_percent <= 100)");
        });
        vat.HasKey(x => x.Id);
        vat.Property(x => x.Id).HasColumnName("id");
        vat.Property(x => x.ReleaseId).HasColumnName("release_id");
        vat.Property(x => x.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
        vat.Property(x => x.LegalBasis).HasColumnName("legal_basis").HasMaxLength(512).IsRequired();
        vat.Property(x => x.RatePercent).HasColumnName("rate_percent").HasPrecision(8,4);
        vat.Property(x => x.EffectiveFromUtc).HasColumnName("effective_from_utc").HasColumnType("timestamp with time zone");
        vat.Property(x => x.EffectiveToUtc).HasColumnName("effective_to_utc").HasColumnType("timestamp with time zone");
        vat.HasOne<CatalogReferenceRelease>().WithMany().HasForeignKey(x => x.ReleaseId).OnDelete(DeleteBehavior.Restrict);
        vat.HasIndex(x => new { x.ReleaseId, x.Code, x.EffectiveFromUtc }).IsUnique().HasDatabaseName("ux_vat_release_code_from");

        var items = b.Entity<CustomerCatalogItem>();
        items.ToTable("customer_catalog_items", t => {
            t.HasCheckConstraint("ck_catalog_item_kind", "kind IN (1,2,3)");
            t.HasCheckConstraint("ck_catalog_item_status", "status BETWEEN 1 AND 4");
            t.HasCheckConstraint("ck_catalog_item_revision", "revision > 0");
        });
        items.HasKey(x => x.Id);
        items.Property(x => x.Id).HasColumnName("id");
        items.Property(x => x.CustomerUserId).HasColumnName("customer_user_id");
        items.Property(x => x.InternalCode).HasColumnName("internal_code").HasMaxLength(100).IsRequired();
        items.Property(x => x.Title).HasColumnName("title").HasMaxLength(256).IsRequired();
        items.Property(x => x.Kind).HasColumnName("kind");
        items.Property(x => x.BaseUnitId).HasColumnName("base_unit_id");
        items.Property(x => x.Status).HasColumnName("status");
        items.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken().HasDefaultValue(1L);
        items.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        items.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
        items.HasOne<ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityUser>().WithMany().HasForeignKey(x => x.CustomerUserId).OnDelete(DeleteBehavior.Restrict);
        items.HasOne<OfficialMeasurementUnit>().WithMany().HasForeignKey(x => x.BaseUnitId).OnDelete(DeleteBehavior.Restrict);
        items.HasIndex(x => new { x.CustomerUserId, x.InternalCode }).IsUnique().HasDatabaseName("ux_customer_catalog_internal_code");

        var conv = b.Entity<CatalogItemUnitConversion>();
        conv.ToTable("catalog_item_unit_conversions", t => {
            t.HasCheckConstraint("ck_catalog_conversion_ratio", "to_base_numerator > 0 AND to_base_denominator > 0");
            t.HasCheckConstraint("ck_catalog_conversion_round", "rounding_policy IN (1,2,3)");
            t.HasCheckConstraint("ck_catalog_conversion_interval", "effective_to_utc IS NULL OR effective_to_utc > effective_from_utc");
        });
        conv.HasKey(x => x.Id);
        conv.Property(x => x.Id).HasColumnName("id");
        conv.Property(x => x.ItemId).HasColumnName("item_id");
        conv.Property(x => x.FromUnitId).HasColumnName("from_unit_id");
        conv.Property(x => x.ToBaseNumerator).HasColumnName("to_base_numerator");
        conv.Property(x => x.ToBaseDenominator).HasColumnName("to_base_denominator");
        conv.Property(x => x.RoundingPolicy).HasColumnName("rounding_policy");
        conv.Property(x => x.EffectiveFromUtc).HasColumnName("effective_from_utc").HasColumnType("timestamp with time zone");
        conv.Property(x => x.EffectiveToUtc).HasColumnName("effective_to_utc").HasColumnType("timestamp with time zone");
        conv.HasOne<CustomerCatalogItem>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        conv.HasOne<OfficialMeasurementUnit>().WithMany().HasForeignKey(x => x.FromUnitId).OnDelete(DeleteBehavior.Restrict);
        conv.HasIndex(x => new { x.ItemId, x.FromUnitId, x.EffectiveFromUtc }).IsUnique().HasDatabaseName("ux_catalog_conversion_item_unit_from");

        var mapping = b.Entity<CatalogOfficialMapping>();
        mapping.ToTable("catalog_official_mappings", t => {
            t.HasCheckConstraint("ck_catalog_mapping_revision", "revision > 0");
            t.HasCheckConstraint("ck_catalog_mapping_interval", "effective_to_utc IS NULL OR effective_to_utc > effective_from_utc");
        });
        mapping.HasKey(x => x.Id);
        mapping.Property(x => x.Id).HasColumnName("id");
        mapping.Property(x => x.ItemId).HasColumnName("item_id");
        mapping.Property(x => x.OfficialEntryId).HasColumnName("official_entry_id");
        mapping.Property(x => x.EffectiveFromUtc).HasColumnName("effective_from_utc").HasColumnType("timestamp with time zone");
        mapping.Property(x => x.EffectiveToUtc).HasColumnName("effective_to_utc").HasColumnType("timestamp with time zone");
        mapping.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken().HasDefaultValue(1L);
        mapping.HasOne<CustomerCatalogItem>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        mapping.HasOne<OfficialGoodsServiceEntry>().WithMany().HasForeignKey(x => x.OfficialEntryId).OnDelete(DeleteBehavior.Restrict);
        mapping.HasIndex(x => new { x.ItemId, x.EffectiveFromUtc }).IsUnique().HasDatabaseName("ux_catalog_mapping_item_from");
    }
}
");
            t.HasCheckConstraint("ck_official_goods_verification", "verification_status IN (1,2)");
        });
        goods.HasKey(x => x.Id);
        goods.Property(x => x.Id).HasColumnName("id");
        goods.Property(x => x.ReleaseId).HasColumnName("release_id");
        goods.Property(x => x.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
        goods.Property(x => x.Title).HasColumnName("title").HasMaxLength(256).IsRequired();
        goods.Property(x => x.Kind).HasColumnName("kind");
        goods.Property(x => x.VerificationStatus).HasColumnName("verification_status");
        goods.HasOne<CatalogReferenceRelease>().WithMany().HasForeignKey(x => x.ReleaseId).OnDelete(DeleteBehavior.Restrict);
        goods.HasIndex(x => new { x.ReleaseId, x.Kind, x.Code }).IsUnique().HasDatabaseName("ux_official_goods_release_kind_code");

        var vat = b.Entity<VatClassificationEntry>();
        vat.ToTable("vat_classification_entries", t => {
            t.HasCheckConstraint("ck_vat_interval", "effective_to_utc IS NULL OR effective_to_utc > effective_from_utc");
            t.HasCheckConstraint("ck_vat_percentage", "rate_percent IS NULL OR (rate_percent >= 0 AND rate_percent <= 100)");
        });
        vat.HasKey(x => x.Id);
        vat.Property(x => x.Id).HasColumnName("id");
        vat.Property(x => x.ReleaseId).HasColumnName("release_id");
        vat.Property(x => x.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
        vat.Property(x => x.LegalBasis).HasColumnName("legal_basis").HasMaxLength(512).IsRequired();
        vat.Property(x => x.RatePercent).HasColumnName("rate_percent").HasPrecision(8,4);
        vat.Property(x => x.EffectiveFromUtc).HasColumnName("effective_from_utc").HasColumnType("timestamp with time zone");
        vat.Property(x => x.EffectiveToUtc).HasColumnName("effective_to_utc").HasColumnType("timestamp with time zone");
        vat.HasOne<CatalogReferenceRelease>().WithMany().HasForeignKey(x => x.ReleaseId).OnDelete(DeleteBehavior.Restrict);
        vat.HasIndex(x => new { x.ReleaseId, x.Code, x.EffectiveFromUtc }).IsUnique().HasDatabaseName("ux_vat_release_code_from");

        var items = b.Entity<CustomerCatalogItem>();
        items.ToTable("customer_catalog_items", t => {
            t.HasCheckConstraint("ck_catalog_item_kind", "kind IN (1,2,3)");
            t.HasCheckConstraint("ck_catalog_item_status", "status BETWEEN 1 AND 4");
            t.HasCheckConstraint("ck_catalog_item_revision", "revision > 0");
        });
        items.HasKey(x => x.Id);
        items.Property(x => x.Id).HasColumnName("id");
        items.Property(x => x.CustomerUserId).HasColumnName("customer_user_id");
        items.Property(x => x.InternalCode).HasColumnName("internal_code").HasMaxLength(100).IsRequired();
        items.Property(x => x.Title).HasColumnName("title").HasMaxLength(256).IsRequired();
        items.Property(x => x.Kind).HasColumnName("kind");
        items.Property(x => x.BaseUnitId).HasColumnName("base_unit_id");
        items.Property(x => x.Status).HasColumnName("status");
        items.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken().HasDefaultValue(1L);
        items.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        items.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
        items.HasOne<ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityUser>().WithMany().HasForeignKey(x => x.CustomerUserId).OnDelete(DeleteBehavior.Restrict);
        items.HasOne<OfficialMeasurementUnit>().WithMany().HasForeignKey(x => x.BaseUnitId).OnDelete(DeleteBehavior.Restrict);
        items.HasIndex(x => new { x.CustomerUserId, x.InternalCode }).IsUnique().HasDatabaseName("ux_customer_catalog_internal_code");

        var conv = b.Entity<CatalogItemUnitConversion>();
        conv.ToTable("catalog_item_unit_conversions", t => {
            t.HasCheckConstraint("ck_catalog_conversion_ratio", "to_base_numerator > 0 AND to_base_denominator > 0");
            t.HasCheckConstraint("ck_catalog_conversion_round", "rounding_policy IN (1,2,3)");
            t.HasCheckConstraint("ck_catalog_conversion_interval", "effective_to_utc IS NULL OR effective_to_utc > effective_from_utc");
        });
        conv.HasKey(x => x.Id);
        conv.Property(x => x.Id).HasColumnName("id");
        conv.Property(x => x.ItemId).HasColumnName("item_id");
        conv.Property(x => x.FromUnitId).HasColumnName("from_unit_id");
        conv.Property(x => x.ToBaseNumerator).HasColumnName("to_base_numerator");
        conv.Property(x => x.ToBaseDenominator).HasColumnName("to_base_denominator");
        conv.Property(x => x.RoundingPolicy).HasColumnName("rounding_policy");
        conv.Property(x => x.EffectiveFromUtc).HasColumnName("effective_from_utc").HasColumnType("timestamp with time zone");
        conv.Property(x => x.EffectiveToUtc).HasColumnName("effective_to_utc").HasColumnType("timestamp with time zone");
        conv.HasOne<CustomerCatalogItem>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        conv.HasOne<OfficialMeasurementUnit>().WithMany().HasForeignKey(x => x.FromUnitId).OnDelete(DeleteBehavior.Restrict);
        conv.HasIndex(x => new { x.ItemId, x.FromUnitId, x.EffectiveFromUtc }).IsUnique().HasDatabaseName("ux_catalog_conversion_item_unit_from");

        var mapping = b.Entity<CatalogOfficialMapping>();
        mapping.ToTable("catalog_official_mappings", t => {
            t.HasCheckConstraint("ck_catalog_mapping_revision", "revision > 0");
            t.HasCheckConstraint("ck_catalog_mapping_interval", "effective_to_utc IS NULL OR effective_to_utc > effective_from_utc");
        });
        mapping.HasKey(x => x.Id);
        mapping.Property(x => x.Id).HasColumnName("id");
        mapping.Property(x => x.ItemId).HasColumnName("item_id");
        mapping.Property(x => x.OfficialEntryId).HasColumnName("official_entry_id");
        mapping.Property(x => x.EffectiveFromUtc).HasColumnName("effective_from_utc").HasColumnType("timestamp with time zone");
        mapping.Property(x => x.EffectiveToUtc).HasColumnName("effective_to_utc").HasColumnType("timestamp with time zone");
        mapping.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken().HasDefaultValue(1L);
        mapping.HasOne<CustomerCatalogItem>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        mapping.HasOne<OfficialGoodsServiceEntry>().WithMany().HasForeignKey(x => x.OfficialEntryId).OnDelete(DeleteBehavior.Restrict);
        mapping.HasIndex(x => new { x.ItemId, x.EffectiveFromUtc }).IsUnique().HasDatabaseName("ux_catalog_mapping_item_from");
    }
}
