$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class JanjaUiCapture {
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int L,T,R,B; }
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out Rect rect);
 [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h,IntPtr dc,int flags);
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int mode);
 [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h,uint message,IntPtr wParam,IntPtr lParam);
}
'@
function Save-Window($process,$path) {
    $process.Refresh()
    $rect=New-Object JanjaUiCapture+Rect
    $null=[JanjaUiCapture]::GetWindowRect($process.MainWindowHandle,[ref]$rect)
    $bitmap=New-Object System.Drawing.Bitmap(($rect.R-$rect.L),($rect.B-$rect.T))
    $graphics=[System.Drawing.Graphics]::FromImage($bitmap)
    $dc=$graphics.GetHdc()
    try {$ok=[JanjaUiCapture]::PrintWindow($process.MainWindowHandle,$dc,2)} finally {$graphics.ReleaseHdc($dc);$graphics.Dispose()}
    if(!$ok){$bitmap.Dispose();throw 'Falha ao capturar janela do aplicativo.'}
    $bitmap.Save($path,[System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}
function Find-Control($window,$id) {
    $condition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$id)
    try{return $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)}catch [System.Runtime.InteropServices.COMException]{return $null}catch [System.Windows.Automation.ElementNotAvailableException]{return $null}
}
function Set-Field($control,$value) {if(!$control){throw 'Campo não encontrado na interface.'};$control.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($value)}
function Invoke-Button($control) {if(!$control){throw 'Botão não encontrado na interface.'};try{$control.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()}catch{throw ('Cannot invoke '+$control.Current.AutomationId+': '+$_.Exception.Message)}}
function Invoke-MenuItem($control) {
    if(!$control){throw 'Item de menu não encontrado.'}
    $pattern=$null
    if($control.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern,[ref]$pattern)){$pattern.Invoke();return}
    $bounds=$control.Current.BoundingRectangle
    $ancestor=$control
    while($ancestor -and !$ancestor.Current.NativeWindowHandle){$ancestor=[System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($ancestor)}
    if(!$ancestor){throw 'Janela nativa do menu não encontrada.'}
    $handle=[IntPtr]$ancestor.Current.NativeWindowHandle
    $rect=New-Object JanjaUiCapture+Rect
    $null=[JanjaUiCapture]::GetWindowRect($handle,[ref]$rect)
    $x=[int]($bounds.X+$bounds.Width/2-$rect.L);$y=[int]($bounds.Y+$bounds.Height/2-$rect.T)
    $position=[IntPtr](($y -shl 16) -bor ($x -band 65535))
    $null=[JanjaUiCapture]::SendMessage($handle,512,[IntPtr]::Zero,$position)
    $null=[JanjaUiCapture]::SendMessage($handle,513,[IntPtr]1,$position)
    $null=[JanjaUiCapture]::SendMessage($handle,514,[IntPtr]::Zero,$position)
}
function Wait-Dialog($process,$title,[int]$seconds=15) {
    $pidCondition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$process.Id)
    $nameCondition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,$title)
    $condition=New-Object System.Windows.Automation.AndCondition($pidCondition,$nameCondition)
    for($i=0;$i -lt ($seconds*10);$i++){$dialog=[System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition);if($dialog){return $dialog};Start-Sleep -Milliseconds 100}
    throw "Diálogo não abriu: $title"
}

