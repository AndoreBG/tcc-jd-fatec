# Whispers Of Unknown — Plano Técnico Fechado: Entidades Noturnas
## Predator e Voyeur — IA inspirada em FNAF

> **Status:** plano técnico aprovado para implementação; este documento não representa código já implementado.  
> **Engine-alvo:** Unity `6000.3.18f1`.  
> **Plataforma inicial:** PC, Input Manager legado.  
> **Escopo inicial:** somente as entidades `Predator` e `Voyeur`, ativas exclusivamente no período **Noite**.

---

## 1. Objetivo

Implementar uma arquitetura de entidades noturnas que seja:

- autorável por Noite;
- determinística para diagnóstico quando necessário, mas com rolagens de IA reproduzíveis;
- independente de `GameObject` visual para decidir lógica;
- integrada a ViewNodes, áudio por anchor, Hotbar/Lanterna, HUD de horário, game over e checkpoint;
- extensível para futuras entidades sem transformar `SceneAudioController`, `GameplaySceneController` ou hotspots em autoridades de IA.

O comportamento é inspirado no ritmo de *Five Nights at Freddy’s*: cada entidade recebe oportunidades periódicas de movimento governadas por uma dificuldade `0..20`, mas a rota, os timers de ataque e os confrontos são autorados.

---

## 2. Decisões fechadas

| Tema | Decisão |
|---|---|
| Entidades iniciais | `Predator` e `Voyeur`. |
| Período | Apenas Noite; não existem entidades ativas no Dia. |
| Persistência | Estado estritamente local da cena noturna. Não entra em save/checkpoint. |
| Reset | Carregamento/recarregamento da Noite reinicializa entidades no estado autorado. |
| Dados por Noite | Cada cena noturna referencia um `NightEntityProfile` próprio. |
| Fonte da referência | Campo em `GameplaySceneDefinition` de período `Night`. |
| Relógio | `12 AM` a `6 AM`, duração fixa de 360 segundos reais. |
| Tick global | Um tick compartilhado a cada 5 segundos reais. |
| Dificuldade | Curva de seis valores `0..20`, um por hora fictícia, por entidade e por perfil noturno. |
| Sorte | Seed por tentativa de Noite, exposta no F8 para reprodução. |
| Aleatoriedade | Apenas em `Inactive` e `Light`. A partir de `Near`, o ataque é determinístico/autorado. |
| Reserva de anchor | De `Near` até `Resolved`, `Terminal` ou reset/fim da Noite. |
| Falta de anchor livre | A entidade permanece em `Light` e tenta novamente em oportunidade futura. |
| Anchor Predator | Apenas categoria `Door`. |
| Anchor Voyeur | Apenas categoria `Window`. |
| Escolha de anchor | Uniforme entre os anchors permitidos e livres. |
| Confronto | Cada `AudioAnchorDefinition` possui um único `encounterViewNodeId`. |
| Views do Voyeur | Reutiliza ViewNodes existentes de Sala e Corredor; não serão criados ViewNodes de janela. |
| UI noturna | Hotbar/Halogênio e Debug F8 permitidos; documentos e Backpack indisponíveis. |
| Terminal | Permite navegação e Hotbar, porém a entidade terminal não pode mais ser enfrentada. |
| 6 AM vs. Terminal | `6 AM` vence qualquer Terminal ainda pendente; cancela a derrota e encerra a Noite. |
| Jumpscare | Fullscreen, tempo não escalado, bloqueia todos os inputs e retorna ao checkpoint do início do Dia. |
| Ausência de assets | Não criar placeholders ou atribuir clips/imagens. Campos permanecem para autoria manual; warnings seguros são esperados. |

---

## 3. Limites de escopo

### Incluído

- Máquina de estados comum e controllers filhos para Predator/Voyeur.
- NightClock, dificuldade por hora, d20, seed e tick global.
- Anchors, reserva exclusiva, mapeamento para ViewNode de confronto.
- Áudio por estado/anchor, bindings visuais locais e HUD de horário.
- Defesa prototípica de porta por hold do mouse.
- Defesa do Voyeur por cobertura do halo Halógeno.
- Terminal, jumpscare, falha noturna e retorno ao checkpoint do Dia.
- Ferramentas completas de diagnóstico no Debug F8.
- Validação de boot e validação de autoria.

### Fora do escopo inicial

- Terceira entidade ou sistema de spawn genérico para NPCs não ameaçadores.
- Movimento contínuo, pathfinding, NavMesh, `Transform` como autoridade de localização ou áudio 3D.
- Sistema de portas persistentes/físicas além do protótipo de hold.
- IA baseada em inventário, fatos, condições de hotspot ou observação do jogador.
- Modo UV, alternância Halogênio/UV e tecla `F` para a lanterna.
- Criação de arte, sprites, animações, clips, SFX ou imagens de jumpscare.
- Save do estado de entidades.

