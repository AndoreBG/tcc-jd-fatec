# Whispers from the Unknown — estado da implementação

> Rádio: desde esta atualização, cada transmissão configurada inicia automaticamente após o boot do Dia, sem exigir navegações. A tabela antiga mantém o campo de navegações apenas para compatibilidade; ele é ignorado. O objetivo continua aparecendo depois do rádio.

> Tutoriais: o menu `Whispers > Dia > Configurar tutoriais e conclusão` limita os hotspots básicos ao Dia 1 e remove a chamada legada de confirmação apenas na coleta da lanterna. O fluxo automático apresenta os painéis ao concluir os objetivos dos Dias 1 a 3; requer executar o menu e testar no Unity.

> Atualização: o objetivo aguarda o término do rádio configurado para o Dia e permanece visível até ser ocultado ou a cena terminar. Sem rádio utilizável ou quando já consumido, aparece ao iniciar. As descrições antigas de duração abaixo ficam substituídas por esta regra.

Documento de situação. Diferente do `Progresso.md`, que é cronológico (o que foi feito em
cada sessão), este é um retrato do agora: o que existe, o que falta, o que está decidido e
o que ainda depende de decisão.

Base do repositório: commit `ad11262` ("fix cena do dia") mais as entregas desta série.
Data: 11 de novembro de 2026.

---

## 1. Resumo

O jogo hoje tem o ciclo jogável ponta a ponta em termos de sistema: menu, Dia com
navegação por pontos de vista, interações e mochila, evento de rádio como camada de
interface dentro do Dia, Noite com entidades, troca de período com checkpoint, e quatro
Dias encadeados até o fim do ciclo.

O que falta é quase todo **autoria**: texto do rádio, arte da VN, áudio, e a decisão sobre
o fechamento do Dia.

| Marco | Escopo | Situação |
|---|---|---|
| M1–M6 | sessão, ciclo, navegação, hotspots, interações, mochila, save | concluído |
| M7 | evento de rádio | concluído, refeito como VN na cena do Dia |
| M8 | cutscene do mundo entre menu e Dia 1 | **retirado** por decisão do usuário |
| M9 | progressão dos Dias | concluído: tabela, objetivos, banner, travas e painel de tutorial. O uso do painel é opcional |
| M10 | fonte SpecialElite | concluído |
| Relógio | sprites trocando por hora | concluído, relógio do Dia estático em 01PM |

Números do repositório: 105 scripts em `Scripts/` (17.302 linhas), 14 scripts em
`Editor/` (4.058 linhas), 102 assets em `SO/`, 3 cenas no Build Profile e uma `day.unity`
com 10.988 linhas de YAML.

---

## 2. O que o jogador percorre hoje

```text
main_menu
   │  Jogar                    → PrepareNewGameSession (grava checkpoint inicial)
   ▼
day  ─ legenda "Dia 1" no carregamento
   │  boot: cortina, apresentação do ViewNode inicial, 1,5s
   │  rádio do Dia toca (boletim de abertura)        [Dia 1]
   │  ou depois da 3ª troca de ViewNode              [Dias 2 a 4]
   │  rádio termina → devolve a sala
   │  botão de fim do Dia
   ▼
night ─ entidades noturnas ativas (Predador, Voyeur, Exhauster)
   │  Noite vencida → checkpoint consolidado
   ▼
day (Dia 2) → … → Dia 4 → rádio termina no modo AdvanceToNight
   ▼
fim do ciclo
```

Detalhes que valem saber antes de mexer:

- O Dia 1 dispara o rádio **no boot**, sem depender de navegação. Os outros Dias dependem
  do contador de navegações, autorado por Dia.
- O rádio dos Dias 1 a 3 devolve o jogador ao mesmo ponto de vista, sem recarregar nada. O
  do Dia 4 encerra o Dia (`returnMode = AdvanceToNight`).
- "Continuar" no menu não passa por cutscene nenhuma: vai direto ao período salvo.

---

## 3. Cenas e Build Profile

| Cena | Papel | No Build Profile |
|---|---|---|
| `main_menu.unity` | menu, opções, modais de novo jogo e continuar | sim, índice 0 |
| `day.unity` | período do Dia, 9 ViewNodes, 9 hotspots de relógio, fluxo do Dia | sim, índice 1 |
| `night.unity` | período da Noite, entidades, relógio com troca de sprites | sim, índice 2 |

