package app.silkquest.companion;

import android.content.*;
import org.json.JSONObject;

public final class QuestActionReceiver extends BroadcastReceiver {
 @Override public void onReceive(Context c,Intent i){
  if("hide".equals(i.getAction())){SyncService.hide(c);return;}
  if(!PhoneClient.paired(c)&&!MobileStore.hasJournal(c))return;
  if("complete".equals(i.getAction()))try{
   if(MobileStore.hasJournal(c)){MobileStore.command(c,new JSONObject().put("kind","complete").put("entity","quest").put("target",i.getStringExtra("id")).put("value",true).toString());}
   else {JSONObject command=new JSONObject().put("id",i.getStringExtra("id")).put("generation",i.getStringExtra("generation")).put("questVersion",i.getStringExtra("questVersion"));if(!PhoneClient.prefs(c).edit().putString("pending",command.toString()).commit())return;}
  }catch(Exception e){return;}
  PhoneClient.prefs(c).edit().putBoolean("enabled",true).apply();if(PhoneClient.paired(c))c.startForegroundService(new Intent(c,SyncService.class).setAction("refresh"));else SyncService.postLocal(c);
 }
}
