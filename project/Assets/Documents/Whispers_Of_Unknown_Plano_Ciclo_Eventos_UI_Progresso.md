# Whispers Of Unknown — Progresso de Implementação (Plano Ciclo/Eventos/UI v1)

> **Atualizado:** 2026-11-11
> **Plano de referência:** `Whispers_Of_Unknown_Plano_Ciclo_Eventos_UI_v1.md`
> **Base:** `main` @ `e667c9c` (sincronizado com o remoto em 2026-11-11)

---

## 1. Estado por fase

| Fase | Conteúdo | Estado |
|---|---|---|
| **1** | M1 — `active` por entidade, Exhauster nulo = desligado | **Concluída (código)** |
| **2** | M2 — ciclo 1 Dia → 1 Noite, N pares pelo `DEF_Night` | **Concluída (código)** |
| **3** | M3 — cheats MIKA e saltos de Dia/Noite | **Concluída (código)** |
| **4** | M4 — mochila de 3 itens + lampião no sótão | **Concluída (assets + menu de autoria)** |
| **5** | M5/M6 — tooltip e `ClockHotspot` | **Concluída (texto da hora + arte por hora; falta a arte)** |
| **6** | M7/M8 — cenas `radio_event` e `world_cutscene` | **M7 concluída; M8 pendente** |
| **7** | Arte do relógio de parede | **Sprites ligados nos 18 hotspots; `clockImage` por menu** |
| **8** | Rádio dos 4 Dias | **4 assets criados e ligados pelo menu; texto pendente** |
| **9** | Tutoriais das entidades em tom de aviso militar | **Textos escritos; tutoriais removidos do jogo (decisão de 2026-11-11)** |
| **10** | Boletim de abertura do Dia 1 | **Rádio toca no boot, sem depender de navegação** |
| **11** | Correção de PPtr quebrado na `day` | **Órfãos removidos; validador de cenas criado** |
| 7 | M9 — progressão dos Dias | Pendente |
| 8 | M10 — fonte, testes EditMode, erratas | Pendente |

---

## 2. Alterações aplicadas

### Arquivos novos

| Arquivo | Papel |
|---|---|
| `Scripts/Debug/DebugCheatController.cs` | Sequência MIKA (`Shift+M/I/K/A`), `Ctrl+P/V/E`, `F1..F4`, `Shift+F1..F4`. Criado em runtime pelo `GameplaySceneController`. |
| `Editor/NightProfileSetup.cs` | Menu `Whispers/Perfis/...`: grava `profileId` único, liga/desliga entidades por Noite, relata o estado. |

### Arquivos alterados

| Arquivo | Alteração |
|---|---|
| `Scripts/Entities/NightEntityProfile.cs` | `EntityNightEntry.active`, `ExhausterNightEntry.active` + `IsConfigured`; validação por entidade ativa; Exhauster nulo não é erro; máximo de 1 Predator/1 Voyeur; exige 1 ameaça ativa. |
| `Scripts/Entities/EntityDirector.cs` | Ignora entradas inativas; Predator/Voyeur opcionais no `Initialize`; `IsEntityActive`; mídia só de entidade ativa. |
| `Scripts/Entities/EntityPresentationCoordinator.cs` | Binding de entidade inativa não é exigido nem validado. |
| `Scripts/Scene/GameplaySceneDefinition.cs` | Propriedade `NightCount` (fonte do tamanho do ciclo). |
| `Scripts/Scene/GameplaySceneController.cs` | `Session.RegisterNightCount(...)` no boot da Noite; tolerância de divergência `DEF_Night` × `CYCLE`; criação do `DebugCheatController`. |
| `Scripts/Session/GameSessionManager.cs` | `NightCount` registrada; `TryEndPeriod` Dia N → Noite N → Dia N+1; normalização `night > day` no `Apply`; `DebugCheatsUnlocked`/`UnlockDebugCheats`; `TryDebugJump`. |
| `Scripts/Session/GameCycleDefinition.cs` | `nightCount` passa a ser fallback (default 4). |
| `SO/CYCLE_Gameplay.asset` | `nightCount: 4`. |

---

## 3. Passos manuais no Unity (fase 1)

1. Abrir o projeto e deixar recompilar.
2. Rodar `Whispers > Perfis > Aplicar entidades ativas e profileId`.
3. Rodar `Whispers > Perfis > Verificar perfis noturnos` e conferir o relatório:
   - Noite 1: `predator [inativa]`, `voyeur [ATIVA]`, Exhauster desligado;
   - Noite 2: Predator e Voyeur ativos;
   - Noite 3 e 4: Predator, Voyeur e Exhauster ativos.
4. Play na `night`: o boot não pode mais mostrar `Boot bloqueado por configurações obrigatórias inválidas`.

---

## 4. Pendências conhecidas desta parte

1. `NIGHT_Entities_1..4` têm hoje `profileId = night_entities_1`. O menu da fase 1 corrige; se não for rodado, o boot continua válido (o `profileId` não bloqueia), mas o diagnóstico fica ambíguo.
2. Bindings visuais do Predator continuam autorados na cena. Com o Predator inativo, eles não são exigidos, mas também não são removidos.
3. A conclusão do ciclo (`BetaEndScreen`) agora ocorre depois da Noite 4. O texto continua o do teste beta.
4. `TryFailNight` em sessão de desenvolvimento (`Play` direto) usa o snapshot do salto quando o cheat de Dia/Noite foi usado.

---

## 5. Correções pós-compilação

| Erro | Causa | Correção |
|---|---|---|
| `CS1061: GameSessionManager does not contain TryRestartCheckpoint` (`GameplaySceneController.cs:698`) | A reescrita de `TryEndPeriod` substituiu um bloco que continha `TryRestartCheckpoint` junto de `CommitNightCheckpoint`. | Método restaurado em `Session/GameSessionManager.cs`, logo após `TryEndPeriod`. `CommitNightCheckpoint` continua removido de propósito: a gravação agora está dentro de `TryEndPeriod`. |

Verificação aplicada depois da correção: comparação de assinaturas de membros de todos os arquivos alterados contra `HEAD`. Nenhum outro membro perdido.

---

## 6. M4 — Mochila de três itens e lampião no sótão

### 6.1 Assets criados

| Asset | Tipo | Conteúdo |
|---|---|---|
| `SO/Itens/ITEM_Lampiao.asset` | `ItemDefinition` | `id = item_lampiao`, `isTool = 1`, `consumesOnUse = 0`. |
| `SO/Backpack/BPSlot_Lanterna.asset` | `BackpackSlotDefinition` | `item_lanterna`, `maxQuantity = 1`, `dragEnabled = 0`. |
| `SO/Backpack/BPSlot_Lampiao.asset` | `BackpackSlotDefinition` | `item_lampiao`, `maxQuantity = 1`, `dragEnabled = 1`. |
| `SO/Backpack/BPSlot_Oleo.asset` | `BackpackSlotDefinition` | `item_oleo`, `maxQuantity = 1`, `dragEnabled = 0`. |
| `SO/Conditions/COLLECTED_NaoColetouLampiao.asset` | `CollectedCondition` | `coleta_lampiao`, `expectTrue = 0`. |
| `SO/Conditions/COLLECTED_NaoColetouOleo.asset` | `CollectedCondition` | `coleta_oleo`, `expectTrue = 0`. |
| `SO/Conditions/FACT_NaoPosicionouLampiao.asset` | `PersistentFactCondition` | `LampPlacedInAttic`, `expectTrue = 0`. |
| `SO/Interactions/INT_PegarLampiao.asset` | `InteractionDefinition` | `AddItem item_lampiao ×1` + `MarkCollected coleta_lampiao`. |
| `SO/Interactions/INT_PegarOleo.asset` | `InteractionDefinition` | `AddItem item_oleo ×1` + `MarkCollected coleta_oleo`. |
| `SO/Interactions/INT_PosicionarLampiao.asset` | `InteractionDefinition` | `RemoveItem item_lampiao ×1` + `SetPersistentFact LampPlacedInAttic`. |

### 6.2 Assets alterados

| Asset | Alteração |
|---|---|
| `SO/Itens/DB_Itens.asset` | A entrada antiga apontava para um `guid` inexistente. Agora lista `item_lanterna`, `item_lampiao` e `item_oleo`. |

### 6.3 Arquivo novo

| Arquivo | Papel |
|---|---|
| `Editor/BackpackItemsSetup.cs` | Dois menus: `Whispers/Mochila/Aplicar três itens e hotspots do Dia` (executa a autoria) e `Whispers/Mochila/Verificar mochila do Dia` (relata o estado). |

### 6.4 O que o menu faz na cena `day`

1. Grava `slotDefinitions` do `BackpackController` com exatamente 3 slots: Lanterna, Lampião, Lata de Óleo.
2. Move `Hotspot_PegarLanterna` do sótão para `__VN_Quarto/Content/Layer_Item`.
3. Cria `__VN_Quarto/Content/Layer_Item` e `__VN_Cozinha/Content/Layer_Item` clonando o `Layer_Item` do sótão (o sótão é o único que tem esse layer hoje).
4. Cria `Hotspot_PegarLampiao` no quarto (`INT_PegarLampiao`, condição `COLLECTED_NaoColetouLampiao`).
5. Cria `Hotspot_PegarOleo` na cozinha (`INT_PegarOleo`, condição `COLLECTED_NaoColetouOleo`).
6. Cria `Hotspot_ToolLampiao_Sotao` no sótão: `ToolHotspot`, `acceptedTools = [item_lampiao]`, `successDefinition = INT_PosicionarLampiao`, condição `FACT_NaoPosicionouLampiao`.
7. Salva a cena e lista no Console o legado que falta apagar à mão.

Padrão de hotspot copiado dos hotspots existentes: `activationMode = Click`, `repeatPolicy = Repeatable`, `conditionPolicy = All`, `unavailableMode = Hidden`. Os eventos `onActivated/onUnavailable/onBlockedHint` dos clones são limpos para não herdar o fim do Dia.

### 6.5 Óleo na Noite — nenhuma alteração necessária

`Scenes/night.unity` já tem `Hotspot_ReabastecerLampiao` com `HASITEM_PossuiOleo` + `LAMP_LampiaoIntacto`, `onActivated → StairLampController.RelightFromInteraction` e `definition = INT_ReabastecerLampiao`. O óleo não é consumido e a mochila não abre na Noite.

### 6.6 Pendências desta parte

1. Posicionar e dimensionar as áreas clicáveis dos três hotspots novos sobre a arte (nascem com alpha 0.08, igual ao padrão dos tutoriais).
2. Arte do lampião no sótão (Dia) e ícone de `ITEM_Lampiao` ainda vazios.
3. Apagar à mão: `BPSlot_Madeira`, `BPSlot_Prego`, `BPSlot_Martelo`, `BPSlot_PeDeCabra`, `ITEM_Madeira`, `ITEM_Prego`, `ITEM_Martelo`, `ITEM_PeDeCabra`. `ITEM_Madeira` também aparece em `playground_day.unity` e `playground_night.unity`; apagar antes de limpar essas cenas deixa referência órfã.
4. Coleta do lampião e do óleo não tem trava de Dia (não existe condição de Dia ainda; entra no M9). O fluxo funciona porque o sótão só abre no Dia 3.
5. O tutorial do Exhauster ao posicionar o lampião (§10.5) entra com o painel de tutorial, na fase 7.

---

## 7. M5 — Tooltip "Ir para <GameplayName>"

### 7.1 Arquivos novos

