# Whispers Of Unknown — Preparação de cartões Trello
## Entidades Noturnas: Predator e Voyeur

> **Documento-fonte:** `Whispers_Of_Unknown_Entidades_Predator_Voyeur_Plano_Tecnico.md`  
> **Status:** preparação de backlog; nenhum cartão abaixo implica implementação concluída.  
> **Engine-alvo:** Unity `6000.3.18f1` · PC · C# · Input Manager legado.  
> **Escopo do pacote:** sistema noturno de entidades, Predator, Voyeur, NightClock, áudio/visual, terminal, jumpscare, retorno ao checkpoint e Debug F8.

---

## Como usar este pacote

1. Criar os cartões com os IDs `ENT-01` a `ENT-18` nas colunas iniciais indicadas.
2. Manter os cartões de autoria de assets separados dos cartões de código: não criar clips, sprites ou placeholders como substituto de conteúdo final.
3. Não iniciar um cartão enquanto suas dependências técnicas não estiverem em revisão/concluídas.
4. Um cartão só vai para `✅ Concluído` após satisfazer todos os critérios de pronto e checklist aplicável.
5. Qualquer divergência do plano técnico deve gerar decisão documentada antes de modificar a arquitetura.
6. O estado de entidades é local à Noite: nunca criar save/checkpoint novo para Predator ou Voyeur.

## Colunas sugeridas

| Coluna | Uso |
|---|---|
| `📋 Programação` | Código runtime, ScriptableObjects, validação, integração de cena. |
| `🔈 Sound Design` | Autoria manual de SFX, loops, mixagem e jumpscares. |
| `🖼️ Arte` | Visuais, regiões de confronto, animações e arte de jumpscare. |
| `👀 Revisão / Teste` | Play Mode, diagnóstico F8, regressão e aceite. |
| `🐞 Bugs` | Falhas confirmadas após testes. |
| `📌 Visão Geral` | Rastreabilidade, decisões e coordenação. |

## Separação por vertical slice

Os cartões não são apenas uma lista técnica: cada slice abaixo deve produzir um incremento testável de gameplay. Os IDs continuam estáveis para permitir rastreamento mesmo quando cartões de autoria aparecem em outro momento do quadro.

### Vertical Slice 5 — Fundação das Entidades Noturnas

**Cartões:** `ENT-01`, `ENT-02`, `ENT-03`, `ENT-04`, `ENT-05`, `ENT-09`, `ENT-15`  
**Incremento jogável:** a cena noturna tem perfil válido, NightClock visível, seed/ticks/d20, curva por hora, reserva de anchors e política de UI noturna. Ainda não exige arte ou áudio final.  
**Saída do slice:** uma entidade pode ser observada em diagnóstico atravessando `Inactive → Light → Near`, reservando anchor corretamente, sem save e sem travar o boot.

### Vertical Slice 6 — Predator: primeira ameaça completa

**Cartões:** `ENT-06`, `ENT-07`, `ENT-10`, `ENT-12`, `ENT-13`  
**Incremento jogável:** Predator possui apresentação visual/sonora, ataca portas, é enfrentado por hold de LMB, entra em Terminal, apresenta jumpscare e retorna ao checkpoint do Dia.  
**Saída do slice:** é possível sobreviver, resolver, falhar e validar a corrida entre Terminal e 6 AM usando somente Predator.

### Vertical Slice 7 — Voyeur: defesa por lanterna

**Cartões:** `ENT-08`, `ENT-11`  
**Incremento jogável:** Voyeur ataca janelas e pode ser repelido exclusivamente pelo halo Halógeno, com cobertura percentual, pausa/retomada de Critical e regras próprias de Terminal.  
**Saída do slice:** as duas entidades podem coexistir, disputar anchors e obedecer a limpeza/arbitragem já criada no slice anterior.

### Vertical Slice 8 — Autoria, diagnóstico e aceite

**Cartões:** `ENT-14`, `ENT-16`, `ENT-17`, `ENT-18`  
**Incremento jogável:** conteúdo autorado, diagnóstico reproduzível pelo F8, validação de mídia/bindings e regressão completa de Noite, checkpoint e UI.  
**Saída do slice:** pacote de entidades pronto para revisão/produção, com pendências de conteúdo explicitamente rastreadas.

---

## Dependências resumidas

