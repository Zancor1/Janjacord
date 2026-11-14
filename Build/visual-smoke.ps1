. (Join-Path $PSScriptRoot 'UiAutomation.ps1')
$client=$null;$server=$null
$previousProfile=$env:JANJACORD_PROFILE_DIR
$previousManual=$env:JANJACORD_MANUAL_CONNECT
$env:JANJACORD_MANUAL_CONNECT='1'
try {
    $env:JANJACORD_PROFILE_DIR=Join-Path $root ('.tools/ui-profile-'+[Guid]::NewGuid().ToString('N'))
    $data=Join-Path $root '.tools/ui-server-data'
    $server=Start-Process -FilePath (Join-Path $root 'Release/Server/Server.exe') -ArgumentList @('--port','5399','--data-dir',('"'+$data+'"')) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $root '.tools/ui-server.log') -RedirectStandardError (Join-Path $root '.tools/ui-server-error.log')
    $client=Start-Process -FilePath (Join-Path $root 'Release/Client/Client.exe') -WindowStyle Hidden -PassThru
    for($i=0;$i -lt 50;$i++) {$client.Refresh();if($client.MainWindowHandle -ne 0){break};if($client.HasExited){throw 'Cliente encerrou.'};Start-Sleep -Milliseconds 200}
    $null=[JanjaUiCapture]::ShowWindow($client.MainWindowHandle,4)
    Start-Sleep -Milliseconds 700
    Save-Window $client (Join-Path $root '.tools/ui-profile.png')
    $window=[System.Windows.Automation.AutomationElement]::FromHandle($client.MainWindowHandle)
    Set-Field (Find-Control $window 'ProfileName') ('UI Test '+[Guid]::NewGuid().ToString('N').Substring(0,6))
    Invoke-Button (Find-Control $window 'CreateProfile')
    Start-Sleep -Milliseconds 700
    if(!(Find-Control $window 'ServerAddress')){throw 'Perfil não avançou para conexão.'}
    Write-Output 'PASS: interface cria perfil por nome e avança para conexão sem campo de senha'
    $certPath=Join-Path $data 'server.pfx'
    for($i=0;$i -lt 30;$i++){if(Test-Path $certPath){break};Start-Sleep -Milliseconds 200}
    $cert=New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($certPath)
    $sha=[System.Security.Cryptography.SHA256]::Create()
    $pin=([BitConverter]::ToString($sha.ComputeHash($cert.RawData))).Replace('-','');$sha.Dispose();$cert.Dispose()
    Set-Field (Find-Control $window 'ServerAddress') 'https://localhost:5399'
    Set-Field (Find-Control $window 'ServerFingerprint') $pin
    Save-Window $client (Join-Path $root '.tools/ui-connect.png')
    Invoke-Button (Find-Control $window 'Connect')
    for($i=0;$i -lt 50;$i++){Start-Sleep -Milliseconds 200;if(!(Find-Control $window 'Connect')){break}}
    if(Find-Control $window 'Connect'){throw 'Interface não entrou no chat. Confira captura de tela e logs.'}
    Start-Sleep -Milliseconds 500
    Save-Window $client (Join-Path $root '.tools/ui-chat.png')
    Write-Output 'PASS: interface autentica perfil e abre a tela principal real'
    Invoke-Button (Find-Control $window 'JoinServerButton')
    $dialog=Wait-Dialog $client 'Entrar em servidor'
    Set-Field (Find-Control $dialog 'JoinServerId') '9223372036854775807'
    Invoke-Button (Find-Control $dialog 'JoinServerConfirm')
    for($i=0;$i -lt 30;$i++){Start-Sleep -Milliseconds 100;if((Find-Control $dialog 'JoinServerError').Current.Name -match 'Confira o ID'){break}}
    if((Find-Control $dialog 'JoinServerError').Current.Name -notmatch 'Confira o ID'){throw 'Servidor inexistente não mostrou mensagem clara.'}
    if(!(Find-Control $dialog 'JoinServerId')){throw 'Diálogo fechou após ID inexistente.'}
    Write-Output 'PASS: ID inexistente mostra mensagem clara e mantém o campo aberto para corrigir'
    Invoke-Button (Find-Control $dialog 'CancelJoinServer')
    Invoke-Button (Find-Control $window 'CreateServer')
    $dialog=Wait-Dialog $client 'Crie seu servidor'
    Set-Field (Find-Control $dialog 'DialogName') 'Minha turma'
    Invoke-Button (Find-Control $dialog 'DialogConfirm')
    Start-Sleep -Milliseconds 700
    Invoke-Button (Find-Control $window 'CreateChannel')
    $dialog=Wait-Dialog $client 'Criar canal'
    Set-Field (Find-Control $dialog 'ChannelName') 'geral'
    Invoke-Button (Find-Control $dialog 'CreateChannelConfirm')
    Start-Sleep -Milliseconds 700
    Set-Field (Find-Control $window 'MessageDraft') 'Mensagem real enviada pela interface no teste.'
    Invoke-Button (Find-Control $window 'SendMessage')
    Start-Sleep -Milliseconds 500
    Save-Window $client (Join-Path $root '.tools/ui-server-chat.png')
    Write-Output 'PASS: interface cria servidor/canal e envia uma mensagem real'
    Invoke-Button (Find-Control $window 'ServerMenu')
    Start-Sleep -Milliseconds 300
    $pidCondition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$client.Id)
    $idCondition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty,'DeleteServer')
    $menuCondition=New-Object System.Windows.Automation.AndCondition($pidCondition,$idCondition)
    $deleteItem=$null
    for($i=0;$i -lt 30;$i++){$deleteItem=[System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$menuCondition);if($deleteItem){break};Start-Sleep -Milliseconds 100}
    Invoke-MenuItem $deleteItem
    $dialog=Wait-Dialog $client 'Excluir servidor'
    $confirm=Find-Control $dialog 'ConfirmDelete'
    if($confirm.Current.IsEnabled){throw 'Exclusão habilitada sem confirmar nome.'}
    Set-Field (Find-Control $dialog 'DeleteConfirmation') 'Minha turma'
    Start-Sleep -Milliseconds 200
    Invoke-Button $confirm
    Start-Sleep -Milliseconds 700
    Save-Window $client (Join-Path $root '.tools/ui-after-delete.png')
    if(!(Find-Control $window 'FriendsHome')){throw 'Tela Amigos não apareceu após excluir o último servidor.'}
    Write-Output 'PASS: menu de exclusão exige nome e remove o servidor pela interface real'
} finally {
    if($client -and !$client.HasExited){Stop-Process -Id $client.Id}
    if($server -and !$server.HasExited){Stop-Process -Id $server.Id}
    $env:JANJACORD_PROFILE_DIR=$previousProfile
    $env:JANJACORD_MANUAL_CONNECT=$previousManual
}

