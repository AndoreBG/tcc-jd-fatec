# Whispers Of Unknown — Plano de Correção: Redundância no Áudio de Entidades

> **Versão:** 1.0
> **Data:** 2026-10-24
> **Engine:** Unity 6000.3.18f1
> **Plataforma:** PC
> **Escopo:** `SceneAudioController`, `AudioRuntimeModels`, `CycleTestUI` (aba Áudio). Eliminação do estado morto `isActive` e da dupla via `threat:`/`entity:` para o mesmo emissor lógico.
> **Status:** **IMPLEMENTADO** (2026-10-24). Validação de compilação e checklist Play Mode pendentes de execução no Editor Unity (sandbox não possui Unity/dotnet). Nenhum dado serializado mudou; nenhum asset precisou de migração.
> **Decisões registradas na implementação:** (1) a substituição do áudio de produção por sinal de debug é **imediata**, sem fade — ferramenta de dev com caminho síncrono, testável em EditMode; (2) `ThreatAudioState` foi **removido integralmente** do código (erratas aplicadas aos docs VS4 e Plano das Entidades); (3) a posse debug × produção é rastreada por `_debugOwnedEntities` no controller (produção reivindica a entidade em todo `ApplyEntityStateAudio`); (4) `currentClipName` é derivado no `GetDebugSnapshot`, sem virar estado de autoridade; (5) novos endpoints: `DebugSimulateEntityState`, `DebugPlayEntitySignal(entityId, anchorId, signalId, out error)`, `DebugClearEntityAudio`.

---

## 0. Origem

