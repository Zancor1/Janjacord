param([string]$ClientPath,[string]$ServerPath)
. (Join-Path $PSScriptRoot 'UiAutomation.ps1')
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class ProfileFilePickerNative {
 delegate bool Callback(IntPtr h,IntPtr p);
 [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr h,Callback c,IntPtr p);
 [DllImport("user32.dll")] static extern int GetDlgCtrlID(IntPtr h);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,StringBuilder text,int max);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,IntPtr process);
 [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
 [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a,uint b,bool attach);
 [DllImport("user32.dll")] static extern IntPtr SetFocus(IntPtr h);
 [DllImport("user32.dll")] static extern IntPtr GetFocus();
 public static bool FocusFileName(IntPtr dialog) {
   IntPtr edit=IntPtr.Zero;EnumChildWindows(dialog,(h,p)=>{var name=new StringBuilder(100);GetClassName(h,name,100);if(name.ToString()=="Edit"&&GetDlgCtrlID(h)==1148){edit=h;return false;}return true;},IntPtr.Zero);
   if(edit==IntPtr.Zero)return false;var current=GetCurrentThreadId();var target=GetWindowThreadProcessId(edit,IntPtr.Zero);bool attached=current!=target&&AttachThreadInput(current,target,true);
   try{SetFocus(edit);return GetFocus()==edit;}finally{if(attached)AttachThreadInput(current,target,false);}
 }
}
"@
if(!$ClientPath){$ClientPath=Join-Path $root 'Release/Client/Client.exe'}
if(!$ServerPath){$ServerPath=Join-Path $root 'Release/Server/Server.exe'}
function Wait-ProfileControl($process,$id){for($i=0;$i -lt 150;$i++){$process.Refresh();if($process.HasExited){throw "Cliente encerrou: $($process.ExitCode)"};if($process.MainWindowHandle -ne 0){$window=[System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle);$control=Find-Control $window $id;if($control){return $control}};Start-Sleep -Milliseconds 200};throw "Controle não apareceu: $id"}
function Select-TestPhoto($process,$owner,$buttonId,$file){
    Invoke-Button (Find-Control $owner $buttonId)
    $picker=Wait-Dialog $process 'Escolha sua foto de perfil'
    $handle=[IntPtr]$picker.Current.NativeWindowHandle
    $null=[ProfileFilePickerNative]::SetForegroundWindow($handle)
    Start-Sleep -Milliseconds 150
    if([ProfileFilePickerNative]::GetForegroundWindow() -ne $handle){throw 'Não foi possível focar o seletor de foto com segurança.'}
    if(![ProfileFilePickerNative]::FocusFileName($handle)){throw 'Não foi possível focar o campo de arquivo no seletor de foto.'}
    [System.Windows.Forms.SendKeys]::SendWait('^a')
    [System.Windows.Forms.SendKeys]::SendWait($file)
    Start-Sleep -Milliseconds 200
    [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
    for($i=0;$i -lt 80;$i++){Start-Sleep -Milliseconds 100;$button=Find-Control $owner $buttonId;if($button -and $button.Current.IsEnabled){return}}
    throw 'A foto não terminou de carregar no seletor.'
}
$previousProfile=$env:JANJACORD_PROFILE_DIR;$previousManual=$env:JANJACORD_MANUAL_CONNECT
$client=$null;$server=$null;$testRoot=Join-Path $root ('.tools/profile-ui-'+[Guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path $testRoot
try{
    foreach($sample in @(@('first.png',[System.Drawing.Color]::SteelBlue),@('second.png',[System.Drawing.Color]::MediumSeaGreen))){$bitmap=New-Object System.Drawing.Bitmap(80,80);$g=[System.Drawing.Graphics]::FromImage($bitmap);$g.Clear($sample[1]);$g.Dispose();$bitmap.Save((Join-Path $testRoot $sample[0]),[System.Drawing.Imaging.ImageFormat]::Png);$bitmap.Dispose()}
    $env:JANJACORD_PROFILE_DIR=Join-Path $testRoot 'profile';$env:JANJACORD_MANUAL_CONNECT='1'
    $data=Join-Path $testRoot 'host';$server=Start-Process -FilePath $ServerPath -ArgumentList @('--port','5499','--data-dir',('"'+$data+'"'),'--noninteractive') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $testRoot 'server.log') -RedirectStandardError (Join-Path $testRoot 'server-error.log')
    $client=Start-Process -FilePath $ClientPath -WindowStyle Hidden -PassThru
    $name='Perfil_'+[Guid]::NewGuid().ToString('N').Substring(0,6)
    Set-Field (Wait-ProfileControl $client 'ProfileName') $name
    $client.Refresh();$window=[System.Windows.Automation.AutomationElement]::FromHandle($client.MainWindowHandle)
    Select-TestPhoto $client $window 'ChooseProfilePhoto' (Join-Path $testRoot 'first.png')
    Invoke-Button (Wait-ProfileControl $client 'CreateProfile')
    $null=Wait-ProfileControl $client 'ServerAddress'
    for($i=0;$i -lt 50;$i++){if(Test-Path (Join-Path $data 'server.pfx')){break};Start-Sleep -Milliseconds 100}
    $cert=New-Object System.Security.Cryptography.X509Certificates.X509Certificate2((Join-Path $data 'server.pfx'));$sha=[Security.Cryptography.SHA256]::Create();$pin=([BitConverter]::ToString($sha.ComputeHash($cert.RawData))).Replace('-','');$sha.Dispose();$cert.Dispose()
    Set-Field (Wait-ProfileControl $client 'ServerAddress') 'https://localhost:5499';Set-Field (Wait-ProfileControl $client 'ServerFingerprint') $pin;Invoke-Button (Wait-ProfileControl $client 'Connect');$null=Wait-ProfileControl $client 'FriendsHome'
    $path=Join-Path $testRoot 'profile/profile.json';$created=Get-Content -Raw -Encoding utf8 $path | ConvertFrom-Json
    if(!$created.Avatar -or !$created.ProtectedKey -or !$created.UserId){throw 'Cadastro inicial não guardou foto/chave/identidade.'}
    Stop-Process -Id $client.Id;$client.WaitForExit();$env:JANJACORD_MANUAL_CONNECT=$null
    $client=Start-Process -FilePath $ClientPath -WindowStyle Hidden -PassThru;$null=Wait-ProfileControl $client 'FriendsHome'
    $restored=Get-Content -Raw -Encoding utf8 $path | ConvertFrom-Json
    if($restored.Name -ne $name -or $restored.Avatar -ne $created.Avatar -or $restored.UserId -ne $created.UserId -or $restored.ProtectedKey -ne $created.ProtectedKey){throw 'O primeiro perfil não foi preservado.'}
    Write-Output 'PASS: nome e foto do primeiro cadastro persistem e o cliente entra sozinho na mesma conta'
    Invoke-Button (Wait-ProfileControl $client 'EditProfileButton');$dialog=Wait-Dialog $client 'Meu perfil'
    $editedName=$name+'_novo';Set-Field (Find-Control $dialog 'EditProfileName') $editedName
    Select-TestPhoto $client $dialog 'ChangeProfilePhoto' (Join-Path $testRoot 'second.png')
    Save-Window $client (Join-Path $root '.tools/ui-profile-edit-main.png')
    Invoke-Button (Find-Control $dialog 'SaveProfileChanges')
    Start-Sleep -Milliseconds 500
    $updated=Get-Content -Raw -Encoding utf8 $path | ConvertFrom-Json
    if($updated.Name -ne $editedName -or !$updated.Avatar -or $updated.Avatar -eq $created.Avatar -or $updated.UserId -ne $created.UserId -or $updated.ProtectedKey -ne $created.ProtectedKey){throw 'Editar nome/foto não manteve a mesma conta.'}
    Invoke-Button (Wait-ProfileControl $client 'EditProfileButton');$dialog=Wait-Dialog $client 'Meu perfil';Invoke-Button (Find-Control $dialog 'RemoveProfilePhoto');Set-Field (Find-Control $dialog 'EditProfileName') 'Nao salvar';Invoke-Button (Find-Control $dialog 'CancelProfileEdit')
    Start-Sleep -Milliseconds 200
    $cancelled=Get-Content -Raw -Encoding utf8 $path | ConvertFrom-Json;if($cancelled.Name -ne $editedName -or $cancelled.Avatar -ne $updated.Avatar){throw 'Cancelar alterou o perfil salvo.'}
    Stop-Process -Id $client.Id;$client.WaitForExit();$client=Start-Process -FilePath $ClientPath -WindowStyle Hidden -PassThru;$null=Wait-ProfileControl $client 'FriendsHome'
    $final=Get-Content -Raw -Encoding utf8 $path | ConvertFrom-Json;if($final.Name -ne $editedName -or $final.Avatar -ne $updated.Avatar -or $final.UserId -ne $created.UserId){throw 'Nome/foto editados sumiram ao reabrir.'}
    Save-Window $client (Join-Path $root '.tools/ui-profile-saved.png')
    Write-Output 'PASS: editar nome/foto pelo seletor real salva sem recriar conta; cancelar não altera; reabrir mantém as alterações'
    Stop-Process -Id $server.Id;$server.WaitForExit();$server=$null
    for($i=0;$i -lt 80;$i++){if((Wait-ProfileControl $client 'ConnectionStatus').Current.Name -match 'offline'){break};Start-Sleep -Milliseconds 100}
    Invoke-Button (Wait-ProfileControl $client 'EditProfileButton');$dialog=Wait-Dialog $client 'Meu perfil'
    Select-TestPhoto $client $dialog 'ChangeProfilePhoto' (Join-Path $testRoot 'first.png')
    $saveOffline=Find-Control $dialog 'SaveProfileChanges';if(!$saveOffline.Current.IsEnabled){throw 'Salvar foto ficou desativado sem rede.'};Invoke-Button $saveOffline
    Start-Sleep -Milliseconds 400
    $offline=Get-Content -Raw -Encoding utf8 $path | ConvertFrom-Json
    if($offline.Avatar -ne $created.Avatar -or !$offline.PendingAvatarSync -or $offline.UserId -ne $created.UserId){throw 'Foto offline não foi salva na mesma conta.'}
    $server=Start-Process -FilePath $ServerPath -ArgumentList @('--port','5499','--data-dir',('"'+$data+'"'),'--noninteractive') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $testRoot 'server-restarted.log') -RedirectStandardError (Join-Path $testRoot 'server-restarted-error.log')
    for($i=0;$i -lt 180;$i++){Start-Sleep -Milliseconds 200;$synced=Get-Content -Raw -Encoding utf8 $path | ConvertFrom-Json;if(!$synced.PendingAvatarSync -and (Wait-ProfileControl $client 'ConnectionStatus').Current.Name -match 'Conectado'){break}}
    if($synced.PendingAvatarSync -or $synced.Avatar -ne $offline.Avatar){throw 'A foto offline não sincronizou após reconectar.'}
    Write-Output 'PASS: salvar foto funciona offline e sincroniza ao reabrir a rede, preservando a conta'
}finally{if($client -and !$client.HasExited){Stop-Process -Id $client.Id};if($server -and !$server.HasExited){Stop-Process -Id $server.Id};$env:JANJACORD_PROFILE_DIR=$previousProfile;$env:JANJACORD_MANUAL_CONNECT=$previousManual}

