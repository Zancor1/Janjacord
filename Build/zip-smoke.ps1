param([switch]$ProfileOnly,[switch]$AudioOnly,[switch]$NoiseOnly,[switch]$ShortcutOnly,[switch]$GroupTrayOnly,[switch]$ScreenFpsOnly)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.IO.Compression.FileSystem
$path=Join-Path $root 'JanjaCord-0.11.2-win-x64.zip'
$zip=[System.IO.Compression.ZipFile]::OpenRead($path)
try{
    $names=@($zip.Entries | ForEach-Object {$_.FullName.Replace('\','/')})
    foreach($folder in @('Client','Server')){foreach($file in @("$folder.exe","$folder.dll","$folder.deps.json","$folder.runtimeconfig.json",'hostpolicy.dll','hostfxr.dll','coreclr.dll','e_sqlite3.dll')){if($names -notcontains "$folder/$file"){throw "ZIP incompleto: $folder/$file"}}}
    foreach($file in @('deep_filter.dll','AudioLicenses/DeepFilterNet-MIT.txt','AudioLicenses/DeepFilterNet-PROVENANCE.txt')){if($names -notcontains "Client/$file"){throw "ZIP missing DeepFilterNet: $file"}}
    if($names -notcontains 'Client/mrwebrtc.dll'){throw 'ZIP sem motor nativo WebRTC'}
    if($names -notcontains 'Client/rnnoise.dll' -or $names -notcontains 'Client/AudioLicenses/rnnoise-COPYING'){throw 'ZIP sem motor/licença do supressor de ruído'}
    if($names -notcontains 'Client/Assets/incoming-call.mp3'){throw 'ZIP sem toque de chamada'}
    foreach($file in @('SIPSorcery.dll','SIPSorceryMedia.Abstractions.dll','NAudio.Wasapi.dll','NAudio.Core.dll','Concentus.dll','AudioLicenses/sipsorcery-LICENSE.md','AudioLicenses/naudio-license.txt')){if($names -notcontains "Client/$file"){throw "ZIP sem dependência de áudio: $file"}}
    if($names | Where-Object {$_ -match '\.(db|pfx)(-|$)'}){throw 'ZIP contém dados privados'}
}finally{$zip.Dispose()}
Write-Output 'PASS: ZIP inclui runtimes e DLLs nativas, inclusive arquivos ocultos, e exclui dados privados'
$extracted=Join-Path $root ('.tools/extracted-0110-'+[Guid]::NewGuid().ToString('N'))
[System.IO.Compression.ZipFile]::ExtractToDirectory($path,$extracted)
if($ScreenFpsOnly){
    & (Join-Path $PSScriptRoot 'auto-smoke.ps1') -ScreenFpsOnly -ClientPath (Join-Path $extracted 'Client/Client.exe')
    return
}
if($GroupTrayOnly){
    & (Join-Path $PSScriptRoot 'auto-smoke.ps1') -GroupTrayOnly -ClientPath (Join-Path $extracted 'Client/Client.exe')
    return
}
if($ShortcutOnly){
    & (Join-Path $PSScriptRoot 'microphone-shortcut-smoke.ps1') -ClientPath (Join-Path $extracted 'Client/Client.exe')
    return
}
if($NoiseOnly){
    & (Join-Path $PSScriptRoot 'auto-smoke.ps1') -QuickCall -NoiseOnly -ClientPath (Join-Path $extracted 'Client/Client.exe')
    Write-Output 'PASS: ZIP extraido: intensidade, teste local e preferencias salvas'
    return
}
if($AudioOnly){
    & (Join-Path $PSScriptRoot 'auto-smoke.ps1') -QuickCall -ClientPath (Join-Path $extracted 'Client/Client.exe')
    Write-Output 'PASS: executaveis extraidos: chamada, dispositivos, volume individual e tela cheia'
    return
}
if(!$ProfileOnly){
    & (Join-Path $PSScriptRoot 'smoke.ps1') -Distribution $extracted
    & (Join-Path $PSScriptRoot 'auto-smoke.ps1') -ClientPath (Join-Path $extracted 'Client/Client.exe')
}
& (Join-Path $PSScriptRoot 'profile-smoke.ps1') -ClientPath (Join-Path $extracted 'Client/Client.exe') -ServerPath (Join-Path $extracted 'Server/Server.exe')
Write-Output 'PASS: executáveis extraídos do ZIP iniciados; cadastro, edição de nome/foto e reabertura da mesma conta verificados'