```text
ENT-01 ─┬─ ENT-02 ─┬─ ENT-04 ─┬─ ENT-06 ─┬─ ENT-10 ─┬─ ENT-12 ─┬─ ENT-16 ─┬─ ENT-18
        │          │          │          │          │          │          │
        │          │          │          ├─ ENT-11 ─┘          └─ ENT-17 ─┘
        │          │          │          │
        │          │          ├─ ENT-07 ─┘
        │          │          └─ ENT-08 ─┬─ ENT-11
        │          │                     └─ ENT-09
        │          └─ ENT-05
        ├─ ENT-03 ───────────────────────┘
        └─ ENT-15 ────────────────────────────────┘

ENT-13 e ENT-14 dependem do núcleo de terminal e devem estar prontos antes da revisão final.
```

---

# ENT-01 — Define o domínio de dados das entidades noturnas

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta · Fundação  
**Vertical Slice:** VS5 — Fundação das Entidades Noturnas  
**Dependências:** nenhuma

## Objetivo

Criar os enums, ScriptableObjects e estruturas serializáveis que representam a identidade das entidades, seus estados, áudio por estado, jumpscare, tuning por Noite e curva de dificuldade.

## Escopo

- `EntityState`: `Inactive`, `Light`, `Near`, `Critical`, `Resolving`, `Resolved`, `Terminal`.
- `EntityDefinition` abstrata.
- `PredatorDefinition` e `VoyeurDefinition`.
- `NightEntityProfile` e entradas ordenadas por entidade.
- Curva de seis níveis de IA `0..20`, um por hora fictícia.
- Tuning comum e tuning especializado de Predator/Voyeur.
- `NightClockSettings` global com 360 s de Noite e tick de 5 s.
- Dados de SFX de entrada, loop opcional e jumpscare por entidade.

## Critério de pronto

- Dados de identidade não contêm estado runtime, referências de cena ou save.
- Tuning por Noite não fica na `EntityDefinition`.
- Cada entrada noturna possui exatamente seis valores válidos de dificuldade.
- `VoyeurDefinition` contém apenas a regra fixa de cobertura mínima de Halógeno; região visual continua local ao binding.
- Durações de jumpscare pertencem à entidade, não à Noite.

## Checklist

- [ ] `EntityState` contém os sete estados aprovados.
- [ ] `EntityDefinition` possui `entityId` estável e nome de exibição.
- [ ] `PredatorDefinition` fixa categoria `Door`.
- [ ] `VoyeurDefinition` fixa categoria `Window` e threshold de cobertura.
- [ ] A apresentação de áudio suporta enter SFX + loop opcional + fades.
- [ ] Jumpscare suporta visual opcional, SFX opcional e duração.
- [ ] `NightEntityProfile` possui entradas de configuração ordenadas.
- [ ] Curvas de IA aceitam apenas inteiros entre 0 e 20.
- [ ] Timers são expressos em segundos reais.
- [ ] Nenhuma estrutura nova escreve em `GameSaveData`.

---

# ENT-02 — Integra o perfil de entidades à definição da cena noturna

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta · Fundação  
**Vertical Slice:** VS5 — Fundação das Entidades Noturnas  
**Dependências:** ENT-01

## Objetivo

Fazer com que cada `GameplaySceneDefinition` de período `Night` referencie explicitamente seu `NightEntityProfile`.

## Critério de pronto

- Cena noturna resolve seu perfil pelo `GameplaySceneDefinition`.
- Cena de Dia não exige nem inicializa entidades noturnas.
- Perfil ausente em Noite é tratado como configuração obrigatória inválida no boot.
- Perfil atribuído incorretamente a Dia gera warning e é ignorado.

## Checklist

- [ ] Campo `nightEntityProfile` adicionado à definição de cena.
- [ ] Campo possui tooltip esclarecendo uso exclusivo de Noite.
- [ ] Boot diferencia Dia de Noite sem criar singleton novo.
- [ ] Perfil noturno é resolvido antes da inicialização do diretor.
- [ ] Falha de perfil não deixa `InputBlocker` preso em Boot.
- [ ] Documentação de dados de cena foi atualizada.

---

# ENT-03 — Autoriza anchors de confronto e mapeia ViewNodes

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta · Autoria de cena  
**Vertical Slice:** VS5 — Fundação das Entidades Noturnas  
**Dependências:** ENT-01

## Objetivo

