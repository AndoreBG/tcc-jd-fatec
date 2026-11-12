# Whispers Of Unknown — Plano Técnico: Ciclo Dia↔Noite, Eventos, UI e Correções

> **Versão:** 1.0
> **Data:** 2026-11-11
> **Engine:** Unity `6000.3.18f1` · URP `17.3.0` · PC · Input Manager legado (`StandaloneInputModule`) · uGUI + TextMesh Pro
> **Branch:** `main` (commit base `70f53c3 — feat exhauster`)
> **Status:** plano aprovado para implementação por AI agents. Este documento NÃO representa código implementado.
> **Documentos que este plano altera:** `Whispers_Of_Unknown_Arquitetura_Navegacao_Audio_v2.md` (§4.7, §4.8, §16.2), `Whispers_Of_Unknown_Ciclo_Dia1_Noites.md` (inteiro), `README.md` (descrição do ciclo e das entidades).

---

## 0. Regras de execução (obrigatórias)

### 0.1 Regras do usuário (texto literal)

> ATENÇÃO: Qualquer dúvida, pergunte à mim. Utilize o protocolo ASD-100 para comunicação comigo. Implemente um código simples e efetivo, nada de overengineering.

### 0.2 Protocolo de comunicação (ASD-STE100)

O protocolo ASD-100 significa **ASD-STE100 — Simplified Technical English**. Use as regras abaixo em todo texto deste projeto.

| Regra | Aplicação |
|---|---|
| Frase curta | Máximo de 20 palavras em instrução. Máximo de 25 palavras em descrição. |
| Voz ativa | Escreva "O `EntityDirector` aplica o estado". Não escreva "O estado é aplicado". |
| Uma instrução por frase | Nunca una dois comandos com "e". |
| Verbo no imperativo | Comece cada passo com um verbo: "Crie", "Altere", "Valide". |
| Um termo por conceito | Use sempre o mesmo termo. Ver glossário em §0.3. |
| Sem sinônimo decorativo | Não use "fase" para "período". Não use "tela" para "ViewNode". |
| Sem ambiguidade de pronome | Repita o nome do objeto. Não use "ele" ou "isso" para sistemas. |
| Sem abreviação nova | Use o nome completo na primeira ocorrência. |
| Dúvida | Pare. Pergunte ao usuário. Registre a pergunta no formato de §18. |

### 0.3 Glossário (termos congelados)

| Termo | Significado |
|---|---|
| **Período** | Dia ou Noite. |
| **Ciclo** | Um Dia e a Noite correspondente. |
| **ViewNode** | Ponto de visão fixo. Nome no código: `ViewNodeController`. |
| **Hotspot** | Região interativa. |
| **Entidade ativa** | Entidade com `active = true` no `NightEntityProfile` da Noite. |
| **Mochila** | Inventário do Dia. Nome no código: `BackpackController`. |
| **Evento de rádio** | Cena `radio_event` com a mensagem narrativa. |
| **Cutscene do mundo** | Cena `world_cutscene` exibida antes do Dia 1. |

### 0.4 Regras de engenharia

1. Não crie um segundo singleton. O único singleton é `GameSessionManager`.
2. Não guarde estado de gameplay em `ScriptableObject`.
3. Não guarde referência de cena no `GameSessionManager`. Passe valores simples.
4. Use o sistema de condições existente. Não crie linguagem de script nova.
5. Use os endpoints de debug que já existem no `EntityDirector`. Não duplique regra de entidade.
6. Não crie placeholder de arte, áudio ou sprite. Campo vazio gera warning.
7. Não use `Time.timeScale`. Use tempo escalado só em gameplay. Use tempo não escalado em UI, transição e evento.
8. Não adicione dependência de pacote novo. `AudioRandomContainer` pertence ao módulo `com.unity.modules.audio`.
9. Um arquivo por classe. Mantenha o namespace `Whispers`.
10. Todo `MonoBehaviour` novo precisa de validação de referência no boot, com mensagem clara.

### 0.5 Limites do ambiente do agente

O agente não tem Unity nem .NET no sandbox. Portanto:

1. O agente entrega código C# compilável por inspeção, sem executar o Editor.
2. O agente NÃO edita arquivos `.unity`, `.prefab` ou `.asset` à mão.
3. Para autoria de cena, o agente cria **scripts de setup de Editor** com `[MenuItem]`. O padrão de referência é `Assets/_Project/Editor/ExhausterSceneSetup.cs`.
4. Todo script de setup é idempotente. Rodar duas vezes não duplica objeto nem referência.
5. Todo script de setup agrupa as alterações em um passo de `Undo`.
6. Todo script de setup termina com um relatório de pendências de autoria manual.

---

## 1. Sumário das mudanças

| ID | Mudança | Tipo | Fase |
|---|---|---|---|
| **M1** | Correção do boot: campo `active` por entidade + Exhauster nulo = desligado | Correção | 1 |
| **M2** | Ciclo volta a 1 Dia → 1 Noite, com N pares lidos do `DEF_Night` | Refatoração | 2 |
| **M3** | Cheats de debug MIKA e atalhos de salto de Dia/Noite | Ferramenta | 3 |
| **M4** | Mochila com Lanterna, Lampião e Lata de Óleo; lampião posicionado no sótão | Conteúdo + sistema | 4 |
| **M5** | Tooltip "Ir para <GameplayName>" nos hotspots de navegação | UI | 5 |
| **M6** | `ClockHotspot` substitui o HUD de tempo | UI | 5 |
| **M7** | Cena `radio_event`: typewriter, 3 imagens, jingle, anchor de áudio | Sistema novo | 6 |
| **M8** | Cena `world_cutscene` entre o menu e o Dia 1 | Sistema novo | 6 |
| **M9** | Progressão narrativa dos Dias: objetivos, travas, tutoriais | Sistema novo | 7 |
| **M10** | Fonte `SpecialElite-Regular SDF` nos elementos novos | UI | 8 |

---

## 2. M1 — Correção do boot bloqueado por Exhauster

### 2.1 Causa raiz

O erro vem de `NightEntityProfile.cs:354`, chamado por `GameplaySceneController.ValidateNightEntities`.

```text
GameplaySceneDefinition.ResolveNightEntityProfile(night)      // NIGHT_Entities_1
→ NightEntityProfile.CollectValidation(...)
→ ExhausterNightEntry.CollectValidation(...)                  // definition == null
→ errors.Add("Exhauster sem ExhausterDefinition no NightEntityProfile.")
→ GameplaySceneController.Start bloqueia o boot
```

Os assets `NIGHT_Entities_1.asset` e `NIGHT_Entities_2.asset` têm bloco `exhauster` preenchido com `definition: {fileID: 0}`. O bloco existe, mas a definição é nula. A validação trata isso como erro.

### 2.2 Regras novas

1. `definition == null` significa **Exhauster desligado**. Não é erro.
2. Cada entrada do perfil tem o campo `active`. O valor padrão é `true`.
3. `active == false` significa entidade **ausente da Noite**. A entidade fica fora de validação, IA, áudio, anchors, apresentação e painel F8.
4. Uma Noite precisa de pelo menos uma ameaça ativa. A ameaça pode ser Predator, Voyeur ou Exhauster.
5. O Predator e o Voyeur não são mais obrigatórios em todas as Noites.

### 2.3 Alterações de código

**`Scripts/Entities/NightEntityProfile.cs` — classe `EntityNightEntry`**

```csharp
[Tooltip("Entidade participa desta Noite. Se falso, a entidade fica fora de validação, IA, áudio e apresentação.")]
public bool active = true;
```

**`Scripts/Entities/NightEntityProfile.cs` — classe `ExhausterNightEntry`**

```csharp
[Tooltip("Exhauster participa desta Noite. Se falso, o lampião não é gameplay.")]
public bool active = true;

public bool IsConfigured => active && definition != null;
```

**`ExhausterNightEntry.CollectValidation` — nova primeira linha**

```csharp
if (definition == null || !active) return;   // desligado: nada a validar
```

**`NightEntityProfile.CollectValidation` — contagem por entidade ativa**

```csharp
int activeThreats = 0;

foreach (EntityNightEntry entry in entries)
{
    if (entry == null) { errors.Add("NightEntityProfile contém uma entrada nula."); continue; }

    // Entrada inativa é ausente desta Noite: não valida nada e não conta.
    if (!entry.active) continue;

    // Daqui em diante, o código atual continua igual:
    //   define se é Predator ou Voyeur, valida anchors, curva, timers e mídia,
    //   e incrementa predatorCount, voyeurCount e activeThreats.
}

if (predatorCount > 1) errors.Add("NightEntityProfile aceita no máximo uma PredatorDefinition ativa.");
if (voyeurCount > 1)   errors.Add("NightEntityProfile aceita no máximo uma VoyeurDefinition ativa.");
if (activeThreats == 0) errors.Add("NightEntityProfile não possui nenhuma ameaça ativa nesta Noite.");
if (exhauster != null && exhauster.IsConfigured) activeThreats++;
```

