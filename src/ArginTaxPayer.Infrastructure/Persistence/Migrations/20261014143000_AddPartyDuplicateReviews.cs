using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ArginTaxPayer.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArginTaxPayerDbContext))]
[Migration("20261014143000_AddPartyDuplicateReviews")]
public sealed class AddPartyDuplicateReviews : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("party_duplicate_reviews",
            columns: t => new
            {
                id = t.Column<Guid>(type: "uuid", nullable: false),
                customer_user_id = t.Column<Guid>(type: "uuid", nullable: false),
                candidate_party_id = t.Column<Guid>(type: "uuid", nullable: false),
                existing_party_id = t.Column<Guid>(type: "uuid", nullable: false),
                reviewer_user_id = t.Column<Guid>(type: "uuid", nullable: false),
                decision = t.Column<int>(type: "integer", nullable: false),
                reason = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                reviewed_at_utc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: t =>
            {
                t.PrimaryKey("PK_party_duplicate_reviews", x => x.id);
                t.ForeignKey("FK_party_duplicate_reviews_customer", x => x.customer_user_id, "identity_users", "id", onDelete: ReferentialAction.Restrict);
                t.ForeignKey("FK_party_duplicate_reviews_reviewer", x => x.reviewer_user_id, "identity_users", "id", onDelete: ReferentialAction.Restrict);
                t.ForeignKey("FK_party_duplicate_reviews_candidate", x => x.candidate_party_id, "parties", "id", onDelete: ReferentialAction.Restrict);
                t.ForeignKey("FK_party_duplicate_reviews_existing", x => x.existing_party_id, "parties", "id", onDelete: ReferentialAction.Restrict);
                t.CheckConstraint("ck_duplicate_review_distinct", "candidate_party_id <> existing_party_id");
                t.CheckConstraint("ck_duplicate_review_decision", "decision IN (1, 2)");
                t.CheckConstraint("ck_duplicate_review_reason", "length(trim(reason)) > 0");
            });
        m.CreateIndex("ix_duplicate_reviews_customer_time", "party_duplicate_reviews", new[] { "customer_user_id", "reviewed_at_utc" });
        m.CreateIndex("IX_party_duplicate_reviews_reviewer_user_id", "party_duplicate_reviews", "reviewer_user_id");
        m.CreateIndex("IX_party_duplicate_reviews_candidate_party_id", "party_duplicate_reviews", "candidate_party_id");
        m.CreateIndex("IX_party_duplicate_reviews_existing_party_id", "party_duplicate_reviews", "existing_party_id");
    }
    protected override void Down(MigrationBuilder m) => m.DropTable("party_duplicate_reviews");
}