Estender `AudioAnchorDefinition` com o ViewNode de confronto e criar os anchors necessários ao primeiro perfil noturno.

## Escopo de autoria inicial

```text
Predator
- Porta da Sala
- Porta da Cozinha

Voyeur
- Janela da Sala
- Janela do Corredor
```

## Critério de pronto

- Todo anchor usado por entidade possui `encounterViewNodeId` válido.
- Predator só recebe anchors `Door`.
- Voyeur só recebe anchors `Window`.
- Janela da Sala e Janela do Corredor reutilizam os ViewNodes existentes de Sala e Corredor.

## Checklist

- [ ] `AudioAnchorDefinition` contém `encounterViewNodeId`.
- [ ] Foram criados anchors de porta da Sala e Cozinha.
- [ ] Anchors de janela da Sala e Corredor foram revisados.
- [ ] Cada ID de confronto resolve um ViewNode da cena noturna.
- [ ] A categoria do anchor é validada contra a entidade.
- [ ] Nenhum anchor de teste é criado em runtime.
- [ ] O primeiro perfil lista somente os quatro anchors aprovados.

---

# ENT-04 — Implementa NightClock, seed e loop de IA global

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta · Núcleo runtime  
**Vertical Slice:** VS5 — Fundação das Entidades Noturnas  
**Dependências:** ENT-01, ENT-02

## Objetivo

Criar o relógio noturno, o gerador de aleatoriedade reproduzível e a cadência global de 5 segundos usada pelas entidades em `Inactive` e `Light`.

## Regras obrigatórias

```text
Noite: 360 s reais
Relógio: 12 AM → 6 AM
Tick: 5 s reais
Oportunidade: d20 <= AI Level da hora
Inactive com sucesso: Light
Light com sucesso: segundo d20
  1–10: Inactive
  11–20: tentativa de Near
```

## Critério de pronto

- A mesma seed reproduz d20, seleção de anchor e desempate fatal no Debug.
- Uma entidade muda no máximo uma vez por tick.
- `aiLevel = 0` não move e `aiLevel = 20` sempre concede oportunidade.
- F8 congela relógio, timers e acumulador de tick.

## Checklist

- [ ] `NightClock` expõe horário, progresso e evento de 6 AM.
- [ ] O diretor usa tempo não escalado para o relógio/tick.
- [ ] Seed é criada por tentativa de Noite e exposta para diagnóstico.
- [ ] D20 é limitado a 1..20 inclusivo.
- [ ] Curva troca corretamente a cada hora fictícia.
- [ ] Segundo d20 de Light não depende da dificuldade.
- [ ] Nenhuma transição dupla ocorre no mesmo tick.
- [ ] Debug pode forçar um tick sem avançar tempo acidentalmente.
- [ ] Abrir F8 congela tempo e fechar retoma sem salto.

---

# ENT-05 — Implementa EntityDirector e reservas exclusivas de anchor

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta · Núcleo runtime  
**Vertical Slice:** VS5 — Fundação das Entidades Noturnas  
**Dependências:** ENT-01, ENT-02, ENT-03, ENT-04

## Objetivo

Implementar a autoridade única de estado, reserva de anchor, seleção uniforme de anchor livre e ciclo local das entidades.

## Critério de pronto

- Somente `EntityDirector` aprova mudanças de estado e anchor.
- `Near` reserva anchor; `Resolved`, encerramento e reset o liberam.
- Sem anchor livre, a entidade fica em `Light`.
- Nenhum anchor é reservado por duas entidades.
- O diretor limpa seus estados ao sair/recarregar a Noite.

## Checklist

- [ ] Controllers não conseguem alterar diretamente entidade irmã.
- [ ] Diretor mantém mapa `anchor → entityId` consistente.
- [ ] Entrada em Near seleciona anchor uniformemente entre disponíveis.
- [ ] Anchor ocupado é removido do sorteio e gera reroll entre livres.
- [ ] Sem anchors livres não causa exceção nem muda estado.
- [ ] `Resolved` libera anchor antes do cooldown.
- [ ] `Terminal` conserva sua reserva até cleanup/fim da Noite.
- [ ] Reset normal, falha, retorno ao menu e destruição limpam reservas.
- [ ] Operações de estado + anchor são atômicas.
- [ ] Logs identificam entidade, anchor, estado e motivo de recusa.

---