Regras da nova validação:

| Regra | Mensagem de erro |
|---|---|
| Máximo de 1 `PredatorDefinition` ativa | `NightEntityProfile aceita no máximo uma PredatorDefinition ativa.` |
| Máximo de 1 `VoyeurDefinition` ativa | `NightEntityProfile aceita no máximo uma VoyeurDefinition ativa.` |
| Mínimo de 1 ameaça ativa | `NightEntityProfile não possui nenhuma ameaça ativa nesta Noite.` |
| Entrada inativa não valida anchors, curva, timers nem mídia | nenhuma |
| Exhauster com `definition == null` não valida nada | nenhuma |

**`Scripts/Entities/EntityDirector.cs`**

1. Em `Initialize`, ignore entrada com `active == false`. Não crie `EntityRuntime`. Não inclua o id em `_orderedEntityIds`.
2. Em `TryEstablishExhauster`, trate `!entry.IsConfigured` como "sem Exhauster". Use `_exhausterEnabled = false`.
3. Em `GetEntityIdsForDebug` e `GetDebugSnapshot`, liste apenas entidades ativas.
4. Adicione o membro público `public bool IsEntityActive(string entityId)` para os cheats de §4.

**`Scripts/Entities/EntityPresentationCoordinator.cs`**

Ignore binding cuja entidade está inativa no perfil. Faça o filtro no boot, na varredura de bindings.

**`Scripts/Audio/SceneAudioController.cs`**

Não altere. O áudio recebe pedido só de entidade ativa. O diretor não emite pedido de entidade inativa.

**`Scripts/Scene/GameplaySceneController.cs`**

1. Em `ValidateNightEntities`, chame `Session.RegisterNightCount(definition.NightCount)`. Use `definition.NightCount` do §3.
2. Mantenha o bloqueio de boot para erro real. Só erro real bloqueia.

### 2.4 Autoria manual obrigatória (depois do código)

| Asset | Alteração |
|---|---|
| `NIGHT_Entities_1.asset` (Noite 1) | `Predator` com `active = false`. Exhauster já fica desligado por `definition` nula. |
| `NIGHT_Entities_2.asset` (Noite 2) | Nada. Predator e Voyeur ativos. Exhauster desligado. |
| `NIGHT_Entities_3.asset` (Noite 3) | Exhauster com `definition` atribuída e `active = true`. |
| `NIGHT_Entities_4.asset` (Noite 4) | Igual à Noite 3, com IA mais alta. |
| Todos | `profileId` único por perfil. Hoje os quatro assets usam `night_entities_1`. |

Crie o script `Editor/NightProfileSetup.cs` com o menu `Whispers/Perfis/Aplicar Active e profileId`. O script corrige o `profileId` e desliga o Predator na Noite 1.

### 2.5 Critérios de aceite

- [ ] Carregar a cena `night` não mostra `Boot bloqueado por configurações obrigatórias inválidas`.
- [ ] A Noite 1 roda só com o Voyeur. O F8 não lista o Predator.
- [ ] A Noite 3 e a Noite 4 rodam com Voyeur, Predator e Exhauster.
- [ ] `NIGHT_Entities_x` com Exhauster sem definição não gera erro nem warning de mídia do Exhauster.
- [ ] Uma Noite com todas as entradas inativas gera erro único e claro.

---

## 3. M2 — Ciclo 1 Dia → 1 Noite por par

### 3.1 Regra nova

```text
Novo jogo → cutscene do mundo → Dia 1 → Noite 1 → Dia 2 → Noite 2 → ... → Dia N → Noite N → fim
```

`N` é a **quantidade de Noites configurada no `DEF_Night`**. O `DEF_Night` atual tem 4 perfis em `nightEntityProfilesByNight`. Portanto o ciclo tem **4 Dias e 4 Noites**.

Regras:

1. Vencer a Noite N carrega o Dia N+1. Não existe Dia intermediário nem Noite dupla.
2. Vencer a Noite N (última) mostra a tela de conclusão. Não carrega cena.
3. Perder na Noite N carrega o início da **Noite N**.
4. Cada período grava checkpoint no seu início: Dia N → `(day = N, night = 0)`; Noite N → `(day = N, night = N)`.
5. Fechar o jogo restaura o início do período gravado.

### 3.2 Fonte da quantidade de Noites

Duas fontes, uma autoridade.

| Elemento | Regra |
|---|---|
| `GameplaySceneDefinition.NightCount` | `nightEntityProfilesByNight` com comprimento > 0 → esse comprimento. Senão → 1. |
| `GameSessionManager.RegisterNightCount(int)` | Guarda o valor recebido do `DEF_Night`. Ignora valor < 1. |
| `GameSessionManager.NightCount` | Devolve o valor registrado. Se não houve registro, devolve `_cycle.EffectiveNightCount`. |
| `GameCycleDefinition.nightCount` | Fallback de autoria. Ajuste `CYCLE_Gameplay.asset` para 4. |

Chamada: no boot da cena de Noite, `GameplaySceneController.ValidateNightEntities` chama `Session.RegisterNightCount(definition.NightCount)`.

### 3.3 `GameSessionManager.TryEndPeriod` (novo fluxo)

```text
period == Day:
    targetNight = day
    checkpoint = working state com (day, night = targetNight)
    grava checkpoint
    period = Night; night = targetNight
    carrega _cycle.nightScene com legenda "Noite {targetNight}"

period == Night:
    if night < NightCount:
        targetDay = night + 1
        checkpoint = working state com (day = targetDay, night = 0)
        grava checkpoint
        day = targetDay; period = Day; night = 0
        carrega _cycle.dayScene com legenda "Dia {targetDay}"
    else:
        IsCycleComplete = true
        notifica SessionStateChanged
        não carrega cena
```

Regras:

1. Grave o checkpoint **antes** de carregar a cena.
2. Falha de gravação cancela a troca e mantém o período atual vivo.
3. Não consolide o trabalho noturno no checkpoint do Dia anterior.
4. O estado de trabalho é a fonte do Dia N+1. Exemplo: `item_lanterna` e `LampPlacedInAttic` continuam válidos.

### 3.4 `GameSaveData` e compatibilidade

O formato v2 já tem `day` e `night`. **Não faça bump de versão.**

Adicione uma regra de normalização no `Apply`:

```csharp
// Novo ciclo: a Noite N pertence ao Dia N.
if (night > day) day = night;
if (night < 0) night = 0;
```

Regra: save antigo com `day = 1, night = 2` vira `day = 2, night = 2`. O jogador continua no início de uma Noite.

### 3.5 Tela de carregamento

`LevelManager` já aceita `loadingPeriod` e `loadingNumber`. Faça estas adaptações:

| Carregamento | `loadingPeriod` | `loadingNumber` | Legenda |
|---|---|---|---|
| Dia N | `GamePeriod.Day` | `N` | `Dia N` |
| Noite N | `GamePeriod.Night` | `N` | `Noite N` |
| Cutscene do mundo | `null` | `0` | sem legenda |
| Evento de rádio | `null` | `0` | sem legenda |
| Menu principal | `null` | `0` | sem legenda |

Regras:

1. `GameSessionManager.StartLoading` deve repassar período e número em todos os caminhos.
2. Exponha `GameSessionManager.StartPreparedPeriod()` para a cutscene (§9.3).
3. `LevelManager` não muda. Nenhum campo novo é necessário.
4. O áudio de carregamento continua por período. Não crie áudio novo para cutscene e rádio.

### 3.6 `BetaEndScreen`

1. A tela mostra só depois da última Noite. A condição `IsCycleComplete` já cobre isso.
2. Ajuste o texto e a legenda do botão. Não altere o fluxo sem save.
3. O botão continua chamando `TryReturnToMainMenu()`.

### 3.7 Assets e cenas

| Asset | Alteração |
|---|---|
| `CYCLE_Gameplay.asset` | `nightCount = 4`. |
| `DEF_Night.asset` | Já tem 4 perfis. Nenhuma alteração. |
| `DEF_Day.asset` | Nenhuma alteração. |
| `CYCLE_Playground.asset` | `nightCount` do playground. Deixe em 1. |

### 3.8 Critérios de aceite

