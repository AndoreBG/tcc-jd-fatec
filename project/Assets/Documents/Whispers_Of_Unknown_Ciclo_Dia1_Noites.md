# Whispers Of Unknown — Ciclo Dia 1 + Noites e Tutoriais do Dia 1

> **Versão:** 1.0
> **Data:** 2026-10-27
> **Engine:** Unity `6000.3.18f1` · PC · URP
> **Escopo:** estrutura do ciclo (1 Dia + 2 Noites), conclusão do teste beta, checkpoint por Noite, tutoriais do Dia 1 e mudanças de autoria nas cenas `day` e `night`.
> **Status:** implementado. Este documento registra as decisões para o TCC e serve de referência de autoria.

---

## 1. Decisões fechadas

| Tema | Decisão |
|---|---|
| Estrutura do ciclo | **1 Dia + 2 Noites consecutivas**, sem Dia intermediário. |
| Fluxo | `Dia 1 → Noite 1 → Noite 2 → tela de conclusão`, sem carregar cena nova no fim. |
| Cenas | **Uma única cena de Noite**, reutilizada nas duas Noites. O que muda é o `NightEntityProfile`. |
| Dificuldade | Noite 1 = `NIGHT_Entities_2`; Noite 2 = `NIGHT_Entities_3`. |
| Save | Checkpoint gravado **no início de cada Noite** (além do início do Dia). |
| Derrota | Falhar na Noite N devolve ao **início da Noite N**, preservando o que foi feito antes. |
| Início da Noite | Somente pela **lanterna**, com confirmação explícita do jogador. |
| Hotspot `EncerrarDia` | **Desativado** na cena de Dia e na de Noite. |
| `CycleTestUI` | **Desativado** nas duas cenas (código preservado). |
| Tutoriais | 3 documentos no **Dia 1**: cliques, Voyeur e Predator. |
| Conclusão | Mensagem `Obrigado por jogar o teste beta.` sobre a própria cena de Noite. |

---

## 2. Fluxo do ciclo

```text
[Menu] Play / Continue
   │
   ▼
Dia 1 ──(lanterna + confirmação)──► Noite 1 ──(6 AM)──► Noite 2 ──(6 AM)──► Tela de conclusão
   │                                    │                    │                      │
   │ checkpoint início do Dia           │ checkpoint N1      │ checkpoint N2        │ botão "Voltar ao menu"
   ▼                                    ▼                    ▼                      ▼
 falha do jogador?                falha ► início N1     falha ► início N2      (sem save, sem consolidar)
```

- **Dia → Noite 1** é solicitado por `DayEndConfirmationController.Confirm()` → `GameplaySceneController.RequestPeriodEnd()` → `GameSessionManager.TryEndPeriod()`.
- **Noite N → Noite N+1** é solicitado pelo `EntityDirector` ao atingir 6 AM (`RequestPeriodEndAfterSixAm`).
- **Fim da última Noite** não carrega cena: `TryEndPeriod` apenas marca `IsCycleComplete` e emite `SessionStateChanged`.

---

## 3. Modelo de dados

### 3.1 `GameCycleDefinition`
```csharp
public string dayScene;      // "day"
public string nightScene;    // "night"  (compartilhada pelas Noites)
public int nightCount = 2;   // Noites consecutivas após o Dia
public string mainMenuScene;
```

### 3.2 `GameplaySceneDefinition` (cena de Noite)
```csharp
public NightEntityProfile nightEntityProfile;              // fallback
public NightEntityProfile[] nightEntityProfilesByNight;    // 0 = Noite 1, 1 = Noite 2
public NightEntityProfile ResolveNightEntityProfile(int night);
```
`DEF_Night` ficou com `[NIGHT_Entities_2, NIGHT_Entities_3]`. O `EntityDirector` resolve o perfil pelo **índice da Noite atual** (`GameSessionManager.night`), não pelo Dia.

### 3.3 `GameSaveData`
```csharp
public const int CurrentVersion = 2;            // v1 = só início do Dia
public const int MinimumSupportedVersion = 1;   // saves v1 continuam aceitos (night = 0)
public int day;
public int night;                               // 0 = início do Dia; N = início da Noite N
```
`GameSessionManager.Apply` deriva o período do checkpoint: `night >= 1 → GamePeriod.Night`.

### 3.4 `GameSessionManager`
| Membro | Significado |
|---|---|
| `night` | Noite atual do ciclo (1-based); 0 = ciclo no Dia. |
| `NightCount` | `GameCycleDefinition.EffectiveNightCount`. |
| `IsCycleComplete` | Teste beta concluído (última Noite vencida). |
| `CurrentPeriodScene` | Cena a carregar para o estado atual (Dia ou Noite). |
| `CurrentLoadingNumber` | Número exibido na tela de carregamento ("Dia X"/"Noite X"). |