| Arquivo | Papel |
|---|---|
| `Scripts/UI/HotspotTooltipUI.cs` | Tooltip de hover: `Show(text)`, `Hide()`, posição por quadrante, clamp contra as bordas, `CanvasGroup` sem raycast, `NoWrap` + `Overflow`. **O texto segue o ponteiro** (`LateUpdate`, só quando o mouse anda; tamanho medido uma vez por `Show`). **Contorno nativo do TMP** (`OUTLINE_ON` no material instanciado; `outlineWidth` 0,22 e cor quase preta, ajustáveis no Inspector). Não aparece com `Modal`, `ToolDrag`, `Transition`, `GameOver` ou `Pause`. |
| `Editor/GameplayUiSetup.cs` | Menu `Whispers/UI/Aplicar tooltip, relógio e objetivos` (cria o tooltip e grava a referência no `GameplaySceneController`) e menu `Whispers/UI/Verificar textos de navegação` (lista hotspot → texto final). |

### 7.2 Arquivos alterados

| Arquivo | Alteração |
|---|---|
| `Scripts/Navigation/ViewNodeDefinition.cs` | Campo `gameplayName`. |
| `Scripts/Navigation/NavigationManager.cs` | `GetGameplayName(string viewNodeId)`: usa `gameplayName`, cai para `debugName`, devolve vazio se o id não existir. |
| `Scripts/Hotspots/HotspotBase.cs` | Ganchos `protected virtual void OnHoverEnter()` / `OnHoverExit()`. Chamados no `OnPointerEnter` (somente com `IsAvailable`) e no `OnPointerExit`. |
| `Scripts/Hotspots/NavigationHotspot.cs` | `DestinationGameplayName`; `OnHoverEnter` mostra `"Ir para " + nome`; `OnHoverExit` esconde. |
| `Scripts/Scene/GameplaySceneController.cs` | Campo `tooltip` + propriedade `Tooltip`; auto-busca por `GetComponentInChildren`; warning de boot quando ausente; warning de ViewNode sem `gameplayName` e sem `debugName`. |
| `SO/ViewNodes/VN_*.asset` (9) | `gameplayName` preenchido: Sala, Quarto, Cozinha, Banheiro, Sótão, Porta da Cozinha, Porta da Sala, Corredor da Frente, Corredor dos Fundos. |

### 7.3 Passos manuais no Unity

1. Deixar recompilar.
2. Abrir `day`, rodar `Whispers > UI > Aplicar tooltip, relógio e objetivos`.
3. Rodar `Whispers > UI > Verificar textos de navegação`: cada hotspot deve mostrar o texto final.
4. Abrir `night` e repetir os passos 2 e 3 (a cena tem Canvas e hotspots próprios).
5. Play: hover em `Hotspot_ToSala` mostra `Ir para Sala`; abrir a mochila esconde o texto.

### 7.4 Pendências desta parte

1. O `CanvasGroup` do tooltip nasce com `alpha = 0`. Se o texto aparecer no editor com o texto de exemplo, é o `Update`/`Show` que controla a visibilidade em Play.
2. Sem `HotspotTooltipUI` na cena, o boot gera warning e o tooltip fica desligado; nada mais quebra.
3. Cores e tamanho de fonte (`30`) são provisórios: o `SpecialElite-Regular SDF` está aplicado, o restante é ajuste visual.

---

## 8. M6 — `ClockHotspot`

### 8.1 Arquivos novos

| Arquivo | Papel |
|---|---|
| `Scripts/Hotspots/ClockHotspot.cs` | Herda de `HotspotBase`. `HoverImmediate` + `Repeatable`. `OnActivated` devolve verdadeiro e não executa ação. Lê `EntityDirector.DisplayHourIndex` por frame (0 a 6). Pede o cursor oculto e mostra o sprite no ponteiro. Libera tudo na saída, no bloqueio, na navegação, no GameOver e no `OnDisable`. |
| `Scripts/UI/ClockCursorPresenter.cs` | `Show(Sprite)` / `Hide()`. Desenha o sprite centrado no ponteiro, no tamanho nativo da arte, no canvas de overlay. |

### 8.2 Arquivos alterados

| Arquivo | Alteração |
|---|---|
| `Scripts/Feedback/GameplayCursorController.cs` | `RequestHiddenCursor(object)` / `ReleaseHiddenCursor(object)` com conjunto de donos; pedidos de donos destruídos são descartados. Nenhum método existente mudou. |
| `Scripts/Hotspots/HotspotBase.cs` | `OnDisable` passou a `protected virtual` para o relógio liberar o cursor. Nenhuma subclasse declarava `OnDisable`. |
| `Scripts/Scene/GameplaySceneController.cs` | Campo `clockPresenter` + propriedade `ClockPresenter`; auto-busca; warnings de boot (apresentador ausente, nenhum `ClockHotspot`, sprites faltando para o período). |
| `Editor/GameplayUiSetup.cs` | Passou a executar o M6: desativa `RelogioUI`, cria `ClockCursorPresenter` abaixo de `ModalUI` e cria `ClockHotspot` em cada ViewNode. O menu `Verificar textos de navegação` também lista o relógio por ViewNode. |

### 8.3 Decisão de estrutura (por que um hotspot por ViewNode)

`ViewNodeController` só marca como apresentados os hotspots que estão dentro de `contentRoot`. Um `ClockHotspot` solto no Canvas nunca receberia `SetPresented(true)` e nunca funcionaria. Por isso o menu cria **um** `ClockHotspot` em cada `Content` de ViewNode (9 na `day`, 9 na `night`, o mesmo objeto por sala).

Posição provisória: canto superior direito, 160×160, `alpha 0.08`. Ajuste sobre o relógio da arte de cada sala.

### 8.4 Passos manuais no Unity

1. Deixar recompilar.
2. Abrir `day`, rodar `Whispers > UI > Aplicar tooltip, relógio e objetivos`.
3. Abrir `night` e rodar o mesmo menu. O `RelogioUI` deve ficar desativado.
4. Rodar `Whispers > UI > Verificar textos de navegação` e conferir: `clockPresenter` atribuído, `RelogioUI` desativado, 9 `ClockHotspot`.
5. Soltar `Day_01PM.png` no `daySprite` de cada hotspot da `day`.
6. Na `night`, preencher `nightHourSprites[0..6]` (12AM … 06AM) em cada hotspot.
7. Play: hover no canto superior direito mostra o relógio; o cursor do sistema some; ao sair, tudo volta.

### 8.5 Pendências desta parte

1. **Arte do relógio não existe.** Sem sprite, o boot gera warning e o relógio não aparece. O script não cria placeholder, por regra do plano §7.3.
2. O tamanho do relógio na tela é o tamanho do PNG. Autorar em ~160 px.
3. A posição do hotspot é a mesma em todas as salas. Se a arte do relógio estiver em lugar diferente por sala, ajustar sala por sala.
4. `NightClockHUD` continua no código, apenas fora da tela.

---

## 9. Correções de hover (2026-11-11)

| Sintoma | Causa | Correção |
|---|---|---|
| O tooltip não aparece quando o ViewNode carrega com o ponteiro já sobre um hotspot. | O `OnPointerEnter` chega durante o bloqueio `Transition` e retorna antes dos ganchos de UI. Depois do desbloqueio o EventSystem não repete o evento, porque o ponteiro nunca saiu do objeto. Nada reabria a UI de hover. | `HotspotBase` ganhou `OpenHoverUi()` / `CloseHoverUi()` com trava `_hoverUiOpen`, e uma rede de segurança no `Update`: enquanto `_presented && !blocked && _isAvailable && _cursorOver`, a UI de hover é aberta; nos demais casos, é fechada. |
| `ClockHotspot` não executava a lógica da base (dwell, detecção de bloqueio, reentrada). | `HotspotBase.Update` era `protected void` e `ClockHotspot` declarava `private void Update()`. O Unity chama só o método mais derivado. | `HotspotBase.Update` passou a `protected virtual void Update()`; `ClockHotspot.Update` virou `protected override` e chama `base.Update()`. |

`OnHoverEnter` / `OnHoverExit` agora só são chamados pelos helpers. Fecham em três casos: saída do ponteiro, `SetPresented(false)` e `OnDisable`.

---

## 10. M6 refeito — texto da hora + arte do relógio por hora (2026-11-11)

Correção do usuário: mostrar a hora é **texto**; os **sprites** são a arte do relógio trocando a cada hora. A versão anterior desenhava o sprite no cursor.

### 10.1 O que mudou

| Arquivo | Alteração |
|---|---|
| `Scripts/Hotspots/ClockHotspot.cs` | Reescrito. `OnHoverEnter` mostra o **texto** da hora pelo `HotspotTooltipUI`. `Update` troca o sprite de `clockImage` quando a hora muda. Campo novo `clockImage`. Campo `hideCursor` removido. |
| `Scripts/UI/ClockCursorPresenter.cs` | **Removido** (arquivo e `.meta`). Não existe mais relógio no cursor. |
| `Scripts/Scene/GameplaySceneController.cs` | Campo/propriedade `clockPresenter` removidos. `ValidateClockHotspots` continua. |
| `Scripts/Feedback/GameplayCursorController.cs` | Revertido ao original. `RequestHiddenCursor` / `ReleaseHiddenCursor` não são mais necessários. |
| `Editor/GameplayUiSetup.cs` | Não cria mais apresentador; **remove** o objeto `ClockCursorPresenter` antigo, se existir na cena. Grava `clockImage` em cada `ClockHotspot` e deixa a `Image` em `alpha 1` com `preserveAspect`. |

### 10.2 Comportamento final

| Função | Como funciona |
|---|---|
| Ver a hora | Texto no `HotspotTooltipUI`, seguindo o ponteiro: `01PM` no Dia, `12AM`…`06AM` na Noite |
| Arte do relógio | `clockImage.sprite` troca quando `DisplayHourIndex` muda; sem hover, sem clique. **No Dia fica parado em 01PM**: aplica uma vez e não lê mais a hora |
| Cursor | Intocado. Nada de cursor oculto |
| Sem arte | O relógio fica invisível e o boot avisa. O texto continua funcionando |

### 10.3 Observação de autoria

A `Image` do `ClockHotspot` é a arte do relógio. Ela nasce com `sprite` vazio e `alpha 1`, então **não aparece nada** até você soltar o PNG. Diferente da versão anterior, não existe mais o retângulo de `alpha 0.08`.

---

## 11. M7 — Evento de rádio (2026-11-11)

### 11.1 Arquivos novos

| Arquivo | Papel |
|---|---|
| `Scripts/Radio/RadioEventDefinition.cs` | `RadioEventDefinition`, `RadioLine`, `RadioReturnMode`. Asset nasce sem linhas. |
| `Scripts/Radio/RadioEventController.cs` | Fluxo completo do evento em tempo não escalado: preto, jingle + espera, revela, typewriter com som por caractere, indicador em laço, avanço por LMB/E/ESPAÇO, fade final e retorno pela sessão. |
| `Scripts/Day/DayFlowController.cs` | Conta navegações do Dia e dispara o rádio. Cresce no M9. |
| `Editor/RadioEventSetup.cs` | Três menus: criar a cena, aplicar o fluxo do Dia, aplicar o anchor nos perfis de áudio. |
| `SO/Radio/RADIO_EventoDia.asset` | `eventId = radio_evento_dia`, `lines` vazias, `returnMode = ReturnToDay`. |
| `SO/Audio/Anchors/ANCHOR_Radio_CorredorFront.asset` | `id = radio_corredorfront`, categoria `Room`, `encounterViewNodeId = vn_corredorfront`. |

### 11.2 Arquivos alterados