# ENT-06 — Implementa apresentação visual por ViewNode

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta · Apresentação  
**Vertical Slice:** VS6 — Predator: primeira ameaça completa  
**Dependências:** ENT-03, ENT-05

## Objetivo

Criar `EntityVisualBinding` e `EntityPresentationCoordinator` para apresentar entidade por combinação de entidade, anchor, estado e ViewNode atual.

## Escopo

- Estados com visual: `Near`, `Critical`, `Resolving`, `Terminal`.
- Binding controla GameObjects e `UnityEvent`s locais.
- Regiões de confronto são `RectTransform`s locais.
- Ausência de binding mantém áudio/lógica e gera warning.

## Critério de pronto

- Conteúdo de ViewNode inativo não perde a capacidade de ser apresentado ao voltar a ser atual.
- Visual só aparece no ViewNode apresentado e com binding compatível.
- Não há lógica de IA dentro do binding.

## Checklist

- [ ] Bindings são localizados inclusive sob conteúdo inativo no boot.
- [ ] Binding referencia entidade, anchor e estados aceitos.
- [ ] `presentationRoot` é ativado/desativado de forma idempotente.
- [ ] `onShown` e `onHidden` são disparados uma única vez por mudança real.
- [ ] Região de porta é exposta para Predator.
- [ ] Região de Voyeur é exposta para cálculo de cobertura.
- [ ] Warning é emitido uma única vez para binding ausente no ViewNode atual.
- [ ] Binding não intercepta raycast indevidamente, salvo região explicitamente usada pela defesa.
- [ ] Coordinator reage à mudança de ViewNode com estado já ativo.

---

# ENT-07 — Integra áudio direto de entidades ao SceneAudioController

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta · Áudio  
**Vertical Slice:** VS6 — Predator: primeira ameaça completa  
**Dependências:** ENT-01, ENT-05

## Objetivo

Permitir que entidades apresentem SFX de entrada e loops opcionais por estado, associados ao anchor ativo, sem depender do `AudioDebugCatalog`.

## Critério de pronto

- `SceneAudioController` continua único dono de `AudioSource`, pool e mixer.
- Áudio de entidade usa grupo `Threats` e a perspectiva do anchor/ViewAudioProfile.
- Troca de estado faz fade-out do loop anterior e fade-in do novo loop quando configurado.
- Clips ausentes geram warning, sem bloquear IA ou transições.

## Checklist

- [ ] API direta recebe `entityId`, anchor, estado e apresentação de áudio.
- [ ] Uma entidade não duplica loop ao reentrar/aplicar o mesmo estado.
- [ ] Limpeza por `entityId` libera fontes persistentes corretamente.
- [ ] SFX de entrada não substitui loop ativo incorretamente.
- [ ] AudioDebugCatalog permanece exclusivo para F8/debug manual.
- [ ] Inactive e Resolved não tocam áudio por padrão.
- [ ] Near, Critical, Resolving e Terminal aceitam SFX + loop opcional.
- [ ] Perfil acústico do ViewNode continua afetando entidade ativa.
- [ ] Erro de clip não prende fonte, mixer ou transição.

---

# ENT-08 — Simplifica a lanterna para Halógeno e expõe cobertura

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta · Hotbar  
**Vertical Slice:** VS7 — Voyeur: defesa por lanterna  
**Dependências:** ENT-06

## Objetivo

Remover UV e tornar a lanterna Halógena uma fonte confiável de cobertura circular para o Voyeur.

## Critério de pronto

- Não existe alternância UV, tecla `F`, label de modo ou ramo UV no código/UI.
- A lanterna expõe se está ativa, centro/radius em Canvas e cobertura contra `RectTransform`.
- Cálculo de cobertura é independente de Physics/Collider/3D.

## Checklist

- [ ] Enum e estado UV foram removidos.
- [ ] Hotbar mantém seleção por tecla da lanterna.
- [ ] Halo segue o cursor como hoje.
- [ ] API informa se Halógeno está realmente ativo.
- [ ] Cobertura é definida por `area(círculo ∩ rect) / area(rect)`.
- [ ] Threshold vem de `VoyeurDefinition`.
- [ ] Canvas/região incompatíveis geram warning seguro.
- [ ] LanternEffect continua sem interagir com hotspots comuns.
- [ ] Documentação da Hotbar remove referência a UV.

---