---

## 4. Modelo conceitual

### 4.1 Separação de responsabilidades

```text
GameplaySceneDefinition (Noite)
    → NightEntityProfile
    → EntityDirector
         ├─ NightClock
         ├─ reservas de AudioAnchorDefinition
         ├─ PredatorController
         ├─ VoyeurController
         ├─ EntityPresentationCoordinator
         ├─ NightClockHUD
         └─ NightGameOverController

EntityControllerBase
    → estado lógico + timers + pedidos ao diretor

EntityVisualBinding (ViewNode)
    → apresentação local e região de confronto

SceneAudioController
    → única autoridade de AudioSources/mixer; apenas apresenta os pedidos de entidade
```

O `EntityDirector` é a única autoridade que pode aprovar mudança de estado ou reserva/liberação de anchor. Controllers filhos nunca alteram diretamente outro controller, `NavigationManager`, pools de áudio ou o checkpoint.

### 4.2 Estado de runtime

O runtime não usa `SceneRuntimeState` para armazenar entidades. O estado fica em objetos locais controlados pelo `EntityDirector`, descartados com a cena:

```text
entityId
entityDefinition
state
activeAnchor
stateEnteredAt
criticalRemainingSeconds
resolveProgressSeconds
resolveStartDeadline
resolvedCooldownRemaining
terminalDeadline
passedThroughResolving
rng diagnostics
```

A única cópia persistida é a seed da tentativa enquanto a cena está viva, somente para diagnóstico F8. Ela não entra em `GameSaveData`.

---

## 5. Máquina de estados comum

### 5.1 Enum planejado

```text
Inactive
Light
Near
Critical
Resolving
Resolved
Terminal
```

O enum atual `ThreatAudioState` continuará sendo uma apresentação de áudio e não será usado como estado completo de gameplay. Uma camada de mapeamento deverá associar os estados audíveis a `ThreatAudioState` quando necessário:

```text
Inactive / Resolved  → Inactive
Light                → Light
Near                 → Near
Critical             → Critical
Resolving / Terminal → Critical ou reprodução direta da entidade
```

A reprodução nova deverá aceitar clips diretos da definição, sem depender do `AudioDebugCatalog`.

### 5.2 Transições válidas da base

```text
Inactive → Light
Light    → Inactive | Near
Near     → Critical
Critical → Resolving | Terminal
Resolving → Resolved | Critical | Terminal
Resolved → Inactive
Terminal → fim de cena / reset somente
```

Regras adicionais:

- Não existe salto direto `Inactive → Near` no primeiro slice.
- Uma entidade realiza no máximo uma mudança de estado/anchor por tick global.
- `Terminal` não retorna para estados anteriores.
- `Resolved` libera o anchor e só retorna a `Inactive` após cooldown.
- `Inactive` e `Light` não reservam anchor.
- `Near`, `Critical`, `Resolving` e `Terminal` reservam anchor.

---

## 6. IA FNAF-like

### 6.1 Curva de dificuldade

Cada entrada noturna possui seis valores inteiros `0..20`:

| Índice | Horário apresentado |
|---:|---|
| 0 | 12 AM até antes de 1 AM |
| 1 | 1 AM até antes de 2 AM |
| 2 | 2 AM até antes de 3 AM |
| 3 | 3 AM até antes de 4 AM |
| 4 | 4 AM até antes de 5 AM |
| 5 | 5 AM até antes de 6 AM |

No tick global, apenas entidades em `Inactive` ou `Light` fazem teste:

```text
roll = d20 (1..20)
if roll <= aiLevelDaHoraAtual:
    entidade recebe uma oportunidade de movimento
else:
    entidade permanece no estado atual
```

`aiLevel = 0` nunca concede oportunidade. `aiLevel = 20` sempre concede oportunidade.

### 6.2 Resultado da oportunidade

#### Em `Inactive`

```text
Oportunidade aprovada → Light
```

Não há seleção de anchor ainda.

#### Em `Light`

Uma oportunidade aprovada faz uma segunda rolagem independente:

```text
1..10  → Inactive
11..20 → tentativa de Near
```

Para tentar `Near`:

1. filtrar anchors permitidos pela entidade;
2. eliminar anchors reservados;
3. sortear uniformemente um anchor livre;
4. reservar o anchor;
5. aplicar `Near`.

Se não houver anchor livre, a entidade permanece em `Light`; não há segunda transição no mesmo tick.

### 6.3 Seed e ordem de avaliação

