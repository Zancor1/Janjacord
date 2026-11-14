# Atualização0.11.2 — intensidade0..1 e teste local de microfone

- `MicrophoneProcessor.Strength` faz mistura alinhada seco/filtrado0..1 com rampas existentes.0 equivale ao bypass e libera estado RNNoise após fade;1 aplica máximo. Default de perfil0.75; `NoiseSuppressionEnabled` continua independente. Valor salvo em LocalProfile e reaplicado ao abrir/entrar/trocar dispositivo.
- Novo `AudioSettingsView` nos três pontos: slider0..1 com passo0.01, valor numérico, retorno local com iniciar/parar, medidor do áudio processado. Aplica ao vivo com debounce80ms; fechar/Aplicar confirma o último valor. Painel rolável com rodapé fixo.
- `MicrophonePreview` usa SelectedAudio separado, mesmo DSP/Opus, decodifica para buffer de reprodução local, sem Connection/peer nem escrita de áudio em arquivo. Para testar deve haver clique explícito; instrução pede fones para evitar realimentação.
- `PauseMicrophoneForTest`: pausa captura/transmissão e saída da chamada original sem alterar Muted/Deafened. Flag persiste se a call for criada durante o teste. Retomada limpa buffers/DSP para não enviar cauda do teste; fechamento/disconnect/encerramento fazem limpeza e restauram. Guardas impedem iniciar retorno após fechar modal durante await.
- `NoiseSuppressionProbe`:0==bypass; RMS de teclado cai monotonicamente de0.04934 em0 para0.00233 em1, com pontos0.25/0.5/0.75. `MediaProbe` abre microfone/retorno reais e verifica contagem do envio original congelada durante teste, retorno local gerando pacotes, retomada ao parar e Muted/Deafened inalterados. Preferência0.37 salva/recarregada no teste de perfil. UIAutomation confere iniciar/parar e intensidade0/1/0.42 salva.
- Protocolo6 e DLL RNNoise portátil mantidos. Testes locais não substituem qualidade no dispositivo de cada pessoa.

# Atualização0.9 — supressor local e menos distorção

- `MicrophoneProcessor`: mono48k, frames480/10ms, ganho único de captura RMS0.14 (1..128 para microfones fracos, subida650ms/redução35ms), pico controlado0.85 e rampas por amostra. RNNoise local com passa-altas70Hz, VAD3frames, hold200ms, pré-roll20ms e abertura/fechamento suaves. Caminhos seco/filtrado alinhados30ms, crossfade50ms ao alternar, limitador adicional5ms antes de Opus. Desligado libera o estado nativo após a transição.
- `SelectedAudio`: captura20ms (antes100ms), acumula480 amostras, processa uma vez e empacota960/20ms para Opus. Processamento sob gate para alternar/dispor sem disputa com a thread de captura. Receiver agora só aplica volume pessoal, sem o segundo AGC agressivo que amplificava artefatos.
- `LookAheadPeakLimiter`: deque de máximos,5ms de antecipação, teto0.98 e release80ms. Substitui o hard clip da mistura; preserva forma de onda sob volume200%. Volume pessoal muda com rampa5ms e0% silencia.
- `AudioSettings` (...): checkbox Supressor de ruído, aplica na hora e salva `NoiseSuppressionEnabled` (defaulttrue). Vale para o microfone local. `CallService` reaplica ao entrar/recriar captura e volume pessoal continua independente.
- RNNoise compilado de fonte BSD `Yellow-Dog-Man/rnnoise` commitf0b73e38c029660bc6e010b583b406f6470f8a1b com Zig0.15.2, x64GNU. Não depende de VCRUNTIME140; importa apenas UCRT/KERNEL32 do Windows10. SHA e licenças em Build/AudioLicenses/rnnoise-provenance.txt. Rebuild opcional: Build/build-rnnoise.ps1 -ZigPath caminho/zig.exe. DLL incluída em Client/Native e copiada à raiz da publicação.
- `NoiseSuppressionProbe`: ruídos sintéticos de teclado/mouse/respiração, fala PT-BR gerada pelo SAPI local, fala34dB mais baixa, misturaSNR10dB, toggle ao vivo/restauração do bypass e distorção de seno forte a200%. Fixtures em .tools/noise-tests, sem gravação do usuário. Rodar Tests.dll --noise caminho/clean-speech.wav.
- Atualizar todos os clientes para remover o AGC duplicado e aplicar filtro em cada microfone. Protocolo6 continua. Testes de bancada não garantem cancelamento de todo teclado/respiração real nem substituem validação com dois PCs.