Não existe mais `radio_event.unity`: o rádio virou camada de interface na `day`. A cena foi
apagada do repositório; a versão antiga está no histórico do commit `ad11262`.

---

## 4. Arquitetura por sistema

Cada sistema tem uma autoridade única. Mexer em um não deve exigir mexer no outro.

### 4.1 Sessão e ciclo — `Scripts/Session/`

`GameSessionManager` é a autoridade do estado: Dia, Noite, itens, fatos, flags persistentes,
checkpoint e carregamento entre cenas. `GameCycleDefinition` (asset `CYCLE_Gameplay`) diz
quais cenas formam o ciclo e quantas Noites ele tem.

Regra: a sessão guarda o que sobrevive à troca de cena. O que é de cena morre com a cena.

### 4.2 Navegação — `Scripts/Navigation/`

`NavigationManager` apresenta um ViewNode por vez e é a única coisa que troca de ponto de
vista. O hotspot apenas **pede**; quem valida, transiciona, bloqueia e emite
`ViewNodeChanged` é o manager. Não há fila nem cooldown.

`ViewNodeDefinition` (assets `VN_*`) diz qual perfil de câmera, qual áudio e qual nome de
gameplay cada nó tem.

### 4.3 Hotspots, condições e interações — `Scripts/Hotspots/`, `Scripts/Conditions/`, `Scripts/Interactions/`

Todo hotspot herda de `HotspotBase`: modo de ativação (hover, hover com espera, clique),
política de repetição, lista de condições com política Todas/Qualquer, e o que fazer quando
indisponível (oculto, bloqueado, bloqueado com dica).

As condições são `ScriptableObject` sem estado de runtime: recebem um `ConditionContext` e
respondem. Hoje existem condições de período, de item, de coleta, de fato, de interação
concluída, de lampião quebrado e, desde o M9, de faixa de Dia.

### 4.4 Interface de cena — `Scripts/UI/`

`CanvasUI` guarda o HUD do jogo: relógio (texto da hora), tooltip de hotspot, mochila e os
modais. Três canvases se empilham por `sortingOrder`:

| Camada | `sortingOrder` | Origem |
|---|---|---|
| HUD, tooltip | 0 e 100 | autorado na cena |
| banner de objetivo | 150 | menu `Whispers > Dia > Criar banner de objetivo` |
| VN do rádio | 200 | menu `Whispers > Rádio > Criar VN na cena day` |

### 4.5 Relógio

A hora é **texto**. Os sprites são arte de cena: 7 imagens
(`clock_wall_12 01 02 03 04 clock_wal_05l 06`) distribuídas em 9 hotspots de relógio pela
cena, cada `Clock` filho esticado no retângulo do hotspot. Durante o Dia o relógio fica
estático em **01PM**; a troca de sprites acontece só na Noite.

O `05l` em `clock_wal_05l.png` é typo do arquivo original e é intencional: o nome é o do
artista e não se renomeia arte.

### 4.6 Áudio — `Scripts/Audio/`

`SceneAudioController` aplica o perfil de áudio do ViewNode no ponto de troca da navegação.
Os perfis (`AUDIO_VN_*`) guardam a perspectiva por âncora de áudio (`ANCHOR_*`). O rádio
tem uma âncora própria (`ANCHOR_Radio_CorredorFront`), aplicada por menu nos nove perfis.

### 4.7 Rádio como VN — `Scripts/Radio/`

`RadioEventController` é componente de interface dentro da `day`. Não carrega cena: cobre o
gameplay, roda o evento e devolve o controle.

```csharp
public bool Play(RadioEventDefinition definition = null);
public bool IsPlaying { get; }
public event Action Finished;
```

Sequência: escurece 0,4s → jingle (espera autorada, só se há jingle) → arte com o primeiro
frame → fade de abertura → linhas com typewriter → fade para preto → retorno. Entrada de
gameplay bloqueada por `InputBlockReason.Cutscene` durante todo o evento.

O jogador pula o typewriter, nunca o evento. Clique durante o preto não conta: a janela de
avanço só abre com linha na tela.

`RadioEventDefinition` (`RADIO_Dia1..4`) guarda linhas, frames, jingle, clips de typewriter,
velocidade e o modo de retorno.

### 4.8 Dia: fluxo, tabela, objetivos e travas — `Scripts/Day/`

`DayFlowController` é quem decide quando o Dia tem rádio e o que o jogador deve fazer nele.
Lê uma tabela (`DayFlowDefinition`) com uma entrada por Dia: objetivo, navegações exigidas
e rádio.