- Cada tentativa de Noite recebe uma seed de RNG.
- Produção usa seed nova; Debug F8 pode ler/reutilizar a seed para reproduzir os mesmos d20 e sorteios.
- A ordem estável de avaliação é definida pela lista do `NightEntityProfile`.
- Se dois Terminals forem produzidos no mesmo tick, o diretor coleta os pedidos fatais antes de limpar entidades e sorteia o vencedor com a mesma RNG da Noite.

---

## 7. NightClock e fim de Noite

### 7.1 `NightClockSettings`

Asset global planejado, compartilhado por todas as Noites:

```text
nightDurationSeconds = 360
entityTickIntervalSeconds = 5
startHour = 12 AM
endHour = 6 AM
```

A duração é fixa entre Noites; por isso não pertence ao perfil de uma entidade.

### 7.2 HUD

`NightClockHUD` recebe referência explícita a um `TextMeshProUGUI` na UI de Noite e exibe:

```text
12 AM, 1 AM, 2 AM, 3 AM, 4 AM, 5 AM, 6 AM
```

A HUD deve atualizar em tempo não escalado e não usar `Time.timeScale`.

### 7.3 Prioridade de 6 AM

Na atualização que cruza 6 AM, a prioridade é:

1. marcar a Noite como concluída;
2. cancelar Terminals pendentes, timers fatais e reservas;
3. impedir novas avaliações/ticks;
4. encerrar entidades e suas apresentações;
5. solicitar `GameplaySceneController.RequestPeriodEnd()`.

Portanto, uma contagem terminal que ainda não iniciou jumpscare perde para 6 AM. Depois de `NightGameOverController` ter iniciado efetivamente o jumpscare, a apresentação fatal já é autoridade e o relógio não poderá revertê-la.

---

## 8. Dados e ScriptableObjects planejados

### 8.1 `EntityDefinition` — base abstrata

Responsabilidade: identidade fixa e apresentação própria da entidade; sem estado runtime e sem tuning específico de Noite.

Campos planejados:

```text
entityId (único e estável)
displayName
allowedAnchorCategory
stateAudio[]
jumpscarePresentation
```

`stateAudio` terá por estado:

```text
state
enterSfxClip (opcional)
enterSfxVolume
presenceLoopClip (opcional)
presenceLoopVolume
fadeInSeconds
fadeOutSeconds
```

`jumpscarePresentation` terá:

```text
fullscreenSprite ou referência visual opcional
jumpscareSfx opcional
durationSeconds
```

A duração do jumpscare pertence à identidade da entidade, não ao perfil noturno.

### 8.2 `PredatorDefinition`

Herda de `EntityDefinition` e fixa:

```text
allowedAnchorCategory = Door
```

Não guarda tempos de defesa, cooldown ou dificuldade; esses valores são por Noite.

### 8.3 `VoyeurDefinition`

Herda de `EntityDefinition` e fixa:

```text
allowedAnchorCategory = Window
requiredLanternMode = Halogen
minimumLanternCoverage (0..1)
```

A região física/visual não pertence à definição: ela é local ao `EntityVisualBinding` de cada ViewNode.

### 8.4 `NightEntityProfile`

Um asset por cena noturna. É referenciado somente por `GameplaySceneDefinition` de período `Night`.

Campos planejados:

```text
profileId
entityEntries (lista ordenada)
```

Cada `EntityNightEntry` terá campos comuns:

```text
entityDefinition
allowedAnchors[]
aiLevelByHour[6]
nearToCriticalSeconds
criticalToTerminalSeconds
resolvedCooldownSeconds
terminalCountdownSeconds
```

Como Predator e Voyeur têm resolução distinta, cada entrada terá tuning especializado serializado por subobjeto/entrada tipada:

```text
PredatorNightTuning
- resolveStartWindowSeconds
- holdDoorSeconds

VoyeurNightTuning
- lightContactRequiredSeconds
```

A implementação pode usar `[SerializeReference]` para entradas tipadas ou wrappers serializáveis explícitos. A decisão de implementação deve privilegiar Inspector compreensível e validação clara; não deve depender de reflexão em runtime.

### 8.5 `AudioAnchorDefinition` ampliado

Campos novos planejados:

```text
encounterViewNodeId
```

Invariantes:

- `Door` precisa apontar para um ViewNode de porta.
- `Window` precisa apontar para o ViewNode de cômodo onde ocorre o confronto.
- O ID precisa existir sob o `NavigationManager` da cena noturna.

Anchors iniciais a autorar:

```text
ANCHOR_Door_Sala       → categoria Door, ViewNode da Porta da Sala
ANCHOR_Door_Cozinha    → categoria Door, ViewNode da Porta da Cozinha
ANCHOR_Window_Sala     → categoria Window, ViewNode da Sala
ANCHOR_Window_Corredor → categoria Window, ViewNode do Corredor
```

`ANCHOR_Window_Quarto` pode permanecer no projeto, mas não entra no primeiro perfil.

---

## 9. Classes runtime planejadas

### 9.1 `EntityControllerBase`

Classe abstrata local à cena.

Responsabilidades:

- expor `EntityDefinition`, estado e anchor atuais em modo somente leitura;
- manter timers runtime delegados pelo diretor;
- aceitar somente transições aprovadas pelo `EntityDirector`;
- emitir eventos de apresentação;
- fornecer hooks virtuais para comportamento específico;
- nunca escolher sozinho a própria rota aleatória ou acessar sessão/save.

Eventos planejados:

```text
StateChanged(entity, previousState, currentState)
AnchorChanged(entity, previousAnchor, currentAnchor)
ResolutionSucceeded(entity)
ResolutionFailed(entity)
TerminalRequested(entity, terminalReason)
```

### 9.2 `PredatorController`

Responsabilidades específicas:

- em `Near`, observar entrada no `encounterViewNodeId` e forçar `Critical`;
- em `Critical`, entrar em `Resolving` quando o jogador estiver no ViewNode de porta;
- em `Resolving`, aplicar a janela inicial e hold contínuo de LMB sobre a região de porta;
- sucesso após `holdDoorSeconds` → `Resolved`;
- soltar LMB, sair do ViewNode ou sair da região → `Terminal` imediato;
- expiração de Critical sem confronto → `Terminal` com contagem terminal normal;
- distinguir Terminal após falha de resolução, que inicia game over imediatamente.

### 9.3 `VoyeurController`

Responsabilidades específicas:

- em `Critical`, consultar contato da lanterna Halógena com a região do binding;
- contato suficiente → pausar `criticalToTerminalSeconds` e entrar em `Resolving`;
- durante `Resolving`, acumular contato contínuo em `lightContactRequiredSeconds`;
- completar contato → `Resolved`;
- perder contato → retornar a `Critical`, mantendo o progresso de luz acumulado e retomando o tempo crítico restante;
- expiração de Critical → `Terminal`;
- se entrar em Terminal já no ViewNode de confronto, game over imediato;
- se Terminal começou fora do ViewNode de confronto, entrar nele antes do fim da contagem solicita game over imediato.

### 9.4 `EntityDirector`

Componente raiz da cena noturna.

Responsabilidades:

- carregar/validar `NightEntityProfile`;
- criar/registrar estados runtime dos controllers;
- executar `NightClock` e tick de 5 s;
- gerar d20 e sorteios de anchor pela seed da tentativa;
- controlar reserva exclusiva de anchors;
- processar operações atômicas de estado + anchor;
- pausar avaliação e timers quando F8 ativa `InputBlockReason.Pause`;
- observar mudanças de ViewNode;
- coordenar áudio, visual, HUD, Terminal, 6 AM e game over;
- limpar todo runtime no encerramento, reset, retorno ao menu, falha ou destruição da cena.

Operações públicas planejadas:

```text
TryTransition(entityId, targetState, optionalAnchor)
TryReserveAnchor(entityId, anchor)
ReleaseAnchor(entityId)
ForceEvaluateTickForDebug()
ForceStateForDebug(...)
ForceAnchorForDebug(...)
GetDebugSnapshot()
```

### 9.5 `EntityPresentationCoordinator`

Autoridade de apresentação local. Escaneia bindings inclusive em objetos inativos durante boot, escuta eventos do diretor e aplica apresentação somente ao ViewNode atualmente apresentado.

Ele é necessário porque o conteúdo de ViewNodes não apresentados fica inativo e, portanto, não pode ser responsável por se inscrever nos eventos globais.

### 9.6 `NightGameOverController`

Responsabilidades:

1. receber a entidade vencedora e sua `JumpscarePresentation`;
2. adicionar `InputBlockReason.GameOver`;
3. bloquear Hotbar, F8, navegação, modal e entrada de gameplay;
4. apresentar imagem/animacão fullscreen e SFX por tempo não escalado;
5. em asset ausente, registrar warning e usar fallback visual seguro sem gerar asset de mídia;
6. ao concluir, solicitar fluxo de falha da Noite;
7. garantir cleanup de overlay, áudio e bloqueio em todos os erros.

---

## 10. Apresentação visual

### 10.1 `EntityVisualBinding`

Componente local sob o conteúdo de um ViewNode. Cada binding representa uma combinação:

```text
EntityDefinition + AudioAnchorDefinition + EntityState
```

