# JanjaCord 0.11.2 — abra só o cliente

Aplicativo desktop Windows x64 em .NET 10/Avalonia, sem navegador. Abra **Client/Client.exe**; não é necessário iniciar Server.exe. A primeira janela disponível hospeda a rede automaticamente e as demais a encontram por descoberta LAN/Radmin.

## Correção de voz 0.8.1

Nivelamento de voz antes da codificação do microfone e na reprodução: alvo RMS0.18, reforço de até 32× para sinais baixos e limiar0.0005. O ganho não reinicia nas pausas; picos fortes reduzem o ganho imediatamente. A conversão de microfone estéreo usa o canal com maior energia para evitar perder metade do volume ou cancelar fases opostas. O controle pessoal0–200% permanece salvo e independente. Atualize os dois clientes para corrigir também a captura de cada microfone. O volume global do Windows não é alterado.

## Novidades 0.8

- **Mensagens para amigos offline:** com o host aberto, o servidor guarda a DM normalmente. Se o próprio host estiver fechado, o cliente permite escrever e guarda uma fila local cifrada pelo Windows, com estado **Pendente**. A fila sobrevive a fechar/reabrir e é enviada automaticamente quando a mesma rede volta. O amigo lê ao retornar. O identificador de reenvio é preservado para evitar duplicar mensagens. Uma recusa definitiva aparece como falha; clique direito no estado para tentar novamente ou cancelar.
- **Tela cheia:** dois cliques no vídeo ou no botão de expandir abrem uma janela em tela cheia com quadros ao vivo. **Esc**, dois cliques no vídeo ampliado ou o botão de sair retornam à conversa. Parar a transmissão/sair da call também fecha a tela cheia.
- **Volume por pessoa:** clique direito no avatar redondo da pessoa na chamada ou no rodapé. O controle vai de **0% a 200%**, vale só para quem ajusta e fica salvo por rede/conta. Funciona durante a chamada e continua aplicado ao reconectar a voz. 0% silencia apenas aquela pessoa; 200% aplica o dobro do ganho de 100%.
- **Vozes baixas:** o recebimento reforça gradualmente fala baixa até 4×, sem aumentar o ganho de silêncio/ruído abaixo do limiar. Um limitador restringe os picos antes da reprodução. O ganho individual continua independente. Isso não altera o volume de outros aplicativos nem o mixer global do Windows.

## Toque de chamada 0.8.2

Chamadas recebidas tocam **toque-de-celular-de-brinquedo.mp3**, fornecido pelo usuário, em repetição enquanto aguardam resposta. O toque para ao atender, recusar, fechar o aviso, cancelar/expirar a chamada, perder a conexão ou fechar o cliente. Usa a saída de áudio escolhida no aplicativo, com padrão do Windows como alternativa se o dispositivo não existir. O arquivo acompanha o ZIP em `Client/Assets/incoming-call.mp3`; mantenha a pasta Assets junto do executável.

## Supressão de ruído e voz natural — 0.9

Na chamada, abra **...** e ligue/desligue **Supressor de ruído**. A opção vem ativada, muda durante a chamada e fica salva para as próximas aberturas. Funciona no **seu microfone**, antes de enviar a voz: cada participante deve usar o cliente atualizado para reduzir os ruídos do próprio microfone.

O processamento usa RNNoise local, filtro de graves e detecção de fala com transições suaves para reduzir teclado, mouse e respiração. Pequenos buffers preservam o início das palavras. Desligar restaura o caminho sem supressão; o nivelamento de voz e o volume pessoal continuam funcionando. Não precisa de GPU, conta ou serviço de IA; a DLL e o modelo estão incluídos no ZIP.

A voz agora recebe **um único nivelamento no microfone**. Foi removido o segundo ganho automático na reprodução, que realçava artefatos e ruídos. Limitadores com antecipação de 5ms na captura e na saída reduzem os picos em vez de cortar a onda bruscamente. O controle individual0–200% continua salvo. **Atualize todos os clientes para0.9** para usar o novo processamento de ponta a ponta.

