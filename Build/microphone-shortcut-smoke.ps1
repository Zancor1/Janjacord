param([string]$ClientPath)
. (Join-Path $PSScriptRoot 'UiAutomation.ps1')
. (Join-Path $PSScriptRoot 'CallInteractions.ps1')
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class JanjaMicContext {
 [StructLayout(LayoutKind.Sequential)] public struct Point {public int X,Y;}
 [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr window,ref Point point);
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr keys,IntPtr point);
 public static void Open(IntPtr window,int x,int y) {
  var p=new Point{X=x,Y=y};if(!ScreenToClient(window,ref p))throw new Exception("Invalid test window");
  var position=new IntPtr((p.Y<<16)|(p.X&65535));
  SendMessage(window,512,IntPtr.Zero,position);
  SendMessage(window,516,new IntPtr(2),position);
  SendMessage(window,517,IntPtr.Zero,position);
 }
}
'@
if(!$ClientPath){$ClientPath=Join-Path $root 'Release/Client/Client.exe'}
$previous=@{}
foreach($key in @('JANJACORD_PROFILE_DIR','JANJACORD_NETWORK_DIR','JANJACORD_DISABLE_IMPORT','JANJACORD_DISABLE_DISCOVERY','JANJACORD_MANUAL_CONNECT')){$previous[$key]=[Environment]::GetEnvironmentVariable($key)}
$process=$null
try {
    $testRoot=Join-Path $root ('.tools/mic-shortcut-'+[Guid]::NewGuid().ToString('N'))
    $env:JANJACORD_PROFILE_DIR=Join-Path $testRoot 'profile'
    $env:JANJACORD_NETWORK_DIR=Join-Path $testRoot 'network'
    $env:JANJACORD_DISABLE_DISCOVERY='1';$env:JANJACORD_DISABLE_IMPORT='1';$env:JANJACORD_MANUAL_CONNECT=$null
    $process=Start-Process -FilePath $ClientPath -WindowStyle Hidden -PassThru
    Set-Field (Wait-ProcessControl $process 'ProfileName') ('Mic_'+[Guid]::NewGuid().ToString('N').Substring(0,6))
    Invoke-Button (Wait-ProcessControl $process 'CreateProfile')
    $null=Wait-ProcessControl $process 'FriendsHome'
    $mic=Wait-ProcessControl $process 'MicrophoneButton'
    if(!$mic.Current.IsEnabled){throw 'Microphone shortcut disabled outside call'}
    Start-Sleep -Milliseconds 500
    $rect=$mic.Current.BoundingRectangle
    [JanjaMicContext]::Open($process.MainWindowHandle,[int]($rect.X+$rect.Width/2),[int]($rect.Y+$rect.Height/2))
    try{$dialog=Wait-Dialog $process ('Dispositivos de '+[char]0xe1+'udio')}
    catch{
        $mic=Wait-ProcessControl $process 'MicrophoneButton';Start-Sleep -Milliseconds 300;$rect=$mic.Current.BoundingRectangle
        [JanjaMicContext]::Open($process.MainWindowHandle,[int]($rect.X+$rect.Width/2),[int]($rect.Y+$rect.Height/2))
        $dialog=Wait-Dialog $process ('Dispositivos de '+[char]0xe1+'udio')
    }
    if(!(Find-Control $dialog 'NoiseSuppressionStrength')){throw 'Shared audio settings missing'}
    $model=Find-Control $dialog 'NoiseSuppressionModel'
    if(!$model){throw 'Neural model selector missing'}
    $initialProfile=Get-Content -Raw -Encoding utf8 (Join-Path $testRoot 'profile/profile.json') | ConvertFrom-Json
    if($initialProfile.NoiseSuppressionAdvanced -ne $true){throw 'Advanced model not selected by default'}
    Invoke-Button (Find-Control $dialog 'TestMicrophone')
    for($i=0;$i -lt 50;$i++){Start-Sleep -Milliseconds 100;if((Find-Control $dialog 'MicrophoneTestStatus').Current.Name -match 'ativo'){break}}
    if((Find-Control $dialog 'MicrophoneTestStatus').Current.Name -notmatch 'ativo'){throw 'Local microphone test did not start outside call'}
    Invoke-Button (Find-Control $dialog 'TestMicrophone')
    for($i=0;$i -lt 50;$i++){Start-Sleep -Milliseconds 100;if((Find-Control $dialog 'MicrophoneTestStatus').Current.Name -eq 'Teste parado'){break}}
    if((Find-Control $dialog 'MicrophoneTestStatus').Current.Name -ne 'Teste parado'){throw 'Microphone test did not stop'}
    $expand=$model.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern);$expand.Expand();Start-Sleep -Milliseconds 200
    $condition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::ListItem)
    $items=$dialog.FindAll([System.Windows.Automation.TreeScope]::Descendants,$condition)
    if($items.Count -ne 2){throw 'Both neural models must be available'}
    $items[1].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select();$expand.Collapse()
    Start-Sleep -Milliseconds 500
    $saved=Get-Content -Raw -Encoding utf8 (Join-Path $testRoot 'profile/profile.json') | ConvertFrom-Json
    if($saved.NoiseSuppressionAdvanced -ne $false){throw 'Model preference was not saved'}
    Invoke-Button (Find-Control $dialog 'CloseAudioSettings')
    $null=Wait-ProcessControl $process 'FriendsHome'
    Write-Output 'PASS: right click outside call; DeepFilterNet selected; local test starts/stops; RNNoise alternative selected and saved'
} finally {
    if($process -and !$process.HasExited){Stop-Process -Id $process.Id -Force}
    foreach($key in $previous.Keys){[Environment]::SetEnvironmentVariable($key,$previous[$key])}
}