`TryEndPeriod`, `TryContinue`, `TryFailNight`, `TryRestartCheckpoint` e `TryNewGame` passaram a usar `CurrentPeriodScene`/`CurrentLoadingNumber`, então **Continuar** pode abrir diretamente uma Noite quando o checkpoint é de uma Noite.

---

## 4. Tutoriais do Dia 1

Três documentos (`DocumentData`) abertos por hotspots de clique, sem sistema novo:

| Documento | ViewNode | Interação | Tema |
|---|---|---|---|
| `DOC_TUTORIAL_Cliques` | `__VN_CorredorFront` | `INT_TUTORIAL_Cliques` | onde e como clicar, cursor, navegação, objetivo do dia |
| `DOC_TUTORIAL_Voyeur` | `__VN_CorredorBack` | `INT_TUTORIAL_Voyeur` | janelas: manter a luz da lanterna até a criatura recuar |
| `DOC_TUTORIAL_Predator` | `__VN_PortaSala` | `INT_TUTORIAL_Predator` | portas: segurar o botão esquerdo sobre a maçaneta |

- Todos usam `InteractionHotspot` com `activationMode = Click`, `unavailableMode = Hidden` e sem condições (sempre relíveis).
- Os hotspots são **filhos do `Content` do respectivo `__VN_*`**, portanto só existem enquanto o ViewNode está apresentado.
- A área clicável usa `Image` com `alpha 0.08` (placeholder de autoria até existir arte).

---

## 5. Início da Noite pela lanterna

1. `Hotspot_PegarLanterna` (`Layer_Item` do `__VN_CorredorFront`) executa `INT_PegarLanterna` (item + coleta).
2. Ao final, `onActivated` chama `DayEndConfirmationController.Open()`.
3. O modal exibe o aviso e bloqueia o cenário com `InputBlockReason.Modal`.
4. **Encerrar o dia** remove o bloqueio e chama `RequestPeriodEnd()`; **Continuar explorando** ou `ESC` apenas fecha.

Como o hotspot da lanterna desaparece depois de coletada, existe também `Hotspot_EncerrarDia_Lanterna` no mesmo ponto do `Layer_Item`, com a condição `HASITEM_TemLanterna` (`unavailableMode = Hidden`): ele só aparece depois que o jogador tem a lanterna e é o caminho para reabrir a confirmação caso tenha cancelado.

---

## 6. Conclusão do teste beta

- `BetaEndScreenController` vive na cena de Noite (`BetaEndScreen`, último filho do `CanvasUI`).
- Escuta `GameSessionManager.SessionStateChanged`; quando `IsCycleComplete` é verdadeiro, mostra o painel com `Fim do teste beta` / `Obrigado por jogar o teste beta.` e bloqueia gameplay com `InputBlockReason.Cutscene`.
- O botão **Voltar ao menu** chama `TryReturnToMainMenu()` (sem save nem consolidação).

---

## 7. O que foi desativado

| Objeto | Cena | Motivo |
|---|---|---|
| `Hotspot_EncerrarDia` | `day` | A Noite começa pela lanterna. |
| `Hotspot_EncerrarDia` | `night` | A Noite termina autoritativamente às 6 AM; o hotspot pularia a Noite. |
| `CycleTestUI` | `day` e `night` | Pedido de escopo. Código preservado; reative o objeto para depurar com F8. |

> Atenção: com o `CycleTestUI` desativado, uma falha de boot (`IsAtTestEntry`) não exibe o painel de debug. Para investigar, reative o objeto na cena.

---

## 8. Checklist de autoria manual

- [ ] Arte dos 3 sprites decorativos do menu principal (`Decor_Right_01..03`).
- [ ] Arte/substituição da área dos hotspots de tutorial (hoje um retângulo com alpha 0.08).
- [ ] Texto final dos tutoriais (o conteúdo atual é provisório e técnico).
- [ ] Posição/tamanho do `Box` do modal `DayEnd_Confirmation` conforme a identidade visual.
- [ ] SFX de conclusão na tela do teste beta (opcional).

---

## 9. Critérios de aceite

1. Iniciar um jogo novo leva ao Dia 1; a Noite só começa pela lanterna + confirmação.
2. Vencer a Noite 1 leva à Noite 2 **sem passar pelo Dia**, com `NIGHT_Entities_3`.
3. Perder na Noite 2 retorna ao **início da Noite 2**, com `NIGHT_Entities_3` novamente.
4. Vencer a Noite 2 mostra a tela de conclusão, sem carregar cena.
5. A tela de carregamento exibe `Dia 1`, `Noite 1` e `Noite 2` com o áudio do período correto.
6. `Hotspot_EncerrarDia` e `CycleTestUI` estão inativos nas duas cenas.
7. Os três tutoriais abrem nos ViewNodes corretos e podem ser relidos.