Testes de bancada usam ruídos simulados e fala sintetizada em português, sem gravar conversas ou microfones. O resultado real varia com o microfone e com a proximidade/intensidade dos sons; ruído misturado à fala pode continuar audível. Não há garantia de cancelamento total nem correção de áudio que já esteja saturado no hardware.

## Testar e ajustar a intensidade — 0.11.2

Na chamada, abra **...**. O controle **Intensidade** vai de **0,00** (sem supressão) a **1,00** (máxima supressão). Aumente se passar muito ruído; reduza se partes da voz sumirem. O valor inicial é0,75 e as mudanças ficam salvas automaticamente. A caixa **Supressor de ruído** continua permitindo ligar/desligar sem perder o valor escolhido.

Clique **Testar meu microfone** para ouvir um retorno local com o mesmo processamento e codec Opus. Use fones para evitar eco. Você pode mudar a intensidade e ligar/desligar o filtro enquanto escuta. O medidor mostra o nível do som processado. Clique **Parar teste** ou feche a janela para encerrar.

Enquanto testa, o envio do microfone e a reprodução da chamada ficam pausados para você; o retorno é local e não é enviado aos outros. Ao parar, o estado anterior de mudo/áudio recebido é preservado. Não há gravação em arquivo. O teste para também ao fechar a janela, sair da chamada ou perder a conexão. Se trocar dispositivos, pare o teste antes de escolher outros.

## Usar

1. Extraia **todo** o ZIP. Mantenha as DLLs e os arquivos de configuração junto ao executável. O runtime está incluído; não precisa instalar SDK.
2. Entre na mesma rede Radmin VPN dos amigos ou na mesma LAN. Abra `Client/Client.exe` em cada PC.
3. Na primeira vez, escolha nome e foto. Clique em **Criar meu perfil**. O cliente encontra a rede existente ou inicia um host embutido. Nas próximas aberturas, entra automaticamente com o perfil salvo.
4. A tela inicial é **Amigos**, com Disponível, Todos, Pendentes e **Adicionar amigos**. O ícone **J** retorna a essa tela, mesmo quando você participa de servidores.
5. Clique **Adicionar amigos**, digite o nome exato usado pela pessoa na mesma rede e envie. A DM aparece imediatamente na barra lateral. O outro usuário aceita em **Pendentes** ou dentro da DM; então vocês podem enviar mensagens. Solicitações recebidas também podem ser recusadas.
6. As DMs são entregues em tempo real e salvas em SQLite. É possível enviar para um amigo offline se o host estiver conectado: ele encontra o histórico ao voltar. As últimas 100 mensagens carregadas por DM também ficam em cache local cifrado pelo Windows para consulta offline.
7. **+** cria um servidor; a seta abaixo entra por ID. No servidor, crie canais de texto ou voz. O menu no nome do servidor permite convidar pelo ID e, para o dono, excluir com confirmação pelo nome.
   Se o ID informado não existir, o diálogo mostra **Esse servidor não existe. Confira o ID e tente novamente.** e permanece aberto para corrigir. IDs inválidos recebem uma mensagem própria.
8. Na DM de um amigo, clique no telefone no topo para ligar. Ele pode **Atender** ou **Recusar**. O microfone abre ao atender; a câmera e a tela só abrem quando você escolhe uma fonte e confirma. O botão vermelho encerra para os dois. Chamadas sem resposta expiram em 35 segundos.
9. A barra compacta tem microfone, câmera, compartilhar tela, **...** e desligar, sem botões decorativos. Clique em **...** para escolher o microfone e os fones/alto-falantes. **Aplicar** muda o áudio da chamada e salva a escolha para as próximas aberturas; o padrão do Windows permanece intacto. Os canais de voz usam os mesmos controles.
10. A versão 0.6 corrige o crash ao enviar DMs e rolar o histórico: a lista podia entregar um item nulo ao reciclar a mensagem. Todos os participantes, inclusive o cliente que hospeda, devem atualizar para 0.7 por causa do novo protocolo de áudio. Seus dados em LocalAppData continuam preservados.


