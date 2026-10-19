package app.silkquest.companion;

import android.content.*;
import org.json.JSONObject;

public final class QuestActionReceiver extends BroadcastReceiver {
 @Override public void onReceive(Context c,Intent i){
  if("hide".equals(i.getAction())){SyncService.hide(c);return;}
  if(!PhoneClient.paired(c))return;
  if("complete".equals(i.getAction()))try{
   JSONObject command=new JSONObject().put("id",i.getStringExtra("id")).put("generation",i.getStringExtra("generation")).put("questVersion",i.getStringExtra("questVersion"));if(!PhoneClient.prefs(c).edit().putString("pending",command.toString()).commit())return;
  }catch(Exception e){return;}
  PhoneClient.prefs(c).edit().putBoolean("enabled",true).apply();c.startForegroundService(new Intent(c,SyncService.class).setAction("refresh"));
 }
}