| Arquivo | Alteração |
|---|---|
| `Scripts/Session/GameSessionManager.cs` | `_consumedDayEvents`, `RequestRadioEvent`, `TryFinishRadioEvent`, `IsDayEventConsumed`, `MarkDayEventConsumed`, `PendingRadioEvent`, `TryConsumeReturnViewNode`, `HasPendingReturnViewNode`, `PendingReturnViewNodeId`, `StartPreparedPeriod`. `PrepareForPeriodChange` e `Apply` limpam os eventos do Dia. |
| `Scripts/Session/GameCycleDefinition.cs` | `radioEventScene` (default `radio_event`) e `worldCutsceneScene` (M8). |
| `Scripts/Navigation/NavigationManager.cs` | `Initialize(initialNodeId, preferredNodeId)`: o retorno vence o inicial; se o id não existir, usa o inicial com warning. |
| `Scripts/Scene/GameplaySceneController.cs` | Campo `dayFlow`; consome o ViewNode de retorno no boot; inicializa o `DayFlowController`; warning de boot quando o Dia está sem `DayFlowController`. |

### 11.3 Decisão sobre `AudioRandomContainer`

O plano (§8.3) preferia `AudioRandomContainer`. O projeto não tem esse asset em nenhum ponto, e o próprio plano aceita `typewriterClips`. Implementado com `typewriterClips` + `PlayOneShot` com sorteio. Se um container for criado depois, a troca é de três linhas em `PlayTypewriterTick`.

### 11.4 Passos manuais

1. Abrir o projeto e recompilar.
2. `Whispers > Rádio > Criar cena do rádio` (cria `Scenes/radio_event.unity` e registra no Build Profile).
3. `Whispers > Rádio > Aplicar fluxo do Dia` com a cena `day` aberta.
4. `Whispers > Rádio > Aplicar anchor nos perfis de áudio`.
5. Autorar `RADIO_EventoDia`: linhas (texto, frame, som), jingle e clips de typewriter.
6. Soltar os sprites `Frame_01..03`.
7. Play na `day`: depois de 3 navegações, o rádio entra; ao terminar, volta ao ViewNode de onde saiu.

### 11.5 Pendências desta parte

1. **Texto e áudio do rádio são autoria do usuário.** Sem linhas, o evento não dispara (comportamento seguro).
2. Os `Frame_01..03` nascem sem sprite: fundo branco até a arte entrar.
3. O anchor do rádio fica registrado nos perfis, mas nada o toca até existir som ambiente de rádio (fora de escopo, §8.4).
4. M8 (cutscene) já tem o campo e o `StartPreparedPeriod` prontos. Falta a cena e o gancho do menu.

---

## 12. Arte do relógio — sprites nos hotspots (2026-11-11)

### 12.1 Mapeamento confirmado pelo usuário

`clock_wall_12`, `clock_wall_01`, `clock_wall_02`, `clock_wall_03`, `clock_wall_04`, `clock_wal_05l`, `clock_wall_06`.

| Campo | Sprite |
|---|---|
| `daySprite` (01PM) | `clock_wall_01` |
| `nightHourSprites[0]` (12AM) | `clock_wall_12` |
| `nightHourSprites[1]` (01AM) | `clock_wall_01` |
| `nightHourSprites[2]` (02AM) | `clock_wall_02` |
| `nightHourSprites[3]` (03AM) | `clock_wall_03` |
| `nightHourSprites[4]` (04AM) | `clock_wall_04` |
| `nightHourSprites[5]` (05AM) | `clock_wal_05l` |
| `nightHourSprites[6]` (06AM) | `clock_wall_06` |

O Dia usa `clock_wall_01` porque 1h da manhã e 1h da tarde têm o mesmo desenho de ponteiros.
O arquivo `clock_wal_05l.png` tem erro de digitação no nome. Não foi renomeado (a pasta `Art/` é autoria do usuário); se renomear, o `.meta` acompanha e o sprite continua ligado.

### 12.2 Aplicado nas cenas

Os 9 `ClockHotspot` de `day` e os 9 de `night` receberam `daySprite` e as 7 posições de `nightHourSprites`. O hotspot de referência que o usuário configurou à mão foi usado como fonte; os outros 8 de cada cena foram preenchidos por igual. Total: 18 hotspots, 144 referências de sprite.

### 12.3 `clockImage` — resolvido pelo menu de UI existente

> **Nota (2026-11-11, depois deste registro):** cheguei a criar um `Editor/ClockArtSetup.cs` com um menu próprio, mas ele foi **removido**. O menu já existente `Whispers > UI > Aplicar tooltip, relógio e objetivos` (`GameplayUiSetup`) faz o mesmo trabalho: cria o `ClockHotspot` que faltar, garante a `Image` no próprio objeto e atribui `clockImage`. Dois menus com geometrias diferentes (imagem filha × imagem do objeto) só criariam confusão. O texto abaixo fica como histórico.

O sprite só aparece se o hotspot tiver uma `Image` para receber. O hotspot de referência da Noite já tinha um filho `Clock` com a `Image`; o do Dia não tinha nenhuma.

`Whispers > UI > Criar arte do relógio` (menu removido; use `Whispers > UI > Aplicar tooltip, relógio e objetivos`):

1. Cria o filho `Clock` (RectTransform + CanvasRenderer + Image) em cada hotspot sem `clockImage`.
2. Estica o filho ao retângulo do hotspot (`anchors 0..1`, offsets zero), `raycastTarget = false`, `preserveAspect = true`.
3. Atribui a `Image` ao campo `clockImage` via `SerializedObject`.
4. Remove os objetos antigos `ClockCursorPresenter` que sobraram nas cenas.
5. Salva a cena e mostra o resumo.

Regra de posicionamento: **o hotspot manda**. Ao arrastar/redimensionar o hotspot sobre o relógio da parede, a arte acompanha, sem distorcer.

### 12.4 Pendências

1. Posicionar os 9 hotspots de cada cena sobre o relógio da parede. Hoje são marcadores pequenos (2×3 unidades) na origem do `Layer_Object`.
2. Depois de rodar o menu nas duas cenas, apagar `Assets/_Project/Scripts/UI/ClockCursorPresenter.cs` e o `.meta`.
3. `unavailableSprite` continua vazio: com os 7 sprites preenchidos, o fallback nunca é usado.

---

## 13. Rádio dos quatro Dias (2026-11-11)

### 13.1 Assets criados

| Asset | `eventId` | Linhas | Jingle | Typewriter | `returnMode` |
|---|---|---|---|---|---|
| `SO/Radio/RADIO_Dia1.asset` | `radio_dia_1` | vazias | vazio | vazio | `ReturnToDay` |
| `SO/Radio/RADIO_Dia2.asset` | `radio_dia_2` | vazias | vazio | vazio | `ReturnToDay` |
| `SO/Radio/RADIO_Dia3.asset` | `radio_dia_3` | vazias | vazio | vazio | `ReturnToDay` |
| `SO/Radio/RADIO_Dia4.asset` | `radio_dia_4` | vazias | vazio | vazio | **`AdvanceToNight`** |

`RADIO_EventoDia.asset` continua no projeto como rascunho genérico e como fallback de teste da cena do rádio (`fallbackDefinition`), mas não está ligado a nenhum Dia.

### 13.2 Ligação por Dia

`DayFlowController.dayRadioEvents[índice]` = Dia `índice + 1`. O menu `Whispers > Rádio > Aplicar fluxo do Dia` passou a preencher as quatro entradas (antes só preenchia a primeira). O array é dimensionado pelo maior valor entre `EffectiveNightCount` e 4.

Cada Dia tem contador próprio: o `DayFlowController` é recriado com a cena do Dia, então as 3 navegações contam de novo a cada Dia. "Já tocou hoje" é a chave `eventId` na sessão, limpa em `Apply()` e em `PrepareForPeriodChange()`.

### 13.3 Estados de asset incompleto

| Falta | Comportamento |
|---|---|
| `lines` vazias | O Dia não dispara nada. Sem tela preta, sem erro. |
| `jingle` vazio | Sem espera: o rádio revela na hora (sem silêncio em tela preta). |
| `typewriterClips` vazias | O texto aparece sem som, com um aviso único no console. |
| `typewriterMixerGroup` vazio | O som sai no grupo padrão do `AudioSource`. |

### 13.4 Texto

O texto das linhas é autoria do usuário. Formato de entrega combinado: um bloco por Dia, uma linha por página, e o frame opcional (`Frame_01..03`). Sem indicação de frame, todas as linhas usam `Frame_01`.

### 13.5 Pendências

1. Texto dos quatro Dias.
2. Jingle e clips de typewriter (o usuário declarou que faz depois).
3. ~~Decidir qual Dia usa `AdvanceToNight`.~~ **Decidido em 2026-11-11: Dia 4.** Os Dias 1 a 3 devolvem o jogador ao ViewNode de origem; o Dia 4 encerra o Dia e carrega a Noite 4.
4. O rádio do Dia 4 precisa de linhas para funcionar: sem linhas o evento não dispara e o Dia 4 não tem como terminar.

---

## 14. Tutoriais das entidades — aviso militar (2026-11-11)

### 14.1 O que mudou

| Documento | Estado |
|---|---|
| `DOC_TUTORIAL_Predator` | Texto reescrito em aviso militar (mesmo `guid`, mesmo `id`) |
| `DOC_TUTORIAL_Voyeur` | Texto reescrito em aviso militar (mesmo `guid`, mesmo `id`) |
| `DOC_TUTORIAL_Exhauster` | **Novo.** Texto derivado do README, do código do lampião e da arte |

### 14.2 Textos

**Predator**

```
AVISO DE OPERAÇÃO — AMEAÇA 02

CLASSIFICAÇÃO: PREDATOR
NATUREZA: força bruta. Não negocia, não recua, não se distrai.
AÇÃO HOSTIL: traciona a maçaneta e força a porta até ela ceder.

CONTRA-MEDIDA
1. Desloque-se até a porta comprometida.
2. Posicione o cursor sobre a maçaneta e MANTENHA o botão esquerdo pressionado. Não solte.
3. A porta cede por acúmulo de pressão. Soltar é entregar a casa.

CRITÉRIO DE FALHA: porta aberta além do limite = entrada da criatura. Não existe segunda chance naquele setor.

SETORES VULNERÁVEIS
• porta da sala
• porta da cozinha

ORDEM PERMANENTE: ao notar a maçaneta forçada, largue o que estiver fazendo e vá segurar a porta. O tempo de deslocamento faz parte da defesa.
```

**Voyeur**

```
AVISO DE OPERAÇÃO — AMEAÇA 01

CLASSIFICAÇÃO: VOYEUR
NATUREZA: infiltração silenciosa. Aproxima-se sem ruído.
AÇÃO HOSTIL: entra pela janela. Não força nada: espera a janela livre.

CONTRA-MEDIDA
1. Aponte a luz para a janela comprometida.
2. MANTENHA o facho sobre ela até a criatura recuar.
3. Luz sobre a janela = avanço interrompido. Sem luz = avanço livre.

EQUIPAMENTO CRÍTICO: lanterna de dínamo.
A carga é contínua e obrigatória. Pressione ESPAÇO para recarregar.
Com carga baixa o facho não sustenta: a criatura NÃO recua.

SETORES VULNERÁVEIS
• janela da sala
• janela do corredor dos fundos

ORDEM PERMANENTE: nunca deixe o facho cair enquanto houver movimento na janela. Recarregue antes de apontar.
```

**Exhauster**