- [ ] Novo jogo leva ao Dia 1 e mostra `Dia 1` na tela de carregamento.
- [ ] Vencer a Noite 1 leva ao Dia 2 com `Dia 2` na legenda.
- [ ] Vencer a Noite 4 mostra a conclusão, sem carregar cena.
- [ ] Perder na Noite 3 volta ao início da Noite 3.
- [ ] O checkpoint do Dia 2 abre com o inventário do fim da Noite 1.
- [ ] Mudar o `DEF_Night` para 3 perfis gera 3 Dias e 3 Noites, sem alterar código.

---

## 4. M3 — Cheats de debug MIKA

### 4.1 Sequência de liberação

1. O jogador pressiona **Shift + M**, depois **Shift + I**, depois **Shift + K**, depois **Shift + A**.
2. Cada tecla precisa de Shift pressionado.
3. A ordem é obrigatória.
4. A sequência reinicia se o jogador errar uma tecla.
5. A sequência reinicia se passarem 4 segundos sem a próxima tecla.
6. Ao completar, o componente grava `GameSessionManager.DebugCheatsUnlocked = true`.
7. O valor é transitório. Não entra no save. Reinicia no fechamento do jogo.

### 4.2 Atalhos

| Tecla | Ação | Condição |
|---|---|---|
| `Ctrl + P` | Força ataque do Predator | Período Noite e Predator ativo na Noite |
| `Ctrl + V` | Força ataque do Voyeur | Período Noite e Voyeur ativo na Noite |
| `Ctrl + E` | Força fim do óleo do lampião | Período Noite e Exhauster ativo na Noite |
| `Shift + F1..F4` | Inicia a Noite X | `X <= NightCount` |
| `F1..F4` | Inicia o Dia X | `X <= NightCount` |

Regras:

1. Todo atalho exige cheats liberados, gameplay pronto (`GameplaySceneController.IsReady`) e nenhum modal aberto.
2. Os atalhos ignoram `InputBlocker`. Use `Input.GetKeyDown` direto.
3. Os atalhos nunca gravam checkpoint.
4. Os atalhos exibem uma linha no Console com o resultado.
5. Se a condição falhar, o atalho não faz nada e escreve um warning.

### 4.3 Implementação

**Novo arquivo `Scripts/Debug/DebugCheatController.cs`**

```csharp
public sealed class DebugCheatController : MonoBehaviour
{
    public void Initialize(GameplaySceneController scene);   // chamado pelo GameplaySceneController
    // Update(): sequência MIKA + despacho dos atalhos
}
```

**`Scripts/Scene/GameplaySceneController.cs`**

1. Em `Awake`, crie o componente:

```csharp
if (!TryGetComponent(out DebugCheatController cheats))
    cheats = gameObject.AddComponent<DebugCheatController>();
cheats.Initialize(this);
```

2. Não adicione o componente em cenas que não são de gameplay.

**`Scripts/Session/GameSessionManager.cs`**

```csharp
public bool DebugCheatsUnlocked { get; private set; }
public void UnlockDebugCheats() { DebugCheatsUnlocked = true; }

/// <summary>Salto de debug: troca de período sem gravar checkpoint.</summary>
public bool TryDebugJump(int day, GamePeriod targetPeriod, out string error);
```

`TryDebugJump` faz:

1. Valida `day >= 1` e `day <= NightCount`.
2. Valida `targetPeriod`. Se `Night`, usa `night = day`. Se `Day`, usa `night = 0`.
3. Ajusta `day`, `night` e `period`.
4. Zera `IsCycleComplete`.
5. Chama `StartLoading` com a cena e a legenda corretas.
6. Não chama `WriteCheckpoint`.

**Chamadas dos ataques**

| Atalho | Chamada |
|---|---|
| `Ctrl + P` | `director.DebugForceTerminal(predatorId, true, out error)` |
| `Ctrl + V` | `director.DebugForceTerminal(voyeurId, true, out error)` |
| `Ctrl + E` | `director.DebugExtinguishLamp(out error)` |

Use `EntityDirector.GetEntityIdsForDebug` para achar o id. Use `TryGetDefinition` para testar o tipo. Não escreva `"predator"` fixo no código.

### 4.4 Critérios de aceite

- [ ] A sequência MIKA libera os atalhos. Uma tecla errada reinicia a sequência.
- [ ] `Ctrl + P` no Dia não faz nada e escreve um warning.
- [ ] `Ctrl + P` na Noite 1 (Predator inativo) não faz nada e escreve um warning.
- [ ] `Ctrl + P` na Noite 2 inicia o jumpscare do Predator.
- [ ] `Shift + F3` abre a Noite 3 com `Noite 3` na legenda e não grava checkpoint.
- [ ] `F2` abre o Dia 2 com `Dia 2` na legenda.

---

## 5. M4 — Mochila com três itens e lampião posicionado no sótão

### 5.1 Itens oficiais

| Item | `id` | Asset | `isTool` | Observação |
|---|---|---|---|---|
| Lanterna | `item_lanterna` | `SO/Itens/ITEM_Lanterna.asset` | `false` | Já existe. Marca o fim do Dia 1. |
| Lampião | `item_lampiao` | `SO/Itens/ITEM_Lampiao.asset` | `true` | Novo. Arrastado no sótão. |
| Lata de Óleo | `item_oleo` | `SO/Itens/ITEM_Oleo.asset` | `false` | Já existe. Uso por condição, não por arraste. |

Slots da mochila, na ordem visual:

| Slot | Asset | Item aceito | `maxQuantity` |
|---|---|---|---|
| 1 | `SO/Backpack/BPSlot_Lanterna.asset` | `item_lanterna` | 1 |
| 2 | `SO/Backpack/BPSlot_Lampiao.asset` | `item_lampiao` | 1 |
| 3 | `SO/Backpack/BPSlot_Oleo.asset` | `item_oleo` | 1 |

Regras:

1. A mochila continua existindo só no Dia.
2. A Lanterna na mochila é marca de progresso. Na Noite ela está sempre na mão. O `LanternController` não lê inventário.
3. O Lampião é ferramenta de arraste. Ele só é usado no sótão.
4. A Lata de Óleo não é ferramenta. O jogador usa a lata direto no hotspot do lampião, sem abrir a mochila.

### 5.2 Legado removido

| Remover | Ação |
|---|---|
| `BPSlot_Madeira`, `BPSlot_Prego`, `BPSlot_Martelo`, `BPSlot_PeDeCabra` | Apague os assets e os `.meta`. |
| `ITEM_Madeira`, `ITEM_Prego`, `ITEM_Martelo`, `ITEM_PeDeCabra` | Remova da lista de `DB_Itens.asset`. Apague os assets. |
| Hotspots `Hotspot_PegarMadeira*`, `Hotspot_PegarPrego*` e equivalentes | Desative na cena `day`. |
| Condições `COLLECTED_*` e `HASITEM_TemMadeira`, `HASITEM_TemPeDeCabra`, `HASITEM_PossuiOleo` | Mantenha só as usadas pelo novo fluxo. Apague as demais. |
| Interações de madeira, prego, martelo e pé de cabra | Apague. |

O script `Editor/BackpackItemsSetup.cs`, menu `Whispers/Mochila/Aplicar três itens`, faz isto:

1. Cria os três `BackpackSlotDefinition`.
2. Cria `ITEM_Lampiao` e registra em `DB_Itens`.
3. No modal da mochila, atribui os três slots.
4. Desativa o quarto slot.
5. Desativa os hotspots do legado na cena `day`.
6. Lista no Console os assets do legado que faltam apagar à mão.

### 5.3 Fluxo do Lampião no Dia

> **Divergência resolvida (2026-11-11):** a descrição original do ciclo dizia "o jogador encontra o lampião no sótão".
> A resposta do usuário corrigiu o fluxo: o lampião é encontrado no **quarto** e **posicionado** no sótão.
> Este plano segue a correção.

```text
Dia 3
  → Hotspot_PegarLampiao (ViewNode vn_quarto)    : adiciona item_lampiao
  → Hotspot_PegarOleo    (ViewNode vn_cozinha)   : adiciona item_oleo
  → Hotspot_ToolLampiao_Sotao (ViewNode vn_sotao, ToolHotspot)
        drop de item_lampiao
        sucesso:
          remove item_lampiao do inventário
          grava fato persistente LampPlacedInAttic
          abre o painel de tutorial do Exhauster (§10.5)
```

Regras:

1. O `ToolHotspot` do sótão aceita só `item_lampiao`. Use o campo `acceptedTools`.
2. O drop no lugar certo usa `successDefinition`. A definição tem dois `InteractionResult`:
   `type = RemoveItem`, `itemId = item_lampiao`, `amount = 1`;
   `type = SetPersistentFact`, `factId = LampPlacedInAttic`.
