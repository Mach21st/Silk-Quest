param(
 [Parameter(Mandatory=$true)][string]$Apk,
 [Parameter(Mandatory=$true)][string]$Adb,
 [Parameter(Mandatory=$true)][string]$AndroidJar,
 [Parameter(Mandatory=$true)][string]$BuildTools,
 [Parameter(Mandatory=$true)][string]$SigningKey,
 [string]$Serial='emulator-5556',
 [int]$ServerPort=5039
)
$ErrorActionPreference='Stop'
function Native([string]$exe,[string[]]$arguments){& $exe @arguments;if($LASTEXITCODE -ne 0){throw "$exe failed ($LASTEXITCODE)"}}
$qemu=& $Adb -P $ServerPort -s $Serial shell getprop ro.kernel.qemu
if($qemu.Trim() -ne '1'){throw 'These tests require a disposable emulator'}
$build=Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Force "$build/classes","$build/dex"|Out-Null
Native (Join-Path $BuildTools 'aapt2.exe') @('link','-o',"$build/base.apk",'-I',$AndroidJar,'--manifest',"$PSScriptRoot/AndroidManifest.xml")
Native 'javac' @('-encoding','UTF-8','-source','8','-target','8','-cp',$AndroidJar,'-d',"$build/classes","$PSScriptRoot/StartupTests.java")
Native 'jar' @('cf',"$build/classes.jar",'-C',"$build/classes",'.')
Native 'java' @('-cp',"$BuildTools/lib/d8.jar",'com.android.tools.r8.D8','--lib',$AndroidJar,'--min-api','26','--output',"$build/dex","$build/classes.jar")
Native 'jar' @('uf',"$build/base.apk",'-C',"$build/dex",'classes.dex')
Native (Join-Path $BuildTools 'zipalign.exe') @('-f','4',"$build/base.apk","$build/aligned.apk")
Native 'java' @('-jar',"$BuildTools/lib/apksigner.jar",'sign','--ks',$SigningKey,'--ks-key-alias','silkquest','--ks-pass','pass:android','--key-pass','pass:android','--out',"$build/tests.apk","$build/aligned.apk")
Native $Adb @('-P',"$ServerPort",'-s',$Serial,'install','-r',$Apk)
Native $Adb @('-P',"$ServerPort",'-s',$Serial,'install','-r','-t',"$build/tests.apk")
Native $Adb @('-P',"$ServerPort",'-s',$Serial,'shell','am','force-stop','app.silkquest.companion')
$output=& $Adb -P $ServerPort -s $Serial shell am instrument -w -r app.silkquest.tests/.StartupTests
$output|Set-Content "$build/result.txt" -Encoding utf8
$output
if(($output -join "`n") -notmatch 'PASS:' -or ($output -join "`n") -notmatch 'INSTRUMENTATION_CODE: -1'){throw 'Android startup integration test failed'}
Native $Adb @('-P',"$ServerPort",'-s',$Serial,'shell','am','force-stop','app.silkquest.companion')
$output=& $Adb -P $ServerPort -s $Serial shell am instrument -w -r -e requireSaved true app.silkquest.tests/.StartupTests
$output|Set-Content "$build/restart-result.txt" -Encoding utf8
$output
if(($output -join "`n") -notmatch 'PASS:' -or ($output -join "`n") -notmatch 'INSTRUMENTATION_CODE: -1'){throw 'Android cold restart integration test failed'}
