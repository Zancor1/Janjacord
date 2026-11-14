$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$dotnet=Join-Path $root '.tools/dotnet/dotnet.exe'
$serverDll=Join-Path $root 'Server/bin/Release/net10.0/Server.dll'
$testsDll=Join-Path $root 'Tests/bin/Release/net10.0/Tests.dll'
$server=$null
function Start-TestServer {
    $process=Start-Process -FilePath $dotnet -ArgumentList @($serverDll,'--local-test') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $root '.tools/test-server.log') -RedirectStandardError (Join-Path $root '.tools/test-server-error.log')
    for($i=0;$i -lt 30;$i++) {
        if($process.HasExited){throw 'Servidor encerrou antes do teste.'}
        try {$null=Invoke-RestMethod 'http://127.0.0.1:5199/health';return $process} catch {Start-Sleep -Milliseconds 200}
    }
    Stop-Process -Id $process.Id
    throw 'Servidor não respondeu.'
}
try {
    $server=Start-TestServer
    & $dotnet $testsDll 'http://127.0.0.1:5199'
    if($LASTEXITCODE -ne 0){throw 'Testes de integração falharam.'}
    Stop-Process -Id $server.Id
    $server.WaitForExit()
    $server=Start-TestServer
    $state=Get-Content (Join-Path (Split-Path $testsDll) 'persistence-check.json') -Raw | ConvertFrom-Json
    $history=Invoke-RestMethod "http://127.0.0.1:5199/api/channels/$($state.ChannelId)/messages" -Headers @{Authorization="Bearer $($state.Token)"}
    if(!($history | Where-Object {$_.id -eq $state.MessageId})){throw 'Mensagem não persistiu após reinício.'}
    Write-Output 'PASS: mensagem e sessão persistiram após reiniciar processo do servidor'
    $direct=Get-Content (Join-Path (Split-Path $testsDll) 'direct-persistence.json') -Raw | ConvertFrom-Json
    $directHistory=Invoke-RestMethod "http://127.0.0.1:5199/api/direct/$($direct.Peer)/messages" -Headers @{Authorization="Bearer $($direct.Token)"}
    if(!($directHistory | Where-Object {$_.id -eq $direct.Message})){throw 'DM não persistiu após reinício.'}
    Write-Output 'PASS: amizade e histórico de DM persistiram após reiniciar o host'
    $savedProfile=Get-Content (Join-Path (Split-Path $testsDll) 'profile-persistence.json') -Raw | ConvertFrom-Json
    $friends=Invoke-RestMethod 'http://127.0.0.1:5199/api/friends' -Headers @{Authorization="Bearer $($savedProfile.Token)"}
    $person=$friends | Where-Object {$_.id -eq $savedProfile.Peer}
    if($person.name -ne $savedProfile.Name -or $person.avatar -ne $savedProfile.Avatar){throw 'Nome/foto não persistiram após reiniciar.'}
    Write-Output 'PASS: nome e foto editados persistiram após reiniciar o host'
} finally {
    if($server -and !$server.HasExited){Stop-Process -Id $server.Id}
    $statePath=Join-Path (Split-Path $testsDll) 'persistence-check.json'
    if(Test-Path $statePath){Remove-Item -LiteralPath $statePath}
    $directPath=Join-Path (Split-Path $testsDll) 'direct-persistence.json'
    if(Test-Path $directPath){Remove-Item -LiteralPath $directPath}
    $profilePath=Join-Path (Split-Path $testsDll) 'profile-persistence.json'
    if(Test-Path $profilePath){Remove-Item -LiteralPath $profilePath}
}