3. Mantenha `ItemDefinition.consumesOnUse = false`. A remoção vem do `InteractionResult`, não do item.
4. A falha devolve o item à mochila. Regra já existente do `BackpackController`.
4. O fato `LampPlacedInAttic` é usado só para narrativa e para a condição da apresentação. Não arme o Exhauster com o fato. A armação continua vindo do `NightEntityProfile` (§2.2).
5. O sótão está travado nos Dias 1 e 2 (§10.2). Logo, o jogador não pode colocar o lampião antes do Dia 3.
### 5.4 Óleo na Noite

1. O hotspot `Hotspot_ReabastecerLampiao` do sótão já usa `HASITEM_PossuiOleo` e `LAMP_LampiaoIntacto`.
2. O óleo **não é consumido**. Regra atual do `StairLampController`.
3. O jogador não abre a mochila na Noite. A posse do óleo basta.
4. Se o jogador não tem o óleo, o hotspot aparece bloqueado com pista.

### 5.5 Critérios de aceite

- [ ] A mochila mostra três slots. Nenhum slot aceita item do legado.
- [ ] Pegar a lanterna no quarto coloca o item no slot 1.
- [ ] O arraste do lampião para fora da mochila funciona. O drop no sótão consome o item e grava `LampPlacedInAttic`.
- [ ] O drop do lampião em outro `ToolHotspot` devolve o item e não revela a solução.
- [ ] Na Noite, o hotspot do lampião aceita o uso do óleo sem abrir a mochila.

---

## 6. M5 — Tooltip "Ir para <GameplayName>"

### 6.1 Dados novos

**`Scripts/Navigation/ViewNodeDefinition.cs`**

```csharp
[Tooltip("Nome de gameplay usado em textos de interface. Ex.: 'Sala', 'Porta da Cozinha'.")]
public string gameplayName;
```

Regra: se `gameplayName` estiver vazio, o tooltip usa `debugName`. Se `debugName` também estiver vazio, o tooltip não aparece e o boot gera warning.

**`Scripts/Hotspots/NavigationHotspot.cs`**

```csharp
public string DestinationGameplayName { get; }   // resolvido pelo NavigationManager
```

**`Scripts/Navigation/NavigationManager.cs`** — endpoint de leitura:

```csharp
/// <summary>Nome de gameplay do ViewNode de destino. Vazio se o id não existir na cena.</summary>
public string GetGameplayName(string viewNodeId);
```

Regra: procure em `GetViewNodes()`. Use `ViewNodeController.Definition.gameplayName`. Não acesse a definição de dentro do hotspot.

### 6.2 Componente de UI

**Novo arquivo `Scripts/UI/HotspotTooltipUI.cs`**

Campos:

```text
CanvasGroup group             // alpha e blocksRaycasts = false
RectTransform root
TMP_Text label                // enableWordWrapping = false, overflow = Overflow
float offsetPixels = 18
float edgePaddingPixels = 8
Canvas canvas                 // Canvas do overlay de gameplay
```

API:

```csharp
public void Show(string text);   // chamado pela NavigationHotspot no PointerEnter
public void Hide();              // chamado no PointerExit, no bloqueio e na navegação
```

### 6.3 Regra de posição

O tooltip usa o espaço do canvas do overlay.

```text
mouse  = posição do ponteiro no espaço do canvas
size   = tamanho medido do texto (preferredWidth, preferredHeight)
anchor = escolha por quadrante:
    mouse.y > metade da tela  → texto ABAIXO do mouse
    mouse.y < metade da tela  → texto ACIMA do mouse
    mouse.x < metade da tela  → texto à DIREITA do mouse
    mouse.x > metade da tela  → texto à ESQUERDA do mouse
```

Regras:

1. O tooltip nunca cruza a borda da tela. Aplique `Mathf.Clamp` com `edgePaddingPixels`.
2. O texto não quebra linha. Use `enableWordWrapping = false`.
3. O texto é sempre `"Ir para " + gameplayName`.
4. O tooltip não captura clique. Use `raycastTarget = false` e `blocksRaycasts = false`.
5. O tooltip não aparece durante `InputBlockReason.Modal`, `InputBlockReason.ToolDrag`, `InputBlockReason.Transition`, `InputBlockReason.GameOver` ou `Pause`.
6. O tooltip não aparece para hotspot indisponível. Use `HotspotBase.IsAvailable`.
7. O tooltip não aparece para hotspot com `unavailableMode = Hidden`.
8. O tooltip some quando a navegação começa.
9. O tooltip aparece sem atraso. Não use fade. O projeto não tem dwell de tooltip.

### 6.4 Integração

**`Scripts/Hotspots/HotspotBase.cs`**

Adicione ao `OnPointerEnter` e ao `OnPointerExit` dois ganchos vazios:

```csharp
protected virtual void OnHoverEnter() { }
protected virtual void OnHoverExit() { }
```

**`Scripts/Hotspots/NavigationHotspot.cs`**

```csharp
protected override void OnHoverEnter()
{
    string name = DestinationGameplayName;
    if (string.IsNullOrWhiteSpace(name)) return;
    Scene.Tooltip.Show("Ir para " + name);
}

protected override void OnHoverExit() => Scene.Tooltip.Hide();
```

**`Scripts/Scene/GameplaySceneController.cs`**

1. Adicione a propriedade `public HotspotTooltipUI Tooltip { get; private set; }`.
2. Valide a referência no boot. Gere warning se ausente.
3. Crie o objeto no script de setup `Editor/GameplayUiSetup.cs` (menu `Whispers/UI/Aplicar tooltip, relógio e objetivos`).

### 6.5 Critérios de aceite

- [ ] Hover em `Hotspot_ToSala` mostra `Ir para Sala`.
- [ ] O texto não quebra linha em nenhuma resolução testada.
- [ ] Mouse no topo → texto abaixo. Mouse na base → texto acima. Mouse à esquerda → texto à direita. Mouse à direita → texto à esquerda.
- [ ] O texto fica dentro da tela em 1280×720 e em 1920×1080.
- [ ] O texto não aparece com a mochila aberta.
- [ ] O texto some ao clicar e navegar.

---

## 7. M6 — `ClockHotspot`

> **Divergência resolvida (2026-11-11):** o texto original dizia que o hotspot mostrava a hora "no lugar do cursor" com os sprites do relógio.
> A resposta do usuário corrigiu o fluxo: **mostrar a hora é um texto** ("01PM", "12AM"…), e **os sprites são a arte do relógio**, que se transforma a cada hora.
> Este plano segue a correção. O texto usa o `HotspotTooltipUI`; o cursor do sistema não é escondido; no Dia o relógio fica parado em `01PM`.

### 7.1 Regra

1. O contador de tempo sai da tela. Desative o objeto `RelogioUI` nas cenas `day` e `night`.
2. O `NightClockHUD` continua no código. Não apague a classe.
3. Um hotspot novo, `ClockHotspot`, mostra a hora no lugar do cursor.
4. No Dia, a hora é sempre `01PM`. O hotspot usa um sprite próprio.
5. Na Noite, o hotspot usa 7 sprites, um para cada hora: `12AM, 01AM, 02AM, 03AM, 04AM, 05AM, 06AM`.
6. A hora vem de `EntityDirector.DisplayHourIndex`. A faixa é 0 a 6.

### 7.2 Componente

**Novo arquivo `Scripts/Hotspots/ClockHotspot.cs`** — herda de `HotspotBase`.

Campos:

```text
Sprite daySprite                       // 01PM
Sprite[] nightHourSprites = new Sprite[7]   // índice 0 = 12AM ... índice 6 = 06AM
Sprite unavailableSprite               // opcional; hora indisponível
bool hideCursor = true
```

Objetos na cena, criados por `Editor/GameplayUiSetup.cs`:

| Cena | Objeto | Sprites |
|---|---|---|
| `day` | `ClockHotspot` | `daySprite` só (01PM) |
| `night` | `ClockHotspot` | `nightHourSprites` com 7 posições |
| `day` e `night` | `ClockCursorPresenter` no Canvas overlay | nenhum |

Regras:

1. `activationMode = HoverImmediate`. `repeatPolicy = Repeatable`.
2. `OnActivated()` devolve `true` e não executa ação. O hotspot só apresenta.
3. Enquanto `IsCursorOver` e `IsAvailable`: peça o cursor oculto ao `GameplayCursorController` e mostre o sprite no ponteiro.
4. Ao sair, ao bloquear, ao navegar ou no GameOver: libere o cursor e esconda o sprite.
5. `DisplayHourIndex` é lido por frame. É leitura de propriedade, sem alocação.
6. O sprite do relógio fica em Canvas overlay, acima dos hotspots e abaixo dos modais.

**`Scripts/Feedback/GameplayCursorController.cs`**

Adicione dois métodos. Não altere os existentes:

```csharp
public void RequestHiddenCursor(object owner);
public void ReleaseHiddenCursor(object owner);
```