Campos planejados:

```text
entityDefinition
anchorDefinition
states[]
presentationRoot (GameObject)
encounterRegion (RectTransform opcional)
onShown (UnityEvent)
onHidden (UnityEvent)
```

Estados que podem ter binding no primeiro slice:

```text
Near
Critical
Resolving
Terminal
```

`Inactive`, `Light` e `Resolved` não precisam de visual por padrão.

### 10.2 Ausência de binding

Se a entidade estiver em estado visual e o ViewNode atual não tiver binding compatível:

- lógica e áudio continuam;
- nenhum visual é inventado;
- é emitido warning uma única vez por combinação de entidade/anchor/estado/ViewNode;
- a ausência não bloqueia a IA.

### 10.3 Regiões de defesa

- Predator: `encounterRegion` representa a área de porta em que LMB deve permanecer pressionado.
- Voyeur: `encounterRegion` representa a área visível do Voyeur que recebe luz.

Bindings de região devem estar no mesmo Canvas e não devem ter rotação; a validação de boot reporta configuração incompatível.

---

## 11. Lanterna Halógena e contato do Voyeur

### 11.1 Mudanças na Hotbar

`HotbarController` e `LanternEffect` deverão remover:

- enum/estado UV;
- alternância pela tecla `F`;
- rótulo e apresentação de modo UV;
- raio/cor/ramo de lógica UV.

A Hotbar continuará oferecendo Lanterna Halógena e Óleo conforme o escopo atual, mas somente Halógeno é método de defesa.

### 11.2 API planejada de `LanternEffect`

O halo já é circular e segue o cursor. Ele deve expor, no mínimo:

```text
IsHalogenActive
ScreenCenter
RadiusInCanvasSpace
TryGetCoverage(RectTransform target, out float coverage)
```

### 11.3 Definição formal de cobertura

A cobertura do Voyeur será:

```text
area(círculo do halo ∩ retângulo da encounterRegion)
---------------------------------------------------
area(encounterRegion)
```

Há contato válido quando:

```text
coverage >= VoyeurDefinition.minimumLanternCoverage
```

A implementação deve operar no espaço local do Canvas. Não deve usar Collider, Physics ou dependência de posição 3D.

---

## 12. Áudio de entidade

### 12.1 Regra de apresentação

- `Inactive`: sem áudio.
- `Light`: SFX de entrada opcional.
- `Near`, `Critical`, `Resolving` e `Terminal`: SFX de entrada e loop opcional próprios, sempre associados ao anchor ativo.
- `Resolved`: sem áudio por padrão, salvo futura autoria explícita.

Toda troca de estado:

1. faz fade-out do loop anterior;
2. reproduz SFX de entrada do novo estado, se configurado;
3. inicia loop novo com fade-in, se configurado;
4. resolve pan, distância, low-pass e reverb pelo `ViewAudioProfile` do anchor atual.

### 12.2 Integração com `SceneAudioController`

Será adicionada API direta de apresentação de entidade, conceitualmente:

```text
ApplyEntityStateAudio(entityId, anchorId, state, presentation)
ClearEntityAudio(entityId)
```

A API recebe clips da `EntityDefinition`; `AudioDebugCatalog` permanece exclusivo de Debug F8. As fontes continuam no grupo `Threats`, persistentes e fora dos filhos dos ViewNodes.

Clips ausentes devem gerar warning de autoria e não bloquear transições de estado.

---

## 13. Fluxos específicos

### 13.1 Predator

```text
Inactive
  └─ oportunidade d20 → Light

Light
  └─ oportunidade + d20 de direção → Inactive ou Near(anchor Door livre)

Near
  ├─ timer Near expira → Critical
  └─ jogador entra no ViewNode da porta → Critical imediato

Critical
  ├─ jogador já está/entra no ViewNode de porta → Resolving imediato
  └─ timer Critical expira → Terminal

Resolving
  ├─ começa hold de LMB na região dentro da janela → acumula defesa
  ├─ hold completo → Resolved
  └─ soltar/sair da região/sair do ViewNode/perder janela → Terminal imediato

Resolved
  └─ cooldown expira → Inactive

Terminal
  ├─ veio de falha em Resolving → game over imediato
  └─ veio de expiração de Critical → contagem terminal → game over
```

### 13.2 Voyeur

