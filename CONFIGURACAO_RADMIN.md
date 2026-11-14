# LAN/Radmin — modo automático 0.4

1. Todos devem entrar na mesma rede Radmin VPN ou LAN.
2. Abra **Client.exe** no primeiro computador. Crie o perfil; ele inicia o host embutido se não encontrar outro.
3. Abra **Client.exe** nos outros PCs. A descoberta procura o mesmo host automaticamente. Não é necessário abrir Server.exe nem configurar port forwarding se a VPN permitir tráfego direto entre os participantes.
4. No Firewall do Windows, permita **Client.exe** para o perfil de rede usado pela interface Radmin, restringindo os endereços remotos aos amigos/sub-rede necessária. São usados HTTPS/TCP em porta dinâmica, UDP 39334 para descoberta e UDP dinâmico para mídia WebRTC. Liberar só TCP 5000 não basta neste modo.
5. Use **Adicionar amigos** e o nome exato. O destinatário aceita em **Pendentes**. A DM aparece para ambos e conserva histórico. Para voz/câmera/tela, entrem no mesmo canal de voz de um servidor.

Não desligue o firewall inteiro. Na primeira conexão automática, o host é descoberto pela própria rede; use apenas uma LAN/VPN confiável. O certificado fica fixado no perfil nas conexões seguintes. O nome é único dentro dessa rede coordenada, não entre redes desconectadas.

## Se a descoberta não funcionar

Algumas configurações de VPN/firewall bloqueiam broadcast. Na tela inicial, escolha **Conectar a uma rede por endereço**. Para consultar o host automático no computador que hospeda, veja `%LOCALAPPDATA%/JanjaCord/network/endpoint.json`: `Host.Port` é a porta e `Host.Fingerprint` a impressão. Use `https://IP-Radmin:porta` nos demais PCs; o endereço 127.0.0.1 do arquivo só serve no próprio host. Compartilhe a impressão diretamente com os amigos.

Teste TCP com `Test-NetConnection IP-RADMIN -Port PORTA`. Para mídia, permita UDP de Client.exe nos dois lados. Não existe relay público para contornar falta de conectividade direta. Se necessário, use o host dedicado opcional com `Server.exe --discover true --port 5000` e informe seu endereço/impressão.

## Disponibilidade e dados

O primeiro cliente é o coordenador; mantenha-o aberto. Se fechar, os demais conservam o cache das últimas DMs e aguardam o mesmo host voltar. Não há transferência automática do banco para outro PC. Reabrir o cliente que hospeda reutiliza os dados em LocalAppData/JanjaCord/host.

Se microfone/câmera não abrirem, confira os dispositivos padrão e as permissões para aplicativos da área de trabalho em Configurações → Privacidade do Windows. O compartilhamento desta versão envia imagem, sem áudio do computador.

Os testes automatizados foram feitos no mesmo Windows. Conexão entre dois computadores físicos pela Radmin ainda precisa ser validada; o fato de a máquina de teste possuir interface Radmin não comprova comunicação com outro PC.