# Atualização 0.8.2 — toque de chamada recebido

`IncomingRingtone` usa NAudio/WasapiOut e `AudioFileReader` para tocar o MP3 fornecido pelo usuário em loop. O projeto copia o arquivo original da raiz para Assets/incoming-call.mp3 na compilação/publicação; ZIP valida a presença. Não gera toque sintético nem modifica o MP3 original. `DirectCallView` inicia ao abrir aviso e para antes de responder, no fechamento/evento encerrado/desconexão; `MainWindow.Closed` libera tudo. Se RPC de resposta falhar e a chamada ainda estiver tocando, reinicia o toque. `RingtoneProbe` verifica repetção, leitura vazia, MP3 real e reprodução/parada WASAPI. Protocolo6 permanece.

# Correção 0.8.1 — voz ainda baixa

- Antigo ganho máximo4 e limiar RMS0.003 deixavam microfones fracos sem reforço suficiente. Novo alvo RMS0.18, teto32, limiar0.0005, ataque de ganho0.12s e redução0.015s, conserva ganho no silêncio e restringe pico pré-volume a0.95. Aplica antes do Opus e depois da decodificação; volume pessoal0–200% continua independente.
- `MicrophoneMonoSource` escolhe canal de maior energia por bloco: evita -6dB com um canal vazio e cancelamento com fases opostas. Sem alteração no mixer global/dispositivos padrão.
- `VoiceLevelProbe` mede entradas RMS0.00071/0.00354/0.01414, verifica ganho até32x, fase/canal vazio, retomada após silêncio,0% e limite de pico. Teste físico com os microfones dos usuários continua pendente.

# Atualização 0.8 — DMs offline, tela cheia e volume individual

- `ConversationCache.Outbox`: mensagens pendentes DPAPI por pin da rede, identificador estável, backup .bak, persistência ao fechar/reabrir. `OutboxView` envia em ordem, tenta novamente a cada 5s/reconexão, reconcilia resposta/evento por ClientId, remove placeholder ao confirmar e sinaliza falha definitiva sem fingir entrega. Contexto do estado oferece tentar/cancelar. Nome/conta/friends preservados. Envios para amizade pendente seguem bloqueados. `StartOfflineRetry` aguarda envio em andamento antes de substituir a janela.
- `VideoTile`: dois cliques/expandir abrem janela nativa FullScreen com a mesma WriteableBitmap atualizada ao vivo; Esc/duplo clique/fechar retornam. Dispose por parada da fonte fecha o visualizador; sem imagem congelada residual.
- `VoicePlaybackSource`: nivelamento gradual de fala baixa (alvo RMS0.12, ganho1..4, limiar0.003), ganho individual0..2 e limitador da mistura. Cada peer tem fonte independente; nenhum mixer alheio/global é alterado.
- `PeerVolumeView`: menu no avatar da call/rodapé, slider0..200 e atalhos0/100/200. Aplica em tempo real por UserId, salva por fingerprint+UserId em ConcurrentDictionary do perfil ao fechar menu e reaplica após reconexão. Sem controle de volume de si mesmo.
- `PlaybackAndOutboxProbe` verifica normalização, silêncio0%, ganho2x em200%, fila cifrada isolada por rede/persistência/ack e preferência de volume salva. `FriendsProbe` inclui destinatário sem conexão SignalR. UI real confere clique direito+175% salvo, duplo clique+Esc e envio quando host está fechado/reaberto.
- Protocolo6 continua compatível com0.7. Pacote0.8, atualizar clientes para novos controles.
- Testes locais não substituem qualidade física de voz/câmera nem dois PCs na Radmin.

# Atualização 0.7 — voz na Radmin, tela separada e foto offline