## Correções 0.7

A voz anuncia as interfaces LAN/Radmin além da rota padrão e preserva sinais que chegam antes da lista de participantes. Uma sala vazia mostra **Aguardando outra pessoa entrar**. Se a conexão de voz não responder em 20 segundos, aparece **Reconectar voz**. O motor de vídeo negocia somente câmera/tela, sem a trilha de áudio nativa vazia; o áudio WASAPI usa o dispositivo padrão normal do Windows. Nenhum volume de outro aplicativo ou preferência global do Windows é alterado pelo cliente.

Quem transmite aparece duas vezes na área da chamada: como pessoa e como **Tela de [nome]**, com a transmissão real em uma entrada independente. Ao parar, apenas a entrada da tela desaparece.

A foto pode ser salva sem conexão. Ela fica no mesmo perfil local e é sincronizada quando a rede volta. O botão Salvar só fica desativado durante a escolha/preparação da foto ou durante o envio. Alterar o nome ainda exige rede para verificar nomes em uso. O diálogo aberto não é fechado pela reconexão automática. Todos os clientes/host precisam usar 0.7 (protocolo 6).

## Como funciona sem abrir servidor

O servidor de comunicação agora pode rodar **dentro do processo Client.exe**. Não é um serviço externo nem um segundo executável iniciado escondido. A descoberta usa UDP 39334; chat/contas/DMs usam HTTPS em uma porta escolhida automaticamente. A mídia é WebRTC direta entre participantes.

**O cliente que hospeda a rede precisa continuar aberto para entregar novas mensagens e gerenciar chamadas.** Se ele fechar, os outros mostram a rede offline e conservam as DMs já carregadas em cache. Ao reabrir o mesmo cliente host, seu banco e certificado são reutilizados e os clientes tentam reconectar. Não há migração automática do banco para outro PC. Isso evita abrir silenciosamente uma rede vazia e aparentar que as conversas sumiram.

Uma rede nova procura um host existente antes de iniciar outro. A inicialização simultânea tenta convergir para um host por ID determinístico. Se a VPN/firewall não transportar descoberta, use **Conectar a uma rede por endereço** na tela de perfil. Esse é o modo avançado, com endereço HTTPS e impressão SHA-256. O Server.exe continua opcional para quem prefere hospedar separadamente; `Server.exe --discover true` também anuncia essa rede.

## Nomes e perfil

Para editar seu perfil, clique no **seu nome ou sua foto no canto inferior esquerdo**. No painel **Meu perfil**, altere o nome, escolha outra foto ou remova a atual e clique em **Salvar alterações**. Cancelar descarta o que foi alterado nesse painel. Para alterar o nome é necessário estar conectado; nomes em uso são recusados sem alterar o perfil anterior. A foto pode ser salva offline e sincronizada depois. Amigos, DMs e identidade da conta são mantidos. O nome/foto atualizados são sincronizados com amigos, membros dos servidores e participantes das chamadas.

O primeiro cadastro já guarda nome, foto e a chave da conta no Windows. Nas próximas aberturas, o cliente reconhece esse perfil e mostra **Bom te ver novamente**, entrando automaticamente sem pedir para criar outra conta. A gravação local é atômica e mantém `profile.json.bak` para recuperação caso o arquivo principal esteja incompleto. Extraia atualizações normalmente: os dados do perfil ficam em LocalAppData e não são substituídos pelo ZIP. Atualize também o cliente que está hospedando a rede para permitir a edição online.

