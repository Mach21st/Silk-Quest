package app.silkquest.companion;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Color;
import android.hardware.Camera;
import android.os.Bundle;
import android.os.SystemClock;
import android.view.SurfaceHolder;
import android.view.SurfaceView;
import android.widget.*;
import com.google.zxing.*;
import com.google.zxing.common.HybridBinarizer;
import java.util.*;

@SuppressWarnings("deprecation")
public final class ScanActivity extends Activity implements SurfaceHolder.Callback {
 Camera camera;SurfaceView surface;TextView hint;volatile boolean busy,finished;long last;
 @Override public void onCreate(Bundle b){super.onCreate(b);LinearLayout root=new LinearLayout(this);root.setOrientation(LinearLayout.VERTICAL);root.setPadding(22,60,22,40);root.setBackgroundColor(Color.rgb(17,27,34));hint=new TextView(this);hint.setText("Наведи камеру на QR в настройках Silk Quest на компьютере");hint.setTextColor(Color.rgb(232,221,203));hint.setTextSize(20);hint.setPadding(0,0,0,24);root.addView(hint);surface=new SurfaceView(this);root.addView(surface,new LinearLayout.LayoutParams(-1,0,1));Button back=new Button(this);back.setText("Назад");back.setOnClickListener(v->finish());root.addView(back);setContentView(root);surface.getHolder().addCallback(this);}
 void start(){if(camera!=null||finished||!surface.getHolder().getSurface().isValid())return;try{
  int id=0;for(int i=0;i<Camera.getNumberOfCameras();i++){Camera.CameraInfo info=new Camera.CameraInfo();Camera.getCameraInfo(i,info);if(info.facing==Camera.CameraInfo.CAMERA_FACING_BACK){id=i;break;}}
  camera=Camera.open(id);Camera.Parameters p=camera.getParameters();List<Camera.Size> sizes=p.getSupportedPreviewSizes();Camera.Size best=null;for(Camera.Size size:sizes)if(size.width<=1280&&(best==null||size.width>best.width))best=size;if(best!=null)p.setPreviewSize(best.width,best.height);List<String> modes=p.getSupportedFocusModes();if(modes!=null&&modes.contains(Camera.Parameters.FOCUS_MODE_CONTINUOUS_PICTURE))p.setFocusMode(Camera.Parameters.FOCUS_MODE_CONTINUOUS_PICTURE);camera.setParameters(p);
  Camera.CameraInfo info=new Camera.CameraInfo();Camera.getCameraInfo(id,info);int rotation=getWindowManager().getDefaultDisplay().getRotation()*90;camera.setDisplayOrientation((info.orientation-rotation+360)%360);camera.setPreviewDisplay(surface.getHolder());
  camera.setPreviewCallback((bytes,c)->{if(busy||finished||SystemClock.uptimeMillis()-last<450)return;last=SystemClock.uptimeMillis();busy=true;Camera.Size size=c.getParameters().getPreviewSize();byte[] copy=bytes.clone();new Thread(()->decode(copy,size.width,size.height),"SilkQuest-QR").start();});camera.startPreview();
 }catch(Exception e){close();hint.setText("Не удалось открыть камеру. Вернись назад и вставь ссылку подключения вручную.");}}
 void decode(byte[] bytes,int width,int height){try{
  Map<DecodeHintType,Object> hints=new EnumMap<>(DecodeHintType.class);hints.put(DecodeHintType.POSSIBLE_FORMATS,Collections.singletonList(BarcodeFormat.QR_CODE));hints.put(DecodeHintType.TRY_HARDER,true);
  MultiFormatReader reader=new MultiFormatReader();reader.setHints(hints);Result result=reader.decodeWithState(new BinaryBitmap(new HybridBinarizer(new PlanarYUVLuminanceSource(bytes,width,height,0,0,width,height,false))));String link=result.getText();
  new PhoneClient.PairLink(link);finished=true;runOnUiThread(()->{setResult(RESULT_OK,new Intent().putExtra("link",link));finish();});
 }catch(Exception ignored){}finally{busy=false;}}
 void close(){if(camera!=null){try{camera.setPreviewCallback(null);camera.stopPreview();}catch(RuntimeException ignored){}finally{camera.release();camera=null;}}}
 @Override protected void onResume(){super.onResume();if(surface!=null)start();}
 @Override protected void onPause(){close();super.onPause();}
 @Override public void surfaceCreated(SurfaceHolder h){start();}
 @Override public void surfaceChanged(SurfaceHolder h,int f,int w,int y){start();}
 @Override public void surfaceDestroyed(SurfaceHolder h){close();}
}