```text
Inactive
  └─ oportunidade d20 → Light

Light
  └─ oportunidade + d20 de direção → Inactive ou Near(anchor Window livre)

Near
  └─ timer Near expira → Critical

Critical
  ├─ halo Halógeno cobre threshold → Resolving e pausa timer Critical
  └─ timer Critical expira → Terminal

Resolving
  ├─ cobertura mantida até a duração requerida → Resolved
  └─ cobertura perdida → Critical, preservando progresso de luz e retomando timer Critical

Resolved
  └─ cooldown expira → Inactive

Terminal
  ├─ jogador já está no encounterViewNode → game over imediato
  ├─ jogador entra no encounterViewNode durante a contagem → game over imediato
  └─ contagem terminal expira → game over
```

---

## 14. Terminal, game over e sessão

### 14.1 Terminal pendente

Enquanto há Terminal pendente:

- a entidade terminal não pode mais ser resolvida;
- jogador pode navegar e usar Hotbar;
- outras entidades são limpas imediatamente;
- documentos e Backpack continuam indisponíveis;
- nenhuma nova entidade pode entrar em IA;
- 6 AM cancela a derrota pendente enquanto jumpscare ainda não começou.

### 14.2 Jumpscare

Quando game over é efetivamente solicitado:

```text
NightGameOverController
  → bloqueia input
  → interrompe/limpa apresentação de entidades
  → mostra jumpscare da entidade vencedora
  → aguarda duração não escalada
  → solicita falha da Noite
```

### 14.3 Fluxo de falha

`GameSessionManager` receberá uma operação sem save, conceitualmente `TryFailNight`:

- exige sessão no período Night;
- não consolida estado;
- não grava checkpoint;
- descarta o trabalho noturno;
- restaura o checkpoint do início do Dia atual;
- carrega a cena do Dia.

`GameplaySceneController` terá fluxo `NightFailure` separado de retorno ao menu e do encerramento normal de período. O Debug F8 usa o mesmo fluxo real.

---

## 15. Restrições de UI e input

### 15.1 Noite normal

| Sistema | Política |
|---|---|
| Hotbar | Disponível. |
| Lanterna Halógena | Disponível se o item tiver sido encontrado. |
| Óleo | Mantido no escopo atual da Hotbar. |
| Documentos | Indisponíveis. |
| Backpack | Indisponível. |
| Debug F8 | Disponível; congela IA e timers. |
| Navegação | Disponível, salvo transição normal, game over ou fluxo global. |

### 15.2 Debug F8

Ao abrir F8:

- adiciona `InputBlockReason.Pause` já existente;
- pausa NightClock, acumuladores de tick, timer de estado, terminal, cooldown e resolução;
- não permite passagem de tempo acidental;
- comandos explícitos de debug podem forçar tick/estado/anchor como ações autorizadas.

### 15.3 Jumpscare

Adicionar `InputBlockReason.GameOver` ao enum. Durante jumpscare, nenhum input de gameplay, Hotbar, F8 ou retorno pode competir com o fluxo.

---

## 16. Boot e validação

### 16.1 Regras bloqueantes para cena noturna

A validação de boot deve bloquear a cena se:

- `GameplaySceneDefinition.period == Night` e `nightEntityProfile` está nulo;
- perfil não contém exatamente uma entrada válida para Predator e Voyeur no primeiro slice;
- `entityId` está vazio ou duplicado;
- curva de dificuldade não possui seis valores ou contém valor fora de `0..20`;
- anchor permitido é nulo, duplicado, de categoria errada ou não pertence à cena/autoria;
- `encounterViewNodeId` está vazio ou não resolve um ViewNode;
- timer obrigatório é negativo ou inválido;
- falta `EntityDirector`, `NightClockHUD` ou `NightGameOverController` necessários à cena.

### 16.2 Warnings não bloqueantes

- Clip de estado, loop ou SFX de jumpscare nulo.
- Sprite/visual de jumpscare nulo.
- Binding visual inexistente para combinação apresentada.
- `encounterRegion` ausente em binding de Predator/Voyeur que depende de interação.
- Região com rotação ou Canvas incompatível para cálculo de cobertura.

### 16.3 Cena diurna

Cena de Dia não requer `NightEntityProfile`; se possuir referência por erro, a validação emite warning e ignora o sistema noturno.

---

## 17. Debug F8 — escopo planejado

A aba Áudio/Entidades deverá exibir:

```text
Horário atual e progresso até 6 AM
Seed da tentativa
Tempo até o próximo tick
Perfil noturno ativo
Reservas de anchors
```

Por entidade:

```text
entityId
estado atual
anchor atual
AI Level da hora
último d20 de oportunidade
último d20 de direção
state timer / critical remaining / resolve progress / cooldown / terminal remaining
binding visual resolvido
áudio ativo
```

Comandos autorizados:

- forçar um tick;
- definir seed;
- forçar estado/anchor;
- liberar anchor;
- simular entrada/saída no ViewNode de confronto;
- simular cobertura da lanterna;
- concluir/falhar resolução;
- iniciar Terminal;
- executar jumpscare e retorno real ao checkpoint.