**Novo arquivo `Scripts/UI/ClockCursorPresenter.cs`**

Campos: `Image clockImage`, `RectTransform root`, `CanvasGroup group`. Métodos: `Show(Sprite sprite)`, `Hide()`.

### 7.3 Sprites (autoria manual)

| Arquivo sugerido | Conteúdo |
|---|---|
| `Art/HUD/Clock/Day_01PM.png` | Relógio do Dia |
| `Art/HUD/Clock/Night_12AM.png` | Relógio de 12AM |
| `Art/HUD/Clock/Night_01AM.png` … `Night_06AM.png` | Um por hora |

Regra: não crie placeholder. Sem sprite, o `ClockCursorPresenter` usa `unavailableSprite` e o boot gera warning.

### 7.4 Critérios de aceite

- [ ] O HUD de tempo não aparece mais em `day` nem em `night`.
- [ ] Hover no `ClockHotspot` no Dia mostra `Day_01PM`.
- [ ] Hover no `ClockHotspot` na Noite mostra o sprite da hora atual.
- [ ] A hora troca de sprite no minuto correto do relógio da Noite.
- [ ] O cursor volta ao normal ao sair do hotspot.
- [ ] O sprite não aparece com modal aberto nem com GameOver.

---

## 8. M7 — Evento de rádio

### 8.1 Estrutura

Cena nova: `Assets/_Project/Scenes/radio_event.unity`. Adicione ao Build Profile.

```text
RadioEvent (raiz)
├── Canvas_Radio (Canvas Screen Space Overlay, CanvasScaler 1920x1080)
│   ├── Frame_01 (Image)
│   ├── Frame_02 (Image)
│   ├── Frame_03 (Image)
│   ├── DialogueFade (Image preto, âncora inferior, altura = 28% da tela)
│   ├── DialogueText (TMP_Text, fonte SpecialElite)
│   └── AdvanceIndicator (Image, canto inferior direito)
└── RadioEventController (MonoBehaviour)
    ├── AudioSource_Jingle
    ├── AudioSource_Typewriter   (AudioRandomContainer)
    └── fader (Image preto fullscreen, CanvasGroup)
```

Regras:

1. A cena não tem ViewNode. Ela tem uma raiz visual `RadioViewRoot`, inativa no início.
2. A cena não tem `GameplaySceneController`, nem `NavigationManager`, nem hotspots.
3. A cena não tem HUD de gameplay.
4. A cena usa tempo não escalado em todo o fluxo.
5. Nenhum objeto da cena grava save.

### 8.2 Sequência obrigatória

```text
1. Fade-in preto → tela coberta e preta.
2. Toca o jingle. Duração de espera: 5 s em tempo não escalado.
3. Ativa RadioViewRoot e o Frame_01.
4. Para cada linha de diálogo:
     a. Aplica a imagem do frame da linha, se mudou.
     b. Escreve o texto com efeito typewriter.
     c. Toca um one-shot por caractere não vazio.
     d. Ao terminar, liga o indicador com fade in e fade out em laço de opacidade.
     e. LMB, E ou ESPAÇO:
          - texto ainda escrevendo → completa o texto agora.
          - texto completo → próxima linha.
5. Fim da última linha → fade-out preto.
6. Aplica o modo de retorno (§8.6).
```

Regras:

1. O jogador pode pular o typewriter. Nunca pode pular o evento inteiro.
2. O indicador só aparece quando a linha está completa.
3. O indicador faz fade in até 1 e fade out até 0, em laço. Use tempo não escalado.
4. Nenhuma tecla avança duas linhas. Use uma trava de um avanço por frame.

### 8.3 Dados

**Novo arquivo `Scripts/Radio/RadioEventDefinition.cs`** — `ScriptableObject`.

```csharp
public enum RadioReturnMode { ReturnToDay, AdvanceToNight }

public sealed class RadioEventDefinition : ScriptableObject
{
    public string eventId;
    public RadioLine[] lines;                  // frameIndex (0..2), text, optionalSfx
    public AudioClip jingle;                   // opcional
    public AudioClip[] typewriterClips;        // randomizer
    public AudioRandomContainer typewriterContainer;  // alternativa preferida
    public AudioMixerGroup typewriterMixerGroup;      // grupo Interactions
    public float charactersPerSecond = 40f;
    public float jingleDelaySeconds = 5f;
    public float indicatorFadeSeconds = 0.6f;
    public RadioReturnMode returnMode = RadioReturnMode.ReturnToDay;
}
```

Regras:

1. Se `lines` estiver vazia, a cena não entra. Gere warning e volte para o Dia.
2. Texto é autoria do usuário. O agente deixa as linhas vazias e o campo pronto.
3. `typewriterClips` e `typewriterContainer` são exclusivos. Se ambos estiverem vazios, o texto aparece sem som e o boot gera warning.
4. Use `AudioRandomContainer` quando estiver preenchido. Atribua em `AudioSource.resource` e chame `AudioSource.Play()`. `AudioRandomContainer` herda de `AudioResource` e pertence ao módulo `com.unity.modules.audio`.
5. Use `AudioSource.PlayOneShot` com escolha aleatória quando só houver `typewriterClips`.
6. Prepare o fallback antes de usar o container. O fallback não pode depender de compilação condicional.
7. O grupo de mixer do som de digitação é `Interactions`. O grupo do jingle é `Transition`.

### 8.4 Anchor de áudio do rádio

**Novo asset `SO/Audio/Anchors/ANCHOR_Radio_CorredorFront.asset`**

```text
id           = radio_corredorfront
displayName  = "Rádio do corredor front"
category     = Room
```

Regras:

1. Adicione a perspectiva do anchor em cada `ViewAudioProfile` da cena `day`. Valor inicial: `pan 0`, `distance Medium`, `occlusion 0.2`.
2. O `SceneAudioController` reproduz o rádio como equipamento persistente (`AudioSourceRole.Equipment`). Não use a fonte de ameaça.
3. O rádio continua audível fora do `__VN_CorredorFront`, com perspectiva autorada.
4. O anchor não toca nada sozinho. Um equipamento precisa ser registrado.
5. Fora de escopo: som de rádio ambiente contínuo. Só o evento usa o anchor nesta entrega.

### 8.5 Gatilho

**Novo arquivo `Scripts/Day/DayFlowController.cs`** (§10).

```text
NavigationManager.ViewNodeChanged
  → incrementa contador de navegações do Dia
  → se contador >= dayEntry.requiredNavigationsBeforeRadio
     e o evento do Dia ainda não tocou
        → GameSessionManager.RequestRadioEvent(returnViewNodeId, returnMode)
```

Regras:

1. O contador é reiniciado quando o Dia começa.
2. O contador não conta a navegação de retorno do evento.
3. A guarda "já tocou hoje" fica no `GameSessionManager` como conjunto transitório `_consumedDayEvents`. Ela é limpa em `Apply` de checkpoint e em troca de período.
4. O `returnViewNodeId` é o id do ViewNode apresentado no momento do gatilho.

### 8.6 Retorno

**`GameSessionManager`** ganha:

```csharp
public bool HasPendingReturnViewNode { get; }
public string PendingReturnViewNodeId { get; }
public bool TryConsumeReturnViewNode(out string viewNodeId);   // lê e limpa
```

Regras:

1. `ReturnToDay`: carrega `dayScene` com legenda `null`. O `GameplaySceneController` apresenta `PendingReturnViewNodeId` em vez do `initialViewNodeId`.
2. `AdvanceToNight`: chama `TryEndPeriod()`. Use este modo no Dia 4.
3. Se o id não existir na cena, use o `initialViewNodeId` e gere warning.
4. Nenhum dado do retorno entra no save.

### 8.7 Critérios de aceite

- [ ] O jingle toca antes do rádio aparecer. A espera é de 5 s.
- [ ] O typewriter escreve com som por caractere no grupo `Interactions`.
- [ ] LMB completa a linha em curso. LMB de novo avança.
- [ ] O indicador aparece só depois de completar a linha.
- [ ] As 3 imagens aparecem na ordem autorada.
- [ ] Ao terminar, o jogador volta ao ViewNode de origem.
- [ ] No Dia 4, o fim do evento carrega a Noite 4.
- [ ] O evento não toca duas vezes no mesmo Dia.

---

## 9. M8 — Cutscene do mundo

### 9.1 Estrutura

Cena nova: `Assets/_Project/Scenes/world_cutscene.unity`. Adicione ao Build Profile, antes de `day`.

```text
WorldCutscene (raiz)
├── Canvas_Cutscene
│   ├── Slide_01..Slide_N (Image)
│   ├── Fade (Image preto)
│   └── SkipLabel (TMP_Text, canto inferior direito, opcional)
└── WorldCutsceneController
```

