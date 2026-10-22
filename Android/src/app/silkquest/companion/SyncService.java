package app.silkquest.companion;

import android.app.*;
import android.content.*;
import android.content.pm.ServiceInfo;
import android.graphics.Color;
import android.net.Uri;
import android.os.*;
import org.json.JSONObject;
import javax.net.ssl.HttpsURLConnection;

public final class SyncService extends Service {
 static final String CHANNEL="pinned-quest",UPDATED="app.silkquest.companion.UPDATED";static final int ID=101;
 volatile boolean stopped;volatile Thread worker;volatile HttpsURLConnection current;volatile boolean online;
 static final class StorageFailure extends java.io.IOException{}
 @Override public IBinder onBind(Intent i){return null;}
 @Override public void onCreate(){super.onCreate();NotificationChannel channel=new NotificationChannel(CHANNEL,"Закреплённый квест",NotificationManager.IMPORTANCE_LOW);channel.setDescription("Текущий квест и связь с домашним компьютером");channel.setShowBadge(false);getSystemService(NotificationManager.class).createNotificationChannel(channel);}
 @Override public int onStartCommand(Intent intent,int flags,int startId){
  if(!PhoneClient.paired(this)||!PhoneClient.prefs(this).getBoolean("enabled",true)){stopSelf();return START_NOT_STICKY;}
  stopped=false;Notification n=notification();if(Build.VERSION.SDK_INT>=29)startForeground(ID,n,ServiceInfo.FOREGROUND_SERVICE_TYPE_CONNECTED_DEVICE);else startForeground(ID,n);
  if(worker==null||!worker.isAlive()){worker=new Thread(this::loop,"SilkQuest-WiFi");worker.start();}else if(intent!=null){HttpsURLConnection c=current;if(c!=null)c.disconnect();worker.interrupt();}
  return START_STICKY;
 }
 void loop(){
  boolean first=true;int retry=0;
  while(!stopped&&PhoneClient.prefs(this).getBoolean("enabled",false)&&PhoneClient.paired(this)){
   try{
    PhoneClient.selectLocalNetwork(this);
    MobileStore.sync(this,c->current=c);
    sendBroadcast(new Intent(UPDATED).setPackage(getPackageName()));
    String pending=PhoneClient.prefs(this).getString("pending",null);
    if(pending!=null){
     PhoneClient.Response completion=PhoneClient.request(this,"/api/complete",new JSONObject(pending),c->current=c);
     if(completion.code==401){revoked();break;}
     if(completion.code==503)throw new StorageFailure();
     if(completion.code!=200&&completion.code!=409)throw new java.io.IOException("Ошибка отправки");
     if(completion.json.optInt("protocol")!=1)throw new java.io.IOException("Неподдерживаемый протокол ответа");
     boolean clear=pending.equals(PhoneClient.prefs(this).getString("pending",null));android.content.SharedPreferences.Editor saved=PhoneClient.prefs(this).edit().putString("cache",completion.json.toString()).putString("status",completion.code==409?"Квест изменён на ПК. Старое действие отменено.":"Синхронизировано по Wi-Fi").putBoolean("online",true).putLong("lastSync",System.currentTimeMillis());if(clear)saved.remove("pending");if(!saved.commit()){if(clear)PhoneClient.prefs(this).edit().putString("pending",pending).commit();throw new java.io.IOException("Не удалось сохранить ответ на телефоне");}update();
    }
    long after=first?0:PhoneClient.cache(this).optLong("revision",0);first=false;
    PhoneClient.Response response=PhoneClient.request(this,"/api/state?after="+after,null,c->current=c);
    if(response.code==401){revoked();break;}
    if(response.code!=200||response.json.optInt("protocol")!=1)throw new java.io.IOException("Неизвестный ответ компьютера");
    accept(response.json,"На связи · домашний Wi-Fi");retry=0;
   }catch(Exception e){
    if(stopped)break;if(!PhoneClient.paired(this)){revoked();break;}online=false;
    String status=e instanceof StorageFailure?"ПК не сохранил действие · повторим отправку":e instanceof javax.net.ssl.SSLException?"Сертификат ПК изменился. Подключись заново через QR.":PhoneClient.prefs(this).contains("pending")?"Нет связи · завершение ожидает Wi-Fi":e.getMessage()!=null?e.getMessage():"Нет связи · журнал доступен офлайн";
    PhoneClient.prefs(this).edit().putString("status",status).putBoolean("online",false).apply();update();
    try{Thread.sleep(Math.min(30000,3000L*(++retry)));}catch(InterruptedException ignored){}
   }
  }
 }
 void accept(JSONObject state,String status)throws Exception{
  if(stopped)return;if(state.optInt("protocol")!=1)throw new java.io.IOException("Неподдерживаемый протокол");online=true;PhoneClient.prefs(this).edit().putString("cache",state.toString()).putString("status",status).putBoolean("online",true).putLong("lastSync",System.currentTimeMillis()).apply();update();
 }
 void revoked(){PhoneClient.prefs(this).edit().remove("token").remove("pending").remove("cache").putBoolean("enabled",false).putString("status","Доступ отозван на компьютере. Подключись заново.").apply();stopped=true;getSystemService(NotificationManager.class).cancel(ID);sendBroadcast(new Intent(UPDATED).setPackage(getPackageName()));stopSelf();}
 void update(){if(stopped)return;try{getSystemService(NotificationManager.class).notify(ID,notification());}catch(SecurityException ignored){}sendBroadcast(new Intent(UPDATED).setPackage(getPackageName()));}
 Notification notification(){return buildNotification(this);}
 static void postLocal(Context c){NotificationChannel channel=new NotificationChannel(CHANNEL,"Закреплённый квест",NotificationManager.IMPORTANCE_LOW);channel.setShowBadge(false);NotificationManager manager=c.getSystemService(NotificationManager.class);manager.createNotificationChannel(channel);try{manager.notify(ID,buildNotification(c));}catch(SecurityException ignored){}}
 static Notification buildNotification(Context context){
  JSONObject state=PhoneClient.cache(context);try{if(MobileStore.hasJournal(context))state=MobileStore.notificationState(context);}catch(Exception ignored){}JSONObject q=state.optJSONObject("quest");boolean pending=PhoneClient.prefs(context).contains("pending");
  String title=q==null?"Silk Quest · нить с компьютером":q.optString("title","Текущий квест");
  String detail=q==null?"Выбери квест на ПК через ··· → Закрепить на телефоне":pending?"Завершение ожидает Wi-Fi":q.optBoolean("done")?"✧ Квест завершён · "+Beads.amount(state.optLong("balance")):q.optInt("completed")+" из "+q.optInt("total")+" · +"+Beads.amount(q.optInt("reward"));
  Intent open=new Intent(context,MainActivity.class).setFlags(Intent.FLAG_ACTIVITY_NEW_TASK|Intent.FLAG_ACTIVITY_CLEAR_TOP);PendingIntent content=PendingIntent.getActivity(context,0,open,PendingIntent.FLAG_UPDATE_CURRENT|PendingIntent.FLAG_IMMUTABLE);
  Notification.Builder b=new Notification.Builder(context,CHANNEL).setSmallIcon(R.drawable.ic_quest).setColor(Color.rgb(194,91,88)).setContentTitle(title).setContentText(detail).setSubText(PhoneClient.prefs(context).getString("status","Подключаемся по Wi-Fi")).setContentIntent(content).setOnlyAlertOnce(true).setOngoing(true).setShowWhen(false).setCategory(Notification.CATEGORY_STATUS).setVisibility(Notification.VISIBILITY_PRIVATE);
  String notes=q==null?"":q.optString("notes");b.setStyle(new Notification.BigTextStyle().bigText(detail+(notes.isEmpty()?"":"\n"+notes)));
  if(q!=null&&!q.optBoolean("done")&&!pending){
   String generation=state.optString("generation"),version=state.optString("questVersion"),id=q.optString("id");
   Intent complete=new Intent(context,QuestActionReceiver.class).setAction("complete").setData(Uri.parse("silkquest://complete/"+id+"/"+generation+"/"+version)).putExtra("id",id).putExtra("generation",generation).putExtra("questVersion",version);
   b.addAction(new Notification.Action.Builder(null,"✓ Завершить",PendingIntent.getBroadcast(context,1,complete,PendingIntent.FLAG_UPDATE_CURRENT|PendingIntent.FLAG_IMMUTABLE)).build());
  }
  Intent refresh=new Intent(context,QuestActionReceiver.class).setAction("refresh");b.addAction(new Notification.Action.Builder(null,"Обновить",PendingIntent.getBroadcast(context,2,refresh,PendingIntent.FLAG_UPDATE_CURRENT|PendingIntent.FLAG_IMMUTABLE)).build());
  Intent hide=new Intent(context,QuestActionReceiver.class).setAction("hide");PendingIntent dismiss=PendingIntent.getBroadcast(context,3,hide,PendingIntent.FLAG_UPDATE_CURRENT|PendingIntent.FLAG_IMMUTABLE);b.setDeleteIntent(dismiss);b.addAction(new Notification.Action.Builder(null,"Убрать",dismiss).build());return b.build();
 }
 static void hide(Context c){PhoneClient.prefs(c).edit().putBoolean("enabled",false).putBoolean("online",false).putString("status","Связь приостановлена. Последний квест сохранён.").apply();c.stopService(new Intent(c,SyncService.class));c.getSystemService(NotificationManager.class).cancel(ID);}
 @Override public void onDestroy(){stopped=true;HttpsURLConnection c=current;if(c!=null)c.disconnect();if(worker!=null)worker.interrupt();if(PhoneClient.prefs(this).getBoolean("enabled",false))stopForeground(STOP_FOREGROUND_DETACH);else stopForeground(STOP_FOREGROUND_REMOVE);super.onDestroy();}
}