```
AVISO DE OPERAÇÃO — AMEAÇA 03

CLASSIFICAÇÃO: EXHAUSTER
NATUREZA: consumidora. Não persegue, não arromba: aguarda.
AÇÃO HOSTIL: permanece imóvel no escuro e consome a chama do lampião. Avança pela ausência de luz, nunca pela força.

IDENTIFICAÇÃO: corpo pálido e descarnado, membros longos, olho único, sem boca. Fica acocorada, mãos abertas no chão e estacas cravadas no dorso.

CONTRA-MEDIDA
1. Mantenha o lampião da escadaria aceso. Chama acesa = perímetro fechado.
2. Reabasteça o lampião com o óleo antes de a chama morrer.
3. Chama apagada abre uma janela de tolerância. Esgotada essa janela, o lampião quebra e o reabastecimento passa a ser recusado.

CRITÉRIO DE FALHA: tolerância esgotada = a criatura ocupa a casa. O lampião quebrado não volta nesta noite.

ORDEM PERMANENTE: óleo em mãos durante toda a ronda.
SETOR CRÍTICO: escadaria do sótão — lampião de óleo.
```

### 14.3 Bases do texto do Exhauster

| Fonte | O que foi usado |
|---|---|
| `README.md` | "ligado ao lampião da escadaria", "avança caso o lampião não seja mantido carregado" |
| `ExhausterDefinition` / `StairLampController` / `LampMaintenanceMath` | óleo como item de reabastecimento (não consumido), janela de tolerância no escuro, lampião quebrado recusa reabastecimento |
| `Art/Entities/exhauster.png` | corpo pálido e descarnado, membros longos, olho único negro, sem boca, acocorada com as mãos abertas no chão, estacas cravadas no dorso |

### 14.4 Ligação

| Peça | Estado |
|---|---|
| `SO/Documents/DOC_TUTORIAL_Exhauster.asset` | criado |
| `SO/Interactions/INT_TUTORIAL_Exhauster.asset` | criado (`type: 7` = abrir documento) |
| `Scenes/day.unity` | novo `Hotspot_Tutorial_Exhauster` dentro de `__VN_Sotao/Content` |

> **Superado em 2026-11-11 (§15):** os três tutoriais de entidade foram removidos do jogo. O parágrafo abaixo descreve como estavam antes da remoção.

O hotspot foi clonado do `Hotspot_Tutorial_Voyeur`: `activationMode = Click` (2), `repeatPolicy = Repeatable` (1), `unavailableMode = Hidden` (0), sem condições, `Image` com alpha `0.08` e 300×180 centrado — o mesmo padrão de placeholder dos outros quatro tutoriais.

Integridade da cena verificada depois da edição: 538 documentos, nenhum id duplicado, nenhum componente órfão, nenhuma assimetria pai/filho, e o mesmo número de referências externas antes e depois.

### 14.5 Pendências

1. Posicionar o `Hotspot_Tutorial_Exhauster` sobre o lampião na vista do Sótão.
2. `DOC_TUTORIAL_Movimentacao` continua com o texto antigo (não foi pedido).
3. Nenhum documento usa `image`: o painel já suporta, se quiser ligar as artes das três criaturas.

---

## 15. Tutoriais de entidade removidos; rádio vira o professor (2026-11-11)

### 15.1 Decisão

O rádio passa a ser a **única** fonte das instruções de defesa. Portas, janelas e lampião saem do formato "documento clicável" e entram no boletim.

| Item | Antes | Agora |
|---|---|---|
| `Hotspot_Tutorial_Predator` (`__VN_PortaSala/Content`) | aberto por clique | **removido da cena** |
| `Hotspot_Tutorial_Voyeur` (`__VN_CorredorBack/Content`) | aberto por clique | **removido da cena** |
| `Hotspot_Tutorial_Exhauster` (`__VN_Sotao/Content`) | aberto por clique | **removido da cena** |
| `INT_TUTORIAL_Predator` / `Voyeur` / `Exhauster` | abriam documento | **assets apagados** |
| `DOC_TUTORIAL_Predator` / `Voyeur` / `Exhauster` | exibidos no painel | **mantidos como texto de apoio** (nada os referencia; servem de base para as falas do rádio) |
| `Hotspot_Tutorial_Cliques` / `Movimentacao` | interface | mantidos |

Os três documentos preservam os avisos militares do §14. Se preferir apagar também, é um comando.

### 15.2 Boletim de abertura do Dia 1

O risco que motivou a mudança: o Dia pode ser encerrado pela lanterna antes de o rádio tocar (3 navegações), e o jogador entrava na Noite 1 sem saber como defender a casa.

`DayFlowController` ganhou dois campos:

| Campo | Valor padrão | Efeito |
|---|---|---|
| `playFirstDayOnBoot` | `true` | No Dia 1, o rádio toca sem depender de navegação |
| `firstDayBootDelaySeconds` | `1.5` | Espera em segundos reais depois da cortina de boot |

Sequência exata:

1. `GameplaySceneController` apresenta o ViewNode inicial e chama `DayFlowController.Initialize(navigation)`.
2. `Initialize` inicia `FirstDayBootRoutine`.
3. A rotina espera `GameplaySceneController.IsReady` (cortina do boot fora).
4. Espera `firstDayBootDelaySeconds`.
5. Confere `session.period == Day` e `session.day <= 1`.
6. Chama `TryTriggerRadio(session, ignoreNavigationCount: true)`.
7. O ViewNode apresentado nesse instante é o destino do retorno — o jogador volta exatamente para onde estava.

Dias 2, 3 e 4 continuam com 3 navegações. O rádio do Dia 4 encerra o Dia (`AdvanceToNight`).

### 15.3 Cuidados que continuam valendo

1. **Sem linhas no `RADIO_Dia1`, o Dia 1 não ensina nada.** O boletim só existe quando o asset tiver texto.
2. O texto do boletim precisa conter os verbos exatos: botão esquerdo na maçaneta, ESPAÇO na lanterna, óleo no lampião.
3. O rádio não é pulável e toca uma vez por Dia. Se o jogador esquecer, não há como reler no jogo — por isso os três `DOC_TUTORIAL_*` ficaram no projeto.

### 15.4 Integridade

`day.unity` depois da remoção: 520 documentos, nenhum id duplicado, nenhum componente órfão, nenhuma assimetria pai/filho, nenhuma referência pendente. `night.unity` intocada (583 documentos). Zero ocorrências dos três `INT_TUTORIAL_*` de entidade na cena; os dois de interface seguem presentes.

---

## 16. Como aplicar a entrega (passo a passo do usuário)

### 16.1 Ordem dos menus

| # | Menu | Cena | O que faz |
|---|---|---|---|
| 1 | `Whispers > UI > Aplicar tooltip, relógio e objetivos` | `day` | tooltip, objetivos, `clockImage` nos 9 hotspots, remove `ClockCursorPresenter` |
| 2 | idem | `night` | mesma coisa na Noite |
| 3 | `Whispers > Mochila > Aplicar três itens e hotspots do Dia` | `day` | M4 ainda não estava aplicado na cena |
| 4 | `Whispers > Rádio > Aplicar fluxo do Dia` | `day` | cria `Manager_DayFlow`, preenche os 4 Dias, 3 navegações, boletim no boot |
| 5 | `Whispers > Rádio > Aplicar anchor nos perfis de áudio` | qualquer | anchor `radio_corredorfront` nos 9 `ViewAudioProfile` |
| 6 | `Whispers > Rádio > Criar cena do rádio` | — | cria `Scenes/radio_event.unity` e registra no Build Profile |

Avisos de ordem:

1. O menu 1 **reposiciona** cada `ClockHotspot` para o canto superior direito (160×160, provisório). Posicione os relógios depois de rodar os menus.
2. O menu 3 é pré-requisito de conteúdo: sem `item_oleo` na mochila, o reabastecimento do lampião não existe e o Exhauster fica invencível.
3. O menu 6 troca a cena aberta. Rode por último.

### 16.2 Conteúdo que só o usuário autoriza

1. Texto das linhas de `RADIO_Dia1` (o boletim que ensina porta, janela e lampião) e dos Dias 2 a 4.
2. Jingle e clips de typewriter (opcionais; sem eles o texto digita mudo).
3. Sprites dos `Frame_01..03` da cena do rádio.
4. Posição dos 9 `ClockHotspot` sobre o relógio de cada parede.
5. Área e posição dos hotspots da mochila (M4).

---

## 17. PPtr quebrado na `day` ao remover os tutoriais (2026-11-11)

### 17.1 O erro

Ao abrir a `day` no Unity:

```
Broken text PPtr in file(Assets/_Project/Scenes/day.unity). Local file identifier (800003021) doesn't exist!
Broken text PPtr in file(Assets/_Project/Scenes/day.unity). Local file identifier (800003031) doesn't exist!
```

### 17.2 A causa

Os hotspots de tutorial do Voyeur e do Predator não eram objetos vazios: cada um tinha um filho `Text (TMP)` (rótulo de placeholder). A remoção do §15 apagou o GameObject do hotspot e todos os componentes dele — mas **os filhos ficaram na cena**, com `m_Father` apontando para um `RectTransform` que deixou de existir.

`800003021` e `800003031` eram exatamente os `RectTransform` dos dois hotspots removidos.

### 17.3 Por que a validação anterior não pegou

A checagem do §15 era de ida: percorria cada `m_Children` e verificava se o filho apontava de volta. Um órfão **não aparece em nenhum `m_Children`** (o pai que o listaria foi apagado), então passou limpo. A verificação precisa ter os dois sentidos: `m_Children → m_Father` e `m_Father → existência do pai`.

### 17.4 A correção

Dois GameObjects `Text (TMP)` (GO `1236029025` e `1560321438`) removidos com seus componentes (RectTransform, CanvasRenderer, TextMeshProUGUI). `day.unity` passou de 520 para 512 documentos.

Validação completa das duas cenas depois da correção:

| Cena | Documentos | Problemas |
|---|---|---|
| `day.unity` | 512 | 0 |
| `night.unity` | 583 | 0 |

Checagens aplicadas: ids duplicados, `m_Component` inexistente, `m_GameObject` inexistente, `m_Father` inexistente (órfão), `m_Children` inexistente, assimetria pai/filho.

### 17.5 Rede de segurança

Novo `Editor/SceneYamlValidator.cs` com o menu **`Whispers > Cenas > Verificar integridade das cenas`**: varre o YAML de todas as cenas em `Assets/_Project/Scenes`, aplica as seis checagens e lista tudo de uma vez, com nome do arquivo e contagem. Roda em segundos e não abre cena nenhuma.

Regra aprendida: **antes de apagar um GameObject no YAML, ler o `m_Children` do Transform/RectTransform dele**. Componentes do objeto não bastam — os filhos sobrevivem ao pai.

---

## 18. Avisos dos tutoriais — terceira passagem, voz humana (2026-11-11)

Os três `DOC_TUTORIAL_*` passaram por três versões. A primeira trazia nomes de chave e parâmetros; a segunda virou aviso militar com etiquetas em caixa alta; a terceira é esta, e é a que fica.

### 18.1 O que mudou na terceira passagem

O problema das duas versões anteriores era o mesmo, visto de dois ângulos: pareciam escritas por um sistema. A segunda tinha etiqueta de assunto no começo, travessão ligando ideia e explicação, tríade do tipo "não é X, não é Y, não é Z" e simetria exata entre os três documentos. Um leitor não escreve assim.

A terceira passagem perseguiu quatro coisas:

