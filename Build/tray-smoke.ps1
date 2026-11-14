param([string]$ClientPath)
. (Join-Path $PSScriptRoot 'UiAutomation.ps1')
. (Join-Path $PSScriptRoot 'CallInteractions.ps1')
. (Join-Path $PSScriptRoot 'GroupTrayInteractions.ps1')
Add-Type -AssemblyName System.Security
if(!$ClientPath){$ClientPath=Join-Path $root 'Release/Client/Client.exe'}
$process=$null;$previous=@{}
foreach($key in @('JANJACORD_PROFILE_DIR','JANJACORD_NETWORK_DIR','JANJACORD_DISABLE_IMPORT','JANJACORD_DISABLE_DISCOVERY','JANJACORD_MANUAL_CONNECT')){$previous[$key]=[Environment]::GetEnvironmentVariable($key)}
try{
    $testRoot=Join-Path $root ('.tools/tray-test-'+[Guid]::NewGuid().ToString('N'))
    $env:JANJACORD_PROFILE_DIR=Join-Path $testRoot 'profile';$env:JANJACORD_NETWORK_DIR=Join-Path $testRoot 'network';$env:JANJACORD_DISABLE_DISCOVERY='1';$env:JANJACORD_DISABLE_IMPORT='1';$env:JANJACORD_MANUAL_CONNECT=$null
    $null=New-Item -ItemType Directory -Path $env:JANJACORD_PROFILE_DIR -Force
    $secret=New-Object byte[] 32;$random=[System.Security.Cryptography.RandomNumberGenerator]::Create();$random.GetBytes($secret);$random.Dispose()
    $protected=[Convert]::ToBase64String([System.Security.Cryptography.ProtectedData]::Protect($secret,$null,[System.Security.Cryptography.DataProtectionScope]::CurrentUser))
    @{Name=('Tray_'+[Guid]::NewGuid().ToString('N').Substring(0,8));ProtectedKey=$protected} | ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $env:JANJACORD_PROFILE_DIR 'profile.json')
    $process=Start-Process -FilePath $ClientPath -WindowStyle Hidden -PassThru
    $null=Wait-ProcessControl $process 'FriendsHome';$process.Refresh();$handle=$process.MainWindowHandle
    [System.Windows.Automation.AutomationElement]::FromHandle($handle).GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close();Start-Sleep -Milliseconds 700
    $process.Refresh();if($process.HasExited -or [JanjaTrayTest]::IsWindowVisible($handle)){throw 'Closing X did not leave background process'}
    $trayMessage=[int](& (Join-Path $root '.tools/dotnet/dotnet.exe') (Join-Path $root 'Tests/bin/Release/net10.0/Tests.dll') --tray-message)
    [JanjaTrayTest]::Tray($process.Id,$trayMessage,$false);Start-Sleep -Milliseconds 500
    if(![JanjaTrayTest]::IsWindowVisible($handle)){throw 'Tray did not restore app'}
    Write-Output 'PASS: closing hides client; real tray callback restores existing session'
    [JanjaTrayTest]::Tray($process.Id,$trayMessage,$true);$exit=Wait-TrayExit $process
    try{$exit.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()}
    catch [System.InvalidOperationException]{$bounds=$exit.Current.BoundingRectangle;[JanjaTrayTest]::ClickMenu($process.Id,[int]($bounds.X+$bounds.Width/2),[int]($bounds.Y+$bounds.Height/2))}
    if(!$process.WaitForExit(15000)){throw 'Exit menu did not terminate client'}
    Write-Output 'PASS: actual tray Exit menu shuts down client process'
}finally{
    if($process -and !$process.HasExited){Stop-Process -Id $process.Id -Force}
    foreach($key in $previous.Keys){[Environment]::SetEnvironmentVariable($key,$previous[$key])}
}