Verificação de duplicidade de campos no sistema de áudio (auditoria de 2026-10-24) concluiu que **não há duplicidade literal** (C#, YAML, fiação de mixer, ids de camadas/anchors/catálogo todos únicos), mas identificou **duas redundâncias reais** no áudio de entidades, ambas herança da API de ameaças de Debug anterior ao pipeline VS5:

1. **`AudioEntityRuntimeState.isActive` é estado morto.** Só recebe `true`; nunca recebe `false`. Inativar uma entidade remove a entrada do dicionário `_entities`. O campo duplica a informação de "a entrada existe no dicionário" sem agregar valor.
2. **Duas APIs paralelas para o mesmo emissor lógico.** A via legada de Debug (`SetThreatState`/`PlayThreatSignal`/`DebugSetEntityAnchor`/`DebugSetThreatState`/`ClearThreatState`, emitters `threat:{id}`, gravam `_entities`) e a via de produção VS5 (`ApplyEntityStateAudio`/`ClearEntityAudio*`, emitters `entity:{id}`, **não** gravam `_entities`). Efeitos:
   - o F8 exibe `snapshot.entities` — que reflete **somente a via de debug**, nunca o áudio real da Noite;
   - se F8 e produção tocarem a mesma entidade, **duas vozes persistentes** (`threat:predator` + `entity:predator`) soam ao mesmo tempo, violando a regra de origem única do documento de arquitetura ("um mesmo acontecimento não deve reproduzir sons equivalentes por mais de uma origem").

Adicional: `currentSignalId` só existe para a via legada e só é consumido por um `GUILayout.Label` do F8.

---

## 1. Objetivo

Uma entidade lógica = **um** modelo de runtime de áudio = **uma** voz persistente, independentemente de quem a acionou (produção ou F8). O F8 passa a enxergar e simular o **mesmo** estado que a produção usa.

---

## 2. Decisões fechadas

| Tema | Decisão |
|---|---|
| Fonte de verdade | O pipeline de produção (`ApplyEntityStateAudio`) é a única via que mantém estado. O F8 torna-se cliente dela via endpoints de debug. |
| Estado por entidade | `AudioEntityRuntimeState` gravado por produção e debug; campos `isActive` e `currentSignalId` removidos do modelo (substituídos por derivação e `debugSignalId`). |
| Emissor | Um único namespace: `entity:{entityId}`. O prefixo `threat:` deixa de existir. |
| Enum exibido | O modelo registra `EntityState` (autoridade VS5). `ThreatAudioState` permanece **somente** como etiqueta de exibição legada via mapeamento explícito, ou é aposentado da aba; decisão de implementação abaixo. |
| Catálogo de debug | `AudioDebugCatalog` continua exclusivo do F8 (invariante do plano VS5). Sinais de ameaça do catálogo passam a tocar **na voz unificada da entidade**, substituindo — não somando — o som de produção. |
| API legada | Removida na mesma entrega: os únicos consumidores (`CycleTestUI`) estão no repositório. Nada de wrappers `[Obsolete]` acumulando dívida. |
| Dados serializados | **Zero mudança de formato**: cenas, assets e o catálogo não são tocados. Nenhuma migração. |
| IA | `EntityDirector` e `EntityPresentationCoordinator` **não mudam**: já chamam `ApplyEntityStateAudio`/`ClearEntityAudioImmediate` com todas as informações necessárias. |
| Debug não toca regra | F8 continua incapaz de alterar estado lógico de entidade, inventário, save ou ciclo (invariantes VS5). A aba simula **somente apresentação sonora**. |

---

## 3. Modelo-alvo

### 3.1 Estado de runtime unificado

```text
AudioEntityRuntimeState              (AudioRuntimeModels.cs)
├── entityId                         (mantém)
├── anchorId                         (renomear de activeAnchorId para refletir posse atual; nenhum consumidor externo usa o nome)
├── entityState: EntityState         (NOVO — substitui threatState)
└── debugSignalId: string|null       (NOVO — substitui currentSignalId; preenchido só por override de debug)

REMOVIDOS: isActive, currentSignalId

Ativo := entrada existe em _entities E entityState ∉ { Inactive, Resolved }
```

Helpers estáticos (não estado):

```text
AudioEntityStateMapper.IsPresentable(EntityState)         // != Inactive && != Resolved
AudioEntityStateMapper.ToLegacyAudioLabel(EntityState)    // usado apenas se o F8 mantiver etiquetas antigas
```

### 3.2 Uma voz por entidade

- Toda reprodução de entidade (produção ou debug) ocupa o emissor `entity:{entityId}` via a mesma rotina de aquisição (`SetPersistentEmitter`/pool persistente existente).
- **Produção** (`ApplyEntityStateAudio`): faz *upsert* da entrada `_entities[entityId]` com `{ anchorId, entityState, debugSignalId: null }` antes de iniciar a transição de áudio. Estados não apresentáveis (`Inactive`/`Resolved`) **removem** a entrada e liberam a voz (hoje o early-exit já zera o emitter; ele passa a gravar a remoção).
- **Debug** (novo `DebugPlayEntitySignal(entityId, anchorId, signalId)`): ocupa **o mesmo** emissor `entity:{entityId}` com o clip do catálogo, marcando `debugSignalId = signalId`. Se havia áudio de produção nesse emissor, ele é interrompido (fade curto) — nunca somado.
- **Saída do modo debug** (`DebugClearEntityAudio(entityId)` ou ao ressurgir o próximo `ApplyEntityStateAudio` real, que sempre zera `debugSignalId`): a entidade volta a refletir o estado de produção.
- `ClearThreatState`, `SetThreatState`, `PlayThreatSignal`, `DebugSetEntityAnchor`, `DebugSetThreatState` são **removidos**.

### 3.3 Snapshot único

`GetDebugSnapshot().entities` passa a refletir produção **e** debug, com campos `entityId | anchorId | entityState | debugSignalId (marcado "[DEBUG]" quando preenchido) | clip atual da voz`.

---

## 4. Alterações por arquivo

### 4.1 `Audio/AudioRuntimeModels.cs`

- `AudioEntityRuntimeState`: remover `isActive`, `currentSignalId`, `threatState`; adicionar `entityState` (`EntityState`) e `debugSignalId`. Ajustar `Copy()`. Atualizar docstring: "estado unificado de áudio por entidade — escrito por produção e debug".
- Manter `AudioEntityRuntimeState` public (F8 e testes consomem).

### 4.2 `Audio/SceneAudioController.cs`

- `ApplyEntityStateAudio`: upsert/remoção em `_entities` conforme `IsPresentable(state)`; ao apresentar, registrar `anchorId`/`entityState` e limpar `debugSignalId`.
- `ClearEntityAudio` / `ClearEntityAudioImmediate`: remover a entrada (regra "remover = inativo").
- Remover métodos legados: `SetThreatState`, `ClearThreatState`, `PlayThreatSignal`, `DebugSetEntityAnchor`, `DebugSetThreatState`. Remover o prefixo `threat:` (buscas remanescentes devem zerar).
- Novos endpoints de debug (aditivos, com prefixo `Debug`):
  - `DebugPlayEntitySignal(entityId, anchorId, catalogSignalId, out string error)` — valida catálogo, ocupa `entity:{id}`, marca `debugSignalId`;
  - `DebugClearEntityAudio(entityId)` — libera o override de debug; se a produção tiver estado ativo registrado, reaplica a apresentação atual (reconsulta formal opcional; o próximo `ApplyEntityStateAudio` real também restaura).
- `GetDebugSnapshot`: incluir `entityState`, `debugSignalId` e o nome do clip na voz `entity:{id}` (`clipName` da `RuntimeVoice` já cobre).
- Guardas existentes preservadas: `isActiveAndEnabled` no teardown de corrotinas; warnings de ausência de clip continuam via `CollectEntityMediaWarnings`/warnings pontuais com `WarnOnce`.

### 4.3 `UI/CycleTestUI.cs` (aba Áudio)

- `DrawAudioState`: rótulo da linha de entidade passa a `entityId | entityState | anchor {anchorId} | sinal [DEBUG] {debugSignalId}`.
- `DrawThreatSimulation` reescrito sobre os novos endpoints:
  - seletor de anchor e de estado continua, mas estados passam a ser `EntityState` apresentáveis (Light/Near/Critical/Resolving/Terminal) com mapeamento direto — ou mantém os quatro estados legados mapeando para Light/Near/Critical; a implementação escolhe a opção mais legível e documenta no rótulo;
  - "Reproduzir sinal" → `DebugPlayEntitySignal`; "Desativar" → `DebugClearEntityAudio`;
  - incluir aviso fixo na caixa: "Simulação de APRESENTAÇÃO sonora; não altera IA, entidade lógica ou save."
- Remover referências a `_selectedThreatState`/`ThreatAudioState` se a decisão for aposentar o enum na aba.

### 4.4 Sem alteração

`EntityDirector`, `EntityPresentationCoordinator`, `AudioDebugCatalog`, `AudioAnchorDefinition`, `ViewAudioProfile`, `EntityDefinition`, cenas e assets.

---

## 5. Ordem de implementação

1. **Inventário final de consumidores** (greps): `SetThreatState`, `PlayThreatSignal`, `ClearThreatState`, `DebugSetEntityAnchor`, `DebugSetThreatState`, `currentSignalId`, `isActive`, `threatState`, `ThreatAudioState` — lista fechada antes de editar (hoje: somente `SceneAudioController` + `CycleTestUI`).
2. Modelo (`AudioRuntimeModels`) + helpers de mapeamento. Compilação guiará os pontos a ajustar em seguida.
3. `SceneAudioController`: gravação no pipeline de produção, emissor único, endpoints de debug, snapshot, remoção dos métodos legados.
4. `CycleTestUI`: nova aba sobre os endpoints.
5. Testes EditMode novos (ver §6) + suíte existente (`VS3SaveTests`, `VS8NightEntityTests`) verde.
6. Checklist Play Mode na `night.unity` + revisão de console (sem warnings novos).
7. *(Opcional)* Errata nos documentos de arquitetura: `ThreatAudioState` deixa de ser "estado audível" e vira etiqueta legada; menção ao emissor único `entity:{id}`.

Cada passo compila isoladamente; o passo 2 força a maioria dos erros de compilação que guiam os passos 3–4.

---

## 6. Testes

### 6.1 EditMode (`Assets/_Project/Editor`, novo arquivo `VS9AudioEntityUnificationTests.cs`)

- `ApplyEntityStateAudio` com estado apresentável registra entrada com `entityState` correto e **sem** `debugSignalId`.
- `ApplyEntityStateAudio` com `Inactive`/`Resolved` remove a entrada.
- `DebugPlayEntitySignal` sobre entidade em produção mantém **exatamente uma** voz persistente para `entity:{id}` e marca `debugSignalId`.
- `DebugClearEntityAudio` remove o override e limpa a marca `[DEBUG]`.
- `GetDebugSnapshot().entities` reflete produção sem nenhuma chamada de debug (regressão do problema original).
- Estrutural (reflection): `AudioEntityRuntimeState` não possui campos `isActive`/`currentSignalId`; `SceneAudioController` não possui método `SetThreatState`/`PlayThreatSignal`.

Nota: os métodos tocam `EnsureInitialized`/corrotinas; seguir o padrão dos testes VS3/VS8 (ambiente de cena mínimo via `new GameObject` + componentes, sem Play Mode) e isolar com `Object.DestroyImmediate` no teardown.

### 6.2 Play Mode / manual (`night.unity`)

- F8 → Áudio: reproduzir sinal do Voyeur enquanto o Voyeur real está em `Critical` com loop: **um** som só (override), marcado `[DEBUG]`; parar o sinal → loop de produção retorna.
- Deixar a Noite rodar: a lista de entidades com áudio do F8 acompanha as transições reais (`Near`/`Critical`/`Resolving`) sem nenhum botão pressionado.
- 6 AM e game over: nenhuma voz `entity:` ou `threat:` permanece tocando (logout limpo).
- Dois Terminals no mesmo tick (forçado via F8 de entidades): áudio não duplica.
- Console sem warnings novos.

---

## 7. Critérios de aceite

- [ ] Uma entidade lógica nunca possui mais de uma voz persistente, em qualquer combinação produção × F8.
- [ ] `snapshot.entities` reflete o áudio de produção em tempo real.
- [ ] `isActive`, `currentSignalId`, `threat:` e `ThreatAudioState` eliminados do modelo/emi-ssores; enum legado removido ou explicitamente reduzido a rótulo.
- [ ] `AudioDebugCatalog` não ganha uso fora do F8; `EntityDirector` não é modificado.
- [ ] Debug não altera IA, save, inventário ou ciclo.
- [ ] Testes EditMode novos e existentes verdes; checklist manual concluído.
- [ ] Nenhum asset/cena alterado (diff zerado fora dos três scripts e do teste novo).

---

## 8. Riscos e mitigações

| Risco | Mitigação |
|---|---|
| F8 perde a simulação de estados enquanto os clips das `EntityDefinition` estiverem vazios | O catálogo continua disponível e agora toca na voz unificada; a simulação de sinais não depende de autoria de entidade. |
| `EntityState` (7 valores) vs. `ThreatAudioState` (4 valores) na UI do F8 | Mapeamento explícito em helper único; rótulo da aba documenta que é simulação de apresentação. |
| Snapshot de áudio aloca por repaint do IMGUI | Fora de escopo estrutural; se o profiling incomodar, buffer reutilizável no mesmo padrão de `EntityDirector.GetDebugSnapshot` (opcional, noutro PR). |
| Alguém depender externamente dos métodos removidos | Grep obrigatório no passo 1; repositório é fechado e os únicos consumidores migrados no mesmo PR. |
| Corrotina de transição iniciada durante teardown | Guarda `isActiveAndEnabled` já existente mantida; teste de `ClearEntityAudioImmediate` coberto. |

---

## 9. Fora de escopo

- Autoria de clips das `EntityDefinition` (pendência de mídia do VS5/VB6, segue por warnings).
- Migração/renomeação de `AudioAnchorDefinition.encounterViewNodeId` ou perspectivas.
- Otimização de alocação dos snapshots do F8.
- Novas entidades, novos estados ou mudanças de tuning.
- Documentação de arquitetura (errata sugerida de forma opcional no passo 7).

---

## 10. Estimativa

Esforço **S** (≈ 1 dia): três arquivos de produção, um arquivo de teste, sem serialização, sem cenas, sem assets. Risco baixo: toda a informação que a produção já fornece (estado + anchor + apresentação) existe nas chamadas atuais; a mudança é de registro e roteamento de voz, não de regra de áudio.