# ENT-09 — Aplica política de UI exclusiva da Noite

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta · Integração  
**Vertical Slice:** VS5 — Fundação das Entidades Noturnas  
**Dependências:** ENT-02

## Objetivo

Garantir que Backpack e documentos não possam ser usados na Noite, mantendo Hotbar e Debug F8 disponíveis em gameplay normal.

## Critério de pronto

- Documentos e Backpack não abrem na Noite por atalho, evento ou referência residual.
- Hotbar permanece disponível até Terminal/jumpscare, conforme regra aprovada.
- F8 permanece disponível e congela IA enquanto aberto.

## Checklist

- [ ] Backpack valida período antes de abrir/arrastar.
- [ ] ModalUI bloqueia abertura de documentos no período Night.
- [ ] UIs já abertas são fechadas com segurança na entrada da Noite.
- [ ] Hotbar não recebe bloqueio indevido em Noite normal.
- [ ] F8 continua autorizado como ferramenta de debug.
- [ ] Terminal não bloqueia navegação/Hotbar por si só.
- [ ] Jumpscare bloqueia Hotbar, F8 e toda UI de gameplay.

---

# ENT-10 — Implementa comportamento do Predator

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta · Entidade  
**Vertical Slice:** VS6 — Predator: primeira ameaça completa  
**Dependências:** ENT-05, ENT-06, ENT-07, ENT-09

## Objetivo

Implementar o ciclo completo do Predator, que ataca portas e é resolvido por hold de LMB na região da porta.

## Fluxo obrigatório

```text
Near → Critical por timer ou entrada na porta
Critical → Resolving se jogador está/entra na porta
Resolving → Resolved após hold contínuo completo
Resolving → Terminal ao perder janela, sair da região/ViewNode ou soltar LMB
Critical → Terminal ao expirar sem defesa
Terminal após Resolving falho → game over imediato
Terminal direto → contagem terminal
```

## Critério de pronto

- Predator usa somente anchors Door.
- Defesa exige LMB contínuo na região configurada.
- A janela de início e duração do hold vêm do perfil da Noite.
- Falha de defesa é irreversível e não deixa input preso.

## Checklist

- [ ] Entrada em ViewNode de porta durante Near força Critical.
- [ ] Critical detecta jogador já presente no ViewNode.
- [ ] Resolving abre janela de início configurável.
- [ ] Hold só acumula com LMB dentro da região da porta.
- [ ] Sair do ViewNode interrompe e causa Terminal imediato.
- [ ] Sair da região ou soltar LMB causa Terminal imediato.
- [ ] Hold completo solicita Resolved.
- [ ] Expiração de Critical sem defesa solicita Terminal normal.
- [ ] Áudio e visual mudam nos estados autorizados.
- [ ] Predator não usa Window nem VisualBinding de Voyeur.

---

# ENT-11 — Implementa comportamento do Voyeur

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta · Entidade  
**Vertical Slice:** VS7 — Voyeur: defesa por lanterna  
**Dependências:** ENT-05, ENT-06, ENT-07, ENT-08, ENT-09, ENT-12

## Objetivo

Integrar o ciclo completo do Voyeur, entidade que ataca janelas, à infraestrutura de Terminal já validada com Predator e resolvê-lo por cobertura de Halógeno sobre sua região visual.

## Fluxo obrigatório

```text
Near → Critical por timer
Critical → Resolving quando cobertura >= threshold
Resolving → Resolved após contato acumulado suficiente
Resolving → Critical ao perder cobertura
Critical → Terminal ao expirar
Terminal na janela/entrada posterior na janela → game over imediato
Terminal fora da janela → contagem terminal
```

## Critério de pronto

- Voyeur usa somente anchors Window.
- Apenas Halógeno pode iniciar/manter Resolving.
- Perder contato preserva o progresso já acumulado de expulsão.
- Timer de Critical pausa em Resolving e retoma do valor anterior.

## Checklist

- [ ] Critical detecta contato já existente ao entrar no estado.
- [ ] Cobertura abaixo do threshold não inicia Resolving.
- [ ] Cobertura válida pausa Critical e inicia Resolving.
- [ ] Progresso de luz usa tempo não escalado e congela no F8.
- [ ] Perder cobertura retorna a Critical sem zerar progresso acumulado.
- [ ] Critical retoma com tempo restante correto.
- [ ] Cobertura suficiente produz Resolved.
- [ ] Expiração de Critical produz Terminal.
- [ ] Terminal imediato ocorre se jogador está/entra no ViewNode de confronto.
- [ ] Voyeur não usa Door nem entrada de LMB do Predator.