```text
DayFlowEntry
 ├── day
 ├── objective (ObjectiveDefinition)      → banner no canto superior esquerdo
 ├── requiredNavigationsBeforeRadio
 └── radioEvent (RadioEventDefinition)
```

A tabela **vence** os campos que já estavam serializados na cena, mas não os apaga: sem
tabela, o Dia funciona como funcionava antes do M9.

As travas de navegação são condições de faixa de Dia aplicadas por **destino de navegação**
(`vn_portasala`, `vn_portacozinha` a partir do Dia 2; `vn_sotao` a partir do Dia 3), no modo
`Blocked`: a porta continua visível e não clica.

### 4.9 Painel de tutorial do Dia — `Scripts/UI/TutorialPanelController.cs`

Painel de conteúdo, aberto por gatilho de cena: o hotspot chama
`DayFlowController.OpenTutorial()` num UnityEvent, e o controller resolve o painel do Dia
corrente na tabela. Não existe detecção automática de "o jogador aprendeu": quem decide
quando o painel aparece é a cena autorada, o mesmo padrão que o `DayEndConfirmationController`
já usava.

```text
DayFlowEntry.tutorial (TutorialPanelDefinition)
   ├── title / body / proceedLabel / illustration
   ├── endPeriodOnProceed       → ligado, o botão encerra o Dia
   └── tutorialId               → chave de "já mostrei neste Dia" (sessão)
```

Regras: bloqueia o cenário com `InputBlockReason.Cutscene`, libera o bloqueio **antes** de
pedir o fim do período (a solicitação exige entrada livre), `ESC` não fecha por padrão, e o
painel abre uma vez por Dia.

Ele convive com o `DayEndConfirmationController`, que continua sendo quem encerra o Dia
hoje. Trocar um pelo outro é decisão de cena, não de código.

### 4.10 Entidades noturnas — `Scripts/Entities/`

`EntityDirector` aplica o perfil da Noite (`NIGHT_Entities_1..4`, asset `ENT_*`) e conduz
Predador, Voyeur e Exhauster. Cada Noite usa a mesma cena `night.unity` e se diferencia pelo
perfil.

### 4.11 Save e checkpoint — `Scripts/Save/`

Um checkpoint por transição de período, gravado antes do carregamento. Falha ao gravar
impede a troca de cena, em vez de trocar e perder o estado.

---

## 5. Dados autorados (assets)

102 assets em `SO/`, organizados por domínio:

| Pasta | Conteúdo |
|---|---|
| `SO/` (raiz) | `CYCLE_Gameplay`, `CYCLE_Playground` |
| `SO/ViewNodes` | 9 `VN_*` |
| `SO/GameplaySceneDefinition` | `DEF_Day`, `DEF_Night` |
| `SO/Radio` | `RADIO_Dia1..4` |
| `SO/Documents` | 5 `DOC_TUTORIAL_*` |
| `SO/Interactions` | 8 `INT_*` |
| `SO/Itens` | 7 `ITEM_*` e o banco `DB_Itens` |
| `SO/Backpack` | 7 `BPSlot_*` |
| `SO/Conditions` | 13 condições (`COLLECTED_*`, `HASITEM_*`, `FACT_*`, `LAMP_*`) |
| `SO/Audio` | catálogo de debug, 9 perfis `AUDIO_VN_*` e âncoras |
| `SO/NightEntities` | 4 perfis de Noite e `SETTINGS_NightClock` |
| `SO/Entities` | `ENT_Predator`, `ENT_Voyeur`, `ENT_Exhauster` |
| `SO/TransitionProfile`, `SO/ViewCameraProfile`, `SO/Feedback`, `SO/GlobalHotspotSettings` | perfis de apoio |

Três pastas **não existem no repositório** porque são criadas por menu no projeto do usuário:

| Pasta / asset | Menu que cria |
|---|---|
| `SO/Day/DayFlow_Gameplay.asset` | `Whispers > Dia > Criar tabela do Dia` |
| `SO/Day/TUTORIAL_Dia1..4.asset` | `Whispers > Dia > Criar painel de tutorial` |
| `SO/Objectives/OBJ_Dia1..4.asset` | `Whispers > Dia > Criar objetivos dos Dias` |
| `SO/Conditions/COND_Dia2`, `COND_Dia3` | `Whispers > Dia > Aplicar travas de navegação` |

