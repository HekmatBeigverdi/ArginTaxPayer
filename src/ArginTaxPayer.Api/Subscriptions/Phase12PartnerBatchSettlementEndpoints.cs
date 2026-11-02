using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.AspNetCore.Mvc;

namespace ArginTaxPayer.Api.Subscriptions;
public static class Phase12PartnerBatchSettlementEndpoints
{
 public sealed record TermsRequest(Guid PartnerUserId,int PartnerBasisPoints);
 public static IEndpointRouteBuilder MapPhase12PartnerBatchSettlementEndpoints(this IEndpointRouteBuilder routes)
 {
  var group=routes.MapGroup("/api/dev/subscriptions/settlements").ExcludeFromDescription().RequireRateLimiting("signing-import");
  group.MapGet("/partners",(HttpContext http,PartnerBatchSettlementService service,CancellationToken ct)=>
   Run(http,false,token=>service.GetPartnersAsync(token,ct)));
  group.MapPut("/terms",(HttpContext http,[FromBody] TermsRequest request,PartnerBatchSettlementService service,CancellationToken ct)=>
   Run(http,true,token=>service.SaveTermsAsync(token,request.PartnerUserId,request.PartnerBasisPoints,ct)));
  group.MapGet("/preview",(HttpContext http,Guid partnerUserId,PartnerBatchSettlementService service,CancellationToken ct,int page=1)=>
   Run(http,false,token=>service.GetPreviewAsync(token,partnerUserId,page,ct)));
  group.MapGet("/batches",(HttpContext http,Guid partnerUserId,PartnerBatchSettlementService service,CancellationToken ct,int page=1)=>
   Run(http,false,token=>service.GetBatchesAsync(token,partnerUserId,page,ct)));
  group.MapPost("/batches",(HttpContext http,[FromBody] PartnerBatchRegisterRequest request,PartnerBatchSettlementService service,CancellationToken ct)=>
   Run(http,true,token=>service.RegisterAsync(token,request,ct)));
  group.MapPost("/expenses",(HttpContext http,[FromBody] PartnerBatchExpenseRequest request,PartnerBatchSettlementService service,CancellationToken ct)=>
   Run(http,true,async token=>new {expenseId=await service.AddExpenseAsync(token,request,ct)}));
  group.MapGet("/batch",(HttpContext http,Guid batchId,PartnerBatchSettlementService service,CancellationToken ct,int page=1)=>
   Run(http,false,token=>service.GetBatchAsync(token,batchId,page,false,ct)));
  group.MapGet("/print",(HttpContext http,Guid batchId,PartnerBatchSettlementService service,CancellationToken ct)=>
   Run(http,false,token=>service.GetBatchAsync(token,batchId,1,true,ct)));
  return routes;
 }
 private static async Task<IResult> Run<T>(HttpContext http,bool write,Func<string,Task<T>> action)
 {
  var ip=http.Connection.RemoteIpAddress;
  if(ip is null||!(IPAddress.IsLoopback(ip)||ip.IsIPv4MappedToIPv6&&IPAddress.IsLoopback(ip.MapToIPv4())))return Results.NotFound();
  http.Response.Headers.CacheControl="no-store";
  if(write&&http.Request.Headers.ContainsKey("Origin"))return Results.StatusCode(403);
  if(write&&http.Request.ContentLength is >4096)return Results.StatusCode(413);
  var authorization=http.Request.Headers.Authorization.ToString();
  if(authorization.Length>4096||!authorization.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)||string.IsNullOrWhiteSpace(authorization[7..]))return Results.Unauthorized();
  try{return Results.Json(await action(authorization[7..].Trim()));}
  catch(UnauthorizedAccessException){return Results.StatusCode(403);}
  catch(ArgumentException){return Results.BadRequest();}
  catch(OverflowException){return Results.BadRequest();}
  catch(InvalidOperationException ex)
  {
   var message=ex.Message switch
   {
    "Preview changed. Refresh before posting."=>"اطلاعات پیش‌نمایش تغییر کرده است. پیش‌نمایش را تازه کنید و دوباره ثبت کنید.",
    "No eligible receipt lines or missing approved terms."=>"رسید قابل تسویه وجود ندارد یا درصد سهم همکار تنظیم نشده است.",
    "Operation key payload conflict." or "Operation key conflict."=>"شناسه عملیات قبلاً با اطلاعات دیگری ثبت شده است.",
    "Total costs exceed collected receipt."=>"مجموع هزینه‌ها نمی‌تواند از مبلغ وصول‌شده رسید بیشتر باشد.",
    "Receipt requires ownership or legacy reconciliation."=>"این رسید به تعیین مالک درگاه یا بررسی تسویه‌های قبلی نیاز دارد.",
    "Partner has multiple root scopes."=>"برای همکار انتخاب‌شده چند مالک اصلی ثبت شده است.",
    _=>"ثبت تسویه انجام نشد. اطلاعات را تازه کنید و دوباره تلاش کنید."
   };
   return Results.Conflict(new {message});
  }
 }
}
