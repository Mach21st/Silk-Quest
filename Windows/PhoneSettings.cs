using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRCoder;

namespace SilkJournal;

public sealed partial class MainWindow
{
 TextBlock? phoneStatusText;
 string? phonePairLink;
 string? phoneAddress;
 bool phonePairExpired;
 TextBlock? phonePairTime;
 void UpdatePhoneStatus()
 {
  if(page!="Настройки")return;if(phoneStatusText!=null)phoneStatusText.Text=app.Phone.Status;
  if(phonePairTime!=null&&app.Phone.PairingActive)phonePairTime.Text=L.F("Код действует ещё {0} мин",Math.Max(1,(int)Math.Ceiling((app.Phone.PairExpires-DateTime.UtcNow).TotalMinutes)));
  if(phonePairLink!=null&&!app.Phone.PairingActive){phonePairExpired=DateTime.UtcNow>=app.Phone.PairExpires;phonePairLink=null;app.RefreshViews();}
 }
 void RenderPhoneSettings()
 {
  var heading=UI.Text(L.T("Телефон · Wi-Fi"),23);heading.Margin=new Thickness(0,8,0,14);tree.Children.Add(heading);
  phonePairTime=null;
  tree.Children.Add(UI.SettingsNote(L.T("Одна домашняя сеть, без аккаунта и облака. На Android нужен компаньон Silk Quest. Квест выбирается через ··· → «Закрепить на телефоне».")));
  var enabled=new CheckBox{Content=L.T("Подключение телефона по Wi-Fi"),IsChecked=app.State.PhoneSyncEnabled,Margin=new Thickness(0,16,0,8)};
  enabled.Click+=(_,_)=>{phonePairLink=null;app.SetPhoneSync(enabled.IsChecked==true);};tree.Children.Add(enabled);
  phoneStatusText=UI.Text(app.Phone.Status,15,app.Phone.Error==null?UI.Paper:UI.Red);phoneStatusText.Margin=new Thickness(0,4,0,12);tree.Children.Add(phoneStatusText);
  if(!app.Phone.Listening)return;
  var addresses=PhoneBridge.Addresses();if(!addresses.Contains(phoneAddress??""))phoneAddress=addresses.FirstOrDefault();
  if(phoneAddress==null){tree.Children.Add(UI.SettingsNote(L.T("Не найден адрес домашней сети. Подключи компьютер к Wi-Fi или к роутеру кабелем.")));return;}
  if(addresses.Count>1)
  {
   tree.Children.Add(UI.Label(L.T("АДРЕС КОМПЬЮТЕРА · ВЫБЕРИ СВОЮ ДОМАШНЮЮ СЕТЬ")));var choices=new WrapPanel();
   foreach(string address in addresses){var b=UI.Button(address,()=>{phoneAddress=address;phonePairLink=null;app.Commit();},phoneAddress==address);b.FontSize=14;b.Margin=new Thickness(0,6,7,6);choices.Children.Add(b);}tree.Children.Add(choices);
  }
  else tree.Children.Add(UI.Text(L.F("Адрес компьютера: {0} · порт {1}",phoneAddress,app.State.PhonePort),14,UI.Muted));
  var pair=UI.Button(app.State.PhoneToken==null?L.T("Показать QR для подключения"):L.T("Подключить другой телефон"),()=>{phonePairExpired=false;phonePairLink=app.Phone.CreatePairLink(phoneAddress!);app.Commit();},true);pair.Margin=new Thickness(0,14,0,10);tree.Children.Add(pair);
  if(phonePairExpired)tree.Children.Add(UI.SettingsNote(L.T("Код истёк. Покажи новый QR для подключения.")));
  if(app.State.PhoneToken!=null)
  {
   var revoke=UI.Button(L.T("Отозвать доступ телефона"),()=>{phonePairLink=null;app.Phone.Revoke();});revoke.Foreground=UI.Muted;tree.Children.Add(revoke);
  }
  if(phonePairLink!=null&&app.Phone.PairingActive)
  {
   using var data=QRCodeGenerator.GenerateQrCode(phonePairLink,QRCodeGenerator.ECCLevel.M);using var qr=new PngByteQRCode(data);
   using var bytes=new MemoryStream(qr.GetGraphic(5));var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=bytes;bitmap.EndInit();bitmap.Freeze();
   var image=new Image{Source=bitmap,Width=228,Height=228,Stretch=Stretch.Uniform};RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.NearestNeighbor);
   var qrFrame=new Border{HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,5,0,12),CornerRadius=new CornerRadius(12),BorderThickness=new Thickness(.8),BorderBrush=UI.B("#A99A83"),Background=new LinearGradientBrush(UI.Color("#252C30"),UI.Color("#111A22"),90),Padding=new Thickness(5)};
   qrFrame.Child=new Border{CornerRadius=new CornerRadius(8),BorderThickness=new Thickness(.7),BorderBrush=UI.B("#53514C"),Padding=new Thickness(9),Child=image};tree.Children.Add(qrFrame);
   phonePairTime=UI.SettingsNote(L.T("Код действует ещё 5 мин"));tree.Children.Add(phonePairTime);
   tree.Children.Add(UI.Text(L.T("В приложении Android нажми «Сканировать QR». Код действует 5 минут и используется один раз. Подключение другого телефона заменит прежнее."),14,UI.Paper));
   var copy=UI.Button(L.T("Скопировать ссылку подключения"),()=>{Clipboard.SetText(phonePairLink);Toast(L.T("Ссылка скопирована. Можно вставить её в Android-компаньон."));});copy.FontSize=14;copy.Margin=new Thickness(0,12,0,10);tree.Children.Add(copy);
  }
  var pinned=app.State.Find(app.State.PhonePinnedId);tree.Children.Add(UI.Text(pinned==null?L.T("На телефон пока ничего не закреплено."):L.T("На телефоне: ")+pinned.Title,15,UI.Paper));
  if(pinned!=null)tree.Children.Add(UI.Button(L.T("Снять квест с телефона"),()=>app.PinPhone(null)));
  tree.Children.Add(UI.SettingsNote(L.T("Компьютер должен быть включён, журнал может оставаться в трее. Вне сети телефон хранит последний квест; завершение отправится при возвращении. Если Windows спросит доступ к сети — разреши только для частной домашней сети. Гостевой Wi-Fi и изоляция устройств роутера могут мешать соединению.")));
 }
}