- **Frase falada.** O texto tem o ritmo de quem está explicando com pressa: frase curta, vírgula, continuação. Nada de período longo com subordinada dentro de subordinada.
- **Nenhum marcador de formulário.** Saiu a etiqueta em caixa alta no começo de cada parágrafo. Saiu a numeração de procedimento. Saiu o travessão como conector.
- **Assimetria.** Os três avisos não têm o mesmo tamanho, a mesma estrutura, nem o mesmo jeito de abrir. Dois começam por uma constatação, um começa por uma ordem.
- **Verbo no imperativo do inspetor.** "Mantenha", "confira", "não force" — como quem passa o plantão para o próximo turno, não como manual de equipamento.

### 18.2 Tamanho

| Documento | Personagem | Título | Caracteres |
|---|---|---|---|
| `DOC_TUTORIAL_Predator.asset` | Predador | AVISO 02 - PORTAS | ~400 |
| `DOC_TUTORIAL_Voyeur.asset` | Voyeur | AVISO 01 - JANELAS | ~400 |
| `DOC_TUTORIAL_Exhauster.asset` | Exhauster | AVISO 03 - O LAMPIÃO | ~400 |

Eram 570 caracteres na versão anterior. O corte de trinta por cento tira o que era enfeite e deixa o que é instrução. O painel de leitura do jogo comporta isso sem rolagem em 1080p.

### 18.3 Consequência para o design

Os avisos **não ensinam mais verbo nenhum**. Nenhum deles diz o que segurar, o que apertar ou o que arrastar — isso é papel do rádio, que é onde a voz do jogo fala direto com o jogador.

A divisão ficou assim:

- **Documento**: o que é a criatura, o que ela faz, o que o jogador deve evitar. Conhecimento de campo.
- **Rádio**: o que o jogador faz com as mãos. Instrução de operação.

Isso também resolveu um problema de repetição: antes o documento e o rádio ensinavam o mesmo comando com palavras diferentes, o que soava como bug.

---

## 19. Rádio como VN dentro da cena do Dia (2026-11-11)

O rádio deixou de ser uma cena. Ele agora é uma camada de UI que cobre o gameplay dentro da própria cena `day`, roda o evento e devolve o controle ao jogador sem recarregar nada.

### 19.1 Por que a cena própria saiu

A versão anterior carregava `radio_event.unity`, apresentava o evento e voltava para o Dia pedindo à sessão que guardasse o ViewNode de retorno (`PendingReturnViewNodeId`). Três problemas:

- **Recarregar o Dia custava caro.** Toda a cena voltava pelo `StartLoading`, com cortina, revalidação de navegação e reconstrução dos apresentadores, para mostrar exatamente a mesma imagem que já estava na tela.
- **O retorno precisava de estado.** A sessão tinha que carregar definição pendente, modo de retorno e ViewNode de retorno — três campos que só existiam para atravessar o carregamento.
- **O evento podia se perder.** Se o carregamento da cena do rádio falhasse (fora do Build Profile, carregamento pendente), o evento ficava marcado como consumido sem ter tocado. Foi preciso inverter a ordem de `MarkDayEventConsumed` para consertar.

Uma camada de UI não tem nenhum dos três. O ViewNode atual continua montado, com o estado dos hotspots, o áudio da sala e a hora intactos. Quando o rádio termina, basta tirar o preto.

### 19.2 O que saiu do código

| Onde | O que saiu |
|---|---|
| `GameCycleDefinition` | campo `radioEventScene` |
| `GameSessionManager` | `RequestRadioEvent`, `TryFinishRadioEvent`, `PendingRadioEvent`, `PendingRadioReturnMode`, `HasPendingReturnViewNode`, `PendingReturnViewNodeId`, `TryConsumeReturnViewNode` e os campos `_pendingRadio`, `_pendingRadioReturnMode`, `_pendingReturnViewNodeId` |
| `GameplaySceneController` | leitura do ViewNode de retorno no boot: `navigationManager.Initialize` voltou a receber só o nó inicial |
| `EditorBuildSettings` | `radio_event.unity` fora do Build Profile |
| `Scenes` | `radio_event.unity` apagada do repositório |

O que **ficou** na sessão: `IsDayEventConsumed` e `MarkDayEventConsumed`. O `HashSet` é transitório, limpo na troca de período e no `Apply` — continua sendo o que impede o mesmo Dia disparar o rádio duas vezes.

O `returnMode` da definição também ficou, com o mesmo significado: `ReturnToDay` nos Dias 1 a 3, `AdvanceToNight` no Dia 4.

### 19.3 Como a VN funciona

`RadioEventController` virou componente de UI. A API é uma só:

```csharp
public bool Play(RadioEventDefinition definition = null);   // falso se já roda ou se não há linhas
public bool IsPlaying { get; }
public RadioEventDefinition CurrentDefinition { get; }
public event Action Finished;
```

A sequência, toda em tempo não escalado:

1. A sala escurece sob o fader (`blackoutSeconds`, 0.4s).
2. Jingle, com espera de `jingleDelaySeconds`. **Sem jingle não existe espera** — seria silêncio de cinco segundos.
3. A arte aparece com o `Frame_01`; o fader abre em `revealSeconds`.
4. Cada linha: troca de frame, typewriter com os clips, indicador pulsando até o avanço.
5. Fim da última linha: fade para preto em `endFadeSeconds` e a VN se esconde.
6. Retorno: `ReturnToDay` abre o preto e devolve o bloqueio; `AdvanceToNight` chama `Session.TryEndPeriod`.

O jogador **pula o typewriter, nunca o evento**. O avanço é lido dentro do próprio controller (mouse, E, Espaço), como era na cena própria.

Entrada de gameplay fica bloqueada por `InputBlockReason.Cutscene`, com `AddReason`/`RemoveReason` no `Blocker` do `GameplaySceneController`. É contagem por motivo, então não desliga o `EventSystem` nem impede a própria VN de ler o avanço.

### 19.4 O fader em repouso

A primeira versão deixava o fader com alpha 1 no `Awake`, para evitar piscada da sala no primeiro frame. O efeito colateral: se nenhum Dia disparasse o rádio, o Dia inteiro ficava preto.

Agora o fader fica **transparente** em repouso e o escurecimento é a primeira coisa que o `Play` faz. A sala aparece normalmente até o evento começar, e o corte para o preto é uma transição de 0.4s, não um estado permanente.

### 19.5 Entrada no fluxo do Dia

`DayFlowController` deixou de pedir carregamento e passou a chamar a VN:

```csharp
if (radioView == null) { Debug.LogWarning(...); return; }
if (!radioView.Play(definition)) return;
session.MarkDayEventConsumed(definition.eventId);
```

A marcação vem **depois** do `Play` aceitar. Se a definição estiver sem linhas, o evento não é marcado e continua disponível para a próxima navegação.

O campo `radioView` é `[SerializeField]`, mas também se resolve sozinho com `FindFirstObjectByType<RadioEventController>(FindObjectsInactive.Include)` no `Initialize`. Na prática: criar a VN na cena já liga tudo, sem precisar rodar o menu do fluxo de novo.

Gatilho por Dia, sem mudança: Dia 1 no boot (1.5s depois da cortina), Dias 2 a 4 na terceira troca de ViewNode.

### 19.6 Menu novo

O aviso de runtime citava um menu que não existia. Agora existe:

**`Whispers > Rádio > Criar VN na cena day`** monta na cena aberta:

- `Canvas_Radio` (Screen Space Overlay, sorting order 200, escala 1920×1080);
- `RadioViewRoot` inativo com `Frame_01`..`Frame_03`, `DialogueFade` (faixa inferior de 28%), `DialogueText` (Special Elite 34) e `AdvanceIndicator`;
- `Fader` preto por cima de tudo;
- dois `AudioSource` (jingle e typewriter);
- `RadioEventController` na raiz do canvas, com todos os campos ligados, `fallbackDefinition` = `RADIO_Dia1` e `scene` = `GameplaySceneController`.

O menu é idempotente: se já houver `RadioEventController` na cena, avisa e não faz nada. O `ApplyDayFlow` (menu de prioridade 71) passou a exigir a VN e a gravar `radioView` no `Manager_DayFlow`.

Pendências que continuam sendo de autoria: **arte nos três frames, linhas nos quatro assets e os clips de áudio**.

### 19.7 Validação feita sem Editor

Sem Unity e sem compilador no ambiente, a validação foi estrutural:

| Checagem | Resultado |
|---|---|
| Chaves e parênteses nos quatro arquivos C# tocados | balanceados |
| Membros removidos de `GameSessionManager` vs. `ad11262` | exatamente os três métodos de rádio; nada mais perdido |
| Referências a `radioEventScene` / `radio_event` no projeto | zero |
| `TryEndPeriod` | intacto (checkpoint inline, Dia→Noite e Noite→Dia) |
| Build Profile | cena `radio_event` removida, restam três cenas |
| Slots `dayRadioEvents` na `day.unity` | quatro GUIDs batendo com `RADIO_Dia1..4` |

O que só o Editor pode dizer: se compila, se o sorting order 200 fica acima do HUD e do tooltip, e se a arte dos frames encaixa na resolução.

### 19.8 Clique durante o preto e o jingle (2026-11-11)

Relato do usuário: "o rádio só inicia quando clico pela primeira vez na cena". A VN não tem nenhum gatilho por clique — a leitura de avanço é a única coisa que um clique faz, e ela só vale com uma linha na tela. O que acontecia é vizinho disso:

**O clique era guardado.** `Update` marcava `_advanceRequested` sempre que `_running` era verdadeiro, e `_running` já era verdadeiro desde o primeiro frame do `Run` — muito antes da primeira linha aparecer. Quem clicasse durante o preto ou durante a espera do jingle teria esse clique consumido em `PlayLine`, e a primeira linha aparecia **já completa**, no mesmo frame. De fora: o clique parecia ter iniciado o rádio.

Correção: a flag `_acceptAdvance` abre só dentro de `PlayLine` e fecha no fim da linha. Clique fora da janela é descartado, não acumulado.

### 19.9 Diagnóstico do gatilho (2026-11-11)

Como o relato não fechava com o código — o gatilho do Dia 1 é `1.5s` depois de a cena ficar pronta, sem input nenhum — o `DayFlowController` ganhou o campo `verboseLog` (ligado por padrão) e passa a escrever no Console, com o prefixo `[Radio/Dia]`:

- o `Initialize`: Dia atual, asset do Dia, quantidade de linhas, boot ligado ou não, navegações exigidas, nome da VN;
- o `FirstDayBootRoutine`: por que o boletim não tocou (período, dia) ou que está chamando;
- cada navegação: contador e par `de -> para`;
- cada motivo de não tocar: sem asset, sem linhas, já consumido, faltam navegações (`2 de 3`);
- a entrega à VN e a recusa dela.

O `RadioEventController` escreve `Play aceito` e a espera do jingle. Com as duas linhas juntas dá para saber se o rádio tocou no boot ou se algo pediu antes.

A espera por `_scene.IsReady` também ganhou teto de 15s: se a cena não ficar pronta (erro de boot, cortina presa), o boletim toca mesmo assim e um warning explica. Antes, uma cortina presa deixava o Dia inteiro sem rádio, sem nenhuma pista.

---

## 20. M8 — Cutscene do mundo entre o menu e o Dia 1 (2026-11-11)

Fechado o M7, o próximo do plano era o M8 (§9): uma cena curta entre "Jogar" e o Dia 1, para o jogo ter uma abertura própria em vez de pular do menu para a sala.

### 20.1 O que entrou

