Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Threading;
public static class JanjaCallMouse {
 [StructLayout(LayoutKind.Sequential)] public struct Point {public int X,Y;}
 [StructLayout(LayoutKind.Sequential)] public struct Mouse {public int X,Y;public uint Data,Flags,Time;public UIntPtr Extra;}
 [StructLayout(LayoutKind.Explicit,Size=40)] public struct Input {[FieldOffset(0)]public uint Type;[FieldOffset(8)]public Mouse Mouse;}
 [DllImport("user32.dll")] static extern uint SendInput(uint count,Input[] inputs,int size);
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);
 [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point point);
 [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr window,uint flag);
 public static void Click(IntPtr window,int x,int y,bool right){
   Point before;GetCursorPos(out before);SetForegroundWindow(window);Thread.Sleep(150);
   if(GetForegroundWindow()!=window)throw new Exception("Não foi possível focar o cliente de teste com segurança.");
   var point=new Point{X=x,Y=y};if(GetAncestor(WindowFromPoint(point),2)!=window)throw new Exception("O vídeo não está visível na posição indicada; clique cancelado.");
   try{SetCursorPos(x,y);Thread.Sleep(80);var clicks=new[]{new Input{Mouse=new Mouse{Flags=right?8u:2u}},new Input{Mouse=new Mouse{Flags=right?16u:4u}}};
     if(SendInput(2,clicks,40)!=2)throw new Exception("Clique não enviado.");if(right)return;Thread.Sleep(100);
     if(GetForegroundWindow()!=window)throw new Exception("O foco mudou antes do segundo clique.");
     if(SendInput(2,clicks,40)!=2)throw new Exception("Segundo clique não enviado.");
   }finally{SetCursorPos(before.X,before.Y);}
 }
}
'@
function Send-Point($process,$x,$y,$kind){
    $process.Refresh();$rect=New-Object JanjaUiCapture+Rect;$null=[JanjaUiCapture]::GetWindowRect($process.MainWindowHandle,[ref]$rect)
    $px=[int]($x-$rect.L);$py=[int]($y-$rect.T);$point=[IntPtr](($py -shl 16) -bor ($px -band 65535));$handle=$process.MainWindowHandle
    $null=[JanjaUiCapture]::SendMessage($handle,512,[IntPtr]::Zero,$point)
    [JanjaCallMouse]::Click($handle,[int]$x,[int]$y,($kind -eq 'right'))
}
function Wait-ProcessControl($process,$id){
    $pidCondition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$process.Id)
    $idCondition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$id)
    $condition=New-Object System.Windows.Automation.AndCondition($pidCondition,$idCondition)
    for($i=0;$i -lt 100;$i++){$control=[System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition);if($control){return $control};Start-Sleep -Milliseconds 100};throw "Controle não encontrado: $id"
}
function Escape-Control($control){
    $ancestor=$control;while($ancestor -and !$ancestor.Current.NativeWindowHandle){$ancestor=[System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($ancestor)}
    if(!$ancestor){throw 'Janela do controle não encontrada.'};$handle=[IntPtr]$ancestor.Current.NativeWindowHandle;$null=[JanjaUiCapture]::SendMessage($handle,256,[IntPtr]27,[IntPtr]::Zero);$null=[JanjaUiCapture]::SendMessage($handle,257,[IntPtr]27,[IntPtr]::Zero)
}
