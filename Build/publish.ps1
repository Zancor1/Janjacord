$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$dotnet=Join-Path $root '.tools/dotnet/dotnet.exe'
if (!(Test-Path $dotnet)) { $dotnet='dotnet' }
foreach ($project in @('Server','Client')) {
    & $dotnet publish (Join-Path $root "$project/$project.csproj") -c Release -r win-x64 --self-contained true -o (Join-Path $root "Release/$project")
    if ($LASTEXITCODE -ne 0) { throw "Falha ao publicar $project" }
}
Copy-Item (Join-Path $root 'README.md'),(Join-Path $root 'CONFIGURACAO_RADMIN.md'),(Join-Path $root 'ESTADO_PROJETO.md'),(Join-Path $root 'THIRD_PARTY_NOTICES.md') (Join-Path $root 'Release')
Copy-Item (Join-Path $root 'Build/WEBRTC_NOTICE.txt') (Join-Path $root 'Release/Client/WEBRTC_NOTICE.txt')
Copy-Item (Join-Path $root 'Build/AudioLicenses') (Join-Path $root 'Release/Client') -Recurse -Force

Copy-Item (Join-Path $root 'Build/VIDEO_FPS_PROVENANCE.md') (Join-Path $root 'Release/Client/VIDEO_FPS_PROVENANCE.md')