Nomes já usados na mesma rede são recusados. Não são criados sufixos automáticos. A verificação normaliza Unicode, espaços nas pontas e maiúsculas/minúsculas, e a reserva é protegida por restrição única no banco. Um nome continua reservado quando seu dono fica offline. Só o mesmo dispositivo pode retornar à conta ou alterar seu nome para outro disponível.

Isso não é um registro global: redes isoladas, sem comunicação entre si, não conseguem verificar os nomes umas das outras. Todos devem conectar à mesma rede descoberta. Use somente uma LAN/VPN confiável: o primeiro encontro do host é automático; depois o certificado é fixado pelo SHA-256 salvo no perfil.

O cliente gera uma chave aleatória, protegida por DPAPI e vinculada à conta Windows, em `%LOCALAPPDATA%/JanjaCord/profile.json`. O servidor guarda seu hash. Não é possível assumir uma conta apenas informando seu nome. Não existe recuperação ou transferência de perfil para outro Windows. A foto é reduzida para PNG 160 × 160.

## Dados e atualização

- Perfil: `%LOCALAPPDATA%/JanjaCord/profile.json`.
- Host automático: `%LOCALAPPDATA%/JanjaCord/host/`, incluindo banco SQLite e certificado privado.
- Cache de DMs: `%LOCALAPPDATA%/JanjaCord/cache-*.bin`, cifrado para o usuário Windows e separado por rede.
- Em uma primeira hospedagem automática, se existir `../Server/data/janjacord.db` junto da instalação anterior, é feita uma cópia SQLite consistente para o novo host, preservando a origem. Mantenha essa pasta antiga ao atualizar se precisar importar seus servidores.

Faça backup da pasta do host com o cliente parado. Não distribua seu banco, perfil ou `server.pfx`. O ZIP gerado não inclui dados pessoais nem conversas de teste. O Server.exe opcional usa a pasta antiga `data` se ela já existir junto dele; caso contrário usa LocalAppData/JanjaCord/Server. Aceita `--port 5002` e `--data-dir "C:/pasta"`.

## Correção do ZIP 0.3

O crash foi reproduzido: o pacote anterior omitia arquivos ocultos do runtime do servidor, entre eles `Server.runtimeconfig.json`, `coreclr.dll` e `e_sqlite3.dll`. O empacotamento passou a incluir arquivos ocultos e a usar a API ZIP do .NET. `Build/zip-smoke.ps1` verifica essas entradas, extrai o ZIP em outra pasta e testa os executáveis extraídos, em vez de confiar apenas na pasta de publicação.

## Mídia e limitações

Voz usa Opus/DTLS-SRTP via SIPSorcery e dispositivos WASAPI selecionáveis via NAudio; vídeo usa VP8 no motor nativo. Câmera: alvo 640 × 480 / 30 FPS com alternativa ao formato padrão do dispositivo. Tela/janela: captura GDI/PrintWindow real, até 720p/1080p, 15/30 FPS, proporção preservada. Mesh até oito conexões por sala, sem SFU/TURN/STUN externos. As chamadas precisam ser retomadas após interrupção da rede.

Ainda não há áudio do computador compartilhado, WGC, aceleração por hardware, 60 FPS, push-to-talk, cancelamento de eco acústico (AEC), arquivos, edição/exclusão/respostas de mensagens, cargos, categorias ou notificações do Windows. Janelas protegidas/minimizadas/GPU podem exigir compartilhar o monitor. Busca filtra o histórico carregado, não todo o banco. Chamadas diretas exigem amizade aceita e ambos online.

Microsoft.MixedReality.WebRTC 2.0.2 é um motor antigo com upstream arquivado. Precisa de manutenção/substituição antes de tratar a aplicação como produto final com suporte. Consulte `THIRD_PARTY_NOTICES.md`. Microfone/câmera físicos, dois PCs na Radmin e carga com vários usuários ainda exigem validação manual; os testes locais não substituem isso.

## Desenvolvimento e testes