### 9.2 Regras

1. A cutscene entra **depois** de "Jogar" e **antes** do carregamento do Dia 1.
2. A cutscene não aparece em "Continuar".
3. A cutscene tem avanço manual e automático. Cada slide tem duração autorada e aceita LMB para avançar.
4. `ESC` não sai da cutscene. Só o fim do último slide avança.
5. No fim, a cutscene chama `GameSessionManager.StartPreparedPeriod()`.
6. A cutscene usa tempo não escalado.
7. Se a cena não estiver no Build Profile, o `MainMenuController` carrega o Dia 1 direto e escreve um warning.

### 9.3 Código

**`Scripts/Session/GameCycleDefinition.cs`**

```csharp
[Tooltip("Cena de cutscene exibida antes do Dia 1. Vazio = vai direto ao Dia 1.")]
public string worldCutsceneScene;
```

**`Scripts/UI/MainMenuController.cs` — `StartNewGame`**

```text
PrepareNewGameSession(out error)   // já existe
if (cycle.worldCutsceneScene é válido e está no Build Profile)
    BeginTransition(loader, cycle.worldCutsceneScene, null, 0)
else
    BeginTransition(loader, Session.CurrentPeriodScene, Session.period, Session.CurrentLoadingNumber)
```

**`Scripts/Session/GameSessionManager.cs`**

```csharp
/// <summary>Carrega o período já preparado pela sessão. Usado pela cutscene.</summary>
public bool StartPreparedPeriod(out string error);
```

**Novo arquivo `Scripts/Cutscene/WorldCutsceneController.cs`**

Campos: `Sprite[] slides`, `float secondsPerSlide`, `Image slideImage`, `CanvasGroup fade`, `GameCycleDefinition cycle`.
Método: `Finish()` → chama `Session.StartPreparedPeriod`.

### 9.4 Critérios de aceite

- [ ] Clicar em "Jogar" exibe a cutscene. A cutscene não mostra legenda de carregamento.
- [ ] O fim da cutscene carrega o Dia 1 com a legenda `Dia 1`.
- [ ] "Continuar" não passa pela cutscene.
- [ ] A cena ausente do Build Profile não bloqueia o início do jogo.

---

## 10. M9 — Progressão dos Dias

### 10.1 Fluxo por Dia

| Dia | Objetivo no canto | Trava de navegação | Gatilho de rádio | Tutorial | Como a Noite começa |
|---|---|---|---|---|---|
| 1 | "Encontre a lanterna" | Sótão e portas bloqueados | Após 3 navegações | Ao pegar a lanterna | Botão do painel |
| 2 | "Vá até as portas" | Sótão bloqueado | Após 3 navegações | Ao entrar em uma porta | Botão do painel |
| 3 | "Encontre o lampião e o óleo" | Nenhuma | Após 3 navegações | Ao posicionar o lampião e ter o óleo | Botão do painel |
| 4 | — | Nenhuma | Imediato ao entrar | — | Fim do rádio (`AdvanceToNight`) |

Regras:

1. A mensagem de rádio é autoria do usuário. O plano entrega a estrutura e os ganchos.
2. O texto da mensagem de canto é autoria do usuário. O plano entrega o componente e os campos.
3. O Dias 1 e 2 não liberam o sótão.
4. O Dia 1 não libera as portas.
5. O Dia 4 não tem exploração obrigatória. O rádio inicia ao entrar no Dia.

### 10.2 Trava de navegação por Dia

**Novo arquivo `Scripts/Conditions/DayAtLeastCondition.cs`** — herda de `HotspotConditionSO`.

```csharp
[Min(1)] public int minDay = 1;
[Min(1)] public int maxDay = 99;      // 99 = sem limite superior

public override bool Evaluate(ConditionContext context)
    => context.Day >= minDay && context.Day <= maxDay;
```

**`Scripts/Conditions/ConditionContext.cs`** — adicione a propriedade `Day`:

```csharp
/// <summary>Dia atual da sessão. 1 = primeiro Dia do ciclo.</summary>
public int Day => _session != null ? _session.day : 1;
```

Uso na cena `day`:

| Hotspot | Condição | `unavailableMode` |
|---|---|---|
| `Hotspot_ToSotao` | `DayAtLeastCondition(minDay = 3)` | `Blocked` |
| `Hotspot_ToPortaSala` | `DayAtLeastCondition(minDay = 2)` | `Blocked` |
| `Hotspot_ToPortaCozinha` | `DayAtLeastCondition(minDay = 2)` | `Blocked` |
| Portas internas usadas na Noite | Nenhuma | — |

Regras:

1. Não escreva lógica de Dia em `NavigationHotspot`. Use condição.
2. A condição é stateless. Ela lê a sessão a cada avaliação.
3. `EvaluateConditions` já é chamado ao entrar no ViewNode. Nenhum código novo é necessário.

### 10.3 Mensagem de objetivo

**Novo arquivo `Scripts/UI/ObjectiveBanner.cs`**

Campos: `CanvasGroup group`, `TMP_Text label`, `float fadeSeconds = 0.4f`, `float holdSeconds = 5f`.

API: `Show(string text)`, `Hide()`.

Regras:

1. O banner fica no canto superior esquerdo.
2. O banner aparece no início do Dia com o texto do `DayFlowDefinition`.
3. O banner faz fade in, espera, e faz fade out.
4. O banner não bloqueia input. `blocksRaycasts = false`.
5. O banner usa tempo não escalado.

### 10.4 Dados por Dia

**Novo arquivo `Scripts/Day/DayFlowDefinition.cs`** — `ScriptableObject`.

```csharp
[Serializable]
public sealed class DayFlowEntry
{
    [Min(1)] public int day = 1;
    public string objectiveText;
    [Min(0)] public int requiredNavigationsBeforeRadio = 3;
    public RadioEventDefinition radioEvent;      // vazio = sem rádio neste Dia
    public TutorialPanelDefinition tutorial;     // vazio = sem tutorial
    public bool advanceToNightAfterRadio;
}

public sealed class DayFlowDefinition : ScriptableObject
{
    public string stageId;
    public DayFlowEntry[] entries;
    public DayFlowEntry FindEntry(int day);
    public void CollectValidation(List<string> errors, List<string> warnings);
}
```

Asset novo: `SO/Day/DayFlow_Gameplay.asset` com 4 entradas.

### 10.5 Painel de tutorial

**Novo arquivo `Scripts/Day/TutorialPanelDefinition.cs`** — `ScriptableObject`.

```csharp
public sealed class TutorialPanelDefinition : ScriptableObject
{
    public string tutorialId;
    public string title;              // autoria do usuário
    [TextArea] public string body;    // autoria do usuário
    public string proceedLabel = "Prosseguir";
    public Sprite illustration;       // opcional
}
```

Regra: o agente cria o asset com título, corpo e ilustração vazios. Não escreve texto narrativo.

**Novo arquivo `Scripts/UI/TutorialPanelController.cs`**

Campos: `CanvasGroup group`, `TMP_Text titleLabel`, `TMP_Text bodyLabel`, `Button proceedButton`, `TMP_Text proceedLabel`.

API: `Open(TutorialPanelDefinition definition)`, `Close()`.

Regras:

1. O painel bloqueia o cenário com `InputBlockReason.Cutscene`.
2. O botão "Prosseguir" fecha o painel e chama `GameplaySceneController.RequestPeriodEnd()`.
3. O conteúdo é autoria do usuário, via `TutorialPanelDefinition` (título, corpo, texto do botão, sprite opcional).
4. O painel substitui o `DayEndConfirmationController` no fluxo. Desative `DayEndConfirmation` na cena `day`.
5. `ESC` não fecha o painel. Só o botão fecha.
6. O painel usa tempo não escalado.

Gatilhos por Dia:

| Dia | Gatilho | Condição de exibição |
|---|---|---|
| 1 | Interação `INT_PegarLanterna` concluída | `HasItem(item_lanterna)` |
| 2 | Entrada em `__VN_PortaSala` ou `__VN_PortaCozinha` | primeira vez no Dia |
| 3 | Drop do lampião no sótão | `LampPlacedInAttic` **e** `HasItem(item_oleo)` |
| 3 | Coleta do óleo depois do lampião | mesma regra |

Regra: o painel abre uma vez por Dia. Guarde a marca em `_consumedDayEvents` da sessão.

### 10.6 `DayFlowController`

**Novo arquivo `Scripts/Day/DayFlowController.cs`** — `MonoBehaviour` na cena `day`.

Campos: `DayFlowDefinition definition`, `ObjectiveBanner banner`, `TutorialPanelController tutorial`, `GameplaySceneController scene`.

Responsabilidades:

1. No boot do Dia: resolve a entrada do Dia e mostra o banner de objetivo.
2. Conta navegações via `NavigationManager.ViewNodeChanged`.
3. Dispara o evento de rádio quando o contador atinge o limite.
4. Abre o painel de tutorial nos gatilhos do §10.5.
5. No Dia 4: dispara o rádio no primeiro frame do Dia.
6. Escreve warning quando a entrada do Dia não existe.

### 10.7 Critérios de aceite

- [ ] Dia 1 mostra "Encontre a lanterna". O sótão e as portas mostram bloqueio.
- [ ] Após 3 navegações no Dia 1, o rádio toca uma única vez.
- [ ] Pegar a lanterna abre o painel. O botão leva à Noite 1.
- [ ] Dia 2 libera as portas e mantém o sótão bloqueado.
- [ ] Dia 3 libera o sótão. O painel abre só com lampião posicionado e óleo no inventário.
- [ ] Dia 4 toca o rádio e carrega a Noite 4 sem exploração.
- [ ] Nenhum Dia abre dois tutoriais.

---

## 11. M10 — Fonte SpecialElite-Regular SDF

### 11.1 Regra

1. A fonte oficial do jogo é `Assets/_Project/Art/Fonts/SpecialElite-Regular SDF.asset`.
2. Todo elemento **novo** deste plano usa essa fonte: tooltip, relógio, objetivo, painel de tutorial, diálogo do rádio, cutscene.
3. Não altere a fonte dos elementos existentes nesta entrega.
4. Defina a fonte como padrão do projeto: `Edit > Project Settings > TextMesh Pro > Settings > Default Font Asset`.

### 11.2 Checklist

- [ ] `TMP_Settings` aponta para `SpecialElite-Regular SDF`.
- [ ] `HotspotTooltipUI.label.font` = SpecialElite.
- [ ] `ObjectiveBanner.label.font` = SpecialElite.
- [ ] `TutorialPanelController` usa SpecialElite no título, corpo e botão.
- [ ] `RadioEventController` usa SpecialElite no diálogo e no indicador.
- [ ] `WorldCutsceneController` usa SpecialElite nos textos.

---

## 12. Mapa de arquivos

### 12.1 Arquivos novos

| Arquivo | Responsabilidade |
|---|---|
| `Scripts/Debug/DebugCheatController.cs` | Sequência MIKA e atalhos de debug. |
| `Scripts/Conditions/DayAtLeastCondition.cs` | Condição de Dia mínimo e máximo. |
| `Scripts/UI/HotspotTooltipUI.cs` | Tooltip "Ir para <nome>". |
| `Scripts/UI/ClockCursorPresenter.cs` | Sprite de relógio no ponteiro. |
| `Scripts/Hotspots/ClockHotspot.cs` | Hotspot do relógio. |
| `Scripts/UI/ObjectiveBanner.cs` | Mensagem de objetivo no canto. |
| `Scripts/UI/TutorialPanelController.cs` | Painel de tutorial com botão de prosseguir. |
| `Scripts/Day/DayFlowDefinition.cs` | Dados por Dia. |
| `Scripts/Day/DayFlowController.cs` | Orquestração do Dia. |
| `Scripts/Day/TutorialPanelDefinition.cs` | Conteúdo do painel de tutorial (autoria do usuário). |
| `Scripts/Radio/RadioEventDefinition.cs` | Dados do evento de rádio. |
| `Scripts/Radio/RadioEventController.cs` | Execução do evento de rádio. |
| `Scripts/Cutscene/WorldCutsceneController.cs` | Cutscene do mundo. |
| `Editor/BackpackItemsSetup.cs` | Setup da mochila de três itens. |
| `Editor/NightProfileSetup.cs` | Setup de `active` e `profileId` dos perfis. |
| `Editor/GameplayUiSetup.cs` | Setup de tooltip, relógio, banner e painel. |
| `Editor/RadioEventSceneSetup.cs` | Cria a cena `radio_event`. |
| `Editor/WorldCutsceneSceneSetup.cs` | Cria a cena `world_cutscene`. |
| `Editor/CycleTests.cs` | Testes EditMode do ciclo. |
| `Editor/ActiveEntityTests.cs` | Testes EditMode de `active`. |
| `Editor/DayFlowTests.cs` | Testes EditMode da progressão. |
| `Editor/RadioEventTests.cs` | Testes EditMode do rádio. |

### 12.2 Arquivos alterados

| Arquivo | Alteração |
|---|---|
| `Scripts/Entities/NightEntityProfile.cs` | Campo `active`; validação por entidade ativa; Exhauster nulo = desligado. |
| `Scripts/Entities/EntityDirector.cs` | Filtro de entidades inativas; `IsEntityActive`. |
| `Scripts/Entities/EntityPresentationCoordinator.cs` | Ignora bindings de entidade inativa. |
| `Scripts/Session/GameSessionManager.cs` | Ciclo 1:1; `RegisterNightCount`; `TryDebugJump`; `StartPreparedPeriod`; rádio pendente; `DebugCheatsUnlocked`. |
| `Scripts/Session/GameCycleDefinition.cs` | Campo `worldCutsceneScene`. |
| `Scripts/Scene/GameplaySceneDefinition.cs` | Propriedade `NightCount`. |
| `Scripts/Scene/GameplaySceneController.cs` | `RegisterNightCount`; `DebugCheatController`; `Tooltip`; ViewNode inicial do retorno do rádio. |
| `Scripts/UI/MainMenuController.cs` | Cutscene antes do Dia 1. |
| `Scripts/Navigation/ViewNodeDefinition.cs` | Campo `gameplayName`. |
| `Scripts/Navigation/NavigationManager.cs` | Endpoint `GetGameplayName(viewNodeId)`. |
| `Scripts/Conditions/ConditionContext.cs` | Propriedade `Day`. |
| `Scripts/Hotspots/HotspotBase.cs` | Ganchos `OnHoverEnter` e `OnHoverExit`. |
| `Scripts/Hotspots/NavigationHotspot.cs` | `DestinationGameplayName`; tooltip. |
| `Scripts/Feedback/GameplayCursorController.cs` | Cursor oculto com dono. |
| `Editor/ExhausterSceneSetup.cs` | Atualiza a referência do hotspot de óleo, se o legado mudar. |

---

## 13. Assets a criar ou alterar

| Caminho | Tipo | Ação |
|---|---|---|
| `SO/Itens/ITEM_Lampiao.asset` | `ItemDefinition` | Criar (`id = item_lampiao`, `isTool = true`). |
| `SO/Backpack/BPSlot_Lanterna.asset` | `BackpackSlotDefinition` | Criar. |
| `SO/Backpack/BPSlot_Lampiao.asset` | `BackpackSlotDefinition` | Criar. |
| `SO/Backpack/BPSlot_Oleo.asset` | `BackpackSlotDefinition` | Criar. |
| `SO/Day/DayFlow_Gameplay.asset` | `DayFlowDefinition` | Criar com 4 entradas. |
| `SO/Radio/RADIO_Day1.asset` … `RADIO_Day4.asset` | `RadioEventDefinition` | Criar com linhas vazias. |
| `SO/Tutorial/TUTORIAL_Voyeur.asset`, `TUTORIAL_Predator.asset`, `TUTORIAL_Exhauster.asset` | `TutorialPanelDefinition` | Criar com campos vazios. |
| `SO/Conditions/DAY_Min3_Sotao.asset` | `DayAtLeastCondition` | Criar. |
| `SO/Conditions/DAY_Min2_Portas.asset` | `DayAtLeastCondition` | Criar. |
| `SO/Audio/Anchors/ANCHOR_Radio_CorredorFront.asset` | `AudioAnchorDefinition` | Criar. |
| `SO/NightEntities/NIGHT_Entities_1..4` | `NightEntityProfile` | `active` e `profileId` corretos. |
| `SO/CYCLE_Gameplay.asset` | `GameCycleDefinition` | `nightCount = 4`, `worldCutsceneScene = world_cutscene`. |
| `Scenes/radio_event.unity` | Cena | Criar pelo setup de Editor. |
| `Scenes/world_cutscene.unity` | Cena | Criar pelo setup de Editor. |
| `ProjectSettings/EditorBuildSettings.asset` | Build Profile | Adicionar as duas cenas novas. |
| Objetos de cena: `HotspotTooltipUI`, `ClockHotspot`, `ClockCursorPresenter`, `ObjectiveBanner`, `TutorialPanel`, `DayFlowController` | Componentes | Criar pelo `GameplayUiSetup` nas cenas `day` e `night`. |

---

## 14. Ordem de implementação

Cada fase compila sozinha. Não avance sem o critério cumprido.

