package app.silkquest.companion;

import android.Manifest;
import android.app.*;
import android.content.*;
import android.content.pm.PackageManager;
import android.graphics.*;
import android.net.Uri;
import android.os.*;
import android.webkit.*;
import android.widget.*;
import android.view.View;
import android.view.Gravity;
import org.json.*;
import java.io.*;
import java.util.concurrent.*;

public final class MainActivity extends Activity {
 static final String APP_BASE="https://appassets.androidplatform.net/assets/mobile/";
 FrameLayout root;LinearLayout startup;TextView startupText;Button retry;String startupDetail="";
 final Runnable bootTimeout=()->{if(!this.ready)showStartupError("The interface did not finish starting.");};
 WebView web;boolean visible,ready,pairing;String incoming="";String exportData;String lastView="";
 final Handler handler=new Handler(Looper.getMainLooper());
 final java.util.concurrent.atomic.AtomicBoolean syncQueued=new java.util.concurrent.atomic.AtomicBoolean();
 final ExecutorService io=Executors.newSingleThreadExecutor();
 final Runnable pulse=new Runnable(){public void run(){if(visible){sync();handler.postDelayed(this,10000);}}};
 final BroadcastReceiver updates=new BroadcastReceiver(){public void onReceive(Context c,Intent i){deliver();}};
 @Override public void onCreate(Bundle saved){super.onCreate(saved);getWindow().setStatusBarColor(Color.rgb(15,24,31));getWindow().setNavigationBarColor(Color.rgb(15,24,31));
  root=new FrameLayout(this);root.setBackgroundColor(Color.rgb(15,24,31));
  web=new WebView(this);web.setBackgroundColor(Color.rgb(15,24,31));web.setOverScrollMode(android.view.View.OVER_SCROLL_NEVER);
  WebSettings settings=web.getSettings();settings.setJavaScriptEnabled(true);settings.setAllowFileAccess(false);settings.setAllowContentAccess(false);settings.setDomStorageEnabled(false);settings.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);settings.setJavaScriptCanOpenWindowsAutomatically(false);settings.setSupportMultipleWindows(false);
  root.setOnApplyWindowInsetsListener((v,insets)->{v.setPadding(insets.getSystemWindowInsetLeft(),insets.getSystemWindowInsetTop(),insets.getSystemWindowInsetRight(),insets.getSystemWindowInsetBottom());return insets;});
  web.addJavascriptInterface(new Bridge(),"SilkNative");
  web.setWebChromeClient(new WebChromeClient(){
   @Override public boolean onConsoleMessage(ConsoleMessage message){if(message.messageLevel()==ConsoleMessage.MessageLevel.ERROR&&!ready)showStartupError("Interface script error (line "+message.lineNumber()+"): "+message.message());return true;}
  });
  web.setWebViewClient(new WebViewClient(){
   @Override public boolean shouldOverrideUrlLoading(WebView v,WebResourceRequest r){return !r.getUrl().toString().equals(APP_BASE+"index.html");}
   @Override public WebResourceResponse shouldInterceptRequest(WebView v,WebResourceRequest r){
    Uri uri=r.getUrl();
    if(!"https".equals(uri.getScheme())||!"appassets.androidplatform.net".equals(uri.getHost())||uri.getPort()!=-1)return blocked();
    String prefix="/assets/mobile/",path=uri.getPath();
    if(path==null||!path.startsWith(prefix))return blocked();
    String file=path.substring(prefix.length());
    if(!file.equals("index.html")&&!file.equals("app.js")&&!file.equals("style.css"))return blocked();
    try{return new WebResourceResponse(file.endsWith(".js")?"text/javascript":file.endsWith(".css")?"text/css":"text/html","UTF-8",getAssets().open("mobile/"+file));}
    catch(IOException error){handler.post(()->showStartupError("Missing interface resource: "+file));return blocked();}
   }
   @Override public void onReceivedError(WebView v,WebResourceRequest r,WebResourceError error){if(r.isForMainFrame())showStartupError("WebView loading error "+error.getErrorCode());}
   @Override public boolean onRenderProcessGone(WebView v,RenderProcessGoneDetail detail){ready=false;showStartupError("Android WebView stopped. Close and reopen Silk Quest.");return true;}
  });
  root.addView(web,new FrameLayout.LayoutParams(-1,-1));
  startup=new LinearLayout(this);startup.setOrientation(LinearLayout.VERTICAL);startup.setGravity(Gravity.CENTER);startup.setPadding(36,36,36,36);startup.setBackgroundColor(Color.rgb(15,24,31));
  startupText=new TextView(this);startupText.setTextColor(Color.rgb(232,221,203));startupText.setTextSize(18);startupText.setGravity(Gravity.CENTER);startup.addView(startupText);
  retry=new Button(this);retry.setText("Повторить / Retry");retry.setOnClickListener(v->loadInterface());startup.addView(retry);
  root.addView(startup,new FrameLayout.LayoutParams(-1,-1));setContentView(root);web.post(()->loadInterface());handleLink(getIntent());if(PhoneClient.prefs(this).getBoolean("enabled",false)&&canNotify()){if(PhoneClient.paired(this))startSync();else SyncService.postLocal(this);}
 }
 void loadInterface(){
  ready=false;lastView="";startupDetail="";startup.setVisibility(View.VISIBLE);startupText.setText("Silk Quest · Загрузка / Loading…");retry.setVisibility(View.GONE);handler.removeCallbacks(bootTimeout);
  try{web.loadUrl(APP_BASE+"index.html");handler.postDelayed(bootTimeout,8000);}
  catch(Exception error){showStartupError("Android WebView: "+error.getClass().getSimpleName()+": "+error.getMessage());}
 }
 void showStartupError(String detail){if(ready||isDestroyed())return;handler.removeCallbacks(bootTimeout);startupDetail=detail;startupText.setText("Не удалось открыть интерфейс. / Could not start the interface.\n\n"+detail+"\n\nSilk Quest 2.0.0");retry.setVisibility(View.VISIBLE);startup.setVisibility(View.VISIBLE);}
 static WebResourceResponse blocked(){return new WebResourceResponse("text/plain","UTF-8",new ByteArrayInputStream(new byte[0]));}
 @Override protected void onNewIntent(Intent i){super.onNewIntent(i);setIntent(i);handleLink(i);}
 void handleLink(Intent i){if(i.getData()!=null&&"silkquest".equals(i.getData().getScheme())){incoming=i.getData().toString();if(ready)emit("silkPairLink",incoming);}}
 @Override protected void onStart(){super.onStart();visible=true;IntentFilter f=new IntentFilter(SyncService.UPDATED);if(Build.VERSION.SDK_INT>=33)registerReceiver(updates,f,Context.RECEIVER_NOT_EXPORTED);else registerReceiver(updates,f);handler.post(pulse);}
 @Override protected void onStop(){visible=false;handler.removeCallbacks(pulse);unregisterReceiver(updates);super.onStop();}
 @Override protected void onDestroy(){handler.removeCallbacksAndMessages(null);io.shutdownNow();web.removeJavascriptInterface("SilkNative");web.destroy();super.onDestroy();}
 @Override public void onBackPressed(){web.evaluateJavascript("window.silkBack && window.silkBack()",null);}
 void emit(String name,String value){runOnUiThread(()->{if(ready&&!isFinishing()&&!isDestroyed())web.evaluateJavascript("window."+name+" && window."+name+"("+JSONObject.quote(value)+")",null);});}
 void deliver(){if(!ready)return;try{String current=MobileStore.view(this);if(!current.equals(lastView)){lastView=current;emit("silkState",current);}}catch(Exception e){emit("silkError",e.getMessage()==null?"Не удалось прочитать журнал. Файл сохранён в памяти приложения.":e.getMessage());}}
 void sync(){if(!PhoneClient.paired(this)||io.isShutdown()||!syncQueued.compareAndSet(false,true))return;io.execute(()->{try{MobileStore.sync(this,null);}catch(Exception e){PhoneClient.prefs(this).edit().putBoolean("online",false).putString("status",e.getMessage()==null?"Нет связи · изменения сохранены на телефоне":e.getMessage()).apply();}finally{syncQueued.set(false);deliver();}});}
 void wake(){handler.post(()->{sync();if(PhoneClient.prefs(this).getBoolean("enabled",false)&&canNotify()){if(PhoneClient.paired(this))startSync();else SyncService.postLocal(this);}});}
 boolean canNotify(){return (Build.VERSION.SDK_INT<33||checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS)==PackageManager.PERMISSION_GRANTED)&&getSystemService(NotificationManager.class).areNotificationsEnabled();}
 void startSync(){PhoneClient.prefs(this).edit().putBoolean("enabled",true).apply();startForegroundService(new Intent(this,SyncService.class).setAction("refresh"));}
 void notifications(){if(Build.VERSION.SDK_INT>=33&&checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS)!=PackageManager.PERMISSION_GRANTED){requestPermissions(new String[]{Manifest.permission.POST_NOTIFICATIONS},1);return;}if(!canNotify()){startActivity(new Intent(android.provider.Settings.ACTION_APP_NOTIFICATION_SETTINGS).putExtra(android.provider.Settings.EXTRA_APP_PACKAGE,getPackageName()));return;}PhoneClient.prefs(this).edit().putBoolean("enabled",true).apply();if(PhoneClient.paired(this))startSync();else SyncService.postLocal(this);deliver();}
 void connect(String raw){if(pairing)return;pairing=true;io.execute(()->{try{PhoneClient.PairLink link=new PhoneClient.PairLink(raw);PhoneClient.selectLocalNetwork(this);PhoneClient.Response response=PhoneClient.request(link.endpoint,link.fingerprint,null,"/api/pair",new JSONObject().put("secret",link.secret).put("device",Build.MANUFACTURER+" "+Build.MODEL),null);if(response.code!=200)throw new IOException("Код истёк или не сохранён на ПК. Покажи новый QR.");if(!PhoneClient.prefs(this).edit().putString("endpoint",link.endpoint).putString("fingerprint",link.fingerprint).putString("token",response.json.getString("token")).remove("pending").putBoolean("online",false).putString("status","Компьютер подключён").commit())throw new IOException("Не удалось сохранить подключение");MobileStore.sync(this,null);emit("silkConnected","");}catch(Exception e){emit("silkError",e.getMessage()==null?"Нет связи с ПК. Проверь Wi-Fi и запущенный журнал.":e.getMessage());}finally{pairing=false;deliver();}});}
 public final class Bridge {
  @JavascriptInterface public void ready(){handler.post(()->{if(isDestroyed()||isFinishing())return;ready=true;handler.removeCallbacks(bootTimeout);startup.setVisibility(View.GONE);lastView="";deliver();if(!incoming.isEmpty())emit("silkPairLink",incoming);});}
  @JavascriptInterface public void startupError(String detail){handler.post(()->showStartupError(detail==null?"JavaScript startup error":detail.substring(0,Math.min(240,detail.length()))));}
  @JavascriptInterface public String state(){try{return MobileStore.view(MainActivity.this);}catch(Exception e){return "{\"error\":"+JSONObject.quote(e.toString())+"}";}}
  @JavascriptInterface public String action(String raw){try{String result=MobileStore.command(MainActivity.this,raw);wake();return result;}catch(Exception e){return "{\"error\":"+JSONObject.quote(e.getMessage())+"}";}}
  @JavascriptInterface public void preferences(String raw){try{MobileStore.preferences(MainActivity.this,raw);}catch(Exception e){emit("silkError",e.getMessage());}}
  @JavascriptInterface public void sync(){wake();}
  @JavascriptInterface public void pair(String raw){handler.post(()->connect(raw));}
  @JavascriptInterface public void scan(){handler.post(()->{if(checkSelfPermission(Manifest.permission.CAMERA)!=PackageManager.PERMISSION_GRANTED)requestPermissions(new String[]{Manifest.permission.CAMERA},2);else startActivityForResult(new Intent(MainActivity.this,ScanActivity.class),3);});}
  @JavascriptInterface public void image(){handler.post(()->startActivityForResult(new Intent(Intent.ACTION_OPEN_DOCUMENT).setType("image/*").addCategory(Intent.CATEGORY_OPENABLE),4));}
  @JavascriptInterface public void notifyQuest(){handler.post(()->notifications());}
  @JavascriptInterface public void hideNotification(){handler.post(()->{SyncService.hide(MainActivity.this);deliver();});}
  @JavascriptInterface public void resolve(boolean phone){try{MobileStore.resolve(MainActivity.this,phone);deliver();wake();}catch(Exception e){emit("silkError",e.getMessage());}}
  @JavascriptInterface public void exportJournal(){handler.post(()->{try{exportData=MobileStore.export(MainActivity.this);startActivityForResult(new Intent(Intent.ACTION_CREATE_DOCUMENT).setType("application/json").addCategory(Intent.CATEGORY_OPENABLE).putExtra(Intent.EXTRA_TITLE,"SilkQuest-mobile-backup.json"),5);}catch(Exception e){emit("silkError",e.getMessage());}});}
  @JavascriptInterface public void importJournal(){handler.post(()->startActivityForResult(new Intent(Intent.ACTION_OPEN_DOCUMENT).setType("application/json").addCategory(Intent.CATEGORY_OPENABLE),6));}
  @JavascriptInterface public void resetJournal(){handler.post(()->new AlertDialog.Builder(MainActivity.this).setTitle("Сбросить мобильный журнал?").setMessage("Будет сохранена локальная копия перед сбросом. Для удобного восстановления сначала экспортируй журнал в файл. Компьютер не затрагивается.").setNegativeButton("Отмена",null).setPositiveButton("Сбросить",(d,w)->{try{SyncService.hide(MainActivity.this);MobileStore.reset(MainActivity.this);emit("silkConnected","");deliver();}catch(Exception e){emit("silkError",e.getMessage());}}).show());}
  @JavascriptInterface public void disconnect(){handler.post(()->new AlertDialog.Builder(MainActivity.this).setTitle("Отключить компьютер?").setMessage("Локальный журнал сохранится. Перед сменой компьютера синхронизируй очередь.").setNegativeButton("Отмена",null).setPositiveButton("Отключить",(d,w)->{try{MobileStore.disconnect(MainActivity.this);SyncService.hide(MainActivity.this);deliver();}catch(Exception e){emit("silkError",e.getMessage());}}).show());}
  @JavascriptInterface public void finish(){handler.post(()->MainActivity.this.finish());}
 }
 @Override protected void onActivityResult(int request,int result,Intent data){super.onActivityResult(request,result,data);if(result!=RESULT_OK||data==null)return;
  if(request==3){incoming=data.getStringExtra("link");if(incoming!=null){emit("silkPairLink",incoming);connect(incoming);}return;}
  if(request==4&&data.getData()!=null){Uri uri=data.getData();io.execute(()->{try{
   BitmapFactory.Options bounds=new BitmapFactory.Options();bounds.inJustDecodeBounds=true;try(InputStream in=getContentResolver().openInputStream(uri)){BitmapFactory.decodeStream(in,null,bounds);}if(bounds.outWidth<=0||bounds.outHeight<=0)throw new IOException("Не удалось открыть изображение");
   Bitmap bitmap;
   if(Build.VERSION.SDK_INT>=28){bitmap=ImageDecoder.decodeBitmap(ImageDecoder.createSource(getContentResolver(),uri),(decoder,info,source)->{int size=Math.max(info.getSize().getWidth(),info.getSize().getHeight());double scale=Math.min(1,1024d/size);decoder.setTargetSize(Math.max(1,(int)(info.getSize().getWidth()*scale)),Math.max(1,(int)(info.getSize().getHeight()*scale)));decoder.setAllocator(ImageDecoder.ALLOCATOR_SOFTWARE);});}
   else{BitmapFactory.Options opt=new BitmapFactory.Options();opt.inSampleSize=1;while(Math.max(bounds.outWidth,bounds.outHeight)/opt.inSampleSize>2048)opt.inSampleSize*=2;try(InputStream in=getContentResolver().openInputStream(uri)){bitmap=BitmapFactory.decodeStream(in,null,opt);}}
   if(bitmap==null)throw new IOException("Неподдерживаемое изображение");double scale=Math.min(1,1024d/Math.max(bitmap.getWidth(),bitmap.getHeight()));if(scale<1){Bitmap small=Bitmap.createScaledBitmap(bitmap,(int)(bitmap.getWidth()*scale),(int)(bitmap.getHeight()*scale),true);bitmap.recycle();bitmap=small;}ByteArrayOutputStream out=new ByteArrayOutputStream();bitmap.compress(Bitmap.CompressFormat.PNG,100,out);bitmap.recycle();emit("silkImage","data:image/png;base64,"+android.util.Base64.encodeToString(out.toByteArray(),android.util.Base64.NO_WRAP));
  }catch(Exception e){emit("silkError",e.getMessage());}});}
  if(request==5&&data.getData()!=null&&exportData!=null){Uri uri=data.getData();String saved=exportData;io.execute(()->{try(OutputStream out=getContentResolver().openOutputStream(uri)){out.write(saved.getBytes(java.nio.charset.StandardCharsets.UTF_8));emit("silkMessage","Резервная копия сохранена");}catch(Exception e){emit("silkError",e.getMessage());}});}
  if(request==6&&data.getData()!=null){Uri uri=data.getData();new AlertDialog.Builder(this).setTitle("Восстановить мобильный журнал?").setMessage("Текущая версия будет сохранена в резервной копии. Затем восстановятся данные и очередь из выбранного файла.").setNegativeButton("Отмена",null).setPositiveButton("Восстановить",(d,w)->io.execute(()->{try(InputStream in=getContentResolver().openInputStream(uri)){MobileStore.restore(this,in);deliver();wake();}catch(Exception e){emit("silkError",e.getMessage());}})).show();}
 }
 @Override public void onRequestPermissionsResult(int request,String[] permissions,int[] results){super.onRequestPermissionsResult(request,permissions,results);if(results.length==0)return;if(request==1&&results[0]==PackageManager.PERMISSION_GRANTED)notifications();if(request==2&&results[0]==PackageManager.PERMISSION_GRANTED)startActivityForResult(new Intent(this,ScanActivity.class),3);}
}