---

# ENT-12 — Implementa Terminal, arbitragem fatal e vitória às 6 AM

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta · Fluxo noturno  
**Vertical Slice:** VS6 — Predator: primeira ameaça completa  
**Dependências:** ENT-04, ENT-05, ENT-10

## Objetivo

Implementar a infraestrutura genérica de Terminal inicialmente exercida pelo Predator: limpeza de entidades concorrentes, derrota pendente e prioridade de 6 AM. O Voyeur integrará suas regras específicas de Terminal no VS7.

## Critério de pronto

- Terminal elimina a capacidade de resolver a entidade, mas preserva navegação/Hotbar até o jumpscare.
- Outras entidades são limpas imediatamente.
- Dois Terminals no mesmo tick escolhem vencedor aleatório reproduzível.
- 6 AM cancela qualquer derrota pendente ainda sem jumpscare.

## Checklist

- [ ] Terminal bloqueia novas avaliações de IA.
- [ ] Terminal limpa estado, áudio e reservas da outra entidade.
- [ ] Diretor coleta pedidos fatais do mesmo tick antes de escolher vencedor.
- [ ] RNG de desempate usa a seed noturna.
- [ ] Predator direto usa delay terminal configurável.
- [ ] Predator após falha em Resolving solicita game over imediato.
- [ ] Cruzar 6 AM cancela contagem terminal pendente.
- [ ] Cruzar 6 AM encerra Noite automaticamente pelo fluxo normal de período.

---

# ENT-13 — Implementa jumpscare e falha de Noite sem save

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta · Sessão e apresentação  
**Vertical Slice:** VS6 — Predator: primeira ameaça completa  
**Dependências:** ENT-12

## Objetivo

Apresentar jumpscare fullscreen da entidade vencedora e retornar ao checkpoint do início do Dia sem salvar ou consolidar a Noite.

## Critério de pronto

- Jumpscare usa visual/SFX/duração da EntityDefinition.
- Input é bloqueado por motivo próprio durante apresentação.
- Fluxo retorna ao Dia pelo checkpoint atual, sem chamar save.
- Asset ausente não deixa tela/input/sessão presos.

## Checklist

- [ ] `InputBlockReason.GameOver` foi adicionado.
- [ ] GameOverController usa tempo não escalado.
- [ ] Jumpscare bloqueia navegação, Hotbar, F8 e retorno ao menu.
- [ ] Fallback seguro funciona sem imagem ou clip atribuídos.
- [ ] `GameSessionManager.TryFailNight` não grava checkpoint.
- [ ] Falha restaura o início do Dia atual.
- [ ] Fluxo é distinto de retorno ao menu e de encerramento normal da Noite.
- [ ] Cleanup executa em sucesso, erro e destruição de cena.
- [ ] F8 usa o mesmo fluxo real quando solicitado.

---

# ENT-14 — Estende o Debug F8 para entidades noturnas

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Média · Diagnóstico  
**Vertical Slice:** VS8 — Autoria, diagnóstico e aceite  
**Dependências:** ENT-04, ENT-05, ENT-10, ENT-11, ENT-13

## Objetivo

Fornecer inspeção e comandos autorizados para reproduzir o comportamento da IA, timers, combate, terminal e jumpscare.

## Critério de pronto

- F8 mostra estado completo e reproduz seed/ticks.
- Comandos de debug não salvam inventário/fatos.
- F8 congelado não deixa IA avançar acidentalmente.

## Checklist

- [ ] Exibe horário, progresso, seed e próximo tick.
- [ ] Exibe dificuldade atual e seis níveis de cada entidade.
- [ ] Exibe estado, anchor e todas as contagens relevantes.
- [ ] Exibe última rolagem de oportunidade e direção.
- [ ] Exibe reservas de anchor e bindings resolvidos.
- [ ] Permite fixar seed e forçar tick.
- [ ] Permite forçar estado, anchor e limpeza.
- [ ] Permite simular entrada/saída do ViewNode de confronto.
- [ ] Permite simular cobertura, sucesso e falha de resolução.
- [ ] Permite forçar Terminal e executar jumpscare real.

---

