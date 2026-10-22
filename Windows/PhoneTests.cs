using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SilkJournal;

static class PhoneTests
{
 public static async Task Run(App app)
 {
  void Check(bool yes,string label){if(!yes)throw new Exception(label);}
  app.State.PhonePort=38929;app.SetPhoneSync(true);Check(app.Phone.Listening,"Local TLS listener starts");
  var link=new Uri(app.Phone.CreatePairLink("127.0.0.1"));var query=link.Query.TrimStart('?').Split('&').Select(p=>p.Split('=',2)).ToDictionary(p=>p[0],p=>p[1]);
  using(var data=QRCoder.QRCodeGenerator.GenerateQrCode(link.OriginalString,QRCoder.QRCodeGenerator.ECCLevel.M))using(var qr=new QRCoder.PngByteQRCode(data))File.WriteAllBytes(Path.Combine(AppContext.BaseDirectory,"pairing-test.png"),qr.GetGraphic(5));
  File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"pairing-test-link.txt"),link.OriginalString);
  bool pinMatched=false;using var handler=new HttpClientHandler{ServerCertificateCustomValidationCallback=(_,cert,_,_)=>pinMatched=cert!=null&&Convert.ToHexString(SHA256.HashData(cert.RawData))==query["fp"]};
  using var client=new HttpClient(handler){BaseAddress=new Uri("https://127.0.0.1:38929"),Timeout=TimeSpan.FromSeconds(35)};
  async Task<HttpResponseMessage> Post(string path,object data)=>await client.PostAsync(path,new StringContent(JsonSerializer.Serialize(data),Encoding.UTF8,"application/json"));
  Check((await client.GetAsync("/api/state")).StatusCode==HttpStatusCode.Unauthorized,"Unauthenticated requests rejected");Check(pinMatched,"TLS certificate matches QR fingerprint");
  Check((await Post("/api/pair",new{secret="bad",device="Test"})).StatusCode==HttpStatusCode.Forbidden,"Wrong pairing secret rejected");
  using(var blocked=new FileStream(app.Store.FilePath+".tmp",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))Check((await Post("/api/pair",new{secret=query["secret"],device="Unsaved pair"})).StatusCode==HttpStatusCode.ServiceUnavailable&&app.State.PhoneToken==null,"Pairing never acknowledged on disk failure");
  link=new Uri(app.Phone.CreatePairLink("127.0.0.1"));query=link.Query.TrimStart('?').Split('&').Select(p=>p.Split('=',2)).ToDictionary(p=>p[0],p=>p[1]);app.Commit(false,false);
  var pair=await Post("/api/pair",new{secret=query["secret"],device="Protocol test"});pair.EnsureSuccessStatusCode();using var paired=JsonDocument.Parse(await pair.Content.ReadAsStringAsync());string token=paired.RootElement.GetProperty("token").GetString()!;
  Check((await Post("/api/pair",new{secret=query["secret"],device="Replay"})).StatusCode==HttpStatusCode.Forbidden,"Pair link is single use");
  client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
  var q=app.State.Quests[1];app.PinPhone(q.Id);using var state=JsonDocument.Parse(await client.GetStringAsync("/api/state"));var root=state.RootElement;
  Check(root.GetProperty("quest").GetProperty("title").GetString()==q.Title,"Pinned quest snapshot");
  string gen=root.GetProperty("generation").GetString()!,version=root.GetProperty("questVersion").GetString()!;
  var request=new{id=q.Id,generation=gen,questVersion=version};long before=app.State.Rosaries;
  long revision=root.GetProperty("revision").GetInt64();app.Commit(false);using(var unchanged=JsonDocument.Parse(await client.GetStringAsync("/api/state")))Check(unchanged.RootElement.GetProperty("revision").GetInt64()==revision,"View-only saves do not create fake phone updates");
  using(var blocked=new FileStream(app.Store.FilePath+".tmp",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))Check((await Post("/api/complete",request)).StatusCode==HttpStatusCode.ServiceUnavailable&&!app.State.Find(q.Id)!.Done,"Phone keeps action queued when PC cannot save");
  using(var blocked=new FileStream(app.Store.FilePath+".tmp",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))Check(!app.Complete(q,true,false)&&!app.State.Find(q.Id)!.Done&&app.State.Rosaries==before,"Failed completion rolls back task and rewards");
  q=app.State.Find(q.Id)!;app.Commit(false,false);Check(!app.Store.Load().Find(q.Id)!.Done,"Failed completion absent from disk");
  (await Post("/api/complete",request)).EnsureSuccessStatusCode();Check(q.Done&&app.State.Rosaries==before+5,"Phone completes quest through shared rewards controller");
  (await Post("/api/complete",request)).EnsureSuccessStatusCode();Check(app.State.Rosaries==before+5,"Retried completion is idempotent");
  app.Complete(q,false);Check((await Post("/api/complete",request)).StatusCode==HttpStatusCode.Conflict&&!q.Done,"Reopened quest rejects stale completion even when visible content matches");q.Title+=" changed";app.Commit();Check((await Post("/api/complete",request)).StatusCode==HttpStatusCode.Conflict&&!q.Done,"Changed quest rejects stale offline action");
  using var current=JsonDocument.Parse(await client.GetStringAsync("/api/state"));long rev=current.RootElement.GetProperty("revision").GetInt64();var waiting=client.GetStringAsync("/api/state?after="+rev);await Task.Delay(100);app.PinPhone(app.State.Quests[2].Id);using var changed=JsonDocument.Parse(await waiting);Check(changed.RootElement.GetProperty("quest").GetProperty("id").GetString()==app.State.Quests[2].Id,"Long poll delivers pin changes");
  Check((await Post("/api/complete",request)).StatusCode==HttpStatusCode.Conflict,"Old notification cannot complete replaced pin");
  await MobileSyncTests.Network(app,client);
  Check(app.Store.Load().PhoneToken==token,"Paired credentials persist");app.Phone.Revoke();Check((await client.GetAsync("/api/state")).StatusCode==HttpStatusCode.Unauthorized,"Revocation invalidates token");
  app.SetPhoneSync(false);Check(!app.Phone.Listening,"Listener stops cleanly");
 }
}
