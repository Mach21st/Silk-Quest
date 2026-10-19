package app.silkquest.companion;

import android.Manifest;
import android.app.*;
import android.content.*;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.os.*;
import android.view.*;
import android.widget.*;
import org.json.JSONObject;

public final class MainActivity extends Activity {
 LinearLayout page;EditText linkField;boolean pairing;
 ScrollView scrollView;TextView statusView;String screenSignature="",renderedQuest="";boolean renderedPairing;
 final BroadcastReceiver updates=new BroadcastReceiver(){public void onReceive(Context c,Intent i){if(!pairing){if(!PhoneClient.paired(c)){pairing=true;render();}else if(!signature().equals(screenSignature))render();else if(statusView!=null)statusView.setText(PhoneClient.statusText(c));}}};
 String signature(){return PhoneClient.prefs(this).getString("cache","")+"|"+PhoneClient.prefs(this).getString("pending","")+"|"+PhoneClient.prefs(this).getBoolean("enabled",false)+"|"+canNotify();}
 int dp(float n){return (int)(getResources().getDisplayMetrics().density*n+.5f);}
 @Override public void onCreate(Bundle b){super.onCreate(b);pairing=!PhoneClient.paired(this);render();if(b!=null){if(pairing&&linkField!=null)linkField.setText(b.getString("pairLink",""));int y=b.getInt("scroll",0);scrollView.post(()->scrollView.scrollTo(0,y));}else handleLink(getIntent());if(PhoneClient.paired(this)&&PhoneClient.prefs(this).getBoolean("enabled",true)&&canNotify())startSync();}
 @Override protected void onSaveInstanceState(Bundle out){super.onSaveInstanceState(out);out.putInt("scroll",scrollView==null?0:scrollView.getScrollY());if(pairing&&linkField!=null)out.putString("pairLink",linkField.getText().toString());}
 @Override protected void onNewIntent(Intent i){super.onNewIntent(i);setIntent(i);handleLink(i);}
 void handleLink(Intent i){if(i.getData()!=null&&"silkquest".equals(i.getData().getScheme())){pairing=true;render();linkField.setText(i.getData().toString());}}
 @Override protected void onStart(){super.onStart();IntentFilter f=new IntentFilter(SyncService.UPDATED);if(Build.VERSION.SDK_INT>=33)registerReceiver(updates,f,Context.RECEIVER_NOT_EXPORTED);else registerReceiver(updates,f);}
 @Override protected void onStop(){unregisterReceiver(updates);if(!pairing&&scrollView!=null)PhoneClient.prefs(this).edit().putInt("screenScroll",scrollView.getScrollY()).apply();super.onStop();}
 TextView text(String value,int size,int color){TextView t=new TextView(this);t.setText(value);t.setTextColor(color);t.setTextSize(size);t.setTypeface(Typeface.create("serif",Typeface.NORMAL));t.setPadding(0,dp(5),0,dp(10));t.setLineSpacing(dp(3),1);page.addView(t);return t;}
 void rule(){View v=new View(this);v.setBackgroundColor(Color.rgb(72,77,76));LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(-1,dp(1));p.setMargins(0,dp(16),0,dp(16));page.addView(v,p);}
 Button button(String title,boolean primary,Runnable action){Button b=new Button(this);b.setText(title);b.setAllCaps(false);b.setTextColor(Color.rgb(232,221,203));b.setTextSize(17);b.setTypeface(Typeface.create("serif",0));GradientDrawable bg=new GradientDrawable();bg.setColor(Color.parseColor(primary?"#873C3C":"#19252E"));bg.setCornerRadius(dp(7));bg.setStroke(dp(1),Color.parseColor(primary?"#BD5A52":"#3D4850"));b.setBackground(bg);b.setPadding(dp(12),dp(10),dp(12),dp(10));LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(-1,-2);p.setMargins(0,dp(6),0,dp(8));page.addView(b,p);b.setOnClickListener(v->action.run());return b;}
 void render(){
  JSONObject cachedQuest=PhoneClient.cache(this).optJSONObject("quest");String nextQuest=cachedQuest==null?"":cachedQuest.optString("id");int previousY=scrollView==null?PhoneClient.prefs(this).getInt("screenScroll",0):scrollView.getScrollY();if((scrollView==null&&pairing)||(scrollView!=null&&(pairing!=renderedPairing||!nextQuest.equals(renderedQuest))))previousY=0;final int restoreY=previousY;renderedPairing=pairing;renderedQuest=nextQuest;screenSignature=signature();
  ScrollView scroll=new ScrollView(this);scroll.setFillViewport(true);scroll.setBackgroundColor(Color.parseColor("#111B22"));scroll.setOnApplyWindowInsetsListener((v,insets)->{v.setPadding(0,insets.getSystemWindowInsetTop(),0,insets.getSystemWindowInsetBottom());return insets;});
  scrollView=scroll;scroll.post(()->scroll.scrollTo(0,restoreY));statusView=null;
  page=new LinearLayout(this);page.setOrientation(LinearLayout.VERTICAL);page.setPadding(dp(25),dp(28),dp(25),dp(28));scroll.addView(page);setContentView(scroll);
  text("✧  Журнал странника",16,Color.parseColor("#B8AB96"));text("Silk Quest",34,Color.parseColor("#E8DDCB"));rule();
  if(pairing||!PhoneClient.paired(this)){renderPair();return;}
  JSONObject state=PhoneClient.cache(this),q=state.optJSONObject("quest");statusView=text(PhoneClient.statusText(this),14,Color.parseColor("#929CA5"));
  if(q==null)text("Твой следующий квест",27,Color.parseColor("#E8DDCB"));else{
   text(q.optBoolean("done")?"✧ ЗАВЕРШЕНО":"● В ФОКУСЕ",12,Color.parseColor("#C25B58"));text(q.optString("title"),28,Color.parseColor("#E8DDCB"));
   text(q.optString("notes"),17,Color.parseColor("#ADB2AE"));text(q.optInt("completed")+" из "+q.optInt("total")+(q.optBoolean("done")?" · награда учтена":" · награда "+Beads.amount(q.optInt("reward"))),15,Color.parseColor("#C9BDA8"));
   if(PhoneClient.prefs(this).contains("pending"))text("Завершение ожидает отправки по Wi-Fi.",15,Color.parseColor("#D79883"));
   if(!q.optBoolean("done")&&!PhoneClient.prefs(this).contains("pending"))button("✓ Завершить квест",true,()->{try{PhoneClient.queueCompletion(this,state);requestSync();}catch(Exception e){message("Не удалось сохранить действие.");}});
  }
  rule();text("Бусы · "+state.optLong("balance"),21,Color.parseColor("#E8DDCB"));
  text("Выбери задачу на компьютере: ··· → Закрепить на телефоне. Вне домашней сети здесь остаётся последний полученный квест.",15,Color.parseColor("#929CA5"));
  button(canNotify()?"Обновить и показать уведомление":"Разрешить уведомления",true,()->requestSync());
  button("Убрать уведомление и приостановить связь",false,()->{SyncService.hide(this);render();});
  button("Отключить этот компьютер",false,()->new AlertDialog.Builder(this).setTitle("Отключить компьютер?").setMessage("Неотправленное завершение будет удалено. Квесты на компьютере останутся.").setNegativeButton("Отмена",null).setPositiveButton("Отключить",(d,w)->{SyncService.hide(this);PhoneClient.prefs(this).edit().clear().apply();pairing=true;render();}).show());
 }
 void renderPair(){
  text("Нить между устройствами",25,Color.parseColor("#E8DDCB"));text("1. Подключи телефон и компьютер к одной домашней сети.\n2. На компьютере открой Настройки → Телефон · Wi-Fi и покажи QR.\n3. Отсканируй его здесь.",16,Color.parseColor("#ADB2AE"));
  button("Сканировать QR",true,()->{if(checkSelfPermission(Manifest.permission.CAMERA)!=PackageManager.PERMISSION_GRANTED)requestPermissions(new String[]{Manifest.permission.CAMERA},2);else scan();});
  text("Или вставь ссылку подключения",14,Color.parseColor("#929CA5"));linkField=new EditText(this);linkField.setTextSize(14);linkField.setTextColor(Color.parseColor("#E8DDCB"));linkField.setHint("silkquest://pair?…");linkField.setHintTextColor(Color.parseColor("#6C7A80"));linkField.setMinLines(2);linkField.setMaxLines(4);linkField.setInputType(android.text.InputType.TYPE_CLASS_TEXT|android.text.InputType.TYPE_TEXT_FLAG_NO_SUGGESTIONS);page.addView(linkField);
  Button connect=button("Подключить компьютер",true,()->connect());connect.setTag("connect");
  text("Без аккаунта и облака. Соединение шифруется; отпечаток компьютера берётся из QR-кода. Ссылка действует 5 минут. Не передавай её другим людям.",13,Color.parseColor("#929CA5"));
 }
 void scan(){startActivityForResult(new Intent(this,ScanActivity.class),3);}
 @Override protected void onActivityResult(int request,int result,Intent data){super.onActivityResult(request,result,data);if(request==3&&result==RESULT_OK&&data!=null){pairing=true;render();linkField.setText(data.getStringExtra("link"));connect();}}
 void connect(){
  final PhoneClient.PairLink link;try{link=new PhoneClient.PairLink(linkField.getText().toString());}catch(Exception e){message(e.getMessage());return;}
  View button=page.findViewWithTag("connect");if(button!=null)button.setEnabled(false);text("Соединяем нить…",15,Color.parseColor("#D79883"));
  new Thread(()->{try{
   PhoneClient.selectLocalNetwork(this);
   JSONObject payload=new JSONObject().put("secret",link.secret).put("device",Build.MANUFACTURER+" "+Build.MODEL);
   PhoneClient.Response response=PhoneClient.request(link.endpoint,link.fingerprint,null,"/api/pair",payload,null);
   if(response.code==503)throw new java.io.IOException("Компьютер не смог сохранить подключение. Проверь сохранение журнала и покажи новый QR.");if(response.code!=200)throw new java.io.IOException("Код истёк или уже использован. Покажи новый QR на компьютере.");
   PhoneClient.prefs(this).edit().putString("endpoint",link.endpoint).putString("fingerprint",link.fingerprint).putString("token",response.json.getString("token")).remove("cache").remove("pending").putBoolean("enabled",true).putString("status","Компьютер подключён").apply();
   runOnUiThread(()->{pairing=false;render();requestSync();});
  }catch(Exception e){runOnUiThread(()->{if(button!=null)button.setEnabled(true);message(e instanceof javax.net.ssl.SSLException?"Сертификат ПК не совпал. Создай новый QR и подключись заново.":"Не удалось подключиться. Проверь одну Wi-Fi сеть, запущенный журнал и разрешение Windows для частной сети. "+(e.getMessage()==null?"":e.getMessage()));});}},"SilkQuest-pair").start();
 }
 boolean canNotify(){return (Build.VERSION.SDK_INT<33||checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS)==PackageManager.PERMISSION_GRANTED)&&getSystemService(NotificationManager.class).areNotificationsEnabled();}
 void requestSync(){if(Build.VERSION.SDK_INT>=33&&checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS)!=PackageManager.PERMISSION_GRANTED){requestPermissions(new String[]{Manifest.permission.POST_NOTIFICATIONS},1);return;}if(!canNotify()){message("Включи уведомления Silk Quest в настройках Android.");startActivity(new Intent(android.provider.Settings.ACTION_APP_NOTIFICATION_SETTINGS).putExtra(android.provider.Settings.EXTRA_APP_PACKAGE,getPackageName()));return;}startSync();render();}
 void startSync(){PhoneClient.prefs(this).edit().putBoolean("enabled",true).apply();startForegroundService(new Intent(this,SyncService.class).setAction("refresh"));}
 @Override public void onRequestPermissionsResult(int request,String[] p,int[] results){super.onRequestPermissionsResult(request,p,results);if(results.length==0)return;if(request==1){if(results[0]==PackageManager.PERMISSION_GRANTED){startSync();render();}else{PhoneClient.prefs(this).edit().putBoolean("enabled",false).apply();message("Для закрепления квеста нужно разрешение на уведомления.");}}if(request==2){if(results[0]==PackageManager.PERMISSION_GRANTED)scan();else message("Можно подключиться вставкой ссылки без доступа к камере.");}}
 void message(String value){Toast.makeText(this,value,Toast.LENGTH_LONG).show();}
}
