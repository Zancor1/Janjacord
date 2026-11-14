# Dependências

## Supressão local de ruído — RNNoise

RNNoise (Mozilla, Jean-Marc Valin e Xiph.Org) usa licença BSD; o texto integral acompanha `Client/AudioLicenses/rnnoise-COPYING`. A DLL Windows x64 foi compilada a partir do fork público Yellow-Dog-Man/rnnoise, revisão fixa `f0b73e38c029660bc6e010b583b406f6470f8a1b`, com Zig 0.15.2/MinGW. Fonte, comando de reconstrução e SHA-256 estão em `rnnoise-provenance.txt`; avisos de Zig/MinGW também acompanham a distribuição. O wrapper e o processamento de entrada são próprios do JanjaCord. O modelo está embutido na DLL e executa localmente.

## Áudio selecionável na versão 0.6

SIPSorcery 10.0.17 fornece WebRTC de áudio DTLS-SRTP; NAudio 2.2.1 usa WASAPI para selecionar os dispositivos do aplicativo; Concentus 2.2.2 fornece Opus. SIPSorceryMedia.Abstractions, BouncyCastle.Cryptography e as demais dependências transitivas acompanham o cliente. Os textos das licenças distribuídos pelos pacotes estão em `Client/AudioLicenses` no ZIP e `Build/AudioLicenses` no código.

A licença do SIPSorcery contém BSD de três cláusulas **com uma restrição geográfica adicional**; consulte o texto integral incluído. NAudio usa MIT; Concentus usa licença BSD. Não é usado SIPSorceryMedia.FFmpeg, nem um binário FFmpeg, nesta integração.

JanjaCord usa .NET/ASP.NET Core, Avalonia, SignalR, Microsoft.Data.Sqlite/SQLitePCLRaw, System.Drawing.Common e Microsoft.MixedReality.WebRTC. As respectivas licenças acompanham seus projetos e pacotes NuGet. As DLLs publicadas devem permanecer junto do executável.

## Microsoft.MixedReality.WebRTC 2.0.2

Origem: https://github.com/microsoft/MixedReality-WebRTC

MIT License

Copyright (c) Microsoft Corporation. All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

O projeto upstream está arquivado desde 2022. O uso nesta versão permite integrar o motor WebRTC nativo com Opus/VP8 sem navegador; não há garantia de atualizações de segurança. A substituição por um motor mantido ou a manutenção de um fork é um requisito antes de tratar a aplicação como produto final com suporte. O motor inclui componentes WebRTC de terceiros; seus avisos estão em https://github.com/microsoft/MixedReality-WebRTC/blob/master/NOTICE.
# DeepFilterNet

O modo Avançado usa DeepFilterNet v0.5.6 e o modelo DeepFilterNet3 de baixa latência, sob MIT ou Apache-2.0. A biblioteca foi compilada a partir do código oficial, com uma interface do aplicativo para inicialização e estados independentes de captura. Licenças, avisos das dependências e procedência estão em `Client/AudioLicenses`. Código e modelos: https://github.com/Rikorose/DeepFilterNet/tree/v0.5.6.

# MR-WebRTC: variante de agendamento para60 FPS

O JanjaCord usa uma variante local de MR-WebRTC 2.0.2 para solicitar quadros externos com intervalo de8ms. A captura mantém a taxa selecionada pelo usuário, sem quadros artificiais. A assinatura original foi removida da DLL modificada; esta variante não é assinada pela Microsoft. Procedência, hashes e alteração reproduzível estão em `Build/VIDEO_FPS_PROVENANCE.md` no código-fonte e em `Client/VIDEO_FPS_PROVENANCE.md` na distribuição. Permanecem os avisos originais do projeto e de suas dependências.

