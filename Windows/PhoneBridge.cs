using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SilkJournal;

// A deliberately small, local-only HTTPS protocol. No URL reservations, admin rights or cloud.
public sealed class PhoneBridge : IDisposable
{
 readonly App app;
 readonly SemaphoreSlim clients=new(8);
 readonly object gate=new();
 CancellationTokenSource? lifetime;
 TcpListener? listener;
 X509Certificate2? certificate;
 string? pairSecret;
 DateTime pairExpires;
 int pairAttempts;
 long revision=DateTime.UtcNow.Ticks;
 string snapshot="{}";
 string signature="";
 string questVersion="";
 public string? Error {get;private set;}
 public bool Listening=>listener!=null;
 public bool PairingActive {get{lock(gate)return pairSecret!=null&&DateTime.UtcNow<pairExpires&&pairAttempts<5;}}
 public DateTime LastSeen {get;private set;}
 public DateTime PairExpires=>pairExpires;
 public string Fingerprint=>certificate==null?"":Convert.ToHexString(SHA256.HashData(certificate.RawData));
 public PhoneBridge(App app){this.app=app;Publish();}
 public static bool IsPrivate(IPAddress address)
 {
  if(address.AddressFamily!=AddressFamily.InterNetwork)return false;
  var b=address.GetAddressBytes();return b[0]==10||b[0]==127||(b[0]==192&&b[1]==168)||(b[0]==172&&b[1]>=16&&b[1]<=31)||(b[0]==169&&b[1]==254);
 }
 public static List<string> Addresses()=>NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType!=NetworkInterfaceType.Loopback).OrderBy(n=>n.GetIPProperties().GatewayAddresses.Any(g=>g.Address.AddressFamily==AddressFamily.InterNetwork&&!g.Address.Equals(IPAddress.Any))?0:1).SelectMany(n=>n.GetIPProperties().UnicastAddresses).Select(a=>a.Address).Where(a=>IsPrivate(a)&&!IPAddress.IsLoopback(a)).Select(a=>a.ToString()).Distinct().OrderBy(a=>a.StartsWith("169.254.",StringComparison.Ordinal)?1:0).ToList();
 public string Status=>Error!=null?Error:!Listening?L.T("Подключение выключено"):app.State.PhoneToken==null?L.T("Телефон ещё не подключён"):$"{app.State.PhoneDeviceName} · "+(DateTime.UtcNow-LastSeen<TimeSpan.FromSeconds(45)?L.T("на связи"):LastSeen==default?L.T("ожидает Wi-Fi"):L.T("связь ")+RelativeTime(LastSeen));
 static string RelativeTime(DateTime time){var age=DateTime.UtcNow-time;return age.TotalMinutes<1?L.T("меньше минуты назад"):age.TotalHours<1?L.F("{0} мин назад",(int)age.TotalMinutes):L.F("{0} ч назад",(int)age.TotalHours);}
 public void Start()
 {
  if(Listening)return;Error=null;
  try
  {
   string file=Path.Combine(Path.GetDirectoryName(app.Store.FilePath)!,"phone-cert.pfx");
   if(File.Exists(file))certificate=new X509Certificate2(File.ReadAllBytes(file));
   else
   {
    using var rsa=RSA.Create(2048);var request=new CertificateRequest("CN=Silk Quest local companion",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
    using var generated=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddYears(5));
    var bytes=generated.Export(X509ContentType.Pfx);File.WriteAllBytes(file,bytes);certificate=new X509Certificate2(bytes);
   }
   lifetime=new CancellationTokenSource();listener=new TcpListener(IPAddress.Any,app.State.PhonePort);listener.Start();
   _=Task.Run(()=>AcceptLoop(lifetime.Token));Publish();
  }
  catch(Exception ex)when(ex is IOException or SocketException or CryptographicException)
  {Stop();Error=L.T("Не удалось включить Wi-Fi: ")+ex.Message;}
 }
 public void Stop(){lock(gate){pairSecret=null;}lifetime?.Cancel();listener?.Stop();listener=null;lifetime?.Dispose();lifetime=null;certificate?.Dispose();certificate=null;}
 public void Dispose()=>Stop();
 public string CreatePairLink(string address)
 {
  if(!Listening)throw new InvalidOperationException(L.T("Wi-Fi выключен"));
  if(!IPAddress.TryParse(address,out var ip)||!IsPrivate(ip))throw new ArgumentException(L.T("Нужен локальный IPv4-адрес"));
  lock(gate){pairSecret=Convert.ToHexString(RandomNumberGenerator.GetBytes(16));pairExpires=DateTime.UtcNow.AddMinutes(5);pairAttempts=0;
   return $"silkquest://pair?host={address}&port={app.State.PhonePort}&fp={Fingerprint}&secret={pairSecret}";}
 }
 public void Revoke(){lock(gate){pairSecret=null;}string? oldToken=app.State.PhoneToken,oldName=app.State.PhoneDeviceName;app.State.PhoneToken=null;app.State.PhoneDeviceName=null;if(!app.Commit(false)){app.State.PhoneToken=oldToken;app.State.PhoneDeviceName=oldName;}else LastSeen=default;app.RefreshViews();}
 public void Publish()
 {
  var s=app.State;var q=s.Find(s.PhonePinnedId);
  questVersion=q==null?"":Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(q))));
  var payload=new{protocol=1,generation=s.PhonePinGeneration,questVersion,balance=s.Rosaries,
   quest=q==null?null:new{id=q.Id,title=q.Title,notes=q.Notes,done=q.Done,reward=s.PendingReward(q),completed=q.Leaves().Count(x=>x.Done),total=q.Leaves().Count(),steps=q.Children.Count==0?Array.Empty<object>():q.Leaves().Select(x=>(object)new{id=x.Id,title=x.Title,done=x.Done}).ToArray()}};
  string content=JsonSerializer.Serialize(payload);if(content==signature)return;signature=content;
  Volatile.Write(ref snapshot,"{\"revision\":"+Interlocked.Increment(ref revision).ToString(System.Globalization.CultureInfo.InvariantCulture)+","+content[1..]);
 }
 async Task AcceptLoop(CancellationToken stop)
 {
  var active=listener!;
  while(!stop.IsCancellationRequested)
  {
   TcpClient client;try{client=await active.AcceptTcpClientAsync(stop);}catch(Exception ex)when(ex is OperationCanceledException or SocketException or ObjectDisposedException){break;}
   if(client.Client.RemoteEndPoint is not IPEndPoint remote||!IsPrivate(remote.Address)||!clients.Wait(0)){client.Dispose();continue;}
   _=Serve(client,stop);
  }
 }
 async Task Serve(TcpClient client,CancellationToken stop)
 {
  try
  {
   using(client)using(var tls=new SslStream(client.GetStream(),false))using(var requestTimeout=CancellationTokenSource.CreateLinkedTokenSource(stop))
   {
    requestTimeout.CancelAfter(TimeSpan.FromSeconds(40));var ct=requestTimeout.Token;
    await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions{ServerCertificate=certificate,EnabledSslProtocols=SslProtocols.Tls12|SslProtocols.Tls13},ct);
    var request=await ReadRequest(tls,ct);
    if(request.Headers.ContainsKey("Origin")){await Reply(tls,403,new{error="Native companion only"},ct);return;}
    if(request.Method=="POST"&&request.Path=="/api/pair")
    {
     using var json=JsonDocument.Parse(request.Body);string secret=json.RootElement.GetProperty("secret").GetString()??"";
     string name=json.RootElement.GetProperty("device").GetString()??"Android";name=new string(name.Where(c=>!char.IsControl(c)).Take(64).ToArray());
     bool accepted;lock(gate){accepted=pairSecret!=null&&DateTime.UtcNow<pairExpires&&pairAttempts++<5&&SecretEqual(secret,pairSecret);if(accepted)pairSecret=null;}
     if(!accepted){await Reply(tls,403,new{error="Pairing expired. Create a new QR code on PC."},ct);return;}
     string token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
     bool saved=await app.Dispatcher.InvokeAsync(()=>{string? oldToken=app.State.PhoneToken,oldName=app.State.PhoneDeviceName;app.State.PhoneToken=token;app.State.PhoneDeviceName=name;if(!app.Commit(false,false)){app.State.PhoneToken=oldToken;app.State.PhoneDeviceName=oldName;app.RefreshViews();return false;}LastSeen=DateTime.UtcNow;app.RefreshViews();return true;});
     if(!saved){await Reply(tls,503,new{error="PC could not save pairing. Create a new QR after resolving storage error."},ct);return;}
     await Reply(tls,200,new{protocol=1,token},ct);return;
    }
    request.Headers.TryGetValue("Authorization",out string? auth);
    string? expected=await app.Dispatcher.InvokeAsync(()=>app.State.PhoneToken);
    if(expected==null||!SecretEqual(auth??"","Bearer "+expected)){await Reply(tls,401,new{error="Phone is not paired"},ct);return;}
    LastSeen=DateTime.UtcNow;
    if(request.Method=="POST"&&request.Path=="/api/sync")
    {
     try
     {
      var body=System.Text.Json.Nodes.JsonNode.Parse(request.Body) as System.Text.Json.Nodes.JsonObject??throw new InvalidDataException("Invalid document");
      var result=await app.Dispatcher.InvokeAsync(()=>app.SyncMobile(body,expected));
      await Reply(tls,result.Status,result.Body,ct);
     }
     catch(Exception ex)when(ex is InvalidDataException or JsonException or InvalidOperationException or FormatException or ArgumentException or OverflowException)
     {await Reply(tls,422,new{error="Invalid journal operation",detail=ex.Message},ct);}
     return;
    }
    if(request.Method=="GET"&&request.Path=="/api/state")
    {
     long.TryParse(request.Query.GetValueOrDefault("after"),out long after);
     if(after==Interlocked.Read(ref revision))for(int i=0;i<50&&after==Interlocked.Read(ref revision);i++)await Task.Delay(500,ct);
     // Revocation also invalidates already-open long polls.
     if(!SecretEqual(expected,await app.Dispatcher.InvokeAsync(()=>app.State.PhoneToken)??"")){await Reply(tls,401,new{error="Revoked"},ct);return;}
     await ReplyRaw(tls,200,Volatile.Read(ref snapshot),ct);return;
    }
    if(request.Method=="POST"&&request.Path=="/api/complete")
    {
     using var json=JsonDocument.Parse(request.Body);string? id=json.RootElement.GetProperty("id").GetString(),generation=json.RootElement.GetProperty("generation").GetString(),version=json.RootElement.GetProperty("questVersion").GetString();
     int result=await app.Dispatcher.InvokeAsync(()=>{
      if(!app.Commit(false,false))return 503; // Never acknowledge an action that has not reached disk.
      if(app.State.PhoneToken!=expected||app.State.PhonePinnedId!=id||app.State.PhonePinGeneration!=generation||app.State.Find(id) is not Quest quest)return 409;
      if(!quest.Done&&version!=questVersion)return 409;
      if(!quest.Done&&!app.Complete(quest,true,false))return 503;return 200;
     });
     if(result==503)await Reply(tls,503,new{error="PC storage unavailable. Keep completion queued."},ct);
     else await ReplyRaw(tls,result,Volatile.Read(ref snapshot),ct);return;
    }
    await Reply(tls,404,new{error="Unknown endpoint"},ct);
   }
  }
  catch(Exception ex)when(ex is IOException or SocketException or OperationCanceledException or AuthenticationException or JsonException or KeyNotFoundException or InvalidOperationException or ObjectDisposedException or ArgumentException){/* A client cannot crash the journal. */}
  finally{clients.Release();}
 }
 internal static bool SecretEqual(string a,string b)=>CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a),Encoding.UTF8.GetBytes(b));
 sealed record Request(string Method,string Path,Dictionary<string,string> Query,Dictionary<string,string> Headers,string Body);
 static async Task<Request> ReadRequest(Stream stream,CancellationToken ct)
 {
  var header=new List<byte>();var one=new byte[1];
  while(header.Count<8192){if(await stream.ReadAsync(one,ct)==0)throw new IOException();header.Add(one[0]);int n=header.Count;if(n>=4&&header[n-4]==13&&header[n-3]==10&&header[n-2]==13&&header[n-1]==10)break;}
  if(header.Count>=8192)throw new IOException("Header too large");
  var lines=Encoding.ASCII.GetString(header.ToArray()).Split("\r\n");var start=lines[0].Split(' ');if(start.Length!=3)throw new IOException("Bad request");
  var uri=new Uri("https://localhost"+start[1]);var headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  foreach(var line in lines.Skip(1)){int colon=line.IndexOf(':');if(colon>0)headers[line[..colon].Trim()]=line[(colon+1)..].Trim();}
  int length=0;if(headers.TryGetValue("Content-Length",out string? raw)&&!int.TryParse(raw,out length))throw new IOException("Bad length");
  if(length<0||length>16*1024*1024||headers.ContainsKey("Transfer-Encoding"))throw new IOException("Unsupported body");
  var body=new byte[length];await stream.ReadExactlyAsync(body,ct);var query=new Dictionary<string,string>();
  foreach(var part in uri.Query.TrimStart('?').Split('&')){var kv=part.Split('=',2);if(kv.Length==2)query[Uri.UnescapeDataString(kv[0])]=Uri.UnescapeDataString(kv[1]);}
  return new Request(start[0],uri.AbsolutePath,query,headers,Encoding.UTF8.GetString(body));
 }
 static Task Reply(Stream s,int status,object body,CancellationToken ct)=>ReplyRaw(s,status,JsonSerializer.Serialize(body),ct);
 static async Task ReplyRaw(Stream s,int status,string body,CancellationToken ct)
 {
  byte[] bytes=Encoding.UTF8.GetBytes(body);byte[] header=Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Response\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n");await s.WriteAsync(header,ct);await s.WriteAsync(bytes,ct);await s.FlushAsync(ct);
 }
}
