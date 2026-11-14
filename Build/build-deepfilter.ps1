param([string]$ZigPath)
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
$root=Split-Path $PSScriptRoot -Parent
$stage=Join-Path $root '.tools/deepfilter-reference'
$null=New-Item -ItemType Directory -Force $stage
if(!$ZigPath){$ZigPath=Join-Path $root '.tools/noise-reference/zig/zig-x86_64-windows-0.15.2/zig.exe'}
if(!(Test-Path $ZigPath)){throw 'Zig 0.15.2 required; pass -ZigPath.'}
$env:CARGO_HOME=Join-Path $stage 'cargo';$env:RUSTUP_HOME=Join-Path $stage 'rustup'
$cargo=Join-Path $env:CARGO_HOME 'bin/cargo.exe'
if(!(Test-Path $cargo)){
    $installer=Join-Path $stage 'rustup-init.exe'
    Invoke-WebRequest -UseBasicParsing https://win.rustup.rs/x86_64 -OutFile $installer
    & $installer -y --no-modify-path --profile minimal --default-host x86_64-pc-windows-gnu --default-toolchain 1.100.0
    if($LASTEXITCODE -ne 0){throw 'Rust setup failed.'}
}
$source=Get-ChildItem (Join-Path $stage 'source') -Directory -ErrorAction SilentlyContinue | Select-Object -First 1
if(!$source){
    $archive=Join-Path $stage 'source.zip'
    Invoke-WebRequest -UseBasicParsing https://api.github.com/repos/Rikorose/DeepFilterNet/zipball/v0.5.6 -OutFile $archive
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($archive,(Join-Path $stage 'source'))
    $source=Get-ChildItem (Join-Path $stage 'source') -Directory | Select-Object -First 1
}
& python (Join-Path $PSScriptRoot 'DeepFilterNative/prepare.py') $source.FullName
if($LASTEXITCODE -ne 0){throw 'Source preparation failed.'}
Copy-Item (Join-Path $PSScriptRoot 'DeepFilterNative/linker.py') $stage -Force
Set-Content -Encoding ascii (Join-Path $stage 'linker.cmd') '@echo off',('python "%~dp0linker.py" %*')
Set-Content -Encoding ascii (Join-Path $stage 'cc.cmd') '@echo off',('"'+$ZigPath+'" cc -target x86_64-windows-gnu %*')
Set-Content -Encoding ascii (Join-Path $stage 'ar.cmd') '@echo off',('"'+$ZigPath+'" ar %*')
$env:CARGO_TARGET_X86_64_PC_WINDOWS_GNU_LINKER=Join-Path $stage 'linker.cmd'
$env:CC_x86_64_pc_windows_gnu=Join-Path $stage 'cc.cmd';$env:AR_x86_64_pc_windows_gnu=Join-Path $stage 'ar.cmd'
$env:ZIG_GLOBAL_CACHE_DIR=Join-Path $root '.tools/noise-reference/zig-cache'
& $cargo update --manifest-path (Join-Path $source.FullName 'Cargo.toml') -p time --precise 0.3.36
if($LASTEXITCODE -ne 0){throw 'Dependency update failed.'}
& $cargo build --manifest-path (Join-Path $source.FullName 'Cargo.toml') -p deep_filter --release --no-default-features --features capi,default-model-ll --lib
if($LASTEXITCODE -ne 0){throw 'DeepFilterNet build failed.'}
Copy-Item (Join-Path $source.FullName 'target/release/df.dll') (Join-Path $root 'Client/Native/deep_filter.dll') -Force
Get-FileHash (Join-Path $root 'Client/Native/deep_filter.dll')