```powershell
./.tools/dotnet/dotnet.exe build Tests/Tests.csproj -c Release
./Build/test.ps1
./.tools/dotnet/dotnet.exe Tests/bin/Release/net10.0/Tests.dll --auto
./Build/publish.ps1
./Build/visual-smoke.ps1
./Build/auto-smoke.ps1
./Build/profile-smoke.ps1
./Build/package.ps1
./Build/zip-smoke.ps1
```

Os testes usam perfis e bancos isolados. `auto-smoke.ps1` abre dois Client.exe reais, adiciona/aceita amizade, troca 65 DMs para forçar a reciclagem da lista, recusa/atende/desliga chamadas, aplica dispositivos de áudio, reabre o cliente, verifica cache offline e tenta reconexão após reabrir o host. `smoke.ps1` também transmite um monitor real entre duas conexões, sem gravar o conteúdo. O teste de mídia não abre câmera/microfone; o teste da interface atende a chamada e silencia os microfones imediatamente.

Veja `CONFIGURACAO_RADMIN.md` para firewall e `ESTADO_PROJETO.md` para continuidade e evidências.

Clique com o botão direito no microfone de mutar para abrir as configurações de áudio e testar o microfone, mesmo fora de uma chamada. O teste só inicia ao clicar em Testar meu microfone.

Supressão reforçada: menos mistura de áudio sem filtro, limiar de voz mais exigente e silenciamento gradual entre falas. Intensidade 0 desliga o efeito e 1 aplica o máximo. Novos perfis iniciam em 0,90; ajustes existentes são preservados.
# Novidade 0.11.2: filtro avançado

Clique com o botão direito no microfone e escolha **Avançado · DeepFilterNet**. O novo modelo neural processa o áudio localmente, sem gravar ou enviar áudio para nuvem. Continua disponível o modo **Leve · RNNoise**, que consome menos CPU. A escolha, a intensidade e o botão liga/desliga ficam salvos; use o teste local para comparar os dois modos no seu microfone.

# Novidade 0.11.2: grupos e bandeja do Windows

Na DM, clique no ícone de pessoas com **+** ao lado do telefone. Escolha seus amigos, dê um nome ao grupo e confirme. O limite é **10 pessoas incluindo você**. Os grupos aparecem na lista de mensagens diretas e guardam o histórico na rede. O telefone inicia uma chamada do grupo; cada pessoa escolhe se deseja entrar.

O **X** agora esconde a janela e mantém o aplicativo conectado em segundo plano, inclusive a rede hospedada por ele. Para abrir novamente, clique no ícone roxo J na bandeja do Windows, que pode estar dentro da setinha. Para encerrar de verdade, clique com o botão direito nesse ícone e escolha **Sair do JanjaCord**.

É possível iniciar uma ligação para um amigo offline. A tentativa dura até35 segundos: se ele se conectar nesse período, recebe o convite. Um aplicativo encerrado por completo ou um computador desligado não pode tocar nem atender. Atualize também os outros clientes e o computador que hospeda a rede para usar grupos.

# 0.11.2: veja as chamadas em andamento

Uma chamada ativa do grupo aparece no topo da conversa com os avatares de quem está na voz e o botão verde **Entrar na chamada**, mesmo se você recusou o convite inicial ou abriu a conversa depois. Na lista de DMs e de membros, **Em chamada** identifica quem está usando a voz. No PV, se o contato estiver em outra conversa, aparece esse status sem revelar os outros participantes. O painel atualiza quando as pessoas entram, saem ou encerram a chamada. Atualize também os outros clientes e o host da rede.

# 0.11.2: transmissão com 15, 30 ou 60 FPS

Em **Compartilhar tela**, escolha 720p ou 1080p com 15, 30 ou 60 FPS. O padrão agora é720p30. A captura do monitor usa buffers GDI reutilizáveis, e o motor e a exibição permitem 60 FPS. Taxa efetiva depende do computador, da rede e da janela compartilhada.