---

## 6. Menus de editor

25 menus, agrupados por domínio. A prioridade define a ordem na barra.

| Prioridade | Menu | Para quê |
|---|---|---|
| 10 | `Whispers > Cenas > Verificar integridade das cenas` | valida o YAML de todas as cenas (ids, componentes, órfãos) |
| 30 | `Whispers > Dia > Criar tabela do Dia` | cria a tabela dos Dias e liga no fluxo |
| 31 | `Whispers > Dia > Criar banner de objetivo` | cria o banner no canto superior esquerdo |
| 32 | `Whispers > Dia > Aplicar travas de navegação` | cria as condições e trava os hotspots por destino |
| 33 | `Whispers > Dia > Validar tabela do Dia` | lista erros e avisos da tabela |
| 34 | `Whispers > Dia > Criar objetivos dos Dias` | cria `OBJ_Dia1..4` e liga na tabela |
| 35 | `Whispers > Dia > Criar painel de tutorial` | cria o `Canvas_Tutorial`, os `TUTORIAL_Dia1..4` e liga no fluxo |
| 40/41 | `Whispers > Mochila > …` | aplica e verifica os itens e hotspots da mochila |
| 60 | `Whispers > UI > Aplicar tooltip, relógio e objetivos` | monta HUD, tooltip e hotspots de relógio |
| 61 | `Whispers > UI > Verificar textos de navegação` | confere os nomes de gameplay |
| 70 | `Whispers > Rádio > Criar VN na cena day` | cria o canvas da VN do rádio |
| 71 | `Whispers > Rádio > Aplicar fluxo do Dia` | cria o `Manager_DayFlow` e liga os quatro Dias |
| 72 | `Whispers > Rádio > Aplicar anchor nos perfis de áudio` | aplica a âncora do rádio nos nove perfis |
| — | `Whispers > Exhauster > …` (3), `Whispers > Perfis > …` (2), `Whispers > Predator`, `Whispers > Voyeur`, `Whispers > VS3`, `Whispers > VS8`, `Whispers > VS9` (2) | autorias e testes das entidades |

Regra de convivência: **os menus não escrevem texto**. Campos de narrativa nascem vazios (ou
com o texto do plano, quando é ponto de partida) e nunca são sobrescritos em asset que já
tem conteúdo.

---

## 7. O que está verificado e o que não está

O ambiente onde o código foi escrito **não tem Unity nem compilador**. Toda validação foi
estrutural:

| Verificação | Como |
|---|---|
| Sintaxe básica | contagem de chaves, parênteses e colchetes em cada arquivo tocado |
| Campos ligados por menu | nome do campo em `SerializedObject` conferido contra a declaração no componente |
| Integridade de cena | validador bidirecional de YAML (pai/filho, PPtr, ids duplicados) |
| Regressões de sessão | diff de membros antes/depois em `GameSessionManager` |
| Assets escritos à mão | GUID conferido contra o `.meta` |

O que **só o Editor pode responder**: se compila, se a ordem visual dos canvases está
certa, se o TMP renderiza como esperado, se a arte encaixa na resolução, e se os cliques
chegam onde deveriam.

---

## 8. Pendências de autoria

Nada aqui é código. É conteúdo.

| O quê | Onde | Estado |
|---|---|---|
| Texto do rádio dos quatro Dias | `SO/Radio/RADIO_Dia1..4`, campo `lines` | **vazio** — sem linha o Dia não dispara rádio |
| Arte da VN do rádio | `Frame_01..03` do `Canvas_Radio` | **vazia** — precisa de sprites |
| Jingle e clips de typewriter | `RADIO_Dia*`, campos `jingle` e `typewriterClips` | **vazios** |
| Texto dos objetivos | `OBJ_Dia1..4`, `title` e `body` | títulos vindos do plano; `body` vazio |
| Texto dos painéis de tutorial | `SO/Day/TUTORIAL_Dia1..4`, `title` e `body` | **vazios** — o painel não abre sem texto |
| Textos dos avisos das entidades | `DOC_TUTORIAL_Predator/Voyeur/Exhauster` | escritos (voz humana, ~400 caracteres), revisáveis |
| Textos dos tutoriais do Dia | `DOC_TUTORIAL_Cliques/Movimentacao` | existentes, não auditados nesta série |

---

## 9. Decisões abertas

Três pontos esperando resposta do usuário. Nenhum bloqueia o jogo rodar.

