package app.silkquest.companion;

final class Beads {
 static String amount(long value) {
  long last=Math.abs(value%100),unit=last%10;
  String noun=last>=11&&last<=14?"бусин":unit==1?"бусина":unit>=2&&unit<=4?"бусины":"бусин";
  return String.format(java.util.Locale.forLanguageTag("ru-RU"),"%,d",value)+" "+noun;
 }
}