Debug não pode gravar save, consolidar ciclo ou alterar inventário/fatos.

---

## 18. Tuning inicial recomendado

Os valores abaixo são apenas baseline técnico para o primeiro `NightEntityProfile`. Devem permanecer editáveis no Inspector e ser reajustados por design.

### 18.1 Curva de dificuldade sugerida

| Hora | Predator | Voyeur |
|---|---:|---:|
| 12 AM | 0 | 0 |
| 1 AM | 3 | 2 |
| 2 AM | 5 | 4 |
| 3 AM | 7 | 6 |
| 4 AM | 9 | 8 |
| 5 AM | 11 | 10 |

### 18.2 Timers sugeridos

| Campo | Predator | Voyeur |
|---|---:|---:|
| Near → Critical | 8 s | 10 s |
| Critical → Terminal | 10 s | 12 s |
| Janela inicial de resolução | 2,5 s | n/a |
| Duração de resolução | 4 s de hold | 5 s de luz acumulada |
| Cooldown após Resolved | 30 s | 25 s |
| Terminal normal → game over | 5 s | 4 s |
| Duração de jumpscare | 2,5 s | 2,5 s |

Esses valores não são conteúdo final e não devem ser confundidos com dificuldade de IA; dificuldade governa apenas oportunidades em `Inactive`/`Light`.

---

## 19. Arquivos e alterações planejadas

### 19.1 Novos scripts

```text
Assets/_Project/Scripts/Entities/
  EntityState.cs
  EntityDefinition.cs
  PredatorDefinition.cs
  VoyeurDefinition.cs
  EntityNightEntry.cs
  PredatorNightTuning.cs
  VoyeurNightTuning.cs
  NightEntityProfile.cs
  NightClockSettings.cs
  NightClock.cs
  EntityControllerBase.cs
  PredatorController.cs
  VoyeurController.cs
  EntityDirector.cs
  EntityVisualBinding.cs
  EntityPresentationCoordinator.cs
  NightClockHUD.cs
  NightGameOverController.cs
  EntityDebugSnapshot.cs
```

Nomes finais podem ser consolidados em menos arquivos sem misturar responsabilidades.

### 19.2 Scripts existentes a alterar

| Arquivo | Alteração planejada |
|---|---|
| `Scene/GameplaySceneDefinition.cs` | Campo `nightEntityProfile` válido somente em Noite. |
| `Scene/GameplaySceneController.cs` | Boot/validação noturna, referência ao diretor e fluxo `NightFailure`. |
| `Session/GameSessionManager.cs` | Operação explícita de falha noturna sem save, retornando ao checkpoint do Dia. |
| `Core/InputBlockReason.cs` | Adicionar `GameOver`. |
| `Navigation/NavigationManager.cs` | Evento confiável de ViewNode apresentado/trocado. |
| `Audio/SceneAudioController.cs` | API de áudio direto para estado de entidade e limpeza por entityId. |
| `Audio/AudioAnchorDefinition.cs` | Campo `encounterViewNodeId`. |
| `Hotbar/HotbarController.cs` | Remover UV/F; expor estado de Halógeno selecionado. |
| `Hotbar/LanternEffect.cs` | Remover UV e expor cálculo de cobertura contra região. |
| `UI/ModalUIController.cs` | Bloquear documentos durante Noite. |
| `Backpack/BackpackController.cs` | Garantir indisponibilidade no período Night. |
| `UI/CycleTestUI.cs` | Aba/controles completos de entidades, seed e game over real. |

### 19.3 Assets e cenas

```text
Assets/_Project/SO/Entities/
  ENT_Predator.asset
  ENT_Voyeur.asset

Assets/_Project/SO/NightEntities/
  NIGHT_Playground_Entities.asset
  SETTINGS_NightClock.asset

Assets/_Project/SO/Audio/Anchors/
  ANCHOR_Door_Sala.asset
  ANCHOR_Door_Cozinha.asset
```

A cena noturna deverá conter e referenciar explicitamente:

```text
Manager_Entities
  EntityDirector
  EntityPresentationCoordinator
  NightGameOverController

UI_NightClock
  NightClockHUD + TextMeshProUGUI

PredatorController
VoyeurController
```

Bindings visuais serão autorados dentro do conteúdo dos ViewNodes existentes. Eles podem permanecer sem arte/clip, mas devem ser configuráveis e diagnosticados.

---

## 20. Ordem de implementação

