param([Parameter(Mandatory=$true)][string]$Fixture)
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
New-Item -ItemType Directory -Force lib,classes|Out-Null
if(!(Test-Path lib/json.jar)){Invoke-WebRequest 'https://repo.maven.apache.org/maven2/org/json/json/20240303/json-20240303.jar' -OutFile lib/json.jar}
if((Get-FileHash lib/json.jar -Algorithm SHA256).Hash.ToLowerInvariant() -ne '3cf6cd6892e32e2b4c1c39e0f52f5248a2f5b37646fdfbb79a66b46b618414ed'){throw 'JSON test dependency checksum mismatch'}
$sources=@(Get-ChildItem src -Recurse -Filter '*.java'|ForEach-Object FullName)
$sources+=Join-Path (Split-Path $PSScriptRoot -Parent) 'src/app/silkquest/companion/MobileStore.java'
javac -encoding UTF-8 -cp lib/json.jar -d classes @sources
if($LASTEXITCODE -ne 0){throw 'Compilation failed'}
$data=Join-Path $PSScriptRoot ('test-data-'+[Guid]::NewGuid().ToString('N'))
java -cp 'classes;lib/json.jar' app.silkquest.companion.ModelTests $Fixture $data
if($LASTEXITCODE -ne 0){throw 'Android model checks failed'}