- `AudioPeer`: `X_ICEIncludeAllInterfaceAddresses=true` inclui NIC da Radmin. Watchdog 20s mostra falha recuperável e `RetryAudio` reconstrói a conexão de voz nos dois lados com `audio-reset`. Sala vazia não mostra conectando indefinidamente.
- `CallService`: sinais que ultrapassam roster são limitados/em fila e consumidos somente quando roster autenticado inclui remetente. Motor nativo agora tem apenas duas m-lines de vídeo (câmera0/tela1); retirada a m-line de áudio vazia que podia ativar a sessão de comunicação do Windows. WASAPI usa Role.Console, não altera mixer alheio nem configuração global. Não usar SetDuckingPreference em outros aplicativos nem registro do Windows.
- `DirectCallView`/`MainWindow`: participante de voz continua como avatar, tela aparece como entrada independente e usa quadro real em `VideoTile`. Estado screen-on vem do transmissor ou do primeiro quadro, não da simples criação de transceiver; parada/saída remove a entrada. Identificadores acessíveis nos nomes/captions permitem verificar ambas as entradas via UIAutomation.
- `ProfileView`: Salvar não depende de SignalR conectado. Foto pode ser salva localmente com `PendingAvatarSync`; reconexão/login atualiza a mesma conta. Nome exige conexão para unicidade. Retry de rede aguarda fechamento do editor para preservar rascunho.
- Protocolo6, versão0.7: atualizar todos. Pin/banco/perfil de versões anteriores preservados.
- Testes locais: `SelectableAudioProbe` confere candidato Radmin26.x ativo, transporte Opus/DTLS-SRTP e timeout20s. `MediaProbe` confere SDP sem m=audio no motor nativo, reconexão de voz e tela real em ambos os sentidos/DM. Scripts UI conferem pessoa+tela separadas e foto salva offline/sincronizada ao voltar host.
- Pendências: validação física entre dois PCs na Radmin, qualidade com fones/microfones/câmera e comportamento de drivers/Bluetooth. Testes locais não provam esses cenários.

# Atualização 0.6 — DMs, chamadas privadas e dispositivos

- Crash confirmado no log .NET Runtime: `NullReferenceException` em `MainWindow.MessageView` durante reciclagem do ListBox/ScrollIntoView. Template agora aceita nulo e retorna controle vazio durante a reciclagem. Envio captura o destinatário antes do await para não anexar resposta em outra DM se o usuário trocar de conversa.
- `DirectCallRegistry/Manager` e `ChatHub`: convidar, atender/recusar, cancelar, expirar em 35s e encerrar ao desconectar. Sala negativa privada exige amizade aceita e conexões específicas do convite aceito. Câmera/tela usam a mesma sinalização e captura real dos canais.
- `DirectCallView`: telefone no topo da DM; aviso de chamada recebida; barra compacta com mic/câmera/tela/.../desligar, avatares redondos e nenhum quadrado de vídeo fictício. Vídeo só aparece ao receber quadro real. Menu ... enumera WASAPI, aplica seleção em chamada e salva IDs no perfil.
- `SelectedAudio`/`AudioPeer`: captura/reprodução WASAPI por aplicativo, reamostragem mono48k/20ms e conexão WebRTC Opus DTLS-SRTP SIPSorcery. O antigo motor nativo continua apenas para vídeo/tela; não reproduz áudio duplicado pelo dispositivo padrão. Não altera o padrão global do Windows. Licenças em Build/AudioLicenses.
- Protocolo 5: atualizar todos os clientes/host para 0.6. Perfil protocolo4 preserva o pin e banco existente durante migração.
- Testes `DirectCallProbe` (convite/consentimento/ocupado/privacidade/encerramento), `SelectableAudioProbe` (Opus960 amostras, ICE/DTLS-SRTP nos dois sentidos sem dispositivo), `MediaProbe` (tela real em DM) e `auto-smoke` (65 DMs e chamadas nos executáveis).
- Continuam pendentes validação de qualidade com dois PCs Radmin, microfones/fones físicos diferentes, câmera física, perda de rede e carga. Não declarar esses testes como realizados.