| Fase | Conteúdo | Gate |
|---|---|---|
| **0** | Leitura deste documento. Conferir os arquivos citados. | Nenhuma alteração feita. |
| **1** | M1: `active`, Exhauster nulo, validação, filtros, `NightProfileSetup`. | A cena `night` abre sem erro de boot. |
| **2** | M2: ciclo 1:1, `RegisterNightCount`, `TryEndPeriod`, normalização do save, legenda de carregamento. | Dia 2 abre após a Noite 1. |
| **3** | M3: `DebugCheatController`, `TryDebugJump`, atalhos. | `F2` e `Shift+F3` funcionam. |
| **4** | M4: itens, slots, setup da mochila, hotspot do lampião no sótão, fato persistente. | Os três itens aparecem na mochila. |
| **5** | M5 e M6: tooltip, `ClockHotspot`, `GameplayUiSetup`. | Tooltip e relógio funcionam nas duas cenas. |
| **6** | M7 e M8: cenas `radio_event` e `world_cutscene`, retorno por ViewNode. | Rádio e cutscene completam o ciclo. |
| **7** | M9: `DayFlowDefinition`, `DayFlowController`, banner, painel, condições de Dia. | Os 4 Dias seguem a tabela de §10.1. |
| **8** | M10, testes EditMode, revisão de warnings, erratas nos documentos. | Checklist de §17 completo. |

---

## 15. Testes

### 15.1 EditMode

| Arquivo | Casos |
|---|---|
| `Editor/ActiveEntityTests.cs` | Exhauster com `definition` nula não gera erro. Entrada inativa não gera erro de anchor. Perfil sem ameaça ativa gera exatamente um erro. Entrada inativa não entra em `_orderedEntityIds`. |
| `Editor/CycleTests.cs` | `NightCount` lido do `DEF_Night`. Dia 1 → Noite 1 → Dia 2 grava checkpoints corretos. Última Noite marca `IsCycleComplete`. `TryFailNight` mantém `day == night`. Save com `night > day` normaliza. |
| `Editor/DayFlowTests.cs` | `DayFlowDefinition.FindEntry` por Dia. `DayAtLeastCondition` respeita `minDay` e `maxDay`. Contador de navegações respeita o limite. Evento não repete no mesmo Dia. |
| `Editor/RadioEventTests.cs` | `RadioEventDefinition` sem linhas devolve falha segura. `RadioReturnMode.AdvanceToNight` chama `TryEndPeriod`. `TryConsumeReturnViewNode` devolve o id uma vez. |

Regras:

1. Siga o padrão de `VS9AudioEntityUnificationTests`.
2. Monte o ambiente com `new GameObject` e componentes. Não use Play Mode.
3. Use `Object.DestroyImmediate` no teardown.
4. Não crie dependência de asset de cena nos testes.

### 15.2 Play Mode (roteiro manual)

1. Novo jogo → cutscene → `Dia 1`.
2. Dia 1: sótão e portas bloqueados. Banner visível.
3. Três navegações → rádio toca → volta ao ViewNode de origem.
4. Pegar a lanterna → painel → botão → `Noite 1`.
5. Noite 1: só o Voyeur. F8 não lista o Predator.
6. `Shift + M, I, K, A` → `Ctrl + P` não faz nada (Predator inativo). `Ctrl + V` funciona.
7. Vencer a Noite 1 → `Dia 2` com legenda correta.
8. `F3` → `Dia 3`. `Shift + F3` → `Noite 3` com Exhauster.
9. `Ctrl + E` → lampião apaga → tolerância → jumpscare → volta ao início da Noite 3.
10. Dia 4: rádio → fim → `Noite 4`.
11. Vencer a Noite 4 → tela de conclusão → menu sem save.
12. Console sem warning novo de autoria.

---

## 16. Riscos e mitigações

| Risco | Mitigação |
|---|---|
| Perfil antigo com `active` ausente lê `true` por padrão e muda o comportamento | O setup de Editor reescreve os 4 perfis e imprime o resultado. |
| Ciclo novo quebra save antigo | Normalização `night > day` no `Apply`. Teste em `CycleTests`. |
| Retorno do rádio perde o ViewNode | `PendingReturnViewNodeId` na sessão. Fallback para `initialViewNodeId` com warning. |
| `AudioRandomContainer` ausente em build | Fallback por `PlayOneShot` com `typewriterClips`. |
| Tooltip cortado em resolução pequena | `Mathf.Clamp` com padding e teste em 1280×720. |
| Cheat de pulo de Dia usado em produção | Cheats liberados só por sequência. Constante única `AllowCheatsInRelease = false`. |
| Painel de tutorial abrir duas vezes | Marca em `_consumedDayEvents`. |
| Cena nova fora do Build Profile | `MainMenuController` e `RadioEventController` validam a cena antes de carregar. |
| Exhauster armado sem lampião colocado | A regra de armação continua no perfil. O tutorial garante a colocação antes da Noite 3. |
| Duas verdades sobre a contagem de Noites | `RegisterNightCount` no boot da Noite. O `CYCLE` é só fallback. |

---

## 17. Definição de pronto

- [ ] O boot da cena `night` não gera erro de configuração obrigatória.
- [ ] O ciclo executa 4 Dias e 4 Noites, com legenda correta em cada carregamento.
- [ ] Perder na Noite N volta ao início da Noite N.
- [ ] Vencer a Noite 4 mostra a conclusão sem carregar cena.
- [ ] Os cheats MIKA e os 5 atalhos funcionam e não gravam save.
- [ ] A mochila tem Lanterna, Lampião e Lata de Óleo. O legado não aparece.
- [ ] O lampião é posicionado no sótão por arraste e o fato `LampPlacedInAttic` é gravado.
- [ ] O tooltip mostra `Ir para <GameplayName>` e nunca quebra linha.
- [ ] O HUD de tempo foi removido. O `ClockHotspot` mostra a hora correta por período.
- [ ] A cena `radio_event` executa jingle, 3 imagens, typewriter, indicador e retorno.
- [ ] A cena `world_cutscene` aparece entre o menu e o Dia 1.
- [ ] Os 4 Dias seguem a tabela de §10.1.
- [ ] Os elementos novos usam a fonte `SpecialElite-Regular SDF`.
- [ ] Os testes EditMode novos passam. Nenhum teste antigo falha.
- [ ] O Console não tem warning novo de erro de conteúdo além das pendências de mídia já conhecidas.

---

## 18. Perguntas abertas (formato ASD-STE100)

Responda antes da fase indicada. Cada pergunta tem uma recomendação.

**Q1 — Fase 4.** O lampião some do inventário depois de posicionado no sótão?
Recomendação: sim. O lampião pertence ao cenário depois da colocação.

**Q2 — Fase 4.** O jogador pode pegar o lampião de volta depois de posicionado?
Recomendação: não, nesta entrega. Um fato persistente controla a colocação.

**Q3 — Fase 6.** A cena `radio_event` usa transição `CrossFade` ou corte seco?
Recomendação: `CrossFade`, com `fadeDuration` do `GameCycleDefinition`.

**Q4 — Fase 6.** O evento de rádio pode repetir no mesmo Dia se o jogador recarregar o checkpoint?
Recomendação: sim. O contador é local e reinicia com o Dia.

**Q5 — Fase 7.** O Dia 4 tem exploração livre antes do rádio?
Recomendação: não. O rádio inicia no primeiro frame do Dia 4.

**Q6 — Fase 7.** O painel de tutorial do Dia 2 abre ao entrar em qualquer uma das portas, ou só na Porta da Sala?
Recomendação: em qualquer uma das duas portas.

**Q7 — Fase 3.** Os cheats ficam disponíveis na build de release?
Recomendação: não. Use `AllowCheatsInRelease = false` e libere só em Editor e Development Build.

**Q8 — Fase 2.** Um save antigo de meio do ciclo pode ser descartado em vez de normalizado?
Recomendação: normalizar. O jogador não perde progresso.

---

## 19. Erratas nos documentos existentes

| Documento | Seção | Errata |
|---|---|---|
| `Arquitetura_Navegacao_Audio_v2.md` | §4.7 e §4.8 | O fluxo agora é Dia N → Noite N → Dia N+1. Não existe sequência de Noites consecutivas. |
| `Arquitetura_Navegacao_Audio_v2.md` | §16.2 | A mochila tem 3 slots: Lanterna, Lampião e Lata de Óleo. |
| `Ciclo_Dia1_Noites.md` | Inteiro | Substituído por §3 e §10 deste plano. O documento fica como histórico. |
| `README.md` | Mecânicas | Trocar "1 Dia + 2 Noites" por "4 Dias e 4 Noites, um par por Noite". |
| `README.md` | Entidades | O Exhauster é manutenção do lampião do sótão, não entidade de porta. |
