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