# Continuidade — JanjaCord 0.5.1

## Alteração 0.5.1

Entrar por ID agora mantém o diálogo aberto e mostra uma mensagem explícita quando o servidor não existe, em vez de apenas Operação recusada: 404 no rodapé. Valida IDs positivos, permite corrigir/repetir e só fecha após entrada bem-sucedida. O cliente mapeia 404 mesmo com um host anterior sem corpo de erro. `visual-smoke.ps1` verifica esse caso com um ID inexistente antes do fluxo de criação/chat/exclusão.

## Alterações 0.5 — edição e persistência de perfil

- `Client/Views/ProfileView.cs`: painel Meu perfil aberto pelo nome/foto no rodapé. Edita nome, escolhe/remova foto, permite cancelar e salva online sem recriar conta. A foto é temporária até clicar Salvar; cancelar não muda os dados. Usa seletor real do Windows e PNG 160 × 160.
- `Server/Networking/ProfileEndpoints.cs`: PUT autenticado `/api/profile`, valida nome/foto, recusa nome ocupado e atualiza atomicamente nome, reserva e avatar do próprio usuário. `ProfileChanged` avisa amigos/membros e atualiza rosters das chamadas, sem alterar IDs, amigos ou DMs.
- `Connection` atualiza o nome da sessão; `ApplyProfileChange` atualiza rodapé, amigos, membros, DMs/cache e títulos. Rótulos dos vídeos acompanham mudanças de nome nas chamadas.
- `LocalProfile.Save` grava arquivo temporário e substitui o principal com backup `.bak`; Load tenta o backup antes de iniciar perfil vazio. Primeira criação continua salvando a chave DPAPI e os dados em LocalAppData. Startup de perfil conhecido mostra uma tela de retorno em vez do formulário de cadastro e entra automaticamente.
- `Tests/ProfileProbe.cs` passou: edição preserva userId/amizade, notifica outra conexão, recusa nome repetido sem perder foto, salva/recarrega primeiro perfil e recupera a mesma chave pelo backup. `test.ps1` verificou nome/foto após reiniciar o host.
- `Build/profile-smoke.ps1` usa cadastro/editor reais, seletor de foto nativo, cancelamento e reabertura do executável; incluído em `zip-smoke.ps1`. Perfis/bancos/fotos de teste são isolados dos dados reais.
- Cadastro e editor bloqueiam a confirmação enquanto a foto selecionada é preparada; uma condição de corrida identificada na validação final permitia confirmar a imagem anterior ao salvar rapidamente. A correção e a reabertura com outra foto foram verificadas na interface.
- Publicação: `JanjaCord-0.5-win-x64.zip`; protocolo de rede permanece 4 para manter a rede e os dados existentes. O host precisa ser atualizado para disponibilizar o novo endpoint de perfil.

Validação específica executada no Client.exe e Server.exe **extraídos do ZIP 0.5**: primeiro cadastro com nome/foto pelo seletor nativo; fechar/reabrir entra automaticamente com mesmo userId/chave/foto; editar nome e outra foto; cancelar uma tentativa posterior de remoção/renomeação não altera o perfil; fechar/reabrir conserva as alterações salvas. O teste do seletor usa foco verificado na janela pertencente ao JanjaCord e a entrada normal do campo de arquivo, pois enviar somente WM_SETTEXT não confirmou a troca da seleção anterior nesse diálogo do Windows. A captura `.tools/ui-profile-saved.png` foi inspecionada.

## Histórico 0.4

## Alterações 0.4 — pedido mais recente