1. Criar enums, definições, perfis e validação de dados sem alterar gameplay.
2. Estender `AudioAnchorDefinition` e criar anchors de porta.
3. Implementar `NightClock`, HUD e `EntityDirector` com seed, dificuldade, tick e reservas.
4. Implementar `EntityControllerBase` e o ciclo comum `Inactive ⇄ Light → Near`.
5. Integrar evento de mudança de ViewNode e apresentação visual básica.
6. Integrar áudio direto de entidade ao `SceneAudioController`.
7. Implementar Predator: Near/Critical/Resolving/Resolved/Terminal.
8. Remover UV e implementar API/cobertura da lanterna.
9. Implementar Voyeur: Critical/Resolving/Resolved/Terminal.
10. Implementar `NightGameOverController` e `GameSessionManager.TryFailNight`.
11. Bloquear Backpack/documentos na Noite e criar HUD de relógio.
12. Estender Debug F8.
13. Configurar o perfil baseline, anchors e bindings de cena.
14. Executar validações estáticas e Play Mode/manual quando Unity estiver disponível.

Cada passo deve manter a cena segura: erro de conteúdo não pode manter `InputBlocker`, áudio, reserva de anchor ou transição presos.

---

## 21. Critérios de aceite

### IA e NightClock

- [ ] A Noite inicia em 12 AM e termina automaticamente em 6 AM após 360 s reais.
- [ ] Cada entidade lê seis níveis de dificuldade `0..20` por hora.
- [ ] `Inactive` e `Light` são os únicos estados que fazem d20 de oportunidade.
- [ ] `Light` usa segundo d20 50/50 para `Inactive` ou `Near`.
- [ ] Uma entidade nunca executa mais de uma mudança no mesmo tick.
- [ ] A mesma seed reproduz os mesmos resultados no F8.
- [ ] F8 congela relógio, timers, cooldowns e RNG até fechamento/comando explícito.

### Anchors e apresentação

- [ ] Predator só reserva Door; Voyeur só reserva Window.
- [ ] Nenhum anchor é ocupado por duas entidades.
- [ ] `Near` reserva anchor; `Resolved`, fim de Noite e cleanup o liberam.
- [ ] Sem anchor livre, a entidade fica em Light.
- [ ] Áudio resolve perspectiva pelo anchor mesmo sem visual disponível.
- [ ] Binding visual ausente gera warning sem bloquear lógica.

### Predator

- [ ] Entrada no ViewNode de porta durante Near força Critical.
- [ ] Critical entra em Resolving se jogador está/entra na porta.
- [ ] LMB na região, dentro da janela, resolve após hold contínuo.
- [ ] Soltar/sair da região/ViewNode ou perder janela termina em Terminal imediato.
- [ ] Resolved aplica cooldown e retorna a Inactive.

### Voyeur

- [ ] Apenas Halógeno gera contato.
- [ ] Cobertura usa o threshold da `VoyeurDefinition` contra região do binding.
- [ ] Contato pausa Critical e inicia Resolving.
- [ ] Perder contato retorna Critical, preservando progresso de luz e tempo crítico restante.
- [ ] Cobertura suficiente conclui Resolved.
- [ ] Terminal na janela ou entrada posterior nela gera game over imediato.

### Terminal, jumpscare e sessão

- [ ] Terminal limpa a outra entidade e impede nova IA.
- [ ] Terminal pendente ainda permite navegação/Hotbar, mas não nova resolução.
- [ ] Se dois Terminals surgirem no mesmo tick, um vencedor aleatório é escolhido de forma reproduzível pela seed.
- [ ] 6 AM cancela Terminal pendente antes do jumpscare.
- [ ] Jumpscare bloqueia todos os inputs e usa tempo não escalado.
- [ ] Jumpscare retorna ao checkpoint do início do Dia sem salvar/consolidar a Noite.
- [ ] Falha em asset de áudio/arte não deixa gameplay, UI ou sessão bloqueados.

---

## 22. Invariantes finais

1. `EntityDirector` é a única autoridade de estado e reserva de anchors.
2. Controllers filhos não gravam save, não carregam cenas e não manipulam diretamente entidades irmãs.
3. `SceneAudioController` continua sendo dono de fontes e mixer.
4. `EntityVisualBinding` apresenta; nunca decide IA.
5. `NightEntityProfile` contém tuning por Noite; `EntityDefinition` contém identidade e apresentação fixa.
6. Nenhum estado de entidade atravessa checkpoint, Day, retorno ao menu ou reload de cena.
7. `Terminal` é irreversível no runtime, salvo vitória automática em 6 AM antes do jumpscare.
8. Não existe UV neste escopo.
9. Não existe interação com documentos ou Backpack durante Noite.
10. Nenhuma falha pode deixar um `InputBlockReason`, reserva, loop de áudio ou overlay preso.