**1. Fim do Dia: botão ou painel.** As duas peças existem agora. O
`DayEndConfirmationController` continua sendo quem encerra o Dia hoje, e o
`TutorialPanelController` está pronto com o botão "Prosseguir", que também pede o fim do
período quando `endPeriodOnProceed` está ligado. Trocar é desligar um e ligar o outro na
cena: não há código a escrever, só uma escolha de desenho. A ressalva de fundo permanece: o
rádio já ensina os verbos, e um painel por Dia pode repetir o que ele disse.

**2. Onde mora o prólogo.** O texto de sete linhas existe e está parado em
`Whispers_Of_Unknown_Prologo.md`. A casa mais barata é uma VN de abertura na cena do Dia,
reaproveitando o desenho do rádio. As alternativas (cartela na tela de carregamento,
documento no abrigo) também estão descritas no arquivo.

**3. Duração do jingle.** `jingleDelaySeconds` está em 5s nos quatro rádios. Se o clipe for
mais curto, o preto continua depois do som. O valor deveria seguir o tamanho do áudio.


Fechadas nesta entrega, para não voltarem como dúvida: **Dia 4 sem exploração** (a tabela
nasce com `requiredNavigationsBeforeRadio = 0` no Dia 4) e **limpeza de legado**
(`RADIO_EventoDia.asset` apagado e `GameCycleDefinition.worldCutsceneScene` removido).

---

## 10. Riscos e dívidas técnicas

**Sem compilação no ambiente de escrita.** Vale para todas as entregas: código validado por
estrutura, não por compilador. O primeiro Play depois de extrair é o teste de verdade.

**O `.git` do ambiente de trabalho é instável.** Em duas sessões o repositório voltou de um
snapshot antigo, com `HEAD` regredindo e objetos recentes ausentes. O trabalho foi refeito em
cima do `ad11262` e conferido por diff contra o `origin/main`. Existem duas proteções fora do
repositório: um patch completo e um script de reparo.

**`git push` não funciona do ambiente de trabalho** (sem credenciais). Todo commit fica
local, esperando o usuário empurrar.

**Cenário autorado à mão é frágil.** A regra que se firmou: cena é montada por menu, nunca
por YAML escrito à mão. As duas vezes em que se tentou editar cena por script, o resultado
foi PPtr quebrado ou órfão silencioso.

**Dois fechamentos de Dia possíveis.** Enquanto a escolha entre botão e painel não for
feita, existe o risco de os dois serem ligados na mesma cena e o jogador ver dois pedidos de
fim de Dia. A cena deve ter um caminho só.

**O relógio do Dia é estático em 01PM.** Foi decisão de design, não limitação: a passagem de
tempo aparece na Noite. Se o Dia precisar de relógio correndo, isso muda o sistema de tempo,
não só a arte.

---

## 11. Histórico desta série de entregas

Seis commits sobre o `ad11262`:

| Commit | Entrega |
|---|---|
| `293507f` | rádio vira VN na cena do Dia (e M8, depois retirado) |
| `63d274b` | prólogo e suporte de texto nos slides |
| `893d2f2` | typewriter e aviso de pulo com E |
| `0f2a228` | retirada da cutscene do mundo (desfaz o M8) |
| `0233eac` | M9 parte 1: tabela dos Dias, banner e travas |
| `69bbceb` | objetivos dos Dias como `ScriptableObject` |
| `b109ea7` | documento de estado atual da implementação |
| *(esta entrega)* | M9 parte 2: painel de tutorial, Dia 4 sem exploração, limpeza de legado |

## 12. Onde ler mais

| Documento | Conteúdo |
|---|---|
| `Whispers_Of_Unknown_Plano_Ciclo_Eventos_UI_v1.md` | plano completo, §1 a §11, com arquitetura, marcos e critérios |
| `Whispers_Of_Unknown_Plano_Ciclo_Eventos_UI_Progresso.md` | o que foi executado, sessão por sessão, §12 a §25 |
| `Whispers_Of_Unknown_Prologo.md` | o texto de abertura e as opções de onde usá-lo |
| este documento | o retrato do agora |

Para o TCC, a leitura útil é esta ordem: plano §2 (arquitetura), este documento §4 (como
cada sistema ficou), e Progresso §19 e §23 (as duas decisões de projeto que mais renderam
argumento: por que o rádio virou camada e por que a cutscene não virou cena).
