Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class JanjaTrayTest {
 [StructLayout(LayoutKind.Sequential)] struct Point {public int X,Y;}
 [StructLayout(LayoutKind.Sequential)] struct Mouse {public int X,Y;public uint Data,Flags,Time;public UIntPtr Extra;}
 [StructLayout(LayoutKind.Explicit,Size=40)] struct Input {[FieldOffset(0)]public uint Type;[FieldOffset(8)]public Mouse Mouse;}
 [DllImport("user32.dll")] static extern uint SendInput(uint count,Input[] inputs,int size);
 [DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);
 [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point point);
 delegate bool Visitor(IntPtr window,IntPtr param);
 [DllImport("user32.dll")] static extern bool EnumWindows(Visitor visitor,IntPtr param);
 [DllImport("user32.dll")] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
 [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
 public static void ClickMenu(int process,int x,int y){
  uint owner;GetWindowThreadProcessId(WindowFromPoint(new Point{X=x,Y=y}),out owner);
  if(owner!=process)throw new Exception("Test menu is not visible; click cancelled");
  Point before;GetCursorPos(out before);
  try{SetCursorPos(x,y);var input=new[]{new Input{Mouse=new Mouse{Flags=2}},new Input{Mouse=new Mouse{Flags=4}}};if(SendInput(2,input,40)!=2)throw new Exception("Menu click failed");}
  finally{SetCursorPos(before.X,before.Y);}
 }
 public static void Tray(int process,int message,bool menu) {
  var handles=new HashSet<IntPtr>();
  EnumWindows((h,p)=>{uint owner;GetWindowThreadProcessId(h,out owner);if(owner==process)handles.Add(h);return true;},IntPtr.Zero);
  IntPtr current=IntPtr.Zero;
  while((current=FindWindowEx(new IntPtr(-3),current,null,null))!=IntPtr.Zero){uint owner;GetWindowThreadProcessId(current,out owner);if(owner==process)handles.Add(current);}
  foreach(var handle in handles)PostMessage(handle,(uint)message,new IntPtr(1),new IntPtr(menu?517:514));
 }
}
'@
function Wait-GroupControl($process){
    for($i=0;$i -lt 100;$i++){
        try{
            $window=Window-Of $process
            $condition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button)
            foreach($control in $window.FindAll([System.Windows.Automation.TreeScope]::Descendants,$condition)){if($control.Current.AutomationId -match '^DirectGroup_\d+$'){return $control}}
        }catch [System.Runtime.InteropServices.COMException]{}catch [System.Windows.Automation.ElementNotAvailableException]{}
        Start-Sleep -Milliseconds 100
    };throw 'Group missing in DM sidebar'
}
function Wait-TrayExit($process){
    $pidCondition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$process.Id)
    $nameCondition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,'Sair do JanjaCord')
    $condition=New-Object System.Windows.Automation.AndCondition($pidCondition,$nameCondition)
    for($i=0;$i -lt 60;$i++){$item=[System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition);if($item){return $item};Start-Sleep -Milliseconds 100};throw 'Tray Exit menu did not appear'
}
function Test-GroupTray($a,$b,$testRoot){
    Invoke-Button (Wait-Control $a 'CreateDirectGroup');$dialog=Wait-Dialog $a 'Criar grupo'
    Set-Field (Find-Control $dialog 'DirectGroupName') 'Turma do teste'
    Invoke-Button (Find-Control $dialog 'ConfirmCreateGroup')
    $groupA=Wait-GroupControl $a;$groupB=Wait-GroupControl $b
    $null=Wait-Control $a 'MessageDraft';Set-Field (Wait-Control $a 'MessageDraft') 'Mensagem do grupo fica salva';Invoke-Button (Wait-Control $a 'SendMessage')
    Invoke-Button $groupB
    for($i=0;$i -lt 60;$i++){if(Has-Text $b 'Mensagem do grupo fica salva'){break};Start-Sleep -Milliseconds 100}
    if(!(Has-Text $b 'Mensagem do grupo fica salva')){throw 'Group message not received'}
    Set-Field (Wait-Control $b 'MessageDraft') 'Resposta no grupo';Invoke-Button (Wait-Control $b 'SendMessage')
    for($i=0;$i -lt 60;$i++){if(Has-Text $a 'Resposta no grupo'){break};Start-Sleep -Milliseconds 100}
    if(!(Has-Text $a 'Resposta no grupo')){throw 'Group response not received'}
    Save-Window $a (Join-Path $root '.tools/ui-group.png')
    Write-Output 'PASS: DM button opens friend picker, creates group and exchanges saved messages between two clients'
    $identityA=Get-Content -Raw -Encoding utf8 (Join-Path $testRoot 'a/profile.json') | ConvertFrom-Json
    Invoke-Button (Wait-Control $a 'StartDirectCall');$ring=Wait-Dialog $b 'Chamada do grupo';Invoke-Button (Find-Control $ring 'RejectGroupCall')
    Start-Sleep -Milliseconds 300
    Save-Window $b (Join-Path $root '.tools/ui-observer-debug.png')
    $join=Wait-Control $b 'JoinConversationCall'
    $observerMic=Find-Control (Window-Of $b) 'CallMicrophone'
    if(!$join.Current.IsEnabled -or ($observerMic -and !$observerMic.Current.IsOffscreen -and $observerMic.Current.IsEnabled)){throw 'Observer should see join action without starting microphone'}
    $null=Wait-Control $b ('VoiceParticipant_'+$identityA.UserId)
    Save-Window $b (Join-Path $root '.tools/ui-ongoing-group-call.png')
    Invoke-Button (Wait-Control $b ('DirectPeer_'+$identityA.UserId))
    for($i=0;$i -lt 60;$i++){if((Wait-Control $b 'ConversationCallStatus').Current.Name -match 'outra chamada'){break};Start-Sleep -Milliseconds 100}
    if((Wait-Control $b 'ConversationCallStatus').Current.Name -notmatch 'outra chamada'){throw 'DM did not indicate friend in another call'}
    Write-Output 'PASS: group observer sees actual participants and Join; DM indicates friend in another call without opening media'
    Invoke-Button (Wait-GroupControl $b);Invoke-Button (Wait-Control $b 'JoinConversationCall')
    for($i=0;$i -lt 80;$i++){if((Wait-Control $b 'CallHangup').Current.IsEnabled){break};Start-Sleep -Milliseconds 100}
    Invoke-Button (Wait-Control $b 'CallHangup')
    for($i=0;$i -lt 60;$i++){if((Wait-Control $b 'JoinConversationCall').Current.IsEnabled){break};Start-Sleep -Milliseconds 100}
    $null=Wait-Control $b ('VoiceParticipant_'+$identityA.UserId)
    Invoke-Button (Wait-Control $a 'CallHangup')
    Write-Output 'PASS: group call invitation and join/leave work in actual clients'
    $identityA=Get-Content -Raw -Encoding utf8 (Join-Path $testRoot 'a/profile.json') | ConvertFrom-Json
    $identityB=Get-Content -Raw -Encoding utf8 (Join-Path $testRoot 'b/profile.json') | ConvertFrom-Json
    Invoke-Button (Wait-Control $a ('DirectPeer_'+$identityB.UserId));Invoke-Button (Wait-Control $b ('DirectPeer_'+$identityA.UserId))
    $a.Refresh();$handle=$a.MainWindowHandle
    (Window-Of $a).GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close();Start-Sleep -Milliseconds 500
    $a.Refresh();if($a.HasExited -or [JanjaTrayTest]::IsWindowVisible($handle)){throw 'X did not keep app hidden in background'}
    Write-Output 'PASS: X hides main window while process and host remain alive'
    Invoke-Button (Wait-Control $b 'StartDirectCall');$ring=Wait-Dialog $a 'Chamada recebida';Invoke-Button (Find-Control $ring 'RejectDirectCall')
    if(![JanjaTrayTest]::IsWindowVisible($handle)){throw 'Incoming call did not restore hidden app'}
    Write-Output 'PASS: incoming call restores the app from background and rings'
    $trayMessage=[int](& (Join-Path $root '.tools/dotnet/dotnet.exe') (Join-Path $root 'Tests/bin/Release/net10.0/Tests.dll') --tray-message)
    (Window-Of $a).GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close();Start-Sleep -Milliseconds 300
    [JanjaTrayTest]::Tray($a.Id,$trayMessage,$false);Start-Sleep -Milliseconds 400
    if(![JanjaTrayTest]::IsWindowVisible($handle)){throw 'Tray click did not restore app'}
    [JanjaTrayTest]::Tray($a.Id,$trayMessage,$true);$exit=Wait-TrayExit $a
    try{$exit.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()}
    catch [System.InvalidOperationException]{$bounds=$exit.Current.BoundingRectangle;[JanjaTrayTest]::ClickMenu($a.Id,[int]($bounds.X+$bounds.Width/2),[int]($bounds.Y+$bounds.Height/2))}
    if(!$a.WaitForExit(15000)){throw 'Tray Exit did not terminate client'}
    Write-Output 'PASS: tray click opens app; real Exit menu terminates process'
}