- Crash 0.3 reproduzido a partir do ZIP extraído: faltavam `Server.runtimeconfig.json`, `coreclr.dll`, `e_sqlite3.dll` e outros arquivos ocultos. Não era apenas falha de compilação. `Build/package.ps1` agora usa cópia com Force e ZipFile.CreateFromDirectory, incluindo ocultos. `Build/zip-smoke.ps1` valida o pacote e executa os arquivos extraídos.
- `Server/ServerHost.cs` extrai a construção do host para uma API pública; `Client` referencia esse projeto e ASP.NET Core. `Client/Networking/AutomaticNetwork.cs` hospeda o servidor dentro de Client.exe, sem iniciar Server.exe. Mantém lock local, endpoint, banco/certificado persistidos e uma etapa de eleição na primeira descoberta.
- `Server/Networking/DiscoveryService.cs` e `Client/Networking/LanDiscovery.cs`: descoberta UDP 39334 por loopback e broadcasts das interfaces IPv4, incluindo tentativa no adaptador Radmin. Tratar SocketError.ConnectionReset/ConnectionRefused é necessário no Windows quando não existe listener. Nonce e limites de tamanho filtram respostas malformadas. A descoberta inicial usa confiança na LAN; conexões seguintes fixam o certificado salvo.
- O primeiro cliente coordena a rede e precisa ficar aberto. Não existe migração distribuída do banco. Perfis conhecidos não entram silenciosamente em uma rede diferente quando o host some: mostram cache offline e tentam retomar a mesma rede. Redes isoladas não garantem unicidade global de nomes.
- `Shared/NameRules.cs`, tabela `user_names`: normalização Unicode/FormKC, caixa e espaços nas pontas; restrição única. Novo cadastro com nome usado retorna 409, sem sufixos automáticos. Contas por chave do dispositivo continuam protegidas por hash e DPAPI.
- `Server/Database/FriendStore.cs`, `Networking/FriendEndpoints.cs`, `PresenceDirectory.cs`: solicitação/aceitação de amizade, presença de conexões reais e DMs persistidas em SQLite. DMs só enviam após aceite; histórico privado exige relacionamento. Reenvio com mesmo clientId é idempotente. SQL `friendships` e `direct_messages` não são afetadas por excluir um servidor.
- `Client/Views/FriendsView.cs`: tela inicial Amigos, filtros Disponível/Todos/Pendentes, botão Adicionar amigos, sidebar de DMs, aceitação/recusa e conversa privada. O ícone J retorna à home sem servidor selecionado. O shell preserva os controles de chamada ao alternar entre home e servidores.
- `Client/Services/ConversationCache.cs`: últimas 100 mensagens carregadas por DM e amigos em arquivo DPAPI por rede. Offline conserva leitura, desabilita envio e não finge presença. Perfil também guarda userId/protocolo. O host SQLite continua fonte principal do histórico; cache não é backup integral.
- `Build/auto-smoke.ps1`: dois Client.exe reais, sem Server.exe, com perfis isolados. Adicionar/aceitar amizade, envio/recebimento de DM, reabrir cliente e ler histórico offline foram executados com sucesso antes do empacotamento. Capturas `.tools/ui-friends.png`, `ui-dm.png` e `ui-offline-dm.png` foram inspecionadas.
- `Tests/FriendsProbe.cs`: entrega em tempo real, histórico, aceitação obrigatória, idempotência e rejeição de nome repetido. `Build/test.ps1` reinicia o host e verifica a DM persistida. `Tests/AutomaticProbe.cs` verificou host embutido, descoberta UDP e conexão/DM de um segundo cliente sem hospedar outro servidor.
- Pendente de teste físico: dois PCs Radmin, dispositivos de voz/câmera e carga. Nenhum teste local deve ser descrito como teste entre dois computadores reais.

## Verificações para concluir cada nova publicação

Build Tests; test.ps1; AutomaticProbe --auto; publish.ps1; visual-smoke.ps1; package.ps1; **zip-smoke.ps1 sobre o ZIP extraído**. Este último verifica DLLs/runtime e executa smoke de mídia mais dois clientes com amigos/DMs. Não entregar novamente apenas com validação da pasta Release.

### Resultado final do ZIP 0.4 em 12/11/2026

`zip-smoke.ps1` concluiu com código 0. Validou runtimes/DLLs ocultos e ausência de banco/chave privada; iniciou Server.exe extraído; verificou HTTPS e vídeo real de monitor nos dois sentidos; iniciou dois Client.exe extraídos sem Server.exe para a rede automática. Na interface, adicionou/aceitou amizade, entregou DM, reabriu o destinatário com histórico, fechou o host e leu o cache offline, reabriu o host original e confirmou reconexão automática com a mesma DM. Os testes de autorização também bloquearam acesso ao histórico privado por uma terceira conta. Nenhuma dessas evidências representa teste entre dois PCs físicos na Radmin.