| Onde | O que mudou |
|---|---|
| `Scripts/Cutscene/WorldCutsceneController.cs` | novo: apresentação dos slides, avanço por tempo e por clique, encerramento pela sessão |
| `Scripts/UI/MainMenuController.cs` | "Jogar" carrega a cutscene quando ela existe e está no Build Profile; `BeginTransition` passou a aceitar período nulo |
| `Editor/CutsceneSceneSetup.cs` | novo menu `Whispers > Cenas > Criar cena world_cutscene` |
| `GameCycleDefinition.worldCutsceneScene` | já existia; agora é usado de verdade |

A cena em si é criada pelo menu, não versionada à mão: ela carrega GUID de sprite e de fonte, e escrever esse YAML à mão é o tipo de coisa que quebra PPtr (§17). O menu monta e salva a cena no lugar de o repositório guardar um arquivo que eu não consigo validar.

### 20.2 Como o fluxo ficou

```text
Jogar
  └─ PrepareNewGameSession        (checkpoint inicial, já existia)
      └─ worldCutsceneScene existe e está no Build Profile?
          ├─ sim  → LevelManager.LoadScene(cutscene, sem legenda de período)
          │           └─ WorldCutsceneController.Finish()
          │               └─ Session.StartPreparedPeriod()  →  day, legenda "Dia 1"
          └─ não  → LevelManager.LoadScene(day, "Dia 1")     (comportamento anterior)
```

Duas decisões que valem registro:

**A cutscene não carrega cena por conta própria.** Ela pede o período já preparado à sessão (`StartPreparedPeriod`), e quem carrega é o `LevelManager`, com a legenda "Dia 1" e o áudio de carregamento. Se a cutscene chamasse `SceneManager.LoadScene`, a legenda e o áudio se perderiam e a sessão ficaria com o checkpoint preparado sem ter sido usada.

**O carregamento da cutscene não tem legenda de período.** `LevelManager.LoadScene` recebe `loadingPeriod` nulo, e é isso que desliga a legenda e o áudio de carregamento. Para isso o `BeginTransition` do menu passou de `GamePeriod` para `GamePeriod?`: nulo significa "carga sem legenda". "Continuar" não mudou — continua indo direto ao período salvo, sem passar pela cutscene.

### 20.3 Sem arte, sem travas

A cena nasce com a lista de slides vazia e o controller encerra no primeiro frame, com uma linha no Console. A cena pode entrar no Build Profile antes da arte existir sem travar o jogo, e o menu não precisa ser desconfigurado enquanto os desenhos não chegam.

Slide sem sprite na lista não trava nada: a tela fica preta naquele slide e a apresentação continua.

### 20.4 Comportamento da apresentação

- Entra com a tela coberta e abre em `fadeSeconds`.
- Cada slide dura `secondsPerSlide`, ou menos se o jogador avançar.
- Avanço: clique, E ou Espaço. **Clique durante o fade não conta** — a janela de avanço só abre com o slide já na tela (mesma correção do §19.8 no rádio).
- `ESC` não sai. A cutscene é a única passagem entre o menu e o Dia 1, então não existe saída antecipada.
- No fim: fade para preto e entrega à sessão.
- Tudo em tempo não escalado.

O rótulo de dica (`SkipLabel`) nasce escondido e só aparece se `advanceHint` tiver texto. O texto é autoria do usuário: campo pronto, vazio.

### 20.5 Menu e Build Profile

`Whispers > Cenas > Criar cena world_cutscene` (prioridade 20):

1. recusa se `Scenes/world_cutscene.unity` já existe;
2. pede para salvar a cena aberta;
3. cria `WorldCutscene` com `Canvas_Cutscene` (Background preto, Slide, SkipLabel, Fade na frente de todos);
4. liga `slideImage`, `fade`, `skipLabel` e `cycle` no controller;
5. salva a cena;
6. insere a cena no Build Profile **logo depois do menu e antes do Dia 1**;
7. grava `worldCutsceneScene = "world_cutscene"` no `GameCycleDefinition` cujo `dayScene` aponta para `day`.

Os itens 6 e 7 são os que costumam ser esquecidos: sem o 6 a cutscene nunca toca (o menu avisa no Console e segue direto ao Dia), e sem o 7 o menu nem tenta.

### 20.6 Como testar antes da arte

Duas formas:

- **Fluxo real:** menu > Jogar. A cena carrega, fica preta, e vai ao Dia 1 quase imediatamente (lista de slides vazia).
- **Cena isolada:** abrir `world_cutscene` e dar Play. A sessão não tem período preparado, então o `StartPreparedPeriod` falha, o controller avisa no Console e carrega `day` direto — é a rede de segurança para não deixar a cena preta e sem saída durante a autoria.

### 20.7 Critérios de aceite do plano

| Critério (§9.4) | Situação |
|---|---|
| "Jogar" exibe a cutscene | sim, quando há cena no Build Profile e slides |
| Cutscene sem legenda de carregamento | sim, `loadingPeriod` nulo |
| Fim da cutscene carrega o Dia 1 com a legenda | sim, via `StartPreparedPeriod` |
| "Continuar" não passa pela cutscene | sim, `ContinueGame` intocado |
| Cena ausente do Build Profile não bloqueia o início | sim, warning e caminho antigo |

Validação estrutural, como sempre sem Editor: chaves, parênteses e colchetes balanceados nos três arquivos C#; os campos que o menu escreve por `SerializedObject` conferem com os campos declarados no controller; `StartPreparedPeriod` existe na sessão com a assinatura usada.

O que só o Editor responde: se a cena nova importa sem erro, se o Build Profile aceita a inserção na posição certa e se a ordem visual (Background, Slide, SkipLabel, Fade) está como esperado.

---

## 21. Prólogo da cutscene e texto nos slides (2026-11-11)

O M8 (§20) entregou a cutscene com imagem e nada mais: `Slide`, `SkipLabel` e `Fade`. Não havia onde escrever o prólogo. Esta entrega abre esse espaço e escreve o texto.

### 21.1 Suporte de texto

`WorldCutsceneController` ganhou:

| Campo | Para quê |
|---|---|
| `slideLines` | uma linha por slide, na mesma ordem dos sprites |
| `slideText` | rótulo TMP onde a linha aparece |
| `autoSecondsFromText` | duração calculada pelo tamanho da linha, ligada por padrão |
| `charactersPerSecond` | velocidade de leitura do cálculo, 16 por padrão |

O número de slides passou a ser o **maior** entre sprites e linhas. Isso permite rodar o
prólogo só com texto sobre fundo preto e soltar a arte depois, sem mexer nas linhas: slide
sem sprite não desenha imagem nenhuma e o texto continua.

A duração de cada slide saiu do valor fixo. `secondsPerSlide` virou piso, e o tamanho da
linha estica o tempo:

```text
duração = max(secondsPerSlide, caracteres / charactersPerSecond)
```

Sem isso, um slide de 181 caracteres e outro de 100 ficariam o mesmo tempo na tela, e o
jogador ou perde o final do longo ou espera parado no curto. Quem clicar avança antes.

### 21.2 O texto

Sete slides, 972 caracteres, cerca de 54 segundos sem clique. O prólogo está em
`Assets/Documents/Whispers_Of_Unknown_Prologo.md`, com a tabela de ritmo, a versão curta de
quatro slides e as notas de escrita.

Decisões que valem registro, porque o texto veio de um resumo de design:

- **Ninguém recebe nome.** Nem o protagonista, nem as criaturas, nem a colônia. Nomear fecha a interpretação, e o jogo é sobre juntar pedaços.
- **O ano entra como fato.** "Em 2076, sobraram poucos de nós" diz a data e o tom na mesma frase, em vez de "no ano de 2076, a humanidade...".
- **A escassez aparece como rotina.** Recolher, seguir rastro, abrir porta destrancada: tarefas de quem perdeu o espanto. É o mesmo tom do rádio dos Dias, onde instrução e medo vêm na mesma voz.
- **O mistério central fica encostado.** O penúltimo slide conta como a história é contada sem citar "documentos" e "monólogos" como categorias. O último planta a voz militar e para, sem confirmar ligação com o sinal da colônia.
- **Nada de "o jogador" nem de "você".** O texto fala de um eu que já está no mundo.

### 21.3 Menu

**`Whispers > Cenas > Adicionar texto na cutscene`** (prioridade 21) resolve o caso de a cena já ter sido criada pelo menu do §20:

1. abre `world_cutscene.unity`;
2. cria o `SlideText` (TMP, terço inferior, centralizado, fonte dos documentos) se ele não existir, ou reaproveita o que estiver lá;
3. coloca o texto **abaixo** do `Fade`, para o preto cobrir o texto nas transições;
4. liga o campo `slideText`;
5. se `slideLines` estiver vazia, preenche com o prólogo. Se já tiver conteúdo, **não sobrescreve**.

O menu de criação (§20.5) também passou a montar o texto e preencher o prólogo de saída,
então uma cena criada agora já nasce com tudo.

### 21.4 O que fica de fora, de propósito

Não há efeito de máquina de escrever no prólogo. A cutscene é curta e o texto é para ser
lido inteiro, não revelado aos poucos: o typewriter faz sentido no rádio, onde há uma voz
transmitindo, e aqui viraria enfeite. Se quiser depois, é o mesmo bloco de `PlayLine` do
`RadioEventController`.

Validação como sempre: chaves, parênteses e colchetes balanceados nos dois arquivos C#; os
campos escritos pelo menu por `SerializedObject` (`slideText`, `slideLines`) conferem com os
declarados no controller; o prólogo no menu tem exatamente as sete linhas do documento.

---

## 22. Typewriter no prólogo e o aviso do E (2026-11-11)

O §21.4 dizia que o prólogo não teria máquina de escrever. O usuário pediu o contrário: a cutscene apresenta o texto com typewriter e um aviso de pulo com a tecla E. Esta seção substitui aquela decisão.

### 22.1 O que o slide faz agora

Cada slide tem um orçamento de tempo, calculado pelo tamanho da linha (§21.1). Dentro dele cabem duas fases:

1. **Typewriter.** A linha aparece letra por letra a `typewriterCharactersPerSecond` (45 por padrão).
2. **Leitura.** Com a linha completa, o slide espera `max(minHoldSeconds, orçamento - tempo de digitação)`.

Um slide de 181 caracteres, por exemplo, gasta uns 4s digitando e fica mais 7s na tela pronta. Não existe espera mínima separada para o typewriter: o tempo de leitura é sempre o total menos o que já foi gasto digitando, então digitar mais rápido não encurta o slide.

### 22.2 E tem dois papéis

| Momento | O que E faz |
|---|---|
| Linha sendo digitada | completa a linha |
| Linha completa | avança para o próximo slide |
| Último slide | encerra e carrega o Dia 1 |

A mesma tecla nunca vale duas coisas no mesmo toque: `_advanceRequested` é zerado entre o fim do typewriter e o começo da espera, como no rádio (§19.8). Sem isso, um E para completar a linha avançaria o slide também, e o jogador pularia o slide inteiro sem querer.

Clique e Espaço fazem o mesmo que E. O aviso na tela fala de E porque é a tecla que o jogador aprende no rádio e vai reusar a vida inteira do jogo.

### 22.3 O aviso

Rótulo `SkipLabel`, canto inferior direito, texto padrão **"E para pular"**, herdam o mesmo TMP do resto da interface. Continua escondido quando `advanceHint` está vazio — a autoria do texto do aviso continua sendo sua.

O menu `Adicionar texto na cutscene` passou a garantir três coisas que a cena criada antes desta entrega não tinha: `slideText` ligado, `advanceHint` preenchido e `skipLabel` ligado (por nome, se o campo estiver vazio). Assim a atualização chega sem precisar recriar a cena.

### 22.4 Pular a cutscene inteira

