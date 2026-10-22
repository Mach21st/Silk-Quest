using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace SilkJournal;

public sealed partial class JournalState
{
 public string SyncJournalId {get;set;}=Guid.NewGuid().ToString("N");
 public HashSet<string> MobileAppliedOperations {get;set;}=new();
}

// Per-field optimistic commands, an atomic batch, and durable operation receipts.
// Device-local preferences, credentials and focus sessions never enter this DTO.
static class MobileSync
{
 internal sealed class Conflict : Exception
 {
  public JsonObject Details {get;}
  public Conflict(JsonObject op,string code,JsonNode? pc):base(code)
  {Details=new JsonObject{["opId"]=S(op,"id"),["target"]=S(op,"target"),["field"]=S(op,"field"),["code"]=code,["pc"]=pc?.DeepClone()};}
 }
 internal static string S(JsonObject o,string key)=>o[key]?.GetValue<string>()??"";
 static int N(JsonObject o,string key)=>o[key]?.GetValue<int>()??0;
 static bool B(JsonObject o,string key)=>o[key]?.GetValue<bool>()??false;
 static bool Equal(JsonNode? a,JsonNode? b)=>JsonNode.DeepEquals(a,b);
 static JsonObject Done(Quest q)=>new(){["value"]=q.Done,["stamp"]=q.CompletionStamp};
 internal static JsonObject QuestDto(Quest q,string parent,int order)=>new(){["id"]=q.Id,["title"]=q.Title,["notes"]=q.Notes,["done"]=Done(q),["today"]=q.Today,["kind"]=(int)q.Kind,["customReward"]=q.CustomReward,["parentId"]=parent,["order"]=order};
 internal static JsonObject ItemDto(ShopItem i)=>new(){["id"]=i.Id,["name"]=i.Name,["description"]=i.Description,["price"]=i.Price,["image"]=i.Image,["cropX"]=i.CropX,["cropY"]=i.CropY,["zoom"]=i.Zoom};
 public static JsonObject Snapshot(JournalState s)
 {
  var quests=new JsonObject();void Walk(List<Quest> list,string parent){for(int i=0;i<list.Count;i++){var q=list[i];quests[q.Id]=QuestDto(q,parent,i);Walk(q.Children,q.Id);}}Walk(s.Quests,"");
  var items=new JsonObject();foreach(var i in s.ShopItems)items[i.Id]=ItemDto(i);
  var purchases=new JsonObject();foreach(var p in s.ShopPurchases)purchases[p.Id]=new JsonObject{["id"]=p.Id,["itemId"]=p.ItemId,["name"]=p.Name,["price"]=p.Price,["boughtAt"]=p.BoughtAt.ToString("O")};
  return new JsonObject{["journalId"]=s.SyncJournalId,["quests"]=quests,["items"]=items,["purchases"]=purchases,["balance"]=s.Rosaries,["rewarded"]=new JsonArray(s.RewardedQuestIds.Select(x=>(JsonNode?)JsonValue.Create(x)).ToArray()),["settings"]=new JsonObject{["mainReward"]=s.MainReward,["sideReward"]=s.SideReward,["stepReward"]=s.StepReward},["pinId"]=s.PhonePinnedId??""};
 }
 static string Text(JsonNode? value,int max){string s=value?.GetValue<string>()??"";if(s.Length>max)throw new InvalidDataException("Text is too long");return s;}
 static int Int(JsonNode? value,int min,int max){int n=value?.GetValue<int>()??0;if(n<min||n>max)throw new InvalidDataException("Number is outside its range");return n;}
 static void Id(string id){if(id.Length<16||id.Length>64||id.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'))throw new InvalidDataException("Invalid ID");}
 static Quest? Parent(JournalState s,Quest q)=>s.Quests.SelectMany(x=>x.Walk()).FirstOrDefault(x=>x.Children.Contains(q));
 static JsonObject LocalQuestDto(JournalState s,Quest q){var parent=Parent(s,q);return QuestDto(q,parent?.Id??"",(parent?.Children??s.Quests).IndexOf(q));}
 static JsonObject Subtree(JournalState s,Quest q){var result=new JsonObject();foreach(var node in q.Walk()){var entry=LocalQuestDto(s,node);entry.Remove("order");result[node.Id]=entry;}return result;}
 static void Expect(JsonObject op,JsonNode? actual,JsonNode? desired=null)
 {if(!Equal(actual,op["base"])&&(desired==null||!Equal(actual,desired)))throw new Conflict(op,"changed",actual);}
 static void QuestField(JournalState s,Quest q,string field,JsonNode? value)
 {
  switch(field)
  {
   case "title":q.Title=Text(value,500);if(string.IsNullOrWhiteSpace(q.Title))throw new InvalidDataException("A title is required");break;
   case "notes":q.Notes=Text(value,20000);break;
   case "today":q.Today=value!.GetValue<bool>();break;
   case "kind":q.Kind=(QuestKind)Int(value,0,3);break;
   case "customReward":q.CustomReward=value==null?null:Int(value,0,100000);break;
   default:throw new InvalidDataException("Unsupported quest field");
  }
 }
 static void ItemField(ShopItem item,string field,JsonNode? value)
 {
  switch(field)
  {
   case "name":item.Name=Text(value,500);if(string.IsNullOrWhiteSpace(item.Name))throw new InvalidDataException("A name is required");break;
   case "description":item.Description=Text(value,20000);break;
   case "price":item.Price=Int(value,0,1000000);break;
   case "image":item.Image=Text(value,6*1024*1024);if(item.Image.Length>0)Convert.FromBase64String(item.Image);break;
   case "cropX":case "cropY":case "zoom":double n=value!.GetValue<double>();if(!double.IsFinite(n)||n<0||n>(field=="zoom"?5:1)||(field=="zoom"&&n<1))throw new InvalidDataException("Invalid crop");if(field=="cropX")item.CropX=n;else if(field=="cropY")item.CropY=n;else item.Zoom=n;break;
   default:throw new InvalidDataException("Unsupported item field");
  }
 }
 public static void Apply(JournalState s,JsonObject op)
 {
  string operation=S(op,"id"),kind=S(op,"kind"),entity=S(op,"entity"),target=S(op,"target"),field=S(op,"field");Id(operation);s.MobileAppliedOperations??=new();if(s.MobileAppliedOperations.Contains(operation))return;
  var priorDone=s.Quests.SelectMany(x=>x.Walk()).ToDictionary(x=>x.Id,x=>x.Done);
  var value=op["value"];Quest? q=s.Find(target);ShopItem? item=s.ShopItems.FirstOrDefault(x=>x.Id==target);
  if(kind=="create")
  {
   Id(target);var data=value as JsonObject??throw new InvalidDataException("Missing entity");
   if(entity=="quest")
   {
    if(q!=null)throw new Conflict(op,"exists",LocalQuestDto(s,q));
    if(s.Quests.SelectMany(x=>x.Walk()).Count()>=5000)throw new InvalidDataException("Quest limit reached");
    string parentId=S(data,"parentId");var parent=s.Find(parentId);if(parentId!=""&&parent==null)throw new Conflict(op,"parentMissing",null);
    int depth=0;for(var ancestor=parent;ancestor!=null;ancestor=Parent(s,ancestor))if(++depth>24)throw new InvalidDataException("Quest nesting limit reached");
    q=new Quest{Id=target};foreach(string f in new[]{"title","notes","today","kind","customReward"})if(data.ContainsKey(f))QuestField(s,q,f,data[f]);(parent?.Children??s.Quests).Add(q);s.Reconcile();
   }
   else if(entity=="item")
   {if(item!=null)throw new Conflict(op,"exists",ItemDto(item));if(s.ShopItems.Count>=1000)throw new InvalidDataException("Shop limit reached");item=new ShopItem{Id=target};foreach(var f in data)if(f.Key!="id")ItemField(item,f.Key,f.Value);s.ShopItems.Add(item);}
   else throw new InvalidDataException("Unsupported entity");
  }
  else if(kind=="set")
  {
   if(entity=="quest")
   {if(q==null)throw new Conflict(op,"missing",null);var dto=(JsonObject)LocalQuestDto(s,q)!;if(!dto.ContainsKey(field))throw new InvalidDataException("Unknown field");Expect(op,dto[field],value);QuestField(s,q,field,value);}
   else if(entity=="item")
   {if(item==null)throw new Conflict(op,"missing",null);var dto=ItemDto(item);if(!dto.ContainsKey(field))throw new InvalidDataException("Unknown field");Expect(op,dto[field],value);ItemField(item,field,value);}
   else if(entity=="settings")
   {var settings=new JsonObject{["mainReward"]=s.MainReward,["sideReward"]=s.SideReward,["stepReward"]=s.StepReward};if(!settings.ContainsKey(field))throw new InvalidDataException("Unknown setting");Expect(op,settings[field],value);int n=Int(value,0,100000);if(field=="mainReward")s.MainReward=n;else if(field=="sideReward")s.SideReward=n;else s.StepReward=n;}
   else throw new InvalidDataException("Unsupported entity");
  }
  else if(kind=="complete")
  {
   if(q==null)throw new Conflict(op,"missing",null);Expect(op,Done(q));var before=s.Quests.SelectMany(x=>x.Walk()).ToDictionary(x=>x.Id,x=>x.Done);s.CompleteQuest(q,value!.GetValue<bool>());
   foreach(var x in s.Quests.SelectMany(x=>x.Walk()))if(before[x.Id]!=x.Done)x.CompletionStamp=operation;
  }
  else if(kind=="delete")
  {
   if(entity=="quest"&&q!=null){Expect(op,Subtree(s,q));var ids=q.Walk().Select(x=>x.Id).ToHashSet();(Parent(s,q)?.Children??s.Quests).Remove(q);if(ids.Contains(s.PhonePinnedId??"")){s.PhonePinnedId=null;s.PhonePinGeneration=Guid.NewGuid().ToString("N");}if(ids.Contains(s.PinnedId??"")){s.PinnedId=null;s.OverlayVisible=false;}s.Reconcile();}
   else if(entity=="item"&&item!=null){Expect(op,ItemDto(item));s.ShopItems.Remove(item);}
   else if(entity!="quest"&&entity!="item")throw new InvalidDataException("Unsupported entity");
  }
  else if(kind=="buy")
  {
   if(!s.ShopPurchases.Any(x=>x.Id==operation))
   {
    if(item==null)throw new Conflict(op,"missing",null);Expect(op,JsonValue.Create(item.Price));if(s.Rosaries<item.Price)throw new Conflict(op,"funds",JsonValue.Create(s.Rosaries));
    s.ShopPurchases.Add(new ShopPurchase{Id=operation,ItemId=item.Id,Name=item.Name,Price=item.Price,BoughtAt=DateTimeOffset.UtcNow});s.Rosaries-=item.Price;
   }
  }
  else if(kind=="pin")
  {Expect(op,JsonValue.Create(s.PhonePinnedId??""),value);string id=Text(value,64);if(id!=""&&s.Find(id)==null)throw new Conflict(op,"missing",null);s.PhonePinnedId=id==""?null:id;s.PhonePinGeneration=Guid.NewGuid().ToString("N");}
  else throw new InvalidDataException("Unknown operation");
  foreach(var node in s.Quests.SelectMany(x=>x.Walk()))if(priorDone.TryGetValue(node.Id,out var wasDone)&&wasDone!=node.Done)node.CompletionStamp=operation;
  s.MobileAppliedOperations.Add(operation);
 }
 internal static ShopItem MergeShopDraft(ShopItem basis,ShopItem edited,ShopItem? latest,out bool conflict)
 {
  conflict=latest==null;if(latest==null)return edited;
  var merged=JsonSerializer.Deserialize<ShopItem>(JsonSerializer.Serialize(latest))!;
  foreach(var property in typeof(ShopItem).GetProperties().Where(x=>x.Name!="Id"))
  {
   object? old=property.GetValue(basis),value=property.GetValue(edited),current=property.GetValue(latest);
   if(Equals(old,value))continue;if(!Equals(old,current)&&!Equals(value,current))conflict=true;property.SetValue(merged,value);
  }
  return merged;
 }
 public static (int Status,JsonObject Body) Process(JournalState original,JsonObject request,out JournalState? changed)
 {
  changed=null;string journal=S(request,"journalId");if(journal!=""&&journal!=original.SyncJournalId)return(409,new JsonObject{["protocol"]=2,["error"]="differentJournal",["state"]=Snapshot(original)});
  var operations=request["operations"] as JsonArray??throw new InvalidDataException("Missing operations");if(operations.Count>128)throw new InvalidDataException("Too many operations");
  string currentRevision=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Snapshot(original).ToJsonString())));
  if(operations.Count==0&&S(request,"after")==currentRevision)return(200,new JsonObject{["protocol"]=2,["unchanged"]=true,["revision"]=currentRevision});
  var staged=UndoAction.Clone(original);
  try{foreach(var op in operations)Apply(staged,op as JsonObject??throw new InvalidDataException("Bad operation"));}
  catch(Conflict ex){return(409,new JsonObject{["protocol"]=2,["state"]=Snapshot(original),["conflict"]=ex.Details});}
  if(operations.Count>0)changed=staged;
  return(200,new JsonObject{["protocol"]=2,["state"]=Snapshot(staged),["revision"]=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Snapshot(staged).ToJsonString()))),["ack"]=new JsonArray(operations.Select(o=>(JsonNode?)JsonValue.Create(S((JsonObject)o!,"id"))).ToArray())});
 }
}

public partial class App
{
 internal (int Status,JsonObject Body) SyncMobile(JsonObject request,string token)
 {
  if(State.PhoneToken!=token)return(401,new JsonObject{["error"]="revoked"});
  if(!Commit(false,false))return(503,new JsonObject{["error"]="storage"});
  var before=State;var result=MobileSync.Process(before,request,out var changed);if(changed==null)return result;
  Journal.CancelEditors();State=changed;
  if(!Commit(false,false)){State=before;RefreshViews();return(503,new JsonObject{["error"]="storage"});}
  if(State.Find(State.FocusQuestId)?.Done!=false)Running=false;
  if(State.Find(State.PinnedId)==null&&Overlay!=null){State.OverlayVisible=false;var old=Overlay;Overlay=null;old.Close();}
  RefreshViews();return result;
 }
}