## Histórico 0.3

## Alterações 0.3

- Novo `Client/Views/ShellView.cs` separa o shell do onboarding/diálogos. Tema preto, colunas compactas, canais de texto e voz separados, controles de chamada no rodapé, menu no nome do servidor, busca funcional no histórico carregado e painel de membros recolhível. Ícones vetoriais próprios, sem ativos proprietários.
- Menu **Excluir servidor** somente para o dono, também acessível por botão direito no ícone. Confirmação exige digitar o nome exato; API continua verificando o dono, independentemente da interface.
- `Server/Networking/CommunityEndpoints.cs`: exclusão transacional de mensagens/canais/associações e servidor; notificação privada aos membros e encerramento das chamadas de todos os participantes. Contas e outros servidores são preservados.
- Tabela `ids` mantém contadores monotônicos para que IDs excluídos não sejam reutilizados por grupos SignalR futuros. `Community` inclui `OwnerId`.
- `Connection` recebe `CommunityDeleted`/`CallEnded`; `CallService` libera fontes e conexões quando o servidor é excluído. O cliente remove o servidor da lista e limpa a conversa.
- `Tests/DeletionProbe.cs`: dono versus membro, atualização no outro cliente, preservação de outro servidor e não reutilização de IDs. `MediaProbe` verifica que a exclusão encerra as duas chamadas. `visual-smoke.ps1` testa criação/chat/menu de exclusão e confirmação pelo nome na interface real, usando dados isolados.
- Publicação IIS desativada: esta aplicação usa Kestrel diretamente em `Server.exe`, sem exigir ou gerar configuração IIS. `Build/package.ps1` gera `JanjaCord-0.3-win-x64.zip`, sem incluir banco ou chave privada.

## Pedido atual e implementação

Foi solicitado um visual melhor, perfil inicial por nome/foto sem senha e microfone, câmera e tela para outros usuários. A interface foi substituída e os caminhos reais de mídia foram integrados. Não declarar que todos os requisitos originais estão completos.

## Arquivos principais

- `Client/Views/MainWindow.cs`, `Ui.cs`: onboarding por nome/foto, etapa separada de servidor, shell, dialogs e controles reais. `VideoTile.cs`: renderização BGRA e fila que mantém só o quadro mais recente por fonte.
- `Client/Services/LocalProfile.cs`: identidade DPAPI em LocalAppData, sem senha visível. `Connection.cs`: API, pin HTTPS, chat e sinalização SignalR.
- `Client/Media/CallService.cs`: motor Microsoft.MixedReality.WebRTC, Opus/VP8, três transceivers por par (áudio, câmera e tela), eleição de offerer por connection ID, fila serial de sinalização/ICE, captura/reprodução e encerramento. O answerer associa os transceivers criados pelo SDP remoto, em vez de criar um conjunto duplicado que deixaria a resposta recvonly.
- `Client/Media/BufferedVideoSource.cs`: trilhas de vídeo reservadas desde o SDP inicial, sem produzir imagens artificiais. Apenas quadros reais da fonte explicitamente ligada são fornecidos ao motor; a captura pode ser ligada/desligada sem mudar emissores RTP.
- `Client/Media/DesktopCapture.cs`: monitores/janelas, captura GDI/PrintWindow real, escala e fonte externa de quadros para codificação WebRTC. Não usa WGC ou áudio de loopback.
- `Server/Authentication/DeviceAccounts.cs`: cadastro/retorno por chave do dispositivo, fotos e sessões. Mantém login por senha legado.
- `Server/Networking/CallDirectory.cs`, `ChatHub.cs`: roster de sala, entrada autorizada, grupos SignalR, validação de sinalização entre participantes do mesmo canal.
- `Server/Database/Store.cs`: banco SQLite, novas tabelas `device_keys` e `profiles`, sem remover dados antigos.
- `Shared`: contratos. `Tests/MediaProbe.cs`: prova de transporte de captura real; `TlsProbe.cs` e `Program.cs`: autenticação/chat. `Build/visual-smoke.ps1`: interação com a interface real por UIAutomation.