O plano §9.2 dizia que `ESC` não sai e que só o fim do último slide avança. A leitura de "pular" pode ser a do typewriter, que é a implementada, ou a da cutscene toda. Para não decidir pelo usuário, existe o campo `eSkipsWholeCutscene`, desligado por padrão:

- **desligado**: E completa a linha e depois avança slide a slide;
- **ligado**: o primeiro E fecha a cutscene sob o preto e carrega o Dia 1.

Com ele ligado, o aviso "E para pular" fica literal. Com ele desligado, o aviso continua honesto, mas o pulo é do texto, não da cena.

### 22.5 Validação

Chaves e parênteses balanceados nos dois arquivos C#; o campo `advanceHint` existe no controller com o mesmo nome escrito pelo menu; `typewriterCharactersPerSecond`, `minHoldSeconds` e `eSkipsWholeCutscene` declarados e usados; `SkipWholeCutscene` é chamado nos dois pontos de avanço (typewriter e espera) e sempre com `yield break` depois, para o slide não continuar por baixo do fade.

O que só o Editor responde: se 45 caracteres por segundo é confortável na Special Elite em 34pt, e se o aviso no canto inferior direito não encosta no texto do último slide.

---

## 23. Cutscene do mundo retirada (2026-11-11)

Decisão do usuário: a cena `world_cutscene` saiu do projeto. O M8 (§20, §21, §22) foi desfeito e o fluxo voltou a ser o que era:

```text
Jogar  →  PrepareNewGameSession  →  LevelManager carrega o Dia 1 com a legenda "Dia 1"
```

### 23.1 O que foi removido

| Onde | O que saiu |
|---|---|
| `Scripts/Cutscene/WorldCutsceneController.cs` | arquivo, meta e a pasta `Scripts/Cutscene` |
| `Editor/CutsceneSceneSetup.cs` | arquivo e meta: saem os menus `Criar cena world_cutscene` e `Adicionar texto na cutscene` |
| `Scripts/UI/MainMenuController.cs` | o desvio do "Jogar" para a cutscene e o parâmetro `GamePeriod?` do `BeginTransition`, que volta a ser `GamePeriod` |
| Build Profile | a cena sai da lista (feito no projeto do usuário, não no repositório) |
| `Scenes/world_cutscene.unity` | a cena é arquivo local, criado por menu: precisa ser apagada no projeto |

### 23.2 O que ficou

`GameCycleDefinition.worldCutsceneScene` continua declarado. Ele já existia antes desta
sequência de trabalho e agora nenhum código o lê: é um campo inerte. Deixado de pé porque
remover campo de `ScriptableObject` mexe no asset do usuário e não traz ganho — mas some da
lista de pendências.

### 23.3 O texto não se perdeu

O prólogo de sete linhas continua em `Assets/Documents/Whispers_Of_Unknown_Prologo.md`,
agora marcado como **parado**, com três candidatos de casa ao fim do arquivo:

1. **VN de abertura na cena do Dia**, com o mesmo desenho do rádio (§19): camada de UI sobre o gameplay, typewriter, aviso do E, devolve a sala. Metade do código já existe.
2. **Texto na tela de carregamento**, trocando ou somando à legenda "Dia 1" que o `LevelManager` já desenha. Cobre o tempo morto do carregamento, mas só cabem frases curtas.
3. **Documento no abrigo**, ao lado dos avisos. Encaixa na narrativa fragmentada, mas depende de o jogador procurar.

As linhas foram escritas para caber nos três: cada uma funciona como slide, cartela ou página de documento.

### 23.4 O que se aprendeu com a ida e a volta

O M8 custou uma cena, um controller, um menu de editor, dois ajustes de fluxo e um texto.
Nada disso virou lixo: o que sobra de útil é a decisão de arquitetura que se confirmou duas
vezes, primeiro no rádio (§19) e agora. **Cena nova só quando ela precisa de câmera, áudio
próprio e ciclo de vida separado.** Para apresentar imagem e texto por cima do que o jogador
já está vendo, camada de UI na cena existente resolve sem carregamento, sem estado de
retorno e sem uma segunda tela de loading.

O prólogo e o typewriter do §22 são exatamente isso: peças de uma VN, não de uma cena. Se o
texto voltar, volta como camada.

---

## 24. M9 — Progressão dos Dias, primeira parte (2026-11-11)

O §10 do plano pede seis coisas para o M9: tabela dos Dias, banner de objetivo, trava de navegação por Dia, painel de tutorial, disparo do rádio por Dia e o Dia 4 sem exploração. Esta entrega fecha quatro delas e deixa o painel de tutorial para uma decisão à parte (§24.5).

### 24.1 O que entrou

| Arquivo | O que é |
|---|---|
| `Scripts/Day/DayFlowDefinition.cs` | novo: `DayFlowEntry` e o `ScriptableObject` da tabela dos Dias, com `FindEntry` e `CollectValidation` |
| `Scripts/UI/ObjectiveBanner.cs` | novo: banner de objetivo, fade in, espera, fade out, sem bloquear input |
| `Scripts/Conditions/DayAtLeastCondition.cs` | novo: trava de navegação por faixa de Dia |
| `Scripts/Conditions/ConditionContext.cs` | ganhou `Day` e `Night` |
| `Scripts/Day/DayFlowController.cs` | lê a tabela; banner no boot do Dia; navegações exigidas por Dia |
| `Editor/DayFlowSetup.cs` | novo: quatro menus da progressão |

### 24.2 A tabela vence, a cena continua valendo

O `DayFlowController` já tinha `dayRadioEvents` e `requiredNavigationsBeforeRadio` serializados na cena, autorados pelo usuário. Em vez de trocar, a tabela é uma **camada nova por cima**:

```text
GetDefinitionForDay(dia)  →  entry.radioEvent            se a tabela existe e tem o Dia
                          →  dayRadioEvents[dia - 1]      senão (comportamento anterior)

RequiredNavigations(dia)  →  entry.requiredNavigationsBeforeRadio  se a tabela existe
                          →  requiredNavigationsBeforeRadio        senão
```

Cena sem tabela continua funcionando exatamente como antes; cena com tabela passa a ser autorada no asset. Isso evita que a progressão narrativa dependa de abrir a cena para mudar uma linha de texto.

`DayFlowEntry` não tem `advanceToNightAfterRadio`, que o plano §10.4 sugeria: quem decide o fim do Dia é o `returnMode` do próprio `RadioEventDefinition` (§13). Duplicar a decisão em dois lugares abre espaço para os dois discordarem, e o rádio já sabe a resposta.

### 24.3 A trava de navegação

`DayAtLeastCondition` tem `minDay` e `maxDay` (99 = sem teto) e lê a sessão a cada avaliação. Ela não guarda estado de propósito: o mesmo hotspot é reapresentado em Dias diferentes e precisa responder conforme o Dia corrente.

A aplicação nos hotspots é feita por **destino de navegação**, não por nome de GameObject:

| Destino | Dia mínimo |
|---|---|
| `vn_portasala` | 2 |
| `vn_portacozinha` | 2 |
| `vn_sotao` | 3 |

Na cena existem vários hotspots apontando para o mesmo destino (`Hotspot_ToSala` aparece duas vezes, `Hotspot_ToCorredorFront` três). Casar por destino trava todos de uma vez; casar por nome deixaria metade passando.

O modo quando indisponível vai para `Blocked`, não `Hidden`: a porta continua visível e não clica. Sumir com a porta diria ao jogador que ela não existe, e o plano quer o contrário, que ele veja que existe e ainda não pode.

### 24.4 Os quatro menus

| Menu | O que faz |
|---|---|
| `Whispers > Dia > Criar tabela do Dia` | cria `SO/Day/DayFlow_Gameplay.asset` com os quatro Dias, rádio ligado e `objectiveText` vazio; liga no `DayFlowController` se a cena `day` estiver aberta |
| `Whispers > Dia > Criar banner de objetivo` | cria `Canvas_Objetivo` (sorting order 150, superior esquerdo) e liga no `DayFlowController` |
| `Whispers > Dia > Aplicar travas de navegação` | cria `COND_Dia2` e `COND_Dia3` e aplica nos hotspots por destino; idempotente |
| `Whispers > Dia > Validar tabela do Dia` | roda `CollectValidation` e lista erros e avisos |

O de travas é idempotente pela mesma razão do §19.6: rodar duas vezes não pode duplicar condição no hotspot nem duplicar estado na cena. Ele também informa quais destinos ficaram sem trava, para a cena autorada com nomes diferentes não passar em silêncio.

### 24.5 O painel de tutorial ficou de fora, de propósito

O §10.5 do plano pede um `TutorialPanelController` que substitui o `DayEndConfirmationController` no fluxo, com gatilhos por Dia (pegar a lanterna no Dia 1, entrar numa porta no Dia 2, posicionar o lampião no Dia 3).

Duas coisas mudaram depois que o plano foi escrito:

1. O §15 removeu os tutoriais de entidade e deu ao rádio o papel de ensinar os verbos. O painel do §10.5 voltaria a ensinar pelos mesmos gatilhos que o rádio já cobre, com o risco de os dois falarem do mesmo comando com palavras diferentes — foi exatamente o problema que o §18.3 resolveu.
2. O `DayEndConfirmationController` está na cena `day`, autorado, com botão. Substituí-lo mexe no fechamento do Dia, que é o caminho mais sensível do ciclo, e o pedido do usuário era a progressão dos Dias, não o fim deles.

Então o painel fica pendente com uma pergunta clara: **o fim do Dia continua sendo o botão do `DayEndConfirmationController`, ou vira um painel de tutorial com "Prosseguir"?** Enquanto a resposta não vier, o caminho que existe hoje continua intacto.

### 24.6 Critérios de aceite do §10.7

| Critério | Situação |
|---|---|
| Dia 1 mostra "Encontre a lanterna" | estrutura pronta: falta o texto no asset (autoria) e rodar o menu do banner |
| Sótão e portas mostram bloqueio no Dia 1 | sim, pelo menu de travas (`Blocked` nas três portas) |
| Rádio toca uma vez depois de 3 navegações | sim, desde que o §19 |
| Dia 2 libera as portas e mantém o sótão | sim, pelas condições |
| Dia 3 libera o sótão | sim |
| Painel abre com lampião e óleo | pendente por decisão (§24.5) |
| Dia 4 toca o rádio sem exploração | depende de `requiredNavigationsBeforeRadio = 0` no Dia 4 da tabela, autorável no asset |

### 24.7 Validação

Chaves, parênteses e colchetes balanceados nos seis arquivos tocados. Os campos escritos pelos menus por `SerializedObject` (`definition`, `objectiveBanner`, `group`, `label`, `conditions`, `unavailableMode`) existem com esses nomes nos componentes. Os destinos usados nas travas (`vn_portasala`, `vn_portacozinha`, `vn_sotao`) conferem com os `destinationId` presentes na `day.unity`.

O que só o Editor responde: se o `Canvas_Objetivo` em 150 fica abaixo do rádio em 200 e acima do HUD, se o TMP em 32 no canto superior esquerdo não encosta na borda em 16:9, e se o `unavailableMode = Blocked` desenha o estado de bloqueio que o usuário autorou.

---

## 25. Objetivos como ScriptableObject (2026-11-11)

O §24 guardava o objetivo do Dia como string dentro do `DayFlowEntry`. Pedido do usuário: seguir o padrão do projeto, que é **um asset por conteúdo**.

### 25.1 O padrão que foi seguido

O projeto já faz assim em três lugares: `DocumentData` (`DOC_*`), `RadioEventDefinition` (`RADIO_*`) e `ItemDefinition` (`ITEM_*`). Todos têm:

