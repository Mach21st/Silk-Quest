param(
 [string]$AndroidJar,
 [string]$BuildTools,
 [string]$SigningKey,
 [string]$OutputApk
)
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
if(!$AndroidJar){$AndroidJar=Join-Path $env:ANDROID_HOME 'platforms/android-36/android.jar'}
if(!$BuildTools){$BuildTools=Join-Path $env:ANDROID_HOME 'build-tools/36.0.0'}
if(!(Test-Path -LiteralPath $AndroidJar)){throw 'Android SDK Platform 36 is required. Supply -AndroidJar or ANDROID_HOME.'}
if(!(Test-Path -LiteralPath (Join-Path $BuildTools 'aapt2.exe'))){throw 'Android SDK Build Tools 36.0.0 are required.'}
if(!$OutputApk){$OutputApk=Join-Path $PSScriptRoot 'SilkQuest-Android.apk'}
$build=Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Force "$build/classes","$build/generated","$build/dex","$build/tmp"|Out-Null
function Native([string]$exe,[string[]]$arguments){
 if($exe-eq 'java'){$arguments=@("-Djava.io.tmpdir=$build/tmp")+$arguments}
 elseif($exe-in @('jar','javac','keytool')){$arguments=@("-J-Djava.io.tmpdir=$build/tmp")+$arguments}
 & $exe @arguments;if($LASTEXITCODE-ne 0){throw "$exe failed ($LASTEXITCODE)"}
}
Native (Join-Path $BuildTools 'aapt2.exe') @('compile','--dir','res','-o',"$build/resources.zip")
Native (Join-Path $BuildTools 'aapt2.exe') @('link','-o',"$build/unsigned.apk",'-I',$AndroidJar,'-A','assets','--manifest','AndroidManifest.xml','--java',"$build/generated",'--min-sdk-version','26','--target-sdk-version','36',"$build/resources.zip")
$sources=@(Get-ChildItem src,build/generated -Filter '*.java' -Recurse|ForEach-Object FullName)
Native 'javac' (@('-encoding','UTF-8','-source','8','-target','8','-classpath',"$AndroidJar;$PSScriptRoot/lib/zxing-core-3.5.3.jar",'-d',"$build/classes")+$sources)
Native 'jar' @('cf',"$build/classes.jar",'-C',"$build/classes",'.')
Native 'java' @('-cp',"$BuildTools/lib/d8.jar",'com.android.tools.r8.D8','--lib',$AndroidJar,'--min-api','26','--output',"$build/dex","$build/classes.jar",'lib/zxing-core-3.5.3.jar')
Native 'jar' @('uf',"$build/unsigned.apk",'-C',"$build/dex",'classes.dex')
Native (Join-Path $BuildTools 'zipalign.exe') @('-f','4',"$build/unsigned.apk","$build/aligned.apk")
if(!$SigningKey){$SigningKey=Join-Path $build 'development.jks'}
if(!(Test-Path -LiteralPath $SigningKey)){
 New-Item -ItemType Directory -Force (Split-Path -Parent $SigningKey)|Out-Null
 Native 'keytool' @('-genkeypair','-keystore',$SigningKey,'-storepass','android','-keypass','android','-alias','silkquest','-dname','CN=SilkQuest Local Development','-keyalg','RSA','-keysize','2048','-validity','10000','-noprompt')
}
# Local development signature. Use a private release keystore and secret-based signing for publication.
Native 'java' @('-jar',"$BuildTools/lib/apksigner.jar",'sign','--ks',$SigningKey,'--ks-key-alias','silkquest','--ks-pass','pass:android','--key-pass','pass:android','--out',$OutputApk,"$build/aligned.apk")
Native 'java' @('-jar',"$BuildTools/lib/apksigner.jar",'verify','--verbose',$OutputApk)
Write-Output "APK: $OutputApk"