## Evidências executadas em 12/11/2026

- Cliente, servidor e suíte compilaram; executáveis self-contained Windows x64 foram publicados.
- Chat: duas conexões, mensagem real, histórico, limite de tamanho, bloqueio sem sessão/não membro, restrição de criação ao proprietário, reinício e persistência de sessão/mensagem passaram.
- HTTPS/WSS: impressão incorreta recusada; correta aceita; autenticação e troca real com o serviço do cliente passaram.
- Cadastro sem senha: mesma chave retornou a mesma identidade em outra conexão.
- WebRTC: duas conexões nativas negociaram ICE, conectaram e transmitiram um monitor real em VP8, com quadro recebido 640 × 358. O outro participante também iniciou a transmissão no sentido inverso. Parar captura e sair das salas passaram. O teste não gravou o conteúdo do monitor. Uma falha inicial de mídia no answerer foi identificada pelo SDP recvonly e corrigida antes da publicação final.
- Câmera: uma câmera detectada por enumeração; **não foi ativada pelo teste automático**. Isso não prova captura/transmissão física.
- UIAutomation: preencheu nome no executável publicado, criou perfil sem campo de senha, preencheu conexão HTTPS e abriu a interface principal. As capturas `.tools/ui-profile.png`, `ui-connect.png`, `ui-chat.png` foram inspecionadas visualmente.
- Não executados: microfone/alto-falante físicos, câmera física transmitida, dois computadores Radmin, carga com vários usuários, falhas de dispositivo e rede reais.

## Dependência nativa e limitações

`mrwebrtc.dll` x64 é copiada explicitamente de `runtimes/win10-x64/native`, porque a seleção padrão de RIDs do .NET 10 não inclui o RID legado. O SDK pode emitir NETSDK1206; o arquivo é incluído e sua inicialização/transmissão foi verificada. Não remover a cópia explícita ao alterar o projeto.

MixedReality-WebRTC 2.0.2 está arquivado. Seu motor precisa de manutenção/substituição antes de uma entrega final com suporte. O aviso não deve ser escondido como se a dependência tivesse manutenção atual. A alternativa SIPSorcery foi pesquisada, mas não integrada.

Sem SFU/TURN externo; mesh máximo de oito conexões por sala. Sem áudio do sistema, WGC, aceleração por hardware, 60 FPS, dispositivos de áudio selecionáveis, push-to-talk ou configuração de AEC. A reconexão de SignalR encerra as fontes; o usuário precisa entrar na chamada novamente. Demais pendências constam no README.

## Teste manual obrigatório antes de declarar mídia plenamente validada

1. Abra Server.exe e dois Client.exe em computadores diferentes; conecte com IP Radmin, mesma impressão e ID. Confirme texto e fotos.
2. Crie/entre no mesmo canal de voz com fones. Fale em cada PC; confirme áudio real no outro. Teste desligar microfone e áudio recebido, sair e entrar. Se falhar, confira dispositivos padrão e permissões Windows.
3. Em um PC, clique Ligar câmera, selecione dispositivo e confirme. Verifique prévia e imagem no outro PC. Desligue e confirme que a transmissão acabou. Repita no sentido inverso e com câmera/tela simultâneas.
4. Compartilhe um monitor e depois uma janela, movendo conteúdo real. Compare a imagem no outro PC; teste qualidade, parar e reiniciar. Janelas GPU/protegidas/minimizadas podem precisar da alternativa por monitor.
5. Interrompa/reestabeleça a VPN. Confirme que as fontes são desligadas e entre novamente na chamada. Teste remoção dos dispositivos durante uso.

## Próximas correções e evolução

Priorizar resultados dos testes físicos, tratamento de renegociação sob perda de pacotes, limpeza de trilhas removidas, ajuste de bitrate por fluxo e cancelamento de capturas de janela que não respondem. Expandir automação de autorização de salas e isolamento de sinalização. Depois, seleção de áudio/volumes, captura WGC, loopback opt-in, recuperação de lacunas de chat e demais recursos do pedido original. Parte da lógica da janela ainda deve migrar a view models.

