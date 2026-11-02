namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

public sealed partial class PartnerBatchSettlementService
{
 // Scope is resolved before any money is aggregated. A first batch freezes identity and terms;
 // only the cumulative approved expense input changes on subsequent lines.
 private const string CandidateSql="""
 WITH source AS (
 SELECT c.id,c.order_id,c.customer_user_id,c.channel,c.amount_irr,c.verified_at_utc,c.external_reference,
 COALESCE(prior.root_user_id,own.root_level1_user_id,o.root_admin_user_id) root,
 CASE WHEN prior.id IS NOT NULL THEN prior.partner_user_id WHEN own.customer_user_id IS NOT NULL THEN own.partner_level2_user_id ELSE o.partner_user_id END partner,
 prior.id prior_id,prior.snapshot old_snapshot,
 COALESCE(prior.gross_irr,0) previous_gross,COALESCE(prior.cost_irr,0) previous_cost,
 COALESCE(prior.root_paid_cost_irr,0) previous_root_cost,COALESCE(prior.partner_share_irr,0) previous_partner,
 COALESCE(prior.root_share_irr,0) previous_root,COALESCE(prior.net_to_root_irr,0) previous_net,
 prior.partner_basis_points frozen_rate,
 COALESCE(legacy.amount_irr,0)+COALESCE(exp.cost,0) cost,
 COALESCE(legacy.amount_irr,0)+COALESCE(exp.root_cost,0) root_cost,
 EXISTS(SELECT 1 FROM commercial_revenue_allocations a JOIN partner_settlement_events e ON e.allocation_id=a.id WHERE a.collection_id=c.id) legacy_conflict,
 COALESCE(NULLIF(trim(profile.company_name),''),NULLIF(trim(profile.first_name||' '||profile.last_name),''),u.normalized_email,u.normalized_mobile,u.id::text) customer_name,
 COALESCE(u.normalized_email,u.normalized_mobile,u.id::text) customer_contact,p.code plan_code,p.kind plan_kind,p.invoice_unit_limit
 FROM commercial_collections c JOIN commercial_orders o ON o.id=c.order_id
 JOIN identity_users u ON u.id=c.customer_user_id
 JOIN subscription_plan_catalog p ON p.id=o.plan_id
 LEFT JOIN software_user_profiles profile ON profile.user_id=c.customer_user_id
 LEFT JOIN customer_commercial_owners own ON own.customer_user_id=c.customer_user_id
 LEFT JOIN LATERAL (SELECT * FROM partner_settlement_batch_lines b WHERE b.collection_id=c.id ORDER BY created_at_utc DESC,id DESC LIMIT 1) prior ON TRUE
 LEFT JOIN commercial_approved_costs legacy ON legacy.collection_id=c.id
 LEFT JOIN LATERAL (SELECT SUM(amount_irr)::bigint cost,SUM(CASE WHEN paid_by_level1 THEN amount_irr ELSE 0 END)::bigint root_cost FROM partner_settlement_expenses WHERE collection_id=c.id) exp ON TRUE
 WHERE o.status<>4
 ), scoped AS (
 SELECT s.*,COALESCE(frozen_rate,(a.value_json->>'basis_points')::integer,t.partner_basis_points) rate
 FROM source s LEFT JOIN application_settings a ON a.scope_user_id=s.root AND a.setting_key='settlement.partner.'||s.partner::text
 LEFT JOIN LATERAL (SELECT partner_basis_points FROM partner_share_terms WHERE root_user_id=s.root AND partner_user_id=s.partner AND effective_from_utc<=s.verified_at_utc AND (effective_until_utc IS NULL OR effective_until_utc>s.verified_at_utc) ORDER BY effective_from_utc DESC,id DESC LIMIT 1) t ON TRUE
 WHERE s.root=@root AND s.partner=@partner
 ), candidates AS (
 SELECT * FROM scoped WHERE channel IN(1,2) AND NOT legacy_conflict
 AND (prior_id IS NULL OR cost<>previous_cost OR root_cost<>previous_root_cost)
 ), shares AS (
 SELECT *,CASE WHEN rate IS NULL THEN 0 ELSE trunc((amount_irr-cost)::numeric*rate/10000)::bigint END partner_share
 FROM candidates
 ), amounts AS (
 SELECT *,amount_irr-cost-partner_share root_share,amount_irr-cost-partner_share+root_cost net
 FROM shares
 ), lines AS (
 SELECT *,jsonb_build_object(
 'collectionId',id,'orderId',order_id,'customerUserId',customer_user_id,
 'customerDisplayName',COALESCE(old_snapshot->>'customerDisplayName',customer_name),
 'customerContact',COALESCE(old_snapshot->>'customerContact',customer_contact),
 'planCode',COALESCE(old_snapshot->>'planCode',plan_code),
 'planKind',COALESCE((old_snapshot->>'planKind')::integer,plan_kind),
 'invoiceUnitLimit',CASE WHEN prior_id IS NOT NULL THEN old_snapshot->'invoiceUnitLimit' ELSE to_jsonb(invoice_unit_limit) END,
 'receivedAtUtc',verified_at_utc,'receiptReference',external_reference,'channel',channel,
 'grossIrr',amount_irr,'approvedCostIrr',cost,'rootPaidCostIrr',root_cost,'partnerBasisPoints',rate,
 'partnerShareIrr',partner_share,'rootShareIrr',root_share,'previousNetToRootIrr',previous_net,
 'netToRootIrr',net,'deltaNetToRootIrr',net-previous_net,'deltaCollectedIrr',amount_irr-previous_gross,
 'deltaCostIrr',cost-previous_cost,'deltaPartnerShareIrr',partner_share-previous_partner,'deltaRootShareIrr',root_share-previous_root) line
 FROM amounts)
 """;
 public async Task<PartnerBatchPreview> GetPreviewAsync(string? bearer,Guid partnerUserId,int page=1,CancellationToken ct=default)
 {
  SubscriptionPaging.Offset(page);var s=await Authorize(bearer,false,ct);var root=await ResolvePartner(s,partnerUserId,ct);
  return await Preview(root,partnerUserId,page,ct);
 }
 private async Task<PartnerBatchPreview> Preview(Guid root,Guid partner,int page,CancellationToken ct,bool all=false)
 {
  // SQL aggregates every candidate, and only the requested ten line rows cross into the UI.
  var totals=await Strings(CandidateSql+"""
 SELECT jsonb_build_object('items','[]'::jsonb,'totalCount',count(*),'page',@page,'pageSize',10,
 'previewFingerprint',md5(COALESCE(string_agg(line::text,'|' ORDER BY id),'')),
 'totalCollectedIrr',COALESCE(sum(amount_irr-previous_gross),0),'cashIrr',COALESCE(sum(CASE WHEN channel=1 THEN amount_irr-previous_gross ELSE 0 END),0),
 'bankIrr',COALESCE(sum(CASE WHEN channel=2 THEN amount_irr-previous_gross ELSE 0 END),0),
 'totalCostIrr',COALESCE(sum(cost-previous_cost),0),'rootPaidCostIrr',COALESCE(sum(root_cost-previous_root_cost),0),
 'totalPartnerShareIrr',COALESCE(sum(partner_share-previous_partner),0),'totalRootShareIrr',COALESCE(sum(root_share-previous_root),0),
 'netToRootIrr',COALESCE(sum(net-previous_net),0),'missingRateCount',count(*) FILTER(WHERE rate IS NULL),
 'pendingGatewayCount',(SELECT count(*) FROM scoped WHERE channel=3),
 'legacyConflictCount',(SELECT count(*) FROM scoped WHERE legacy_conflict),
 'canRegister',count(*)>0 AND count(*) FILTER(WHERE rate IS NULL OR rate<0 OR rate>10000 OR cost>amount_irr)=0)::text FROM lines
 """,ct,("root",root),("partner",partner),("page",page));
  var summary=Decode<PartnerBatchPreview>(totals[0]);
  var rows=await Strings(CandidateSql+"SELECT line::text FROM lines ORDER BY id LIMIT @limit OFFSET @offset",ct,("root",root),("partner",partner),("limit",all?int.MaxValue:10),("offset",all?0:SubscriptionPaging.Offset(page)));
  return summary with {Items=rows.Select(Decode<PartnerBatchLine>).ToArray()};
 }
}
