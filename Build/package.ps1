$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$stage=Join-Path $root ('.tools/package-'+[Guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path (Join-Path $stage 'Server') -Force
Copy-Item -LiteralPath (Join-Path $root 'Release/Client') -Destination (Join-Path $stage 'Client') -Recurse -Force
# Never distribute the host's database, sessions or private TLS key.
Get-ChildItem -LiteralPath (Join-Path $root 'Release/Server') -File -Force | Where-Object {$_.Extension -in @('.exe','.dll','.pdb') -or $_.Name -in @('Server.deps.json','Server.runtimeconfig.json')} | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $stage 'Server') -Force}
foreach($document in @('README.md','CONFIGURACAO_RADMIN.md','ESTADO_PROJETO.md','THIRD_PARTY_NOTICES.md')){Copy-Item -LiteralPath (Join-Path $root $document) -Destination $stage}
$archive=Join-Path $root 'JanjaCord-0.11.2-win-x64.zip'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$temporaryArchive=Join-Path $root ('.tools/package-'+[Guid]::NewGuid().ToString('N')+'.zip')
[System.IO.Compression.ZipFile]::CreateFromDirectory($stage,$temporaryArchive,[System.IO.Compression.CompressionLevel]::Optimal,$false)
Move-Item -LiteralPath $temporaryArchive -Destination $archive -Force
Get-Item -LiteralPath $archive | Select-Object FullName,Length
