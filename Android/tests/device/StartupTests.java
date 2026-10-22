package app.silkquest.tests;

import android.app.*;
import android.content.Intent;
import android.os.Bundle;
import android.webkit.WebView;
import android.graphics.Bitmap;
import java.io.*;
import java.lang.reflect.Field;
import java.util.concurrent.*;

/** Runs inside the installed APK on Android, including its real WebView and bridge. */
public final class StartupTests extends Instrumentation {
 boolean requireSaved;
 public void onCreate(Bundle args){super.onCreate(args);requireSaved=args!=null&&"true".equals(args.getString("requireSaved"));start();}
 public void onStart(){
  Bundle result=new Bundle();
  try{
   Intent intent=new Intent().setClassName("app.silkquest.companion","app.silkquest.companion.MainActivity").addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
   Activity activity=startActivitySync(intent);
   Field wf=activity.getClass().getDeclaredField("web");wf.setAccessible(true);
   Field rf=activity.getClass().getDeclaredField("ready");rf.setAccessible(true);
   String[] snapshot={""};boolean success=false;
   long deadline=android.os.SystemClock.elapsedRealtime()+30000;
   for(int attempt=0;attempt<20&&android.os.SystemClock.elapsedRealtime()<deadline;attempt++){
    CountDownLatch done=new CountDownLatch(1);boolean[] ready={false};
    runOnMainSync(()->{try{
     ready[0]=rf.getBoolean(activity);
     ((WebView)wf.get(activity)).evaluateJavascript("JSON.stringify({url:location.href,document:document.readyState,body:document.body&&document.body.innerText,bridge:typeof SilkNative,accept:typeof accept,nav:document.querySelectorAll('#nav button').length})",value->{snapshot[0]=value;done.countDown();});
    }catch(Exception error){snapshot[0]=error.toString();done.countDown();}});
    // A cold WebView renderer can start after the first evaluation is queued.
    if(!done.await(2,TimeUnit.SECONDS))continue;
    if(ready[0]&&snapshot[0].contains("\\\"nav\\\":5")){success=true;break;}
    Thread.sleep(500);
   }
   result.putString("snapshot",snapshot[0]);
   if(!success)throw new Exception("Startup did not render the five main tabs: "+snapshot[0]);
   CountDownLatch interactions=new CountDownLatch(1);String[] checked={""};
   String script="(()=>{try{"
    +"const title='Android startup regression';let existing=Object.values(S.quests).find(q=>q.title===title);"
    +(requireSaved?"if(!existing)return 'FAIL: quest was not saved across process restart';":"")
    +"document.querySelectorAll('#nav button')[4].click();if(tab!=='settings')return 'FAIL: Settings tab';"
    +"pref({language:'en'});accept(SilkNative.state());if(document.querySelector('h1').textContent!=='Settings')return 'FAIL: English settings';"
    +"document.querySelectorAll('#nav button')[3].click();if(document.querySelector('h1').textContent!=='Shop')return 'FAIL: Shop tab';"
    +"pref({language:'ru'});accept(SilkNative.state());setTab('today');"
    +"if(!existing){editQuest(null);document.querySelector('#sheet input[type=text]').value=title;document.querySelector('#sheet .primary').click();closeSheet(true);}"
    +"if(!Object.values(JSON.parse(SilkNative.state()).state.quests).some(q=>q.title===title))return 'FAIL: offline quest write';"
    +"pref({timer:null});updateTimer();"
    +"for(let i=0;i<5;i++){const id='scroll-check-'+i;if(!Q(id)&&!action({kind:'create',entity:'quest',target:id,value:{id,title:'Scroll boundary '+i,notes:'',today:false,kind:2,customReward:null,parentId:'',order:i+1,done:{value:false,stamp:''}}}))return 'FAIL: scroll fixture';}"
    +"for(const name of ['today','quests','journey','shop','settings']){setTab(name);const padding=parseFloat(getComputedStyle(document.querySelector('main')).paddingBottom);if(Math.abs(padding-90)>1)return 'FAIL: excessive bottom padding on '+name+': '+padding;}"
    +"setTab('journey');window.scrollTo(0,document.documentElement.scrollHeight);const gap=document.querySelector('#nav').getBoundingClientRect().top-document.querySelector('main').lastElementChild.getBoundingClientRect().bottom;if(gap<8||gap>22)return 'FAIL: scroll boundary gap '+gap;"
    +"const quest=Object.values(S.quests).find(q=>q.title===title);startFocus(quest.id);if(!document.body.classList.contains('has-focus'))return 'FAIL: focus dock clearance';pref({timer:null});updateTimer();if(document.body.classList.contains('has-focus'))return 'FAIL: stale focus clearance';"
    +"setTab('today');return 'PASS: Settings, English, Shop, offline quest persistence, five scroll paddings, scroll boundary and focus clearance';"
    +"}catch(error){return 'FAIL: '+error.message;}})()";
   runOnMainSync(()->{try{((WebView)wf.get(activity)).evaluateJavascript(script,value->{checked[0]=value;interactions.countDown();});}catch(Exception e){checked[0]=e.toString();interactions.countDown();}});
   if(!interactions.await(10,TimeUnit.SECONDS)||!checked[0].contains("PASS:"))throw new Exception("Interactions: "+checked[0]);
   CountDownLatch imageDone=new CountDownLatch(1);String[] imageResult={""};
   Bitmap pixel=Bitmap.createBitmap(2,2,Bitmap.Config.ARGB_8888);pixel.eraseColor(android.graphics.Color.BLUE);
   ByteArrayOutputStream imageBytes=new ByteArrayOutputStream();pixel.compress(Bitmap.CompressFormat.PNG,100,imageBytes);pixel.recycle();
   String picture="data:image/png;base64,"+android.util.Base64.encodeToString(imageBytes.toByteArray(),android.util.Base64.NO_WRAP);
   runOnMainSync(()->{try{WebView view=(WebView)wf.get(activity);view.evaluateJavascript("window.testPicture=new Image();testPicture.src="+org.json.JSONObject.quote(picture),ignored->view.postDelayed(()->view.evaluateJavascript("testPicture.complete && testPicture.naturalWidth===2",value->{imageResult[0]=value;imageDone.countDown();}),500));}catch(Exception error){imageResult[0]=error.toString();imageDone.countDown();}});
   if(!imageDone.await(5,TimeUnit.SECONDS)||!"true".equals(imageResult[0]))throw new Exception("Local shop image did not load: "+imageResult[0]);
   result.putString("stream","PASS: real Android WebView startup, native bridge, five tabs; "+checked[0]+(requireSaved?"; survived cold restart":"")+"\n");
   finish(Activity.RESULT_OK,result);
  }catch(Exception error){result.putString("stream","FAIL: "+error+"\n");finish(Activity.RESULT_CANCELED,result);}
 }
}
