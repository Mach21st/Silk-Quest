using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
namespace SilkJournal;
static class MobileSyncTests
{
 static JsonObject Op(string kind,string entity,string target,string field="",JsonNode? value=null,JsonNode? basis=null)=>new(){["id"]=Guid.NewGuid().ToString("N"),["kind"]=kind,["entity"]=entity,["target"]=target,["field"]=field,["value"]=value?.DeepClone(),["base"]=basis?.DeepClone()};
 static JsonObject Request(JournalState s,params JsonObject[] ops)=>new(){["journalId"]=s.SyncJournalId,["operations"]=new JsonArray(ops.Select(x=>(JsonNode?)x.DeepClone()).ToArray())};
 public static void Models(Action<bool,string> check)
 {
  var oldItem=new ShopItem{Name="Original",Description="Original description",Price=15};var pcEdit=new ShopItem{Id=oldItem.Id,Name="PC name",Description=oldItem.Description,Price=15};var phoneEdit=new ShopItem{Id=oldItem.Id,Name=oldItem.Name,Description="Phone description",Price=15};
  var mergedDraft=MobileSync.MergeShopDraft(oldItem,pcEdit,phoneEdit,out bool draftConflict);check(!draftConflict&&mergedDraft.Name=="PC name"&&mergedDraft.Description=="Phone description","PC shop drafts preserve unrelated mobile edits");
  phoneEdit.Name="Phone name";MobileSync.MergeShopDraft(oldItem,pcEdit,phoneEdit,out draftConflict);check(draftConflict,"PC shop draft detects same-field mobile conflict");
  MobileSync.MergeShopDraft(oldItem,pcEdit,null,out draftConflict);check(draftConflict,"PC shop editor cannot silently resurrect a mobile deletion");
  var fixture=JournalState.Starter();fixture.Rosaries=100;var fq=fixture.Quests[1];var fi=fixture.ShopItems[0];
  var fixtureOps=new[]{Op("complete","quest",fq.Id,value:JsonValue.Create(true),basis:MobileSync.Snapshot(fixture)["quests"]![fq.Id]!["done"]),Op("buy","item",fi.Id,basis:JsonValue.Create(fi.Price)),Op("set","quest",fq.Id,"notes",JsonValue.Create("Offline note"),JsonValue.Create(fq.Notes))};
  var fixtureResult=MobileSync.Process(fixture,Request(fixture,fixtureOps),out var fixtureAfter);
  File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"mobile-sync-fixture.json"),new JsonObject{["before"]=MobileSync.Snapshot(fixture),["operations"]=Request(fixture,fixtureOps)["operations"]!.DeepClone(),["after"]=MobileSync.Snapshot(fixtureAfter!)}.ToJsonString());
  var s=JournalState.Starter();s.PhoneToken="never-export-this";s.PhoneDeviceName="Private device";s.FocusSeconds=213;var q=s.Quests[1];string id=q.Id;
  var snapshot=MobileSync.Snapshot(s);check(!snapshot.ToJsonString().Contains("never-export-this")&&!snapshot.ContainsKey("focusSeconds"),"Mobile DTO excludes credentials and focus session");
  var title=Op("set","quest",id,"title",JsonValue.Create("Phone title"),JsonValue.Create(q.Title));q.Notes="PC notes";
  var result=MobileSync.Process(s,Request(s,title),out var changed);check(result.Status==200&&changed!.Find(id)!.Title=="Phone title"&&changed.Find(id)!.Notes=="PC notes","Independent edits merge per field");check(s.Find(id)!.Title!="Phone title","Batch staging does not mutate original");s=changed!;
  var retry=MobileSync.Process(s,Request(s,title),out changed);check(retry.Status==200&&changed!.Find(id)!.Title=="Phone title","A retried operation is idempotent");
  var phone=Op("set","quest",id,"title",JsonValue.Create("Phone changed"),JsonValue.Create("Phone title"));s.Find(id)!.Title="PC changed";
  result=MobileSync.Process(s,Request(s,phone),out changed);check(result.Status==409&&changed==null&&result.Body["conflict"]!["pc"]!.GetValue<string>()=="PC changed","Same-field changes produce explicit conflict");
  phone["base"]="PC changed";result=MobileSync.Process(s,Request(s,phone),out changed);check(result.Status==200&&changed!.Find(id)!.Title=="Phone changed","Explicit phone choice rebases conflict");s=changed!;
  var complete=Op("complete","quest",id,value:JsonValue.Create(true),basis:MobileSync.Snapshot(s)["quests"]![id]!["done"]);long balance=s.Rosaries;result=MobileSync.Process(s,Request(s,complete),out changed);check(result.Status==200&&changed!.Rosaries==balance+5&&changed.Find(id)!.CompletionStamp==complete["id"]!.GetValue<string>(),"Mobile completion uses desktop ledger and deterministic stamp");s=changed!;
  result=MobileSync.Process(s,Request(s,complete),out changed);check(result.Status==200&&changed!.Rosaries==s.Rosaries,"Lost response cannot award twice");
  s.CompleteQuest(s.Find(id)!,false);result=MobileSync.Process(s,Request(s,Op("complete","quest",id,value:JsonValue.Create(true),basis:new JsonObject{["value"]=false,["stamp"]=""})),out changed);check(result.Status==409&&!s.Find(id)!.Done,"Stale offline completion cannot re-close a reopened quest");
  var root=s.Quests[0];var mainComplete=Op("complete","quest",root.Id,value:JsonValue.Create(true),basis:MobileSync.Snapshot(s)["quests"]![root.Id]!["done"]);result=MobileSync.Process(s,Request(s,mainComplete),out changed);check(result.Status==200&&changed!.Quests[0].Walk().All(x=>x.Done)&&changed.Quests[0].Walk().Where(x=>!root.Walk().First(y=>y.Id==x.Id).Done).All(x=>x.CompletionStamp==mainComplete["id"]!.GetValue<string>()),"Grouped completion shares reward logic and stamps");s=changed!;
  s.Rosaries=40;var item=s.ShopItems[0];var buy=Op("buy","item",item.Id,basis:JsonValue.Create(item.Price));result=MobileSync.Process(s,Request(s,buy),out changed);check(result.Status==200&&changed!.Rosaries==25&&changed.ShopPurchases.Count==1,"Mobile purchase deducts exactly once");s=changed!;result=MobileSync.Process(s,Request(s,buy),out changed);check(result.Status==200&&changed!.Rosaries==25&&changed.ShopPurchases.Count==1,"Purchase retry is idempotent");
  var expensive=Op("buy","item",item.Id,basis:JsonValue.Create(item.Price));s.Rosaries=0;var independent=Op("set","quest",id,"notes",JsonValue.Create("Should not commit"),JsonValue.Create(s.Find(id)!.Notes));result=MobileSync.Process(s,Request(s,independent,expensive),out changed);check(result.Status==409&&changed==null&&s.Find(id)!.Notes!="Should not commit"&&s.ShopPurchases.Count==1,"Insufficient balance rolls back the entire batch");
  var priceChange=Op("buy","item",item.Id,basis:JsonValue.Create(item.Price));item.Price=30;result=MobileSync.Process(s,Request(s,priceChange),out changed);check(result.Status==409,"Changed price requires a new choice");
  string fresh=Guid.NewGuid().ToString("N");var create=Op("create","quest",fresh,value:new JsonObject{["title"]="New offline step",["notes"]="",["today"]=true,["kind"]=3,["parentId"]=root.Id});result=MobileSync.Process(s,Request(s,create),out changed);check(result.Status==200&&changed!.Find(fresh)!=null&&!changed.Quests[0].Done,"Offline child creation reopens a completed group");s=changed!;
  var current=MobileSync.Snapshot(s);var subtree=new JsonObject();foreach(var node in s.Find(fresh)!.Walk()){var entry=(JsonObject)current["quests"]![node.Id]!.DeepClone();entry.Remove("order");subtree[node.Id]=entry;}var del=Op("delete","quest",fresh,basis:subtree);s.Find(fresh)!.Notes="Changed on PC";result=MobileSync.Process(s,Request(s,del),out changed);check(result.Status==409&&s.Find(fresh)!=null,"Delete-versus-edit does not silently discard text");
  var wrong=Request(s);wrong["journalId"]=Guid.NewGuid().ToString("N");result=MobileSync.Process(s,wrong,out changed);check(result.Status==409&&result.Body["error"]!.GetValue<string>()=="differentJournal","A different PC journal cannot silently replace phone data");
  result=MobileSync.Process(s,Request(s),out changed);string rev=result.Body["revision"]!.GetValue<string>();var poll=Request(s);poll["after"]=rev;result=MobileSync.Process(s,poll,out changed);check(result.Status==200&&result.Body["unchanged"]!.GetValue<bool>()&&!result.Body.ContainsKey("state"),"Unchanged journal poll avoids re-sending images");
  string persisted=System.Text.Json.JsonSerializer.Serialize(s);var restored=System.Text.Json.JsonSerializer.Deserialize<JournalState>(persisted)!;check(restored.SyncJournalId==s.SyncJournalId&&restored.MobileAppliedOperations.SetEquals(s.MobileAppliedOperations),"Journal identity and durable operation receipts survive restart");
 }
 public static async Task Network(App app,HttpClient client)
 {
  void Check(bool ok,string name){if(!ok)throw new Exception(name);}
  async Task<HttpResponseMessage> Post(JsonObject body)=>await client.PostAsync("/api/sync",new StringContent(body.ToJsonString(),Encoding.UTF8,"application/json"));
  using var initial=await Post(Request(app.State));Check(initial.StatusCode==HttpStatusCode.OK,"Full journal endpoint available over authenticated TLS");var first=JsonNode.Parse(await initial.Content.ReadAsStringAsync())!;Check(first["protocol"]!.GetValue<int>()==2,"Full sync protocol version");
  var q=app.State.Quests[1];var title=Op("set","quest",q.Id,"title",JsonValue.Create("Edited on phone over TLS"),JsonValue.Create(q.Title));
  using(var blocked=new FileStream(app.Store.FilePath+".tmp",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){using var failed=await Post(Request(app.State,title));Check(failed.StatusCode==HttpStatusCode.ServiceUnavailable&&app.State.Find(q.Id)!.Title!="Edited on phone over TLS","Full sync does not acknowledge an unavailable disk");}
  using var success=await Post(Request(app.State,title));Check(success.StatusCode==HttpStatusCode.OK&&app.Store.Load().Find(q.Id)!.Title=="Edited on phone over TLS","Full journal writes reach disk before acknowledgement");
  using var repeat=await Post(Request(app.State,title));Check(repeat.StatusCode==HttpStatusCode.OK&&app.State.MobileAppliedOperations.Contains(title["id"]!.GetValue<string>()),"Real network duplicate is acknowledged from receipts");
  client.DefaultRequestHeaders.Add("Origin","https://untrusted.example");using var rejected=await Post(Request(app.State));Check(rejected.StatusCode==HttpStatusCode.Forbidden,"Browser-origin full journal requests remain forbidden");client.DefaultRequestHeaders.Remove("Origin");
 }
}