Conta sem senha é vinculada a chave local, não ao nome. Apagar perfil perde a identidade; não há recuperação/transferência nem vinculação com contas legadas. Não substituir esse mecanismo por login apenas pelo nome, o que permitiria assumir contas de terceiros.

Clique com o botão direito no microfone de mutar para abrir as configurações de áudio e testar o microfone, mesmo fora de uma chamada. O teste só inicia ao clicar em Testar meu microfone.

Supressão reforçada: menos mistura de áudio sem filtro, limiar de voz mais exigente e silenciamento gradual entre falas. Intensidade 0 desliga o efeito e 1 aplica o máximo. Novos perfis iniciam em 0,90; ajustes existentes são preservados.
# 0.11.2: DeepFilterNet3 de baixa latência

- Novo modo Avançado, padrão para perfis sem preferência de modelo, e alternativa Leve com RNNoise.
- Processamento nativo local em blocos de10ms, com estados separados para chamada e teste de microfone. Sem plugin externo, driver virtual ou servidor de processamento.
- Avaliação com fala sintetizada em português, fala34dB mais baixa, teclado/mouse/respiração simulados e fala misturada com teclado. O teste verifica também tempo real e desligamento do efeito.
- O resultado em microfones reais depende do ambiente; não foi estabelecida equivalência com o Krisp.

# 0.11.2: grupos privados, chamadas offline e bandeja

- Botão de pessoas com + na DM, seleção dos amigos aceitos, busca e limite de10 integrantes incluindo o criador. Limite também imposto no servidor.
- Grupos e mensagens persistidos no SQLite; mensagens em tempo real, histórico autenticado, tentativas idempotentes e acesso restrito aos integrantes. Reiniciar o host mantém grupos e histórico.
- Sala privada de voz do grupo com câmera/tela pelos mesmos controles de chamada. Sair da chamada não remove o grupo nem desliga os demais participantes.
- Chamadas para amigos offline deixam de ser recusadas; convites pendentes são entregues ao conectar durante os35 segundos de tentativa.
- X mantém processo, conexão e host ativos na bandeja. Clique restaura; menu Sair encerra os recursos e o processo. Restaurar a janela não inicia outra conexão.
- Testes: criação/limite, privacidade, mensagens, reinício, convite offline, roster com10 usuários; verificação nativa da interface e das ações da bandeja.

# 0.11.2: presença de voz e chamadas visíveis nas conversas

- Painel no topo com cartões dos participantes reais, status e entrada explícita. Observar não abre microfone, câmera ou conexão de mídia.
- Estado da chamada entregue aos membros do grupo e aos dois usuários da DM. Snapshot autenticado recupera chamadas já iniciadas; atualizações com revisão evitam reativar estado antigo.
- Presença de voz nos amigos e membros, sem expor detalhes de conversas privadas de terceiros. A saída do último participante remove o painel.
- Testes de servidor: observador fora da chamada, snapshot tardio, status dos amigos, encerramento, sinalização e isolamento de DMs/grupos. Verificação nativa da interface: recusar convite, visualizar participantes, entrar posteriormente e ver contato em outra chamada no PV.

# 0.11.2: FPS de compartilhamento

- Seis opções explícitas:720p e1080p, cada resolução com15/30/60 FPS; padrão720p30. Seleção usa objetos com resolução e FPS, sem depender de cálculos sobre o índice.
- Captura de monitor com DIB reutilizável e redimensionamento direto, sem criar e redimensionar dois bitmaps a cada quadro. Captura de janela preserva PrintWindow.
- Variante local do agendador de quadros do MR-WebRTC; procedência e alteração reproduzível em Build/VIDEO_FPS_PROVENANCE.md. Exibição de tela e receptor deixam de limitar a taxa a cerca de30 FPS. Bitrate adapta-se à resolução e taxa escolhidas.
- Medição local: fonte nativa15.1/30.2/60.2 FPS; captura real59.7 FPS e recepção55.3 FPS via VP8/DTLS-SRTP no modo60. Não é garantia de desempenho em outros PCs ou redes.