# ENT-15 — Configura perfil noturno baseline e a cena Playground

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta · Autoria técnica  
**Vertical Slice:** VS5 — Fundação das Entidades Noturnas  
**Dependências:** ENT-01, ENT-02, ENT-03, ENT-04, ENT-05

## Objetivo

Criar e atribuir o primeiro `NightEntityProfile`, NightClockSettings, EntityDefinitions, anchors e referências de cena usando o baseline técnico aprovado.

## Valores baseline

| Hora | Predator | Voyeur |
|---|---:|---:|
| 12 AM | 0 | 0 |
| 1 AM | 3 | 2 |
| 2 AM | 5 | 4 |
| 3 AM | 7 | 6 |
| 4 AM | 9 | 8 |
| 5 AM | 11 | 10 |

| Timer | Predator | Voyeur |
|---|---:|---:|
| Near → Critical | 8 s | 10 s |
| Critical → Terminal | 10 s | 12 s |
| Janela inicial | 2,5 s | n/a |
| Resolução | 4 s de hold | 5 s de contato acumulado |
| Cooldown | 30 s | 25 s |
| Terminal normal | 5 s | 4 s |
| Jumpscare | 2,5 s | 2,5 s |

## Critério de pronto

- A cena noturna possui todas as referências técnicas válidas.
- Campos de mídia permanecem vazios para autoria manual, sem placeholders.
- Boot passa com warnings esperados de mídia/binding ainda não autorados.

## Checklist

- [ ] `ENT_Predator` e `ENT_Voyeur` foram criados.
- [ ] NightClockSettings contém 360 s/5 s.
- [ ] Perfil do Playground contém os valores baseline.
- [ ] Perfil usa exatamente os anchors aprovados.
- [ ] `DEF_Night_Playground` referencia o perfil.
- [ ] Cena possui Manager_Entities e componentes necessários.
- [ ] HUD possui label de horário referenciado.
- [ ] Campos de clips/sprites não recebem placeholders.
- [ ] Warnings de mídia ausente são claros e não bloqueiam boot.

---

# ENT-16 — Produz/autoriza apresentação visual e regiões de confronto

**Coluna inicial:** 🖼️ Arte  
**Etiqueta:** Prioridade Média · Dependência de conteúdo  
**Vertical Slice:** VS8 — Autoria, diagnóstico e aceite  
**Dependências:** ENT-06, ENT-08, ENT-10, ENT-11

## Objetivo

Autorizar visuais dos estados de ataque, regiões de porta/janela, UnityEvents e jumpscares sem transferir lógica para a apresentação.

## Critério de pronto

- Cada visual está ligado a entidade, anchor, estado e ViewNode corretos.
- Regiões de confronto correspondem ao conteúdo visual apresentado.
- Jumpscare possui material visual legível no fullscreen.
- A falta de arte não é mascarada por placeholders criados pelo código.

## Checklist

- [ ] Bindings de Near/Critical/Resolving/Terminal foram autorados quando houver arte.
- [ ] Região de porta coincide com a área defensável do Predator.
- [ ] Região do Voyeur coincide com sua área iluminável.
- [ ] Regiões usam Canvas compatível e não têm rotação indevida.
- [ ] UnityEvents não mudam estado de entidade diretamente.
- [ ] Jumpscare do Predator foi atribuído quando disponível.
- [ ] Jumpscare do Voyeur foi atribuído quando disponível.
- [ ] Visual não apresentado é desligado corretamente ao trocar ViewNode.

---

# ENT-17 — Produz/autoriza SFX, loops e mixagem de entidades

**Coluna inicial:** 🔈 Sound Design  
**Etiqueta:** Prioridade Média · Dependência de conteúdo  
**Vertical Slice:** VS8 — Autoria, diagnóstico e aceite  
**Dependências:** ENT-07, ENT-10, ENT-11, ENT-13

## Objetivo

Autorizar os sons por estado e jumpscare de Predator/Voyeur respeitando anchors, grupo Threats e inteligibilidade.

## Critério de pronto

- Cada estado necessário possui enter SFX e/ou loop quando o conteúdo exigir.
- Áudio por estado é distinto e reconhecível.
- Loops não cortam ao mudar ViewNode, estado ou ao iniciar terminal.
- Jumpscare possui SFX próprio quando disponível.

## Checklist

