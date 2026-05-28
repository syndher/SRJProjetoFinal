# Sistema de Redes para Jogos - Projeto Final

## Miguel Filipe nº22408872

### Introdução

O projeto consiste num simples jogo dos tanques onde dois jogadores vão um contra o outro, as balas podem acertar no seu próprio tanque e estas rebatem pelas paredes do cenário tal como uma parede que é alterada dinâmicamente ao longo do jogo.

### Técnicas

O jogo funciona por Unity Relay.

Foi utilizado o "Unity Netcode for GameObjects" para implementar o jogo de ação, cliente/servidor sem qualquer tipo de login/matchmaking, tal como Unity Relay, Unity Transport (UTP) e Unity Authentication simplesmente para a utilização do Relay e não para login em si.

Ao ler os argumentos passados quando se corre a build, neste caso quando se pressiona Server ou Join, este envia "--server" ou "--client (code)" respetivamente sendo apenas necessário uma build para tanto correr um servidor como juntar-se a um.

Os jogadores, as balas e a própria parede rotativa sao networkBehaviours, de modo a que o servidor instancia as balas disparadas pelos jogadores através do ServerRPC. A parede rotativa contem um componente Network Transform para se manter em sincronia com todos os jogadores através do servidor, tal como as balas tem o componente Network Object.

O próprio NetworkSetup regista os jogadores que entram no jogo, passando essa informação para o InGameUI, sendo capaz de sincronizar as barras de vida corretas para cada jogador tal como se alguem perdeu e é necessário mostrar um ecrã de vitória.

Para o jogo começar, ambos jogadores tem de já ter entrado no servidor

### Estado/Uso

O projeto encontra-se pouco responsivo em termos de gameplay mas inteiramente funcional por build sem qualquer erro.

Instruções:

- Uma build corre em servidor pela opção Server.
- Jogadores entram nesse servidor pelo código disponível no canto do ecrã disponível da sua build.

Controlos:

- Setas horizontais - Virar
- Setas verticais - Mover
- Espaço - Disparar

### Bibliografia

A pesquisa para a construção deste projeto focou-se maioritariamente nos videos do professor da disciplina tal como a utilização do código de networking providenciado pelo mesmo, modificado para obter a funcionabilidade atual.

Unity Documentation : <https://docs.unity3d.com/560/Documentation/ScriptReference/Network.html>

Ajuda de AI : <https://www.deepseek.com/>
