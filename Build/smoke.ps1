param([string]$Distribution)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$Distribution){$Distribution=Join-Path $root 'Release'}
$server=$null
$client=$null
$previousProfile=$env:JANJACORD_PROFILE_DIR
$previousManual=$env:JANJACORD_MANUAL_CONNECT
try {
    $testData=Join-Path $root '.tools/smoke-data'
    $server=Start-Process -FilePath (Join-Path $Distribution 'Server/Server.exe') -ArgumentList @('--port','5299','--data-dir',('"'+$testData+'"'),'--noninteractive') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $root '.tools/tls-server.log') -RedirectStandardError (Join-Path $root '.tools/tls-server-error.log')
    $certPath=Join-Path $testData 'server.pfx'
    for($i=0;$i -lt 50;$i++) {
        if($server.HasExited){throw 'Servidor publicado encerrou.'}
        if(Test-Path $certPath){break}
        Start-Sleep -Milliseconds 200
    }
    $cert=New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($certPath)
    $sha=[System.Security.Cryptography.SHA256]::Create()
    $pin=([BitConverter]::ToString($sha.ComputeHash($cert.RawData))).Replace('-','')
    $cert.Dispose()
    $sha.Dispose()
    & (Join-Path $root '.tools/dotnet/dotnet.exe') (Join-Path $root 'Tests/bin/Release/net10.0/Tests.dll') --tls 'https://localhost:5299' $pin
    if($LASTEXITCODE -ne 0){throw 'Teste HTTPS falhou.'}
    & (Join-Path $root '.tools/dotnet/dotnet.exe') (Join-Path $root 'Tests/bin/Release/net10.0/Tests.dll') --media 'https://localhost:5299' $pin
    if($LASTEXITCODE -ne 0){throw 'Teste de transmissão WebRTC falhou.'}
    $env:JANJACORD_PROFILE_DIR=Join-Path $root ('.tools/startup-'+[Guid]::NewGuid().ToString('N'));$env:JANJACORD_MANUAL_CONNECT='1'
    $client=Start-Process -FilePath (Join-Path $Distribution 'Client/Client.exe') -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 3
    $client.Refresh()
    if($client.HasExited){throw "Cliente encerrou ao iniciar: $($client.ExitCode)"}
    Write-Output 'PASS: Client.exe publicado permaneceu executando após inicialização (sem validação visual)'
} finally {
    if($client -and !$client.HasExited){Stop-Process -Id $client.Id}
    if($server -and !$server.HasExited){Stop-Process -Id $server.Id}
    $env:JANJACORD_PROFILE_DIR=$previousProfile;$env:JANJACORD_MANUAL_CONNECT=$previousManual
}