- [ ] Light de cada entidade possui efeito próprio quando autorado.
- [ ] Near, Critical, Resolving e Terminal possuem apresentações distintas quando autoradas.
- [ ] Clips são associados ao estado correto na EntityDefinition.
- [ ] Volumes e fades foram ajustados para Threats.
- [ ] Áudio resolve perspectiva no anchor correto.
- [ ] Transição de estado não duplica loops.
- [ ] Cleanup de Resolved, Terminal, 6 AM e game over interrompe loops.
- [ ] Ausência de clip mantém warning claro e fluxo funcional.

---

# ENT-18 — Valida integração completa de entidades noturnas

**Coluna inicial:** 👀 Revisão / Teste  
**Etiqueta:** Prioridade Alta · Aceite final  
**Vertical Slice:** VS8 — Autoria, diagnóstico e aceite  
**Dependências:** ENT-10 a ENT-17

## Objetivo

Validar todo o pacote no Play Mode e por diagnóstico, incluindo IA, estados, UI, áudio, visuais, game over, checkpoint e regressão de ciclo.

## Critério de pronto

- Predator e Voyeur respeitam todos os invariantes do plano técnico.
- Não existem bloqueios permanentes, fontes órfãs, reservas presas ou retorno indevido de save.
- 6 AM, Terminal e game over têm prioridade correta.
- Todo problema restante vira cartão de bug rastreável.

## Checklist

### NightClock e IA

- [ ] Noite dura 360 s e mostra 12 AM → 6 AM.
- [ ] Tick ocorre a cada 5 s.
- [ ] Curvas por hora alteram a chance corretamente.
- [ ] Seed reproduz a mesma sequência no F8.
- [ ] F8 congela sem pular tick/timer na retomada.

### Anchors e apresentação

- [ ] Predator nunca ocupa Window e Voyeur nunca ocupa Door.
- [ ] Mesmo anchor não é ocupado por duas entidades.
- [ ] Anchor indisponível mantém entidade em Light.
- [ ] Binding ausente mantém áudio e gera warning único.
- [ ] Troca de ViewNode atualiza visual sem reiniciar IA.

### Predator e Voyeur

- [ ] Predator conclui resolução com hold de LMB na porta.
- [ ] Falhas do Predator geram Terminal imediato conforme regra.
- [ ] Voyeur aceita somente Halógeno.
- [ ] Voyeur preserva progresso de luz ao perder/reobter contato.
- [ ] Voyeur entra em game over imediato ao jogador alcançar a janela durante Terminal.

### Terminal, 6 AM e sessão

- [ ] Terminal limpa outra entidade.
- [ ] Terminal pendente ainda permite navegação/Hotbar.
- [ ] Jumpscare bloqueia tudo e usa tempo não escalado.
- [ ] Game over não grava nem consolida Noite.
- [ ] Game over retorna ao checkpoint do início do Dia.
- [ ] 6 AM vence Terminal pendente antes do jumpscare.
- [ ] Jumpscare iniciado não deixa estado parcial/overlay/input presos.

### Regressão

- [ ] Documentos e Backpack permanecem indisponíveis na Noite.
- [ ] Hotbar Halógena continua funcionando.
- [ ] Não existe UV, tecla F ou label de modo restante.
- [ ] Retorno ao menu sem save continua seguro.
- [ ] Dia → Noite → próximo Dia continua funcional sem entidades ativas no Dia.

---

## Ordem recomendada de execução

| Ordem | Cartões | Marco |
|---:|---|---|
| VS5 | ENT-01, ENT-02, ENT-03, ENT-04, ENT-05, ENT-09, ENT-15 | Fundação noturna, perfil, relógio, RNG, anchors, reservas e UI |
| VS6 | ENT-06, ENT-07, ENT-10, ENT-12, ENT-13 | Predator completo: apresentação, combate, Terminal, jumpscare e checkpoint |
| VS7 | ENT-08, ENT-11 | Voyeur completo: Halógeno, cobertura, resolução e Terminal específico |
| VS8 | ENT-14, ENT-16, ENT-17, ENT-18 | Debug, conteúdo, revisão e aceite final |

---

## Regra de encerramento do pacote

O pacote de entidades somente pode ser considerado concluído quando `ENT-18` estiver aprovado e todos os warnings restantes de mídia/binding forem reconhecidos como autoria pendente deliberada ou convertidos em cartões de bug/conteúdo.
