package app.silkquest.companion;

import android.content.Context;
import android.util.AtomicFile;
import org.json.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.util.*;

// Journal + optimistic outbox are written together before an action is shown as saved.
final class MobileStore {
 private static JSONObject doc;
 private static final Object lock=new Object(), networkLock=new Object();
 static JSONObject copy(JSONObject x)throws JSONException{return new JSONObject(x.toString());}
 static JSONArray copy(JSONArray x)throws JSONException{return new JSONArray(x.toString());}
 static String id(){return UUID.randomUUID().toString().replace("-","");}
 static JSONObject empty()throws JSONException{return new JSONObject().put("journalId","").put("quests",new JSONObject()).put("items",new JSONObject()).put("purchases",new JSONObject()).put("balance",0).put("rewarded",new JSONArray()).put("settings",new JSONObject().put("mainReward",25).put("sideReward",5).put("stepReward",5)).put("pinId","");}
 static AtomicFile file(Context c){return new AtomicFile(new File(c.getFilesDir(),"mobile-journal.json"));}
 static JSONObject load(Context c)throws Exception {
  if(doc!=null)return doc;
  if(file(c).getBaseFile().exists()||new File(c.getFilesDir(),"mobile-journal.json.bak").exists())try(InputStream in=file(c).openRead()){ByteArrayOutputStream out=new ByteArrayOutputStream();byte[] b=new byte[8192];int n;while((n=in.read(b))!=-1){if(out.size()+n>48*1024*1024)throw new IOException("Journal is too large");out.write(b,0,n);}doc=new JSONObject(out.toString("UTF-8"));}
  else doc=new JSONObject().put("state",empty()).put("queue",new JSONArray()).put("preferences",new JSONObject().put("language","ru").put("focusMinutes",25).put("motion",true).put("weathering",true));
  return doc;
 }
 static void save(Context c,JSONObject next)throws Exception {
  byte[] bytes=next.toString().getBytes(StandardCharsets.UTF_8);if(bytes.length>48*1024*1024)throw new IOException("Журнал слишком большой. Уменьши картинки товаров.");
  AtomicFile f=file(c);FileOutputStream out=null;
  try{out=f.startWrite();out.write(bytes);f.finishWrite(out);doc=next;}catch(Exception e){if(out!=null)f.failWrite(out);throw e;}
 }
 static String view(Context c)throws Exception {synchronized(lock){JSONObject d=copy(load(c));d.put("paired",PhoneClient.paired(c)).put("online",PhoneClient.prefs(c).getBoolean("online",false)).put("status",PhoneClient.statusText(c));return d.toString();}}
 static boolean hasJournal(Context c){synchronized(lock){try{return load(c).getJSONObject("state").getJSONObject("quests").length()>0;}catch(Exception e){return false;}}}
 static JSONArray children(JSONObject s,String parent)throws JSONException {
  JSONObject all=s.getJSONObject("quests");ArrayList<JSONObject> found=new ArrayList<>();Iterator<String> keys=all.keys();while(keys.hasNext()){JSONObject q=all.getJSONObject(keys.next());if(parent.equals(q.optString("parentId")))found.add(q);}
  Collections.sort(found,(a,b)->{int n=Integer.compare(a.optInt("order"),b.optInt("order"));return n==0?a.optString("id").compareTo(b.optString("id")):n;});JSONArray a=new JSONArray();for(JSONObject q:found)a.put(q);return a;
 }
 static ArrayList<JSONObject> subtree(JSONObject s,String target)throws JSONException {ArrayList<JSONObject> a=new ArrayList<>();JSONObject q=s.getJSONObject("quests").optJSONObject(target);if(q==null)return a;a.add(q);JSONArray kids=children(s,target);for(int i=0;i<kids.length();i++)a.addAll(subtree(s,kids.getJSONObject(i).getString("id")));return a;}
 static int reward(JSONObject s,JSONObject q)throws JSONException {if(!q.isNull("customReward"))return q.optInt("customReward");int kind=q.optInt("kind");if(kind==0)kind=children(s,q.getString("id")).length()>0?1:q.optString("parentId").isEmpty()?2:3;return s.getJSONObject("settings").optInt(kind==1?"mainReward":kind==2?"sideReward":"stepReward",5);}
 static boolean claimed(JSONArray a,String id){for(int i=0;i<a.length();i++)if(id.equals(a.optString(i)))return true;return false;}
 static void reconcile(JSONObject s,String parent,String stamp)throws JSONException {JSONArray kids=children(s,parent);for(int i=0;i<kids.length();i++)reconcile(s,kids.getJSONObject(i).getString("id"),stamp);JSONObject q=s.getJSONObject("quests").optJSONObject(parent);if(q!=null&&kids.length()>0){boolean done=true;for(int i=0;i<kids.length();i++)done&=kids.getJSONObject(i).getJSONObject("done").optBoolean("value");if(q.getJSONObject("done").optBoolean("value")!=done)q.put("done",new JSONObject().put("value",done).put("stamp",stamp));}}
 static void apply(JSONObject s,JSONObject op)throws Exception {
  String kind=op.getString("kind"),entity=op.optString("entity"),target=op.optString("target"),field=op.optString("field");JSONObject quests=s.getJSONObject("quests"),items=s.getJSONObject("items");Object v=op.opt("value");
  if(kind.equals("create")){JSONObject value=copy(op.getJSONObject("value"));value.put("id",target);(entity.equals("quest")?quests:items).put(target,value);if(entity.equals("quest"))reconcile(s,"",op.getString("id"));}
  else if(kind.equals("set")){JSONObject record=entity.equals("quest")?quests.optJSONObject(target):entity.equals("item")?items.optJSONObject(target):s.getJSONObject("settings");if(record!=null)record.put(field,v==null?JSONObject.NULL:v);}
  else if(kind.equals("complete")){
   HashSet<String> unfinished=new HashSet<>();Iterator<String> keys=quests.keys();while(keys.hasNext()){String key=keys.next();if(!quests.getJSONObject(key).getJSONObject("done").optBoolean("value"))unfinished.add(key);}
   for(JSONObject q:subtree(s,target))if(q.getJSONObject("done").optBoolean("value")!=op.getBoolean("value"))q.put("done",new JSONObject().put("value",op.getBoolean("value")).put("stamp",op.getString("id")));
   reconcile(s,"",op.getString("id"));
   if(op.getBoolean("value")){JSONArray rewarded=s.getJSONArray("rewarded");long balance=s.optLong("balance");for(String key:unfinished){JSONObject q=quests.getJSONObject(key);if(q.getJSONObject("done").optBoolean("value")&&!claimed(rewarded,key)){balance+=reward(s,q);rewarded.put(key);}}s.put("balance",balance);}
  }
  else if(kind.equals("delete")){if(entity.equals("quest")){for(JSONObject q:subtree(s,target)){String key=q.getString("id");quests.remove(key);if(key.equals(s.optString("pinId")))s.put("pinId","");}reconcile(s,"",op.getString("id"));}else items.remove(target);}
  else if(kind.equals("buy")){JSONObject item=items.optJSONObject(target);JSONObject purchases=s.getJSONObject("purchases");if(item!=null&&!purchases.has(op.getString("id"))){long price=item.optLong("price");if(s.optLong("balance")<price)throw new IOException("Недостаточно бусин");purchases.put(op.getString("id"),new JSONObject().put("id",op.getString("id")).put("itemId",target).put("name",item.optString("name")).put("price",price).put("boughtAt",java.time.Instant.now().toString()));s.put("balance",s.optLong("balance")-price);}}
  else if(kind.equals("pin"))s.put("pinId",v);
  else throw new IOException("Unknown operation");
 }
 static Object baseline(JSONObject s,JSONObject op)throws Exception {
  String kind=op.getString("kind"),entity=op.optString("entity"),target=op.optString("target");JSONObject record=s.getJSONObject(entity.equals("item")?"items":"quests").optJSONObject(target);
  if(kind.equals("pin"))return s.optString("pinId");
  if(kind.equals("buy"))return record==null?JSONObject.NULL:record.opt("price");
  if(kind.equals("complete"))return record==null?JSONObject.NULL:record.getJSONObject("done");
  if(kind.equals("set")){if(entity.equals("settings"))record=s.getJSONObject("settings");return record==null?JSONObject.NULL:record.opt(op.getString("field"));}
  if(kind.equals("delete")&&entity.equals("quest")){JSONObject tree=new JSONObject();for(JSONObject q:subtree(s,target)){JSONObject entry=copy(q);entry.remove("order");tree.put(q.getString("id"),entry);};return tree;}
  if(kind.equals("delete"))return record==null?JSONObject.NULL:copy(record);
  return JSONObject.NULL;
 }
 static String command(Context c,String raw)throws Exception {synchronized(lock){JSONObject next=copy(load(c));if(next.has("conflict"))throw new IOException("Сначала разреши конфликт синхронизации.");JSONObject op=new JSONObject(raw);op.put("id",id());JSONObject state=next.getJSONObject("state");if(!op.has("base"))op.put("base",baseline(state,op));apply(state,op);next.getJSONArray("queue").put(op);validate(next);save(c,next);return view(c);}}
 static void preferences(Context c,String raw)throws Exception {synchronized(lock){JSONObject next=copy(load(c));JSONObject p=new JSONObject(raw),old=next.getJSONObject("preferences");Iterator<String> k=p.keys();while(k.hasNext()){String key=k.next();old.put(key,p.get(key));}save(c,next);}}
 static void sync(Context c,PhoneClient.ConnectionHook hook)throws Exception {synchronized(networkLock){
  if(!PhoneClient.paired(c))return;final JSONObject request;final JSONArray sent;final String epoch,token;
  synchronized(lock){JSONObject d=load(c);if(d.has("conflict"))return;epoch=d.optString("epoch");token=PhoneClient.prefs(c).getString("token","");JSONArray queue=d.getJSONArray("queue");sent=new JSONArray();int bytes=0;for(int i=0;i<Math.min(128,queue.length());i++){JSONObject op=copy(queue.getJSONObject(i));int size=op.toString().getBytes(StandardCharsets.UTF_8).length;if(sent.length()>0&&bytes+size>14*1024*1024)break;if(size>14*1024*1024)throw new IOException("Изображение слишком большое для обмена");sent.put(op);bytes+=size;}request=new JSONObject().put("journalId",d.getJSONObject("state").optString("journalId")).put("operations",sent).put("after",d.optString("serverRevision"));}
  PhoneClient.selectLocalNetwork(c);PhoneClient.Response response=PhoneClient.request(c,"/api/sync",request,hook);
  synchronized(lock){if(!epoch.equals(load(c).optString("epoch"))||!token.equals(PhoneClient.prefs(c).getString("token","")))return;}
  if(response.code==401){PhoneClient.prefs(c).edit().remove("token").putBoolean("enabled",false).putBoolean("online",false).apply();throw new IOException("Доступ отозван. Подключи компьютер заново через QR.");}if(response.code==404)throw new IOException("Для полной синхронизации обнови приложение на ПК до мобильной тестовой версии.");if(response.code==503)throw new IOException("ПК не сохранил изменения. Очередь остаётся на телефоне.");
  if(response.code!=200&&response.code!=409)throw new IOException("ПК отклонил изменения: "+response.json.optString("detail",response.json.optString("error")));
  if(response.code==200&&response.json.optBoolean("unchanged")){PhoneClient.prefs(c).edit().putBoolean("online",true).putString("status","Синхронизировано по Wi-Fi").putLong("lastSync",System.currentTimeMillis()).apply();return;}
  synchronized(lock){JSONObject next=copy(load(c));if(response.code==409){next.put("conflict",copy(response.json));save(c,next);return;}
   if(response.json.optInt("protocol")!=2)throw new IOException("Неподдерживаемый протокол");JSONArray ack=response.json.getJSONArray("ack"),old=next.getJSONArray("queue"),remaining=new JSONArray();HashSet<String> ids=new HashSet<>();for(int i=0;i<ack.length();i++)ids.add(ack.getString(i));
   JSONObject state=copy(response.json.getJSONObject("state"));for(int i=0;i<old.length();i++){JSONObject op=old.getJSONObject(i);if(!ids.contains(op.getString("id"))){remaining.put(op);replay(state,op);}}
   next.put("state",state).put("queue",remaining).put("serverRevision",response.json.optString("revision"));next.remove("conflict");save(c,next);
  }
  PhoneClient.prefs(c).edit().putBoolean("online",true).putString("status","Синхронизировано по Wi-Fi").putLong("lastSync",System.currentTimeMillis()).apply();
 }}
 static void resolve(Context c,boolean phone)throws Exception {synchronized(lock){JSONObject next=copy(load(c)),conflict=next.getJSONObject("conflict");if(conflict.optString("error").equals("differentJournal"))throw new IOException("Это другой журнал. Экспортируй данные и отключи старый компьютер в настройках.");JSONObject detail=conflict.getJSONObject("conflict");if(phone&&(detail.optString("code").equals("funds")||detail.optString("code").equals("parentMissing")||detail.optString("code").equals("missing")||detail.optString("code").equals("exists")))throw new IOException("Это действие нельзя повторить. Выбери версию ПК; остальные изменения сохранятся в очереди.");
  // Keep a local recovery copy before changing the conflict decision.
  try(FileOutputStream backup=new FileOutputStream(new File(c.getFilesDir(),"before-conflict.json"))){backup.write(next.toString().getBytes(StandardCharsets.UTF_8));backup.getFD().sync();}
  JSONArray queue=next.getJSONArray("queue"),kept=new JSONArray();JSONObject disputed=null;for(int i=0;i<queue.length();i++)if(queue.getJSONObject(i).getString("id").equals(detail.optString("opId")))disputed=queue.getJSONObject(i);for(int i=0;i<queue.length();i++){JSONObject op=queue.getJSONObject(i);boolean sameField=!phone&&disputed!=null&&"set".equals(disputed.optString("kind"))&&"set".equals(op.optString("kind"))&&disputed.optString("entity").equals(op.optString("entity"))&&disputed.optString("target").equals(op.optString("target"))&&disputed.optString("field").equals(op.optString("field"));if(sameField)continue;if(op.getString("id").equals(detail.optString("opId"))){if(!phone)continue;op.put("base",detail.opt("pc"));}kept.put(op);}
  JSONObject state=copy(conflict.getJSONObject("state"));for(int i=0;i<kept.length();i++)replay(state,kept.getJSONObject(i));next.put("state",state).put("queue",kept);next.remove("conflict");save(c,next);
 }}
 static String export(Context c)throws Exception{synchronized(lock){return copy(load(c)).toString(2);}}
 static void disconnect(Context c)throws Exception{synchronized(lock){if(load(c).getJSONArray("queue").length()>0)throw new IOException("Сначала синхронизируй очередь или экспортируй и сбрось локальный журнал.");JSONObject next=copy(load(c));save(c,next);PhoneClient.prefs(c).edit().remove("token").remove("fingerprint").remove("endpoint").putBoolean("enabled",false).apply();}}
 static void replay(JSONObject s,JSONObject op)throws Exception{try{apply(s,op);}catch(IOException e){if(!"buy".equals(op.optString("kind")))throw e;}}
 static JSONObject notificationState(Context c)throws Exception{synchronized(lock){JSONObject s=load(c).getJSONObject("state"),q=s.getJSONObject("quests").optJSONObject(s.optString("pinId"));JSONObject state=new JSONObject().put("balance",s.optLong("balance")).put("generation","mobile").put("questVersion",q==null?"":q.getJSONObject("done").optString("stamp"));if(q==null)return state;int total=0,completed=0;for(JSONObject node:subtree(s,q.getString("id")))if(children(s,node.getString("id")).length()==0){total++;if(node.getJSONObject("done").optBoolean("value"))completed++;}JSONObject predicted=copy(s);apply(predicted,new JSONObject().put("id",id()).put("kind","complete").put("target",q.getString("id")).put("value",true));return state.put("quest",new JSONObject().put("id",q.getString("id")).put("title",q.optString("title")).put("notes",q.optString("notes")).put("done",q.getJSONObject("done").optBoolean("value")).put("completed",completed).put("total",total).put("reward",Math.max(0,predicted.optLong("balance")-s.optLong("balance"))));}}
 static void validate(JSONObject d)throws Exception {
  JSONObject s=d.getJSONObject("state"),quests=s.getJSONObject("quests");s.getJSONObject("items");s.getJSONObject("settings");s.getJSONObject("purchases");s.getJSONArray("rewarded");d.getJSONObject("preferences");JSONArray queue=d.getJSONArray("queue");if(quests.length()>5000||s.getJSONObject("items").length()>1000||queue.length()>10000)throw new IOException("Слишком большой журнал");
  Iterator<String> keys=quests.keys();while(keys.hasNext()){String key=keys.next();JSONObject q=quests.getJSONObject(key);if(!key.equals(q.getString("id"))||q.getString("title").length()>500)throw new IOException("Повреждённый квест");q.getJSONObject("done").getBoolean("value");String parent=q.optString("parentId");HashSet<String> seen=new HashSet<>();seen.add(key);while(!parent.isEmpty()){if(!seen.add(parent)||seen.size()>25||!quests.has(parent))throw new IOException("Повреждённое дерево задач");parent=quests.getJSONObject(parent).optString("parentId");}}
  for(int i=0;i<queue.length();i++){JSONObject op=queue.getJSONObject(i);if(!op.getString("id").matches("[A-Za-z0-9-]{16,64}"))throw new IOException("Повреждённая очередь");op.getString("kind");}
 }
 static void restore(Context c,InputStream in)throws Exception{synchronized(lock){ByteArrayOutputStream out=new ByteArrayOutputStream();byte[] b=new byte[8192];int n;while((n=in.read(b))!=-1){if(out.size()+n>48*1024*1024)throw new IOException("Слишком большой файл");out.write(b,0,n);}JSONObject next=new JSONObject(out.toString("UTF-8"));validate(next);next.put("epoch",id());try(FileOutputStream backup=new FileOutputStream(new File(c.getFilesDir(),"before-restore.json"))){backup.write(export(c).getBytes(StandardCharsets.UTF_8));backup.getFD().sync();}save(c,next);}}
 static void reset(Context c)throws Exception{synchronized(lock){try(FileOutputStream backup=new FileOutputStream(new File(c.getFilesDir(),"before-reset.json"))){backup.write(export(c).getBytes(StandardCharsets.UTF_8));backup.getFD().sync();}JSONObject next=new JSONObject().put("epoch",id()).put("state",empty()).put("queue",new JSONArray()).put("preferences",copy(load(c).getJSONObject("preferences")));next.getJSONObject("preferences").remove("timer");save(c,next);PhoneClient.prefs(c).edit().remove("token").remove("fingerprint").remove("endpoint").remove("cache").remove("pending").putBoolean("enabled",false).apply();}}
 private MobileStore(){}
}
