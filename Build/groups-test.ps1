$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$dotnet=Join-Path $root '.tools/dotnet/dotnet.exe'
$data=Join-Path $root ('.tools/groups-server-'+[Guid]::NewGuid().ToString('N'))
$server=$null
function Start-GroupServer {
    $process=Start-Process -FilePath $dotnet -ArgumentList @((Join-Path $root 'Server/bin/Release/net10.0/Server.dll'),'--local-test','--data-dir',$data) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $root '.tools/groups-server.log') -RedirectStandardError (Join-Path $root '.tools/groups-server-error.log')
    for($i=0;$i -lt 50;$i++){if($process.HasExited){throw 'Test server exited'};try{$null=Invoke-RestMethod http://127.0.0.1:5199/health;return $process}catch{Start-Sleep -Milliseconds 200}}
    Stop-Process -Id $process.Id -Force;throw 'Test server unavailable'
}
try {
    $server=Start-GroupServer
    & $dotnet (Join-Path $root 'Tests/bin/Release/net10.0/Tests.dll') --groups http://127.0.0.1:5199
    if($LASTEXITCODE -ne 0){throw 'Group integration test failed'}
    Stop-Process -Id $server.Id -Force;$server.WaitForExit();$server=Start-GroupServer
    $state=Get-Content -Raw (Join-Path $root 'Tests/bin/Release/net10.0/group-persistence.json') | ConvertFrom-Json
    $headers=@{Authorization="Bearer $($state.Token)"}
    $groups=Invoke-RestMethod http://127.0.0.1:5199/api/groups -Headers $headers
    $history=Invoke-RestMethod "http://127.0.0.1:5199/api/groups/$($state.GroupId)/messages" -Headers $headers
    if(!($groups | Where-Object {$_.id -eq $state.GroupId}) -or !($history | Where-Object {$_.id -eq $state.MessageId})){throw 'Group or history lost after restart'}
    Write-Output 'PASS: group membership and message history survive server restart'
} finally {if($server -and !$server.HasExited){Stop-Process -Id $server.Id -Force}}
