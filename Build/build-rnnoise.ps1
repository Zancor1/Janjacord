param([string]$ZigPath)
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
$root=Split-Path $PSScriptRoot -Parent
if(!$ZigPath){$ZigPath=Join-Path $root '.tools/noise-reference/zig/zig-x86_64-windows-0.15.2/zig.exe'}
if(!(Test-Path -LiteralPath $ZigPath)){throw 'Informe -ZigPath para o compilador Zig 0.15.2 (https://ziglang.org/download/).'}
$commit='f0b73e38c029660bc6e010b583b406f6470f8a1b'
$stage=Join-Path $root ('.tools/rnnoise-build-'+[Guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path $stage
$archive=Join-Path $stage 'source.zip'
Invoke-WebRequest -UseBasicParsing -Uri ('https://api.github.com/repos/Yellow-Dog-Man/rnnoise/zipball/'+$commit) -OutFile $archive
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory($archive,(Join-Path $stage 'source'))
$source=(Get-ChildItem (Join-Path $stage 'source') -Directory | Select-Object -First 1).FullName
$env:ZIG_GLOBAL_CACHE_DIR=Join-Path $root '.tools/noise-reference/zig-cache'
$files=Get-ChildItem (Join-Path $source 'src') -Filter '*.c' | ForEach-Object {$_.FullName}
$target=Join-Path $root 'Client/Native/rnnoise.dll'
& $ZigPath cc -shared -O2 -s -target x86_64-windows-gnu -DRNNOISE_BUILD -DDLL_EXPORT -DCOMPILE_OPUS -I (Join-Path $source 'include') -I (Join-Path $source 'src') @files -o $target -lm
if($LASTEXITCODE -ne 0){throw 'Falha ao compilar RNNoise.'}
Get-FileHash -LiteralPath $target
