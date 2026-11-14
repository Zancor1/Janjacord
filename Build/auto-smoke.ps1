param([string]$ClientPath,[switch]$QuickCall,[switch]$OfflineOnly,[switch]$NoiseOnly,[switch]$GroupTrayOnly,[switch]$ScreenFpsOnly)
. (Join-Path $PSScriptRoot 'UiAutomation.ps1')
. (Join-Path $PSScriptRoot 'CallInteractions.ps1')
. (Join-Path $PSScriptRoot 'GroupTrayInteractions.ps1')
. (Join-Path $PSScriptRoot 'ScreenFpsInteractions.ps1')
if(!$ClientPath){$ClientPath=Join-Path $root 'Release/Client/Client.exe'}
function Wait-Control($process,$id,[int]$seconds=30){
    for($i=0;$i -lt $seconds*5;$i++){$process.Refresh();if($process.HasExited){throw "Cliente encerrou: $($process.ExitCode)"};if($process.MainWindowHandle -ne 0){$window=[System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle);$control=Find-Control $window $id;if($control){return $control}};Start-Sleep -Milliseconds 200};throw "Controle não apareceu: $id"
}
function Window-Of($process){$process.Refresh();return [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)}
function Has-Text($process,$text){$condition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,$text);return (Window-Of $process).FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)}
function Select-PhysicalDevice($combo,$dialog){
    $expand=$combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern);$expand.Expand();Start-Sleep -Milliseconds 200
    $condition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::ListItem)
    $items=$dialog.FindAll([System.Windows.Automation.TreeScope]::Descendants,$condition)
    if($items.Count -lt 2){throw 'Dispositivos reais não apareceram na lista.'}
    $items[1].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select();$expand.Collapse()
}
$a=$null;$b=$null
$previousProfile=$env:JANJACORD_PROFILE_DIR;$previousNetwork=$env:JANJACORD_NETWORK_DIR;$previousManual=$env:JANJACORD_MANUAL_CONNECT;$previousDiscovery=$env:JANJACORD_DISABLE_DISCOVERY;$previousImport=$env:JANJACORD_DISABLE_IMPORT
$testRoot=Join-Path $root ('.tools/auto-ui-'+[Guid]::NewGuid().ToString('N'))
$nameA='Alice_'+[Guid]::NewGuid().ToString('N').Substring(0,6);$nameB='Bob_'+[Guid]::NewGuid().ToString('N').Substring(0,6)
try{
    $env:JANJACORD_MANUAL_CONNECT=$null;$env:JANJACORD_DISABLE_IMPORT='1';$env:JANJACORD_DISABLE_DISCOVERY='1';$env:JANJACORD_NETWORK_DIR=Join-Path $testRoot 'shared'
    $env:JANJACORD_PROFILE_DIR=Join-Path $testRoot 'a'
    $a=Start-Process -FilePath $ClientPath -WindowStyle Hidden -PassThru -RedirectStandardError (Join-Path $root '.tools/auto-ui-a-error.log')
    Set-Field (Wait-Control $a 'ProfileName') $nameA;Invoke-Button (Wait-Control $a 'CreateProfile')
    $null=Wait-Control $a 'FriendsHome';Write-Output 'PASS: Client.exe inicia sua rede e abre Amigos sem abrir Server.exe'
    Save-Window $a (Join-Path $root '.tools/ui-friends.png')
    $env:JANJACORD_PROFILE_DIR=Join-Path $testRoot 'b'
    $b=Start-Process -FilePath $ClientPath -WindowStyle Hidden -PassThru -RedirectStandardError (Join-Path $root '.tools/auto-ui-b-error.log')
    Set-Field (Wait-Control $b 'ProfileName') $nameB;Invoke-Button (Wait-Control $b 'CreateProfile');$null=Wait-Control $b 'FriendsHome'
    $nameA=(Get-Content -Raw -Encoding utf8 (Join-Path $testRoot 'a/profile.json') | ConvertFrom-Json).Name
    $nameB=(Get-Content -Raw -Encoding utf8 (Join-Path $testRoot 'b/profile.json') | ConvertFrom-Json).Name
    Invoke-Button (Wait-Control $a 'AddFriendButton');$dialog=Wait-Dialog $a 'Adicionar amigos';Set-Field (Find-Control $dialog 'FriendName') $nameB;Invoke-Button (Find-Control $dialog 'SendFriendRequest')
    $null=Wait-Control $a 'MessageDraft'
    $identityA=Get-Content -Raw -Encoding utf8 (Join-Path $testRoot 'a/profile.json') | ConvertFrom-Json
    Invoke-Button (Wait-Control $b 'FriendsTab_pending');Invoke-Button (Wait-Control $b ('AcceptFriend_'+$identityA.UserId))
    for($i=0;$i -lt 40;$i++){if((Wait-Control $a 'MessageDraft').Current.IsEnabled){break};Start-Sleep -Milliseconds 200}
    $message='DM real enviada entre dois Client.exe no teste'
    Set-Field (Wait-Control $a 'MessageDraft') $message;Invoke-Button (Wait-Control $a 'SendMessage')
    for($i=0;$i -lt 40;$i++){if(Has-Text $b $message){break};Start-Sleep -Milliseconds 200}
    if(!(Has-Text $b $message)){throw 'A mensagem não apareceu no outro cliente.'}
    Save-Window $b (Join-Path $root '.tools/ui-dm.png');Write-Output 'PASS: Adicionar amigos, aceitar e trocar DM funcionam na interface de dois clientes'
    if($ScreenFpsOnly){Test-ScreenFps $a $b $testRoot;return}
    if($GroupTrayOnly){Test-GroupTray $a $b $testRoot;return}
    if(!$OfflineOnly){
    if(!$QuickCall){for($n=1;$n -le 65;$n++){
        Set-Field (Wait-Control $a 'MessageDraft') ("Mensagem de rolagem "+$n);Invoke-Button (Wait-Control $a 'SendMessage')
        for($i=0;$i -lt 50;$i++){if([string]::IsNullOrEmpty((Wait-Control $a 'MessageDraft').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value)){break};Start-Sleep -Milliseconds 40}
    }
    for($i=0;$i -lt 40;$i++){if(Has-Text $b 'Mensagem de rolagem 65'){break};Start-Sleep -Milliseconds 100}
    if(!(Has-Text $b 'Mensagem de rolagem 65')){throw 'DM não resistiu à rolagem com múltiplas mensagens.'}
    Write-Output 'PASS: 65 DMs seguidas e reciclagem da lista nos dois executáveis sem crash'
    }
    Invoke-Button (Wait-Control $a 'StartDirectCall');$ring=Wait-Dialog $b 'Chamada recebida';Invoke-Button (Find-Control $ring 'RejectDirectCall')
    Start-Sleep -Milliseconds 600
    Invoke-Button (Wait-Control $a 'StartDirectCall');$ring=Wait-Dialog $b 'Chamada recebida';Invoke-Button (Find-Control $ring 'AcceptDirectCall')
    for($i=0;$i -lt 100;$i++){if((Wait-Control $a 'CallMicrophone').Current.IsEnabled -and (Wait-Control $b 'CallMicrophone').Current.IsEnabled){break};Start-Sleep -Milliseconds 100}
    Invoke-Button (Wait-Control $a 'CallMicrophone');Invoke-Button (Wait-Control $b 'CallMicrophone')
    Start-Sleep -Milliseconds 600
    Invoke-Button (Wait-Control $a 'CallAudioDevices');$devices=Wait-Dialog $a 'Dispositivos de áudio'
    if(!(Find-Control $devices 'AudioInputDevice') -or !(Find-Control $devices 'AudioOutputDevice')){throw 'Seletores de dispositivo ausentes.'}
    $suppression=Find-Control $devices 'NoiseSuppressionToggle'
    if(!$suppression){throw 'Botão do supressor não apareceu nos três pontos.'}
    $toggle=$suppression.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    $toggle.Toggle()
    for($i=0;$i -lt 50;$i++){Start-Sleep -Milliseconds 100;if($suppression.Current.IsEnabled -and (Find-Control $devices 'NoiseSuppressionStatus').Current.Name -eq 'Desativado'){break}}
    $noisePreference=Get-Content -Raw -Encoding utf8 (Join-Path $testRoot 'a/profile.json') | ConvertFrom-Json
    if($noisePreference.NoiseSuppressionEnabled -ne $false){throw 'Desligar supressor não foi salvo.'}
    $toggle.Toggle()
    for($i=0;$i -lt 50;$i++){Start-Sleep -Milliseconds 100;if($suppression.Current.IsEnabled -and (Find-Control $devices 'NoiseSuppressionStatus').Current.Name -eq 'Ativado'){break}}
    $noisePreference=Get-Content -Raw -Encoding utf8 (Join-Path $testRoot 'a/profile.json') | ConvertFrom-Json
    if($noisePreference.NoiseSuppressionEnabled -ne $true){throw 'Ligar supressor não foi salvo.'}
    Write-Output 'PASS: supressor liga/desliga nos três pontos durante a chamada e salva a preferência imediatamente'
    $strengthSlider=Find-Control $devices 'NoiseSuppressionStrength'
    $noiseRange=$strengthSlider.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
    $noiseRange.SetValue(0)
    for($i=0;$i -lt 50;$i++){Start-Sleep -Milliseconds 100;if((Find-Control $devices 'NoiseSuppressionStatus').Current.Name -eq 'Sem supressão (0)'){break}}
    if((Find-Control $devices 'NoiseSuppressionStatus').Current.Name -ne 'Sem supressão (0)'){throw 'Intensidade0 não desativou o efeito.'}
    Invoke-Button (Find-Control $devices 'TestMicrophone')
    for($i=0;$i -lt 50;$i++){Start-Sleep -Milliseconds 100;if((Find-Control $devices 'MicrophoneTestStatus').Current.Name -match 'ativo'){break}}
    if((Find-Control $devices 'MicrophoneTestStatus').Current.Name -notmatch 'ativo'){throw 'Teste de microfone não iniciou.'}
    $noiseRange.SetValue(1);Start-Sleep -Milliseconds 300
    $noiseRange.SetValue(.42);Start-Sleep -Milliseconds 300
    Invoke-Button (Find-Control $devices 'TestMicrophone')
    for($i=0;$i -lt 50;$i++){Start-Sleep -Milliseconds 100;if((Find-Control $devices 'MicrophoneTestStatus').Current.Name -eq 'Teste parado'){break}}
    if((Find-Control $devices 'MicrophoneTestStatus').Current.Name -ne 'Teste parado'){throw 'Teste de microfone não parou.'}
    $noisePreference=Get-Content -Raw -Encoding utf8 (Join-Path $testRoot 'a/profile.json') | ConvertFrom-Json
    if([Math]::Abs($noisePreference.NoiseSuppressionStrength-.42) -gt .001){throw 'Intensidade não foi salva.'}
    Write-Output 'PASS: intensidade0..1 funciona durante retorno local, teste inicia/para e preferência0.42 fica salva'
    Invoke-Button (Find-Control $devices 'TestMicrophone')
    for($i=0;$i -lt 50;$i++){Start-Sleep -Milliseconds 100;if((Find-Control $devices 'MicrophoneTestStatus').Current.Name -match 'ativo'){break}}
    Invoke-Button (Find-Control $devices 'CloseAudioSettings')
    for($i=0;$i -lt 50;$i++){Start-Sleep -Milliseconds 100;if((Wait-Control $a 'CallAudioDevices').Current.IsEnabled){break}}
    Invoke-Button (Wait-Control $a 'CallAudioDevices');$devices=Wait-Dialog $a 'Dispositivos de áudio'
    $restoredRange=(Find-Control $devices 'NoiseSuppressionStrength').GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
    if([Math]::Abs($restoredRange.Current.Value-.42) -gt .001 -or (Find-Control $devices 'MicrophoneTestStatus').Current.Name -ne 'Teste parado'){throw 'Fechar não parou o teste ou perdeu a intensidade.'}
    Write-Output 'PASS: fechar encerra o retorno e reabrir mantém a intensidade sem iniciar teste automaticamente'
    Select-PhysicalDevice (Find-Control $devices 'AudioInputDevice') $devices
    Select-PhysicalDevice (Find-Control $devices 'AudioOutputDevice') $devices
    Invoke-Button (Find-Control $devices 'ApplyAudioDevices');Start-Sleep -Milliseconds 500
    $savedAudio=Get-Content -Raw -Encoding utf8 (Join-Path $testRoot 'a/profile.json') | ConvertFrom-Json
    if(!$savedAudio.InputDeviceId -or !$savedAudio.OutputDeviceId){throw 'A seleção de áudio não foi salva no perfil.'}
    if($NoiseOnly){
        Invoke-Button (Wait-Control $a 'CallHangup')
        for($i=0;$i -lt 60;$i++){if(!(Find-Control (Window-Of $b) 'CallHangup')){break};Start-Sleep -Milliseconds 100}
        if(Find-Control (Window-Of $b) 'CallHangup'){throw 'Call nao encerrou apos testar microfone.'}
        Write-Output 'PASS: controles de intensidade/retorno no cliente real e encerramento da chamada'
        return
    }
    Save-Window $b (Join-Path $root '.tools/ui-direct-call.png')
    $person=Wait-Control $b ('VoiceParticipant_'+$identityA.UserId);$bounds=$person.Current.BoundingRectangle
    Send-Point $b ($bounds.X+$bounds.Width/2) ($bounds.Y-38) 'right'
    $volumeSlider=Wait-ProcessControl $b ('PeerVolumeSlider_'+$identityA.UserId)
    $range=$volumeSlider.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern);$range.SetValue(175)
    Start-Sleep -Milliseconds 200
    if((Wait-ProcessControl $b 'PeerVolumeValue').Current.Name -ne '175%'){throw 'Volume individual não atualizou.'}
    Escape-Control $volumeSlider;Start-Sleep -Milliseconds 300
    $personalVolume=Get-Content -Raw -Encoding utf8 (Join-Path $testRoot 'b/profile.json') | ConvertFrom-Json
    $volumeKey=$personalVolume.Fingerprint+':'+$identityA.UserId
    if($personalVolume.PeerVolumes.$volumeKey -ne 175){throw 'Volume pessoal não foi salvo.'}
    Write-Output 'PASS: clique direito no avatar abre volume individual e salva 175%'
    Invoke-Button (Wait-Control $a 'CallScreenShare');$shareDialog=Wait-Dialog $a 'Compartilhar tela';Invoke-Button (Find-Control $shareDialog 'StartScreenShare')
    $null=Wait-Control $b ('ScreenParticipant_'+$identityA.UserId)
    $null=Wait-Control $b ('VoiceParticipant_'+$identityA.UserId)
    $preview=Wait-Control $b 'VideoPreview';$bounds=$preview.Current.BoundingRectangle
    Send-Point $b ($bounds.X+$bounds.Width/2) ($bounds.Y+$bounds.Height/2) 'double'
    $closeFull=Wait-ProcessControl $b 'CloseFullscreenVideo'
    Escape-Control $closeFull;Start-Sleep -Milliseconds 250
    $null=Wait-Control $b 'MessageDraft'
    Write-Output 'PASS: dois cliques abrem transmissão ao vivo em tela cheia e Esc retorna à DM'
    for($i=0;$i -lt 60;$i++){if((Wait-Control $a 'CallScreenShare').Current.IsEnabled){break};Start-Sleep -Milliseconds 100}
    Invoke-Button (Wait-Control $a 'CallScreenShare')
    for($i=0;$i -lt 60;$i++){if(!(Find-Control (Window-Of $b) ('ScreenParticipant_'+$identityA.UserId))){break};Start-Sleep -Milliseconds 100}
    if(Find-Control (Window-Of $b) ('ScreenParticipant_'+$identityA.UserId)){throw 'O participante de transmissão ficou preso após parar a tela.'}
    Write-Output 'PASS: tela aparece como participante separado da pessoa e desaparece ao parar'
    Invoke-Button (Wait-Control $a 'CallHangup')
    for($i=0;$i -lt 60;$i++){if(!(Find-Control (Window-Of $b) 'CallHangup')){break};Start-Sleep -Milliseconds 100}
    if(Find-Control (Window-Of $b) 'CallHangup'){throw 'Desligar não encerrou a chamada no outro cliente.'}
    Write-Output 'PASS: ligar/recusar/atender na DM, controles compactos, menu de áudio e desligar nos dois Client.exe'
    if($QuickCall){return}
    }
    Stop-Process -Id $b.Id;$b.WaitForExit()
    $b=Start-Process -FilePath $ClientPath -WindowStyle Hidden -PassThru
    $null=Wait-Control $b 'FriendsHome';Invoke-Button (Wait-Control $b ('DirectPeer_'+$identityA.UserId))
    for($i=0;$i -lt 30;$i++){if(Has-Text $b $message){break};Start-Sleep -Milliseconds 200}
    if(!(Has-Text $b $message)){throw 'Histórico sumiu após reabrir o cliente.'};Write-Output 'PASS: reabrir Client.exe entra automaticamente e conserva a DM'
    Stop-Process -Id $a.Id;$a.WaitForExit();$a=$null
    Stop-Process -Id $b.Id;$b.WaitForExit()
    $b=Start-Process -FilePath $ClientPath -WindowStyle Hidden -PassThru
    $null=Wait-Control $b 'FriendsHome';Invoke-Button (Wait-Control $b ('DirectPeer_'+$identityA.UserId))
    Start-Sleep -Milliseconds 400
    if(!(Has-Text $b $message)){throw 'Cache offline não conservou a DM.'}
    if(!(Wait-Control $b 'MessageDraft').Current.IsEnabled){throw 'Fila de mensagens offline ficou desativada.'}
    $queued='Mensagem enviada enquanto o host estava offline'
    Set-Field (Wait-Control $b 'MessageDraft') $queued;Invoke-Button (Wait-Control $b 'SendMessage');Start-Sleep -Milliseconds 300
    if(!(Has-Text $b $queued)){throw 'Mensagem pendente não apareceu.'}
    Stop-Process -Id $b.Id;$b.WaitForExit();$b=Start-Process -FilePath $ClientPath -WindowStyle Hidden -PassThru
    $null=Wait-Control $b 'FriendsHome';Invoke-Button (Wait-Control $b ('DirectPeer_'+$identityA.UserId));Start-Sleep -Milliseconds 300
    if(!(Has-Text $b $queued)){throw 'Fila offline não sobreviveu ao reinício.'}
    Save-Window $b (Join-Path $root '.tools/ui-offline-dm.png');Write-Output 'PASS: com host fechado, DM salva continua visível no cache protegido do cliente'
    $env:JANJACORD_PROFILE_DIR=Join-Path $testRoot 'a'
    $a=Start-Process -FilePath $ClientPath -WindowStyle Hidden -PassThru
    $null=Wait-Control $a 'FriendsHome'
    for($i=0;$i -lt 180;$i++){try{if((Wait-Control $b 'ConnectionStatus').Current.Name -match 'Conectado'){break}}catch [System.Runtime.InteropServices.COMException]{}catch [System.Windows.Automation.ElementNotAvailableException]{};Start-Sleep -Milliseconds 200}
    if((Wait-Control $b 'ConnectionStatus').Current.Name -notmatch 'Conectado'){throw 'Cliente offline não retomou conexão com o host reaberto.'}
    Invoke-Button (Wait-Control $b ('DirectPeer_'+$identityA.UserId))
    Start-Sleep -Milliseconds 400
    if(!(Has-Text $b $message)){throw 'Histórico não retornou após reconectar ao host.'}
    Write-Output 'PASS: reabrir o cliente que hospeda restaura a rede e o outro cliente reconecta com o histórico'
    $identityB=Get-Content -Raw -Encoding utf8 (Join-Path $testRoot 'b/profile.json') | ConvertFrom-Json
    Invoke-Button (Wait-Control $a ('DirectPeer_'+$identityB.UserId))
    for($i=0;$i -lt 100;$i++){if(Has-Text $a $queued){break};Start-Sleep -Milliseconds 100}
    if(!(Has-Text $a $queued)){throw 'A mensagem em fila não chegou ao amigo quando o host voltou.'}
    Write-Output 'PASS: DM enviada sem host persiste ao fechar/reabrir e é entregue automaticamente ao amigo quando volta'
}finally{
    if($b -and !$b.HasExited){Stop-Process -Id $b.Id};if($a -and !$a.HasExited){Stop-Process -Id $a.Id}
    $env:JANJACORD_PROFILE_DIR=$previousProfile;$env:JANJACORD_NETWORK_DIR=$previousNetwork;$env:JANJACORD_MANUAL_CONNECT=$previousManual;$env:JANJACORD_DISABLE_IMPORT=$previousImport;$env:JANJACORD_DISABLE_DISCOVERY=$previousDiscovery
}