- classe `ScriptableObject` com `[CreateAssetMenu(fileName = "PREFIXO_Nome", menuName = "Whispers/<Domínio>/<Tipo>")]`;
- ID estável em texto (`doc_tutorial_predator`, `radio_dia_1`);
- conteúdo textual em campos próprios, com `[TextArea]` para o corpo;
- asset prefixado em maiúsculas, dentro de uma pasta de domínio em `SO/`.

`ObjectiveDefinition` faz o mesmo:

| Campo | Para quê |
|---|---|
| `objectiveId` | ID estável, `obj_dia_1` a `obj_dia_4` |
| `title` | linha principal, a que o jogador lê primeiro |
| `body` | complemento opcional, exibido abaixo |
| `DisplayText` | título e corpo em uma string, para o banner |

Asset: `SO/Objectives/OBJ_Dia1..4.asset`.

### 25.2 O que mudou no código

`DayFlowEntry.objectiveText` (string) virou `DayFlowEntry.objective` (`ObjectiveDefinition`). O campo antigo não foi mantido como legado: ele nasceu vazio no §24, ninguém autorou texto nele, e deixar os dois caminhos permitiria dois lugares para escrever a mesma coisa — que é o problema que o §18.3 e o §24.2 já tinham resolvido em outros pontos.

`DayFlowController.ShowObjective` passou a ler `entry.objective.DisplayText`. Dia sem objetivo ligado, ou com objetivo sem texto, não abre banner e escreve o motivo no log.

A validação da tabela (`CollectValidation`) ganhou os dois casos: sem objetivo e objetivo vazio, com o nome do asset no aviso.

### 25.3 Por que asset e não string

Três motivos concretos, e nenhum é estético:

1. **Reaproveitamento.** O mesmo objetivo pode aparecer em dois Dias apontando para o mesmo asset. Com string, seria texto duplicado em dois lugares e uma edição esquecida.
2. **Autoria fora da cena.** O texto do objetivo passa a ser editado no asset, como documento e rádio, sem abrir `day.unity` nem a tabela.
3. **Referência rastreável.** O `Progresso` e o TCC podem citar `OBJ_Dia1` como peça narrativa, do mesmo jeito que citam `DOC_TUTORIAL_Predator`.

### 25.4 Títulos iniciais

Os objetivos nascem com o texto da tabela do plano (§10.1), porque era isso que o usuário tinha em mão:

| Asset | Título inicial | Corpo |
|---|---|---|
| `OBJ_Dia1` | Encontre a lanterna | vazio |
| `OBJ_Dia2` | Vá até as portas | vazio |
| `OBJ_Dia3` | Encontre o lampião e o óleo | vazio |
| `OBJ_Dia4` | vazio (o plano não pede objetivo) | vazio |

São ponto de partida, não texto final: o menu **nunca** sobrescreve título de asset que já existe. O `body` nasce vazio em todos, para o complemento ser autoria de quem escreve a narrativa.

### 25.5 Menu

**`Whispers > Dia > Criar objetivos dos Dias`** (prioridade 34):

1. cria a pasta `SO/Objectives` se faltar;
2. cria `OBJ_Dia1..4` que ainda não existirem, com ID e título do plano;
3. liga cada objetivo na entrada do Dia correspondente da tabela, **só** onde a referência está vazia;
4. informa quantos assets existem, quantos foram ligados e se a tabela ainda não foi criada.

O menu `Criar tabela do Dia` também passou a chamar o mesmo passo, então uma tabela nova já nasce com os quatro objetivos ligados.

### 25.6 Validação

Chaves e parênteses balanceados nos três arquivos C# tocados; `objectiveText` não existe mais em nenhum ponto do projeto; `ObjectiveDefinition` tem `[CreateAssetMenu]` no padrão `Whispers/Day/Objective`; os campos escritos pelo menu (`entries[].objective`) conferem com o `DayFlowEntry`.

O que só o Editor responde: se o `DisplayText` com duas linhas cabe no banner em 880x140 no canto superior esquerdo, e se o texto de duas linhas não colide com o HUD.

---

## 26. M9 parte 2 — painel de tutorial e fechamento de pendências (2026-11-11)

O §24 deixou o painel de tutorial em aberto por dois motivos: o rádio já ensina os verbos e mexer no fechamento do Dia é mexer no caminho mais sensível do ciclo. A saída foi construir o painel **sem trocar nada**: ele existe como peça pronta e desligada, e quem encerra o Dia continua sendo o `DayEndConfirmationController`.

### 26.1 O que entrou

| Arquivo | O que é |
|---|---|
| `Scripts/Day/TutorialPanelDefinition.cs` | novo: conteúdo do painel (`tutorialId`, `title`, `body`, `proceedLabel`, `illustration`, `endPeriodOnProceed`) |
| `Scripts/UI/TutorialPanelController.cs` | novo: painel que bloqueia o cenário, abre por definição e pede o fim do Dia no "Prosseguir" |
| `Scripts/Day/DayFlowDefinition.cs` | `DayFlowEntry.tutorial` e validação do painel |
| `Scripts/Day/DayFlowController.cs` | `OpenTutorial()` público, uma vez por Dia |
| `Editor/DayFlowSetup.cs` | menu `Criar painel de tutorial` (35), `Canvas_Tutorial` e `TUTORIAL_Dia1..4` |

### 26.2 Por que gatilho por UnityEvent e não automático

O plano §10.5 listava gatilhos por Dia: pegar a lanterna, entrar numa porta, posicionar o lampião. Implementar isso em código exigiria assinar eventos de interação, evento que o `InteractionManager` não emite hoje, e amarraria a narrativa a três condições específicas que nem existem como conteúdo ainda.

O projeto já tinha a resposta: o `DayEndConfirmationController` abre por UnityEvent de hotspot. O painel seguiu o mesmo caminho:

```text
hotspot.onActivated → DayFlowController.OpenTutorial()
                         └─ resolve o painel do Dia na tabela
                            respeita "uma vez por Dia"
                            abre o TutorialPanelController
```

Assim o gatilho é autorado na cena, como todo o resto, e o código não precisa saber se o Dia 2 começa numa porta, num lampião ou numa nota no chão.

### 26.3 A marca de "já mostrei"

A chave é `tutorial_<dia>_<tutorialId>`, gravada em `MarkDayEventConsumed` — o mesmo mecanismo que impede o rádio de tocar duas vezes. Ele é transitório e limpo na troca de período, então cada Dia tem sua própria memória de painéis.

### 26.4 As duas regras de bloqueio

O painel usa `InputBlockReason.Cutscene`, e não `Modal`: o rádio também usa `Cutscene`, e os dois nunca deveriam estar abertos ao mesmo tempo. Se estiverem, a contagem por motivo evita que o segundo fechamento libere a entrada do primeiro.

O bloqueio é removido **antes** de `TryRequestPeriodEnd`. Isso não é detalhe de estilo: `RequestPeriodEnd` exige a entrada livre, e pedir com bloqueio ativo resulta em recusa silenciosa. O `DayEndConfirmationController` já fazia assim, e a razão está registrada no comentário dele.

### 26.5 O que foi decidido de vez

**Dia 4 sem exploração.** A tabela passa a nascer com `requiredNavigationsBeforeRadio = 0` no Dia 4, e 3 nos outros. Antes o campo nascia em 3 em todos e o Dia 4 dependia de o usuário editar à mão.

**Limpeza de legado.** `RADIO_EventoDia.asset` foi apagado (não referenciado desde os quatro Dias) e `GameCycleDefinition.worldCutsceneScene` foi removido do script e do `CYCLE_Gameplay.asset`, porque ficou inerte depois que a cutscene saiu (§23).

### 26.6 O que continua em aberto

Só o desenho do fim do Dia: **botão do `DayEndConfirmationController` ou painel com "Prosseguir"?** As duas peças funcionam e as duas pedem o fim do período do mesmo jeito. O risco novo, registrado no estado atual, é ligar as duas na mesma cena e o jogador receber dois pedidos.

### 26.7 Validação

Chaves e parênteses balanceados nos cinco arquivos C#, incluindo os 711 linhas do menu de editor. Os campos que o menu escreve (`panel`, `group`, `titleLabel`, `bodyLabel`, `illustrationImage`, `proceedButton`, `proceedLabel`, `fallbackDefinition`, `tutorialPanel`) conferem com as declarações nos componentes. Nenhuma referência a `worldCutsceneScene` ou `RADIO_EventoDia` sobrou no projeto.

O que só o Editor responde: se o `Canvas_Tutorial` em 180 fica acima do banner (150) e abaixo da VN (200), se o `Dim` com alpha 0,88 come o clique do cenário como esperado, e se o botão "Prosseguir" responde ao clique com o painel aberto.


## 27. Correção: objetivo após o rádio

O objetivo aguarda o evento `Finished` da VN antes de aparecer; o banner permanece visível até ser ocultado ou a cena ser descarregada. Se não houver rádio com linhas ou componente de rádio, ou se o evento já tiver sido consumido, o objetivo aparece na inicialização. Caso o rádio exija navegações, o objetivo aguarda essas navegações e o término da transmissão. Se o rádio avançar diretamente para a Noite, não há retorno ao Dia e o evento de conclusão não é emitido. Nenhuma execução no Unity foi validada neste ambiente.


## Correção dos tutoriais e gatilhos de conclusão

Na cena remota analisada, os dois hotspots dos documentos de cliques e movimentação não possuíam condições, e o `DayFlowController.OpenTutorial` não era chamado pela cena nem pelo fluxo. A lanterna ainda chamava o antigo modal de confirmação; isso poderia bloquear ou antecipar o fim do Dia.

O menu `Whispers > Dia > Configurar tutoriais e conclusão` acrescenta a condição de Dia exatamente 1 aos dois hotspots, sem apagar outras condições; usa modo Oculto e exige CanvasGroup. Verifica e liga o controller do painel; remove somente a chamada persistente `DayEndConfirmationController.Open` da coleta da lanterna, preservando os demais eventos e o outro hotspot de confirmação. Salve a cena. Assets criados não são removidos por Undo.

O fluxo verifica os objetivos após as interações e aguarda fim de bloqueios: Dia 1, lanterna no inventário; Dia 2, primeira entrada em uma das portas; Dia 3, fato `LampPlacedInAttic` e óleo no inventário, em qualquer ordem. Uma vez por Dia; o consumo é marcado somente depois da abertura. Sem conteúdo no asset, painel não abre. Se o pedido de fim de período for recusado, o painel reabre para permitir nova tentativa. Não foram compilados nem testados no Unity ou Play Mode.


## Rádio automático no início de cada Dia

A pedido do usuário, a transmissão configurada inicia em cada Dia depois de o gameplay ficar pronto e da espera de abertura em segundos reais. O fluxo aguarda desbloqueio de input e fim de outra transmissão antes de abrir a VN; não depende mais de contagem de navegações. O objetivo permanece aguardando o fim da transmissão. O consumo continua evitando repetição na mesma sessão de Dia.

Os antigos campos de contagem e de habilitação específica do Dia 1 foram preservados e ocultados apenas para compatibilidade serializada, mas não controlam mais a execução. Os menus de criação foram atualizados para não sugerir a regra antiga. Assets existentes não são reescritos. Um rádio ausente, sem linhas ou com VN indisponível é ignorado com aviso, e o objetivo não fica esperando. Na base analisada `RADIO_Dia4` está sem linhas e não tocará até receber conteúdo. Se o rádio tiver retorno `AdvanceToNight`, segue diretamente para a Noite conforme o asset. Não houve compilação nem teste no Unity.
