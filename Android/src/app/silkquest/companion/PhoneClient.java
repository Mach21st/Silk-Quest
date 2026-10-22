package app.silkquest.companion;

import android.content.Context;
import android.content.SharedPreferences;
import android.net.Uri;
import org.json.JSONObject;
import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.security.*;
import java.security.cert.X509Certificate;
import javax.net.ssl.*;

final class PhoneClient {
 static volatile android.net.Network localNetwork;
 static void selectLocalNetwork(Context c)throws IOException{
  android.net.ConnectivityManager manager=c.getSystemService(android.net.ConnectivityManager.class);localNetwork=null;
  for(android.net.Network network:manager.getAllNetworks()){android.net.NetworkCapabilities caps=manager.getNetworkCapabilities(network);if(caps!=null&&(caps.hasTransport(android.net.NetworkCapabilities.TRANSPORT_WIFI)||caps.hasTransport(android.net.NetworkCapabilities.TRANSPORT_ETHERNET))){localNetwork=network;return;}}
  throw new IOException("Подключись к домашней сети Wi-Fi.");
 }
 static SharedPreferences prefs(Context c){return c.getSharedPreferences("silkquest",Context.MODE_PRIVATE);}
 static JSONObject cache(Context c){try{return new JSONObject(prefs(c).getString("cache","{}"));}catch(Exception e){return new JSONObject();}}
 static boolean paired(Context c){return prefs(c).contains("token");}
 static String statusText(Context c){SharedPreferences p=prefs(c);String status=p.getString("status","Ожидание связи с компьютером");long last=p.getLong("lastSync",0);if(p.getBoolean("online",false)||last==0||!p.getBoolean("enabled",false))return status;long minutes=Math.max(0,(System.currentTimeMillis()-last)/60000);return status+" · обновлено "+(minutes==0?"недавно":minutes<60?minutes+" мин назад":minutes/60+" ч назад");}
 static String hex(byte[] bytes){StringBuilder b=new StringBuilder();for(byte v:bytes)b.append(String.format(java.util.Locale.ROOT,"%02X",v&255));return b.toString();}
 static final class PairLink {
  final String endpoint,fingerprint,secret;
  PairLink(String raw)throws Exception{
   Uri u=Uri.parse(raw.trim());if(!"silkquest".equals(u.getScheme())||!"pair".equals(u.getHost()))throw new IOException("Нужна ссылка подключения Silk Quest из настроек компьютера.");
   String host=u.getQueryParameter("host"),port=u.getQueryParameter("port");fingerprint=u.getQueryParameter("fp");secret=u.getQueryParameter("secret");
   if(host==null||!host.matches("[0-9.]+")||!local(host)||port==null||Integer.parseInt(port)<1024||Integer.parseInt(port)>65535||fingerprint==null||!fingerprint.matches("[A-Fa-f0-9]{64}")||secret==null||!secret.matches("[A-Fa-f0-9]{32}"))throw new IOException("Ссылка подключения повреждена.");
   endpoint="https://"+host+":"+port;
  }
  static boolean local(String host){String[] parts=host.split("\\.");if(parts.length!=4)return false;int[] a=new int[4];try{for(int i=0;i<4;i++){a[i]=Integer.parseInt(parts[i]);if(a[i]<0||a[i]>255)return false;}}catch(Exception e){return false;}return a[0]==10||a[0]==127||(a[0]==192&&a[1]==168)||(a[0]==172&&a[1]>=16&&a[1]<=31)||(a[0]==169&&a[1]==254);}
 }
 interface ConnectionHook {void opened(HttpsURLConnection c);}
 static final class Response {final int code;final JSONObject json;Response(int c,JSONObject j){code=c;json=j;}}
 static Response request(String endpoint,String fingerprint,String token,String path,JSONObject body,ConnectionHook hook)throws Exception{
  // The QR fingerprint is the trust anchor; never trust all certificates.
  TrustManager[] trust={new X509TrustManager(){public X509Certificate[] getAcceptedIssuers(){return new X509Certificate[0];}public void checkClientTrusted(X509Certificate[] c,String a)throws java.security.cert.CertificateException{throw new java.security.cert.CertificateException();}public void checkServerTrusted(X509Certificate[] chain,String auth)throws java.security.cert.CertificateException{try{if(chain.length==0||!MessageDigest.isEqual(hex(MessageDigest.getInstance("SHA-256").digest(chain[0].getEncoded())).getBytes(StandardCharsets.US_ASCII),fingerprint.toUpperCase(java.util.Locale.ROOT).getBytes(StandardCharsets.US_ASCII)))throw new java.security.cert.CertificateException("Сертификат компьютера не совпадает с QR-кодом.");chain[0].checkValidity();}catch(java.security.cert.CertificateException e){throw e;}catch(Exception e){throw new java.security.cert.CertificateException(e);}}}};
  SSLContext ssl=SSLContext.getInstance("TLS");ssl.init(null,trust,new SecureRandom());
  URL url=new URL(endpoint+path);android.net.Network network=localNetwork;HttpsURLConnection c=(HttpsURLConnection)(network==null?url.openConnection(Proxy.NO_PROXY):network.openConnection(url,Proxy.NO_PROXY));c.setSSLSocketFactory(ssl.getSocketFactory());c.setHostnameVerifier((host,session)->true); // Identity already checked against the exact certificate above.
  c.setConnectTimeout(6000);c.setReadTimeout(32000);c.setInstanceFollowRedirects(false);c.setUseCaches(false);if(token!=null)c.setRequestProperty("Authorization","Bearer "+token);if(hook!=null)hook.opened(c);
  try{
   if(body!=null){byte[] bytes=body.toString().getBytes(StandardCharsets.UTF_8);c.setRequestMethod("POST");c.setDoOutput(true);c.setRequestProperty("Content-Type","application/json; charset=utf-8");c.setFixedLengthStreamingMode(bytes.length);try(OutputStream out=c.getOutputStream()){out.write(bytes);}}
   int code=c.getResponseCode();InputStream in=code>=400?c.getErrorStream():c.getInputStream();if(in==null)throw new IOException("Пустой ответ компьютера.");ByteArrayOutputStream out=new ByteArrayOutputStream();try(InputStream source=in){byte[] block=new byte[4096];int n;while((n=source.read(block))!=-1){if(out.size()+n>32*1024*1024)throw new IOException("Слишком большой ответ.");out.write(block,0,n);}}
   return new Response(code,new JSONObject(out.toString("UTF-8")));
  }finally{if(hook!=null)hook.opened(null);c.disconnect();}
 }
 static Response request(Context c,String path,JSONObject body,ConnectionHook hook)throws Exception{SharedPreferences p=prefs(c);return request(p.getString("endpoint",""),p.getString("fingerprint",""),p.getString("token",null),path,body,hook);}
 static void queueCompletion(Context c,JSONObject state)throws Exception{
  JSONObject q=state.optJSONObject("quest");if(q==null||q.optBoolean("done"))return;
  JSONObject command=new JSONObject().put("id",q.getString("id")).put("generation",state.getString("generation")).put("questVersion",state.getString("questVersion"));if(!prefs(c).edit().putString("pending",command.toString()).commit())throw new IOException("Не удалось сохранить очередь на телефоне.");
 }
 private PhoneClient(){}
}
