# Whispers Of Unknown — Arquitetura Final de Navegação, Hotspots e Áudio

> **Versão:** 2.0 — consolidada  
> **Data:** 2026-08-17  
> **Engine:** Unity 6000.3.18f1  
> **Plataforma:** PC  
> **Render:** URP  
> **Input:** Input Manager legado (`StandaloneInputModule`)  
> **Escopo:** ViewNodes, câmera e parallax, hotspots, condições, ferramentas, transições, UI modal, áudio e checkpoint entre ciclos.  
> **Status:** referência oficial para implementação. Substitui a versão 1.0 e os documentos de conceito anteriores.

---

## 0. Objetivo e terminologia

Este documento define o comportamento esperado dos sistemas de navegação por pontos de visão, interação por hotspots, apresentação de câmera, áudio e persistência entre os períodos de Dia e Noite.

A arquitetura privilegia simplicidade, previsibilidade e edição direta no Inspector. Não há movimentação 3D livre. O jogador navega entre composições 2D fixas, apresentadas por uma câmera compartilhada, com possibilidade de tilt, pan, shake e parallax em camadas.

### 0.1 Termos oficiais

| Termo | Significado |
|---|---|
| **Etapa** | Capítulo, local ou unidade de progressão atual da campanha. |
| **Período** | Uma das duas partes do ciclo: Dia ou Noite. |
| **Ciclo** | Conjunto formado pelo Dia e pela Noite correspondentes. |
| **Checkpoint diário** | Estado consolidado no início do Dia atual. É o ponto restaurado ao carregar o slot. |
| **Estado de trabalho do ciclo** | Cópia em memória do estado persistente, modificada durante Dia e Noite, mas ainda não consolidada no save. |
| **ViewNode** | Ponto de visão fixo apresentado ao jogador. |
| **ViewNode apresentado** | Único ViewNode cujos filhos visuais e funcionais estão habilitados e preparados para receber entrada. |
| **Hotspot** | Região interativa associada a navegação, interação ou uso de ferramenta. |
| **Reentrada** | Exigência de que o cursor saia fisicamente da região e volte a entrar antes de o hotspot poder reagir. |
| **Fato persistente** | Decisão, descoberta ou alteração que pode atravessar períodos e checkpoints sem ser um item de inventário. |

Neste documento, Dia e Noite são chamados de **períodos**, evitando o uso ambíguo do termo “fase”.

---

## 1. Decisões consolidadas

| # | Eixo | Decisão |
|---|---|---|
| 1 | Estrutura de cenas | **Cenas separadas para Dia e Noite**, com estruturas locais espelhadas por prefabs compartilhados. |
| 2 | Representação do ViewNode | **GameObject na cena**. A raiz `ViewNodeController` permanece ativa; seus filhos visuais e funcionais são habilitados somente quando o ViewNode é apresentado. `ViewNodeDefinition` armazena apenas dados fixos. |
| 3 | Câmera | **Uma câmera compartilhada** para todos os ViewNodes, controlada por um rig. ViewNodes podem definir perfil de pan, tilt, shake, zoom e parallax. |
| 4 | Topologia global | **Um único singleton persistente**, `GameSessionManager`, com `DontDestroyOnLoad`. Managers de gameplay permanecem locais às cenas e são referenciados pelo Inspector. |
| 5 | Ativação de navegação | Três modos por hotspot: **hover imediato**, **hover com permanência** e **clique**. O primeiro vertical slice implementa hover imediato. |
| 6 | Condições | `ScriptableObjects` reutilizáveis e sem estado de runtime; política **Todas/Qualquer** por hotspot; atualização por eventos; avaliação completa ao entrar no ViewNode; validação final antes da execução. |
| 7 | Condição não atendida | Configurável por hotspot: **oculto**, **visível bloqueado** ou **visível bloqueado com pista**. |
| 8 | Ferramentas | Seleção pelo inventário; clique aplica a ferramenta no `ToolHotspot`; falha não consome nem remove a seleção; destaque de alvos válidos é opcional por ferramenta e desligado por padrão. |
| 9 | Transições | `TransitionProfile` reutilizável por link, com ocultação, ponto de troca e revelação. Entrada permanece bloqueada durante toda a transição e por **0,05 segundo em tempo não escalado** após seu término visual. |
| 10 | Cooldown | **Não existe cooldown genérico de ViewNode ou hotspot.** Repetição é controlada por bloqueio, ação em andamento, nova entrada deliberada e reentrada do cursor. Timers específicos pertencem às mecânicas que os exigirem. |
| 11 | Save | O slot representa sempre o **checkpoint do início do Dia atual**. Dia → Noite usa estado em memória e não sobrescreve o checkpoint. A conclusão da Noite consolida o checkpoint do próximo Dia. |
| 12 | Retomada | Sair, falhar ou fechar o jogo durante Dia ou Noite restaura o início do Dia daquele ciclo. |
| 13 | Persistência | Save contém inventário consolidado, IDs de itens coletados, fatos persistentes, etapa/Dia e metadados. Estado local de cena não é salvo. |
| 14 | Input | Input Manager legado com `StandaloneInputModule` no `EventSystem`. |
| 15 | Domínio de tempo | Dwell e gameplay usam tempo escalado. Transições, margem de 0,05 segundo e UI usam tempo não escalado. Áudio possui regras próprias de pausa e mixagem. |
| 16 | Sobreposição de hotspots | Proibida entre regiões que possam ficar interativas simultaneamente dentro do mesmo ViewNode. É erro de conteúdo e gera warning. |
| 17 | Eventos | Eventos C# entre sistemas; `UnityEvents` apenas para respostas locais e autoradas no Inspector. |
| 18 | Áudio | `SceneAudioController` é a autoridade local de reprodução, continuidade e mixagem. ViewNode define o ambiente-alvo; link define como ocorre a mudança; `TransitionProfile` define apenas o SFX de transição. |

---

## 2. Princípios arquiteturais

1. **Responsabilidade única** — cada classe possui uma função delimitada.
2. **Persistência explícita** — estado de cena, estado de trabalho do ciclo e estado consolidado no save são categorias diferentes.
3. **Hotspot solicita; manager executa** — hotspots nunca trocam ViewNode, alteram inventário, encerram período ou carregam cena diretamente.
4. **Apresentação não decide regra** — visuais e sons representam o estado; não são a fonte de verdade do gameplay.
5. **Dados fixos em ScriptableObjects** — definições de itens, condições, feedback, transição, câmera e áudio podem ser reutilizadas sem carregar estado de sessão.
6. **Estado mutável fora de ScriptableObjects** — condições e perfis não armazenam progresso, resultado de avaliação ou referências transitórias da cena.
7. **Comportamento previsível** — uma solicitação por vez, sem fila de navegação, bloqueio total durante transições e reentrada obrigatória.
8. **Atualização no ponto de uso** — ViewNode é integralmente resolvido ao entrar; toda ação é revalidada imediatamente antes da execução.
9. **Sistemas lógicos continuam fora de tela** — ameaças, equipamentos e áudio persistente não dependem da ativação visual de um ViewNode.
10. **Sem abstração antecipada desnecessária** — Event Channels, Runtime Variables, carregamento dinâmico e sistemas acústicos complexos só serão adicionados quando uma funcionalidade concreta exigir.

---

## 3. Visão geral da estrutura

```text
GameSessionManager (singleton persistente, DontDestroyOnLoad)
├── SaveSystem (classe comum; I/O de checkpoint)
├── Estado de trabalho do ciclo
│   ├── Inventário
│   ├── IDs de itens coletados
│   └── Fatos persistentes ainda não consolidados
├── Slot, etapa e Dia atuais
├── Ferramenta selecionada (transitória; limpa na troca de período)
├── Fluxo global Dia ⇄ Noite
└── Fonte de áudio exclusiva da passagem global entre cenas, se necessária

── Cena Dia / Cena Noite ──
GameplaySceneController
├── NavigationManager
│   ├── ViewNodeController(s)
│   │   ├── composição visual
│   │   ├── camadas de parallax
│   │   ├── elementos locais
│   │   └── hotspots
│   └── TransitionController
├── ViewCameraController
│   └── câmera compartilhada e rig de apresentação
├── InteractionManager
├── SceneRuntimeState
├── SceneAudioController
├── InputBlocker
└── ModalUIController
    ├── InventoryPanel
    └── DocumentPanel
```

### 3.1 Regra de dependência

- Hotspots conhecem somente seu manager local, suas condições e seus dados de apresentação.
- Managers locais podem consultar o `GameSessionManager` para inventário, itens coletados, fatos persistentes e ferramenta selecionada.
- `GameSessionManager` não guarda referências para `ViewNodeController`, hotspots, managers ou objetos de cena.
- Fluxos locais preparam a cena para encerramento antes de solicitar ao `GameSessionManager` a troca global.
- Sistemas visuais não alteram diretamente o save.

---

## 4. Camada global, estado de trabalho e checkpoint

### 4.1 Classes globais

| Classe | Tipo | Responsabilidade |
|---|---|---|
| `GameSessionManager` | `MonoBehaviour`, singleton | Mantém slot ativo, etapa/Dia atuais, estado de trabalho do ciclo, inventário, coletados, fatos persistentes, ferramenta selecionada e fluxo global de troca de período. |
| `SaveSystem` | Classe comum | Lê e grava JSON em `Application.persistentDataPath`. Não contém regra de gameplay. |
| `GameSaveData` | `[Serializable]` | Representação completa de um checkpoint diário. |
| `InventoryEntry` | `[Serializable]` | ID de item e quantidade consolidada ou de trabalho. |
| `ItemDefinition` | `ScriptableObject` | ID estável, nome, ícone, descrição, categoria e propriedades fixas de item/ferramenta. |

### 4.2 Conteúdo mínimo do checkpoint

O `GameSaveData` deve representar o início de um Dia e conter, no mínimo:

- versão do formato de save;
- slot ativo e metadados de exibição;
- ID da etapa;
- ID ou índice do Dia;
- inventário no início do Dia;
- IDs de itens coletados em ciclos já consolidados;
- IDs de fatos persistentes já consolidados;
- informações adicionais estritamente necessárias para reconstruir o início do Dia.

O checkpoint não contém:

- ViewNode atual;
- período Noite como ponto de retomada;
- `SceneRuntimeState`;
- ameaças ativas;
- tempo interno do período;
- dwell;
- ferramenta selecionada;
- estado de transições;
- mídia ou áudio em reprodução;
- estado local de hotspots;
- alterações ainda não consolidadas do ciclo atual.

### 4.3 Categorias de estado

| Categoria | Exemplos | Sobrevive à troca Dia → Noite? | Sobrevive ao fechamento do jogo? |
|---|---|---:|---:|
| Estado local de cena | ViewNode, porta aberta temporariamente, rádio ligado, dwell, ameaças da cena | Não, salvo conversão explícita | Não |
| Estado de trabalho do ciclo | Itens coletados no Dia, recursos preparados, fatos que alteram a Noite | Sim, em memória | Não |
| Checkpoint consolidado | Inventário inicial do Dia, coletados anteriores, fatos de ciclos concluídos | Sim | Sim |
| Dados fixos | `ItemDefinition`, condições, perfis | Sim | Sim; não são progresso |

### 4.4 Inventário, itens coletados e fatos persistentes

- **Inventário** representa o que o jogador possui no estado de trabalho atual.
- **IDs de itens coletados** impedem que um item reapareça depois de coletado ou consumido.
- **Fatos persistentes** representam decisões ou descobertas que não são itens.

Exemplos de fatos:

- `KitchenWindowReinforced`;
- `GeneratorRepaired`;
- `BackDoorTrapPrepared`;
- `RadioCodeDiscovered`;
- `BasementIgnored`.

Fatos iniciais são booleanos e identificados por IDs estáveis. Estados com quantidade ou nível só serão adicionados quando o GDD exigir uma consequência que não possa ser representada por inventário ou fato booleano.

### 4.5 Estado de trabalho do ciclo

Ao carregar o checkpoint, o `GameSessionManager` cria a cópia de trabalho usada pelo Dia e pela Noite.

Durante o ciclo:

- alterações afetam a cópia em memória;
- a cena da Noite consulta essa cópia;
- o checkpoint em disco permanece inalterado;
- reiniciar ou sair descarta a cópia e recarrega o checkpoint.

Não é necessário um sistema de transações separado: o arquivo em disco é o checkpoint; o estado do singleton é a cópia de trabalho.

### 4.6 Início de novo jogo ou novo Dia

1. Determinar etapa e Dia iniciais.
2. Montar o estado inicial consolidado.
3. Gravar o checkpoint diário.
4. Carregar a cena do Dia.
5. Ativar o ViewNode inicial fixo definido pela cena.

### 4.7 Fluxo Dia → Noite

1. A cena do Dia bloqueia entrada e encerra interações pendentes.
2. Resultados que devem afetar a Noite já devem estar no inventário ou nos fatos da cópia de trabalho do `GameSessionManager`.
3. Estado exclusivamente local permanece no `SceneRuntimeState` e será descartado.
4. A ferramenta selecionada é limpa.
5. `SceneAudioController` faz a saída controlada do áudio do Dia.
6. A cena solicita ao `GameSessionManager` a passagem global.
7. O efeito global é apresentado e a cena da Noite é carregada.
8. A Noite consulta o estado de trabalho existente em memória.
9. O checkpoint em disco não é sobrescrito.

### 4.8 Fluxo Noite → próximo Dia

1. A Noite é concluída.
2. Resultados que devem continuar são consolidados no estado de trabalho.
3. Estados temporários da Noite são descartados.
4. É preparado o estado inicial do próximo Dia.
5. `SaveSystem` grava o novo checkpoint com segurança.
6. Somente após sucesso da gravação, a passagem global prossegue.
7. A cena do próximo Dia é carregada no ViewNode inicial.

Se o save falhar, o avanço é interrompido e o jogador recebe opção de tentar novamente ou voltar ao menu. O checkpoint anterior não deve ser corrompido nem substituído por dados incompletos.

### 4.9 Saída, falha e retomada

- Sair durante o Dia ou a Noite descarta o estado de trabalho ainda não consolidado.
- Falhar durante a Noite retorna ao checkpoint do início daquele Dia.
- Carregar o slot sempre abre a cena do Dia definida no checkpoint.
- O jogador deve ser avisado de que sair perde o progresso desde o início do Dia.

Texto de referência para UI:

> O progresso é consolidado ao concluir a Noite. Sair agora fará você retornar ao início do Dia atual.

---

## 5. Camada de cena

### 5.1 Classes locais

| Classe | Tipo | Responsabilidade |
|---|---|---|
| `GameplaySceneController` | `MonoBehaviour` | Raiz da cena; valida referências; coordena boot e encerramento local. |
| `GameplaySceneDefinition` | `ScriptableObject` | ID da cena, etapa, período, ViewNode inicial e configurações gerais. |
| `SceneRuntimeState` | Classe comum | Flags e alterações temporárias da cena; emite eventos de mudança. |
| `NavigationManager` | `MonoBehaviour` | Mantém o ViewNode apresentado e coordena solicitações de navegação. |
| `TransitionController` | `MonoBehaviour` | Apresenta as fases visuais do `TransitionProfile` em tempo não escalado. |
| `ViewCameraController` | `MonoBehaviour` | Aplica perfil de câmera, pan, tilt, parallax, recentralização e shake. |
| `InteractionManager` | `MonoBehaviour` | Valida e executa interações, uso de ferramentas e resultados. |
| `SceneAudioController` | `MonoBehaviour` | Autoridade local de ambiente, perspectiva acústica, equipamentos, ameaças e crossfades. |
| `InputBlocker` | Classe comum ou `MonoBehaviour` | Mantém bloqueios por motivo e informa o estado de entrada de gameplay. |
| `ModalUIController` | `MonoBehaviour` | Abre e fecha modais e aplica bloqueio de gameplay/mixagem apropriada. |

### 5.2 Sequência de boot da cena

1. `GameplaySceneController` obtém e valida managers locais.
2. Valida a `GameplaySceneDefinition` e a existência de um único ViewNode inicial.
3. Confirma que o período da cena corresponde ao fluxo solicitado.
4. Mantém todos os ViewNodes fora do estado apresentado.
5. Configura o `SceneRuntimeState` inicial.
6. Configura o áudio-base da cena.
7. Bloqueia entrada por motivo de boot.
8. Prepara e apresenta o ViewNode inicial.
9. Resolve estado visual, condições, câmera e áudio do ViewNode.
10. Aplica a regra de reentrada para hotspots sob o cursor.
11. Libera o bloqueio após a apresentação inicial.

### 5.3 InputBlocker

O `InputBlocker` usa contagem de motivos. Um desbloqueio só ocorre quando todos os motivos anteriormente adicionados foram removidos.

Motivos previstos:

- boot;
- transição;
- modal;
- pausa;
- cutscene;
- encerramento de período.

Enquanto o gameplay estiver bloqueado:

- hotspots não processam hover, dwell ou clique;
- dwell ativo é cancelado e zerado;
- solicitações de navegação ou interação são descartadas;
- não existe fila;
- UI autorizada para o motivo atual continua funcionando.

O `InputBlocker` não desativa globalmente o `EventSystem`. Um modal bloqueia o cenário, mas seus próprios botões e itens permanecem interativos.

Ao liberar qualquer bloqueio, hotspots sob o cursor exigem saída física e nova entrada.

---

## 6. ViewNodes e ciclo de vida

### 6.1 Representação

Cada ViewNode possui:

- uma raiz `ViewNodeController`, sempre ativa enquanto a cena existir;
- filhos visuais e funcionais controlados;
- uma `ViewNodeDefinition` com dados fixos;
- hotspots locais;
- camadas de composição e parallax;
- respostas autoradas de entrada e saída.

Somente um ViewNode está **logicamente apresentado** por vez. “Ativo” na linguagem de design significa apresentado, não necessariamente `activeSelf` da raiz.

### 6.2 ViewNodeDefinition

A definição contém:

- ID estável;
- nome de debug;
- referência ao perfil de câmera;
- referência ao perfil acústico;
- metadados necessários para autoria e diagnóstico.

A definição não contém:

- estado de interação;
- resultado de condição;
- progresso de dwell;
- referências transitórias de managers;
- estado de ameaça;
- flags mutáveis.

### 6.3 Entrada em um ViewNode

A preparação acontece enquanto o gameplay está bloqueado:

1. Ativar ou preparar os filhos do destino sem permitir raycast.
2. Consultar `SceneRuntimeState`, inventário e fatos relevantes.
3. Resolver camadas, objetos removidos e alterações visuais.
4. Sincronizar representações de equipamentos e ameaças.
5. Avaliar todas as condições de todos os hotspots do ViewNode.
6. Atualizar apresentação de disponível, bloqueado, pista ou oculto.
7. Aplicar o `ViewCameraProfile`.
8. Aplicar o `ViewAudioProfile` conforme a regra do link.
9. Marcar hotspots sob o cursor como “exige saída”.
10. Emitir `OnNodeEnter` para respostas locais já coerentes com o estado.
11. Manter raycasts de gameplay bloqueados até a conclusão da transição.

Essa avaliação integral é obrigatória porque o ViewNode pode ter permanecido fora da apresentação enquanto inventário e flags mudavam.

### 6.4 Saída de um ViewNode

1. Bloquear entrada, se ainda não estiver bloqueada.
2. Cancelar dwell e impedir novas solicitações.
3. Remover os hotspots da participação efetiva no raycast.
4. Emitir `OnNodeExit`.
5. Encerrar apenas efeitos locais que não devem persistir.
6. Desativar os filhos visuais e funcionais.
7. Preservar estado lógico no `SceneRuntimeState` ou no sistema proprietário.

### 6.5 Estado visual compartilhado

Uma alteração direta no visual é permitida somente quando a representação é exclusiva daquele ViewNode e não precisa sobreviver a sua saída.

Quando um objeto aparece em mais de um ponto de visão ou precisa permanecer alterado:

- a interação modifica `SceneRuntimeState`;
- cada ViewNode possui sua própria representação;
- a representação é resolvida ao entrar;
- o GameObject visual nunca é a única fonte de verdade.

### 6.6 Estado de animação

- Animações narrativas de uso único dependem de uma flag de estado.
- Animações de equipamentos contínuos devem sincronizar com o sistema do equipamento ou com o tempo de gameplay, não apenas reiniciar ao entrar.
- Animações decorativas podem reiniciar quando o ViewNode é apresentado.
- Ameaças não podem congelar porque sua representação visual foi desativada.

### 6.7 Memória

Desativar filhos reduz renderização e atualização, mas não garante descarregamento de texturas e demais assets referenciados pela cena.

A arquitetura atual será mantida enquanto respeitar o orçamento de memória do vertical slice. Carregamento dinâmico, cache ou Addressables só serão considerados se o profiling demonstrar necessidade.

---

## 7. Câmera, tilt e parallax

### 7.1 Modelo de câmera

Existe uma câmera compartilhada para todos os ViewNodes. “Câmera estática” significa que a navegação não ocorre movendo fisicamente a câmera entre ambientes. A câmera ainda pode receber efeitos locais.

Responsabilidades conceituais do rig:

- posição-base;
- deslocamento e tilt pelo mouse;
- shake de gameplay;
- zoom;
- composição final pela câmera.

### 7.2 ViewCameraProfile

Cada ViewNode pode referenciar um perfil contendo:

- ativação de movimento pelo mouse;
- deslocamento horizontal e vertical máximo;
- roll máximo;
- suavização;
- dead zone;
- intensidade global de parallax;
- zoom ou tamanho ortográfico;
- intensidade máxima de shake;
- comportamento de recentralização;
- margem de overscan esperada.

A implementação inicial usa câmera ortográfica, pan em X/Y, pequeno roll e parallax manual. Yaw, pitch e perspectiva real só serão usados em ViewNodes especiais se houver necessidade visual comprovada.

### 7.3 Parallax

Camadas do ViewNode possuem multiplicadores de movimento:

- fundo: deslocamento baixo;
- arquitetura: baixo a médio;
- objetos principais: médio;
- primeiro plano: alto;
- UI global: nenhum.

A arte precisa possuir overscan suficiente para que o maior deslocamento permitido não revele bordas vazias.

### 7.4 Hotspots e espaço de coordenadas

- Hotspot de objeto acompanha exatamente a camada visual do objeto.
- Hotspot de navegação abstrata pode permanecer fixo na tela.
- Composição e hotspots que se movem devem compartilhar o mesmo espaço de transformação.
- A UI global e modal permanece em Canvas separado e estável.
- Um Canvas `Screen Space - Overlay` não deve representar hotspots que precisam acompanhar movimento de câmera ou parallax, salvo compensação explícita.

A movimentação da câmera ou da composição não deve, sozinha, satisfazer a regra de reentrada. A reentrada exige saída física do cursor da região após o desbloqueio.

### 7.5 Câmera durante transição

- Resposta ao mouse é suspensa durante a fase de ocultação.
- A câmera recentraliza ou interpola conforme o perfil.
- O perfil do destino é aplicado no ponto de troca.
- A resposta ao mouse retorna após a revelação.
- Shake pode continuar apenas se for parte autorada da transição.

### 7.6 Acessibilidade

Configurações futuras devem permitir reduzir ou desativar:

- movimento pelo mouse;
- camera shake;
- roll;
- intensidade de distorção;
- flashes.

---

## 8. Navegação e transições

### 8.1 Classes e dados

| Elemento | Tipo | Responsabilidade |
|---|---|---|
| `NavigationManager` | `MonoBehaviour` | ViewNode apresentado, validação, troca e coordenação com bloqueio/transição. |
| `ViewNodeController` | `MonoBehaviour` | Ciclo de vida e ativação dos filhos de um ponto de visão. |
| `ViewNodeDefinition` | `ScriptableObject` | Dados fixos do ponto de visão. |
| `NavigationHotspot` | `MonoBehaviour` | Detecta intenção e solicita navegação. |
| `NavigationLinkDefinition` | `[Serializable]` | Destino, `TransitionProfile`, modo de áudio e eventual configuração especial do link. |
| `TransitionProfile` | `ScriptableObject` | Efeito visual, duração das fases, intensidade, SFX e momento do SFX. |
| `TransitionController` | `MonoBehaviour` | Executa ocultação, sinaliza ponto de troca e executa revelação. |

### 8.2 Responsabilidade do TransitionProfile

O perfil define:

- tipo de efeito: corte, fade, glitch VHS, ruído ou especial;
- duração de ocultação;
- ponto de troca;
- duração de revelação;
- intensidade e parâmetros visuais;
- SFX da transição;
- momento de início do SFX.

O perfil não define ambiente permanente do destino e não possui tempo de bloqueio independente. O bloqueio cobre automaticamente todo o fluxo, incluindo a margem final de 0,05 segundo.

### 8.3 Sequência completa de navegação

1. `NavigationHotspot` verifica bloqueio, condição e destino básico.
2. Solicita navegação ao `NavigationManager`.
3. O manager revalida destino configurado, destino diferente do atual e ausência de transição em andamento.
4. Solicitação inválida é descartada e gera warning apropriado.
5. `InputBlocker` adiciona o motivo transição.
6. Dwell é cancelado e raycasts de gameplay deixam de produzir ações.
7. `TransitionController` inicia o perfil em tempo não escalado.
8. O SFX de transição começa no momento definido.
9. O áudio atual inicia a política do link.
10. A fase de ocultação cobre o ViewNode atual.
11. No ponto de troca, ocorre `OnNodeExit` e os filhos atuais são desativados.
12. O manager atualiza a referência de ViewNode apresentado.
13. Os filhos do destino são ativados sem entrada.
14. Estado visual, condições, câmera e áudio do destino são resolvidos.
15. `OnNodeEnter` é emitido.
16. A fase de revelação apresenta o destino.
17. A transição visual termina.
18. O bloqueio permanece por mais 0,05 segundo em tempo não escalado.
19. O motivo transição é removido do `InputBlocker`.
20. Hotspots sob o cursor continuam exigindo saída e nova entrada.

### 8.4 Modos de ativação do NavigationHotspot

| Modo | Comportamento |
|---|---|
| **Hover imediato** | Solicita navegação na primeira entrada válida do cursor. |
| **Hover com permanência** | Inicia dwell em tempo escalado; sair, bloquear ou perder condição cancela e zera. |
| **Clique** | Exige um novo clique válido dentro da região. |

O primeiro vertical slice utiliza apenas hover imediato, mas o campo de modo já existe na base.

### 8.5 Ausência de cooldown

Não existe cooldown por ViewNode ou hotspot.

A proteção contra repetição é composta por:

- transição em andamento;
- bloqueio integral;
- descarte de solicitações concorrentes;
- margem de 0,05 segundo;
- reentrada obrigatória;
- exigência de novo clique quando aplicável;
- bloqueio da ação enquanto ela estiver em execução.

Se uma mecânica exigir tempo de recarga, esse timer pertence ao equipamento ou sistema correspondente, não ao hotspot genérico.

### 8.6 Regras fixas

- Não existe histórico automático de navegação.
- Retorno é sempre um `NavigationHotspot` explícito.
- Não existe fila de navegação.
- Navegação para o próprio ViewNode é inválida.
- Hotspot de destino inativo ou inválido não altera o ViewNode atual.
- Erro durante transição restaura ou mantém o ViewNode anterior e sempre libera o motivo de bloqueio.

---

## 9. Hierarquia e comportamento de hotspots

### 9.1 HotspotBase

`HotspotBase` é abstrata e possui três subclasses principais:

- `NavigationHotspot`;
- `InteractionHotspot`;
- `ToolHotspot`.

A base controla:

- estado habilitado/desabilitado;
- participação no raycast;
- entrada e saída do cursor;
- bloqueio de gameplay;
- modo de ativação;
- dwell;
- condições Todas/Qualquer;
- apresentação quando indisponível;
- repetição: uso único ou repetível;
- estado de uso durante a cena;
- flag “exige saída”;
- perfil de feedback;
- eventos comuns de sucesso e falha.

A base não conhece regras específicas de navegação, inventário, documentos ou ferramentas.

### 9.2 Detecção

- Canvas e `EventSystem` com `StandaloneInputModule`.
- Regiões definidas por `RectTransform`.
- Eventos de ponteiro para entrada, saída e clique.
- Apenas hotspots do ViewNode apresentado participam efetivamente de interação.
- Hotspots em camadas móveis acompanham a transformação visual correspondente.

### 9.3 Repetição

- **Uso único:** após sucesso, permanece consumido durante a cena ou conforme estado proprietário.
- **Repetível:** aceita nova ativação deliberada depois que a execução anterior terminar e as regras de reentrada/clique forem satisfeitas.
- Não existe repetível com cooldown genérico.

### 9.4 Prioridade de indisponibilidade

Quando mais de um estado se aplica, a prioridade conceitual é:

1. bloqueio global;
2. ViewNode não apresentado;
3. ação em andamento;
4. uso único já consumido;
5. condição não atendida;
6. disponível.

### 9.5 Sobreposição

A verificação compara apenas hotspots que possam ficar ativos ao mesmo tempo:

- mesmo ViewNode;
- mesmo contexto de gameplay;
- regiões com raycast simultâneo.

Não compara:

- hotspots de ViewNodes diferentes;
- UI modal com hotspots do cenário;
- elementos que nunca ficam interativos simultaneamente.

---

## 10. Interações e ferramentas

### 10.1 InteractionHotspot

Usado para:

- examinar;
- coletar;
- ler;
- abrir;
- operar equipamento;
- iniciar interface específica;
- executar consequência narrativa.

Ações são descritas por `InteractionDefinition` e resultados. Deve-se evitar uma única classe com dezenas de opções condicionais no Inspector.

### 10.2 Fluxo de interação

1. Hotspot detecta intenção válida.
2. Solicita ao `InteractionManager`.
3. Manager verifica bloqueio, ação em andamento e condições.
4. Realiza validação final.
5. Considera a ação comprometida após a validação final.
6. Executa resultados pelos sistemas responsáveis.
7. Atualiza `SceneRuntimeState`, inventário ou fatos quando necessário.
8. Emite feedback e eventos.
9. Libera o estado de ação em andamento.

Uma segunda solicitação durante a execução é descartada. Isso é controle de concorrência, não cooldown.

### 10.3 InteractionResult

Consequências previstas:

- entregar ou remover item;
- registrar item coletado;
- alterar flag temporária;
- registrar fato persistente na cópia de trabalho;
- remover ou trocar representação visual;
- solicitar navegação;
- solicitar abertura de modal;
- solicitar encerramento do período.

O resultado descreve a consequência, mas respeita a autoridade dos managers. Navegação é solicitada ao `NavigationManager`; troca de período é solicitada ao fluxo global; inventário é alterado pelo sistema proprietário.

### 10.4 ToolHotspot

Fluxo:

1. Jogador abre o inventário.
2. Seleciona uma ferramenta, registrada de forma transitória no `GameSessionManager`.
3. Se `destacaAlvosValidos` estiver habilitado, alvos compatíveis do ViewNode atual recebem destaque sutil.
4. Clique no `ToolHotspot` solicita uso ao `InteractionManager`.
5. Manager valida bloqueio, condições e compatibilidade.
6. Em sucesso, executa resultados e consome carga somente se a definição da ferramenta determinar.
7. Em falha, apresenta feedback genérico, não consome e mantém a seleção.
8. A falha não revela qual seria a ferramenta correta.

A ferramenta selecionada é limpa na troca Dia → Noite e Noite → Dia, e não faz parte do checkpoint.

---

## 11. Sistema de condições

### 11.1 Modelo

`HotspotConditionSO` é um `ScriptableObject` abstrato e sem estado mutável de runtime.

Condições iniciais:

- `HasItemCondition`;
- `RuntimeFlagCondition`;
- `PersistentFactCondition`;
- `PeriodCondition`;
- `InteractionDoneCondition`.

Condições futuras, somente quando os sistemas existirem:

- `ThreatStateCondition`;
- `ResourceMinimumCondition`;
- `EquipmentWorkingCondition`.

### 11.2 Composição

Cada hotspot utiliza:

- **Todas:** todas as condições precisam ser verdadeiras;
- **Qualquer:** ao menos uma condição precisa ser verdadeira.

Não existem grupos aninhados na primeira implementação.

### 11.3 Avaliação

- Avaliação integral ao preparar o ViewNode.
- Atualização por eventos enquanto o ViewNode estiver apresentado.
- Eventos relevantes: inventário, fatos, flags e estados de sistemas futuros.
- Sem polling por frame.
- Validação final imediatamente antes da execução.
- Perda de condição durante dwell cancela e zera o progresso.
- Depois da validação final e do compromisso da ação, mudanças posteriores não interrompem retroativamente a ação já confirmada.

### 11.4 Apresentação quando indisponível

| Modo | Comportamento |
|---|---|
| **Oculto** | Não participa da comunicação visual. Usado para segredos e conteúdo que não deve ser sinalizado. |
| **Visível bloqueado** | Indica que existe algo, sem explicar a solução. |
| **Visível com pista** | Comunica motivo ou requisito quando o puzzle precisa orientar. |

---

## 12. Feedback e configurações globais

| Elemento | Tipo | Conteúdo |
|---|---|---|
| `HotspotFeedbackProfile` | `ScriptableObject` | Cursor por estado, indicador de dwell, hover, sons de entrada/ativação/falha/bloqueio e apresentação de condição não atendida. |
| `GlobalHotspotSettings` | `ScriptableObject` | Dwell padrão, margem pós-transição de 0,05 segundo, perfis default de feedback e transição, política padrão de repetição. |

Não existe configuração de cooldown.

O hotspot informa mudanças de estado; o sistema de feedback apresenta. Gameplay não espera um efeito local terminar, salvo quando uma transição global explicitamente bloqueia a entrada.

### 12.1 Autoridade de sons de feedback

- `HotspotFeedbackProfile`: sons abstratos de hover, dwell, bloqueio, confirmação ou falha de interface.
- `InteractionDefinition`: som diegético do resultado, como porta, ferramenta ou coleta.
- `TransitionProfile`: SFX da troca de ViewNode.
- `UnityEvents`: apenas reação sonora única não coberta pelos perfis.

Um mesmo acontecimento não deve reproduzir sons equivalentes por mais de uma origem.

---

## 13. Arquitetura de áudio

### 13.1 Objetivos

O áudio deve:

1. sustentar a atmosfera contínua da casa;
2. representar espaços fora do enquadramento;
3. comunicar estado de ameaças e equipamentos;
4. reforçar rádio, fitas e corrupção analógica;
5. preservar incerteza sem esconder acidentalmente informações críticas.

A arquitetura não depende de um cenário 3D real. Direção e distância podem ser autoradas por pan, volume, filtros, reverberação e perfis por ViewNode.

### 13.2 Autoridades

| Elemento | Autoridade |
|---|---|
| `GameplaySceneDefinition` / cena | Ambiente-base do período. |
| `ViewNodeDefinition` | Referência ao perfil acústico de estado estável. |
| `ViewAudioProfile` | Perspectiva acústica do ponto de visão. |
| `NavigationLinkDefinition` | Forma de transição entre o áudio atual e o destino. |
| `TransitionProfile` | SFX e timing do efeito de navegação, não ambiente persistente. |
| `SceneAudioController` | Reprodução, continuidade, crossfade, filtros, prioridade e mixagem local. |
| `HotspotFeedbackProfile` | Feedback abstrato de hotspot. |
| `InteractionDefinition` | Som diegético de interação. |
| `GameSessionManager` | Som estritamente necessário para atravessar o carregamento global, sem gerenciar áudio de cena. |

### 13.3 Escopos de áudio

#### Ambiente contínuo da cena

Exemplos:

- chuva;
- vento;
- ruído estrutural;
- rede elétrica;
- camada tonal do Dia ou Noite.

É mantido pelo `SceneAudioController` e não reinicia a cada ViewNode.

#### Perspectiva do ViewNode

O ViewNode altera como o ambiente é ouvido:

- volume;
- pan;
- abafamento;
- reverberação;
- presença de camadas locais;
- intensidade de interferência.

#### Som local do ViewNode

Existe apenas enquanto aquele ponto é apresentado:

- goteira próxima;
- lâmpada visível;
- objeto balançando;
- detalhe de primeiro plano.

Entra e sai por fade curto quando necessário.

#### Equipamento persistente

Continua ao mudar de ViewNode:

- rádio;
- gerador;
- exaustor;
- telefone;
- alarme;
- fita em reprodução.

Não pode depender dos filhos desativados do ViewNode. Seu estado pertence ao sistema do equipamento ou ao `SceneRuntimeState`, e sua reprodução é mantida pelo `SceneAudioController`.

#### Ameaças e eventos do mundo

Sinais de ameaça independem do ViewNode visual ativo. O sistema lógico informa origem e estado; o `SceneAudioController` apresenta o sinal segundo o ponto de visão atual.

#### Media

Rádio e fitas possuem grupo próprio para filtros, compressão, saturação, ruído e foco durante modais.

#### UI e transição

Feedback de cursor, inventário e transições permanece separado do mundo para permitir controle de volume e mixagem.

### 13.4 ViewAudioProfile

O perfil pode definir:

- zona acústica;
- intensidade do ambiente-base;
- pan e volume das camadas;
- abafamento e reverberação;
- camadas locais permitidas;
- sons aleatórios daquele ponto;
- equipamentos próximos;
- perspectiva de fontes persistentes;
- intensidade de ruído/interferência.

O perfil contém configuração fixa e não mantém fontes tocando ou estado de runtime.

### 13.5 Modos de áudio do NavigationLink

| Modo | Regra |
|---|---|
| **Manter** | Não reinicia o ambiente. Interpola somente perspectiva, volume, pan e filtros. |
| **Crossfade** | Reduz ambiente atual e introduz ambiente de destino durante a transição. |
| **Imediato** | Troca abrupta e intencional, apropriada para câmera, monitor ou corte de fita. |
| **Especial** | Comportamento autorado executado pelo `SceneAudioController`. |

Se o link não especificar um modo, utiliza o padrão definido pela cena ou configuração global.

### 13.6 Sequência de áudio na navegação

1. Entrada é bloqueada.
2. `TransitionProfile` inicia seu SFX, se houver.
3. `SceneAudioController` inicia o modo do link.
4. Durante ocultação, áudio atual é mantido, interpolado ou reduzido.
5. No ponto de troca, o perfil acústico do destino é aplicado.
6. Durante revelação, a nova perspectiva ou ambiente entra.
7. O crossfade termina junto ou imediatamente após a transição visual.
8. A margem de 0,05 segundo não cria novo cooldown de áudio; ela apenas mantém o input bloqueado.

### 13.7 Perspectiva de ameaças

A apresentação inicial pode utilizar relações autoradas em vez de acústica 3D complexa:

- esquerda;
- direita;
- centro;
- atrás;
- próximo;
- médio;
- distante;
- abafado.

A mesma ameaça pode ser ouvida de forma diferente conforme o ViewNode atual. A orientação principal é determinada pelo ViewNode, não pela posição exata do mouse.

### 13.8 Mixer sugerido

```text
Master
├── World
│   ├── Ambience
│   ├── Threats
│   ├── Interactions
│   └── Equipment
├── Media
│   ├── Radio
│   └── Tapes
├── Transition
├── UI
└── Music
```

### 13.9 Prioridade e inteligibilidade

- Sinais críticos devem permanecer reconhecíveis.
- Ambiência pode ser reduzida temporariamente quando um sinal mecânico importante tocar.
- Ruídos aleatórios não devem imitar perfeitamente uma pista crítica, salvo quando a ambiguidade for deliberada e testada.
- Volume não deve ser o único diferencial entre níveis de perigo; ritmo, timbre, pan e repetição também comunicam estado.
- Efeitos VHS podem degradar pistas, mas não torná-las sistematicamente impossíveis de compreender.

### 13.10 Randomização

Sons aleatórios devem definir:

- intervalo mínimo e máximo;
- prevenção de repetição imediata;
- limite de simultaneidade;
- variações discretas de volume e pitch;
- possibilidade ou proibição de tocar durante sinal crítico;
- disponibilidade por Dia, Noite, ViewNode ou estado.

### 13.11 Modais

- Abrir modal bloqueia hotspots do cenário, mas não necessariamente pausa o mundo.
- Inventário e documentos podem reduzir discretamente o ambiente.
- Fita ou rádio em foco realçam `Media` e reduzem grupos concorrentes.
- Se o gameplay continuar, sinais de ameaça continuam audíveis.
- Fechar modal restaura a mixagem anterior e exige reentrada dos hotspots do cenário.

O ato de pausar e o ato de abrir modal são conceitos separados. Um modal só altera `timeScale` se o GDD declarar explicitamente que ele pausa o gameplay.

### 13.12 Pausa

- Não gerar novos sinais de ameaça enquanto o jogador não puder reagir.
- Sons de UI continuam.
- Ambiente pode continuar reduzido ou filtrado.
- Transições já iniciadas continuam, conforme a regra de tempo não escalado.
- O comportamento de rádio e fita depende de serem mídia de UI ou equipamento do mundo.

### 13.13 Passagem entre cenas

Dia → Noite:

1. `SceneAudioController` faz fade ou encerramento controlado.
2. A cena solicita a passagem global.
3. Um SFX que precise atravessar o carregamento pode usar a fonte persistente do fluxo global.
4. A nova cena inicia seu ambiente por fade.

Não existe um segundo singleton de áudio.

### 13.14 Movimento de câmera

Tilt e parallax não reposicionam significativamente sinais de gameplay. Pequenos movimentos podem modular sutilmente um som decorativo, mas uma pista não deve mudar de lado porque o jogador moveu o cursor.

### 13.15 Acessibilidade de áudio

Planejar:

- volumes separados por categorias principais;
- legendas ou indicadores opcionais para sinais críticos;
- opção de redução de faixa dinâmica;
- preservação da leitura mecânica para jogadores com dificuldade auditiva.

---

## 14. Eventos

| Mecanismo | Uso |
|---|---|
| Eventos C# | Comunicação entre managers e sistemas: ViewNode mudou, transição iniciou/terminou, inventário/fato/flag mudou, ferramenta selecionada, bloqueio alterado, hotspot ativado. |
| `UnityEvents` | Respostas locais no Inspector: animação, reação visual, som único e narrativa simples. |

Regras:

- Eventos locais permanecem na cena.
- `GameSessionManager` participa somente de alterações com impacto no estado de trabalho ou fluxo global.
- Objetos que se inscrevem em eventos removem sua inscrição no fim do ciclo de vida apropriado.
- Event Channels e Runtime Variables não fazem parte desta implementação inicial; poderão ser adotados por sistemas futuros se reduzirem dependências reais.

---

## 15. Domínios de tempo

| Sistema | Domínio |
|---|---|
| Dwell e timers de gameplay | Tempo escalado |
| Timers próprios de equipamentos/ameaças | Tempo escalado, salvo regra específica do sistema |
| Transições de ViewNode | Tempo não escalado |
| Margem pós-transição de 0,05 segundo | Tempo não escalado |
| Animações de UI e modais | Tempo não escalado |
| Áudio | Reprodução independente de `timeScale`, controlada por mixagem e regras de pausa |

Não existe cooldown genérico na tabela de tempo.

---

## 16. UI modal

| Classe | Responsabilidade |
|---|---|
| `ModalUIController` | Abre/fecha painéis, adiciona/remove motivo de bloqueio e solicita perfil de mixagem. |
| `InventoryPanel` | Exibe inventário e permite selecionar/cancelar ferramenta. |
| `InventoryItemUI` | Representa um item na interface. |
| `DocumentPanel` | Exibe documentos, imagens, anotações, pistas e mídia apropriada. |

Regras:

- Modal bloqueia todos os hotspots do cenário.
- Controles do próprio modal permanecem funcionais.
- Fechar modal exige reentrada nos hotspots sob o cursor.
- Abrir modal cancela dwell.
- Modal não pausa automaticamente o gameplay.
- Pausa, quando existente, adiciona motivo próprio ao `InputBlocker`.

---

## 17. Diagnóstico e validação

### 17.1 Validação no boot

- referências obrigatórias ausentes;
- `GameplaySceneDefinition` incompatível;
- ausência ou duplicidade de ViewNode inicial;
- IDs duplicados de ViewNode;
- managers locais ausentes;
- câmera ou EventSystem ausente;
- perfil default obrigatório ausente.

### 17.2 Validação de ViewNode e hotspots

- `NavigationHotspot` sem destino;
- destino fora da cena ou inválido;
- navegação para o próprio ViewNode;
- hotspot sem configuração obrigatória;
- hotspot de ViewNode não apresentado tentando executar;
- condição inválida;
- sobreposição dentro do mesmo ViewNode e contexto;
- hotspot móvel sem correspondência com a camada visual quando aplicável.

### 17.3 Validação de áudio

- ViewNode sem perfil acústico quando exigido;
- link sem modo e sem fallback;
- áudio contínuo anexado a filhos que serão desativados;
- duplicidade de ambiente ou SFX;
- equipamento persistente sem autoridade de reprodução definida.

### 17.4 Recuperação de erros

- Erros de conteúdo geram warnings claros com cena, ViewNode e hotspot.
- Solicitação inválida não altera estado.
- Falha durante transição mantém/restaura o ViewNode anterior.
- Todo caminho de erro remove o motivo correspondente do `InputBlocker`.
- Falha de save impede avanço e oferece nova tentativa; não é tratada apenas como warning.

---

## 18. Riscos e mitigações

| Risco | Mitigação |
|---|---|
| Referências do Inspector quebradas | Validação no boot com identificação exata do campo. |
| Duplicação entre cenas Dia/Noite | Prefabs compartilhados para managers, Canvas e UI. |
| Todos os assets de ViewNodes carregados | Profiling de memória no vertical slice; carregamento dinâmico somente se necessário. |
| Scripts de raízes não apresentadas continuam ativos | Raiz contém apenas controle leve; lógica visual/funcional fica nos filhos. |
| ViewNode perde evento enquanto não apresentado | Avaliação integral obrigatória ao entrar. |
| Mesmo objeto aparece diferente em dois ViewNodes | Estado compartilhado no `SceneRuntimeState`; apresentação resolvida ao entrar. |
| Hotspot desalinha com parallax | Hotspot compartilha transformação da camada visual. |
| Hover dispara após transição/modal | Bloqueio, margem de 0,05 segundo e reentrada física. |
| Save sobrescreve progresso antes do ciclo terminar | Checkpoint só é consolidado após conclusão da Noite. |
| Jogador perde progresso sem entender | Aviso explícito de checkpoint diário na UI. |
| Som persistente para ao trocar ViewNode | Reprodução mantida pelo `SceneAudioController`, fora dos filhos controlados. |
| Pista crítica é mascarada | Prioridade de `Threats`, ducking e testes de inteligibilidade. |
| Áudio é reproduzido duas vezes | Autoridade clara entre feedback, interação, transição e ambiente. |
| Pausa congela transição | Transições e margem final em tempo não escalado. |

---

## 19. Lista final de classes e assets

### Globais e save

- `GameSessionManager`
- `SaveSystem`
- `GameSaveData`
- `InventoryEntry`
- `ItemDefinition`

### Cena

- `GameplaySceneController`
- `GameplaySceneDefinition`
- `SceneRuntimeState`
- `InputBlocker`
- `SceneAudioController`

### Navegação e transição

- `NavigationManager`
- `ViewNodeController`
- `ViewNodeDefinition`
- `NavigationHotspot`
- `NavigationLinkDefinition`
- `TransitionProfile`
- `TransitionController`

### Câmera e apresentação

- `ViewCameraController`
- `ViewCameraProfile`
- `ParallaxLayer`

### Hotspots e interação

- `HotspotBase`
- `InteractionManager`
- `InteractionHotspot`
- `ToolHotspot`
- `InteractionDefinition`
- `InteractionResult`

### Condições

- `HotspotConditionSO`
- `HasItemCondition`
- `RuntimeFlagCondition`
- `PersistentFactCondition`
- `PeriodCondition`
- `InteractionDoneCondition`

### Áudio e feedback

- `ViewAudioProfile`
- `HotspotFeedbackProfile`
- `GlobalHotspotSettings`
- assets de `AudioMixer` e seus grupos

### UI

- `ModalUIController`
- `InventoryPanel`
- `InventoryItemUI`
- `DocumentPanel`

---

## 20. Ordem recomendada de implementação

1. `GameSessionManager` com estado de trabalho, sem save inicialmente.
2. `GameplaySceneController`, `GameplaySceneDefinition` e `InputBlocker`.
3. `HotspotBase` com enter/exit, bloqueio, reentrada e modo de ativação.
4. `NavigationManager`, `ViewNodeController` e `NavigationHotspot` com corte seco.
5. `TransitionController` e `TransitionProfile` com ocultação, ponto de troca, revelação e margem de 0,05 segundo.
6. `ViewCameraController`, perfil básico e alinhamento de hotspot; parallax simples.
7. **Meta 1 — navegação:** dois ViewNodes, hover imediato, transição, bloqueio, reentrada, tilt e hotspot alinhado.
8. `SceneRuntimeState` e atualização integral no `OnNodeEnter`.
9. `HotspotConditionSO`, subclasses iniciais, Todas/Qualquer e apresentações de indisponibilidade.
10. `InteractionManager`, `InteractionHotspot`, definições e resultados.
11. `ItemDefinition`, inventário, `ModalUIController` e `InventoryPanel`.
12. `ToolHotspot` com sucesso, falha genérica e destaque opcional.
13. `PersistentFactCondition` e fatos no estado de trabalho.
14. Segunda cena espelhada e transferência Dia → Noite sem salvar checkpoint.
15. `SaveSystem`, checkpoint de início do Dia e consolidação Noite → próximo Dia.
16. **Meta 2 — ciclo completo:** iniciar Dia, coletar/preparar, carregar Noite em memória, concluir Noite, criar checkpoint seguinte e confirmar rollback ao sair antes da conclusão.
17. Dwell e `HotspotFeedbackProfile`; manter ausência de cooldown.
18. `SceneAudioController`, mixer e ambiente contínuo.
19. `ViewAudioProfile` e modos Manter/Crossfade/Imediato.
20. Equipamento persistente e primeiro sinal de ameaça independente do ViewNode.
21. `DocumentPanel`, media, mixagem modal e pausa.
22. Glitch VHS e demais efeitos URP de transição.
23. Profiling de memória, renderização e áudio do vertical slice.

---

## 21. Critérios de aceite

### 21.1 Navegação

- Apenas um ViewNode está logicamente apresentado.
- Somente seus hotspots recebem entrada.
- Raízes de outros ViewNodes não executam lógica visual ou interativa.
- Hover, dwell e clique são modos distintos.
- Dwell é cancelado ao sair, bloquear ou perder condição.
- Solicitações concorrentes são descartadas e não existe fila.
- Transição contém ocultação, ponto de troca e revelação.
- Entrada permanece bloqueada até 0,05 segundo após o término visual.
- Hotspot sob o cursor exige saída e nova entrada.
- Não existe cooldown genérico.
- Erro de transição não deixa o sistema bloqueado.

### 21.2 Câmera e hotspots

- Uma única câmera apresenta todos os ViewNodes.
- Tilt, pan e parallax respeitam o perfil do ViewNode.
- Nenhum efeito revela bordas vazias dentro dos limites definidos.
- Hotspot de objeto acompanha sua camada visual.
- UI global permanece estável.
- Movimento da câmera não satisfaz sozinho a regra de reentrada.

### 21.3 Estado e condições

- ViewNode é integralmente atualizado ao entrar.
- Condições são stateless e reutilizáveis.
- Apenas ViewNode atual reage a eventos, mas todos reavaliam ao entrar.
- Toda ação é revalidada antes da execução.
- Estado compartilhado aparece coerentemente em todos os pontos de visão.
- Interações em andamento não executam duas vezes.

### 21.4 Ferramentas e UI

- Falha de ferramenta não consome e mantém seleção.
- Falha não revela automaticamente a solução.
- Destaque de alvo é opcional por ferramenta.
- Modal bloqueia cenário, mas não seus controles.
- Fechar modal exige reentrada.
- Ferramenta selecionada é limpa na troca de período.

### 21.5 Checkpoint

- Slot sempre carrega no início do Dia registrado.
- Dia → Noite mantém alterações em memória sem sobrescrever o checkpoint.
- Sair durante Dia ou Noite restaura o início do Dia.
- Falhar durante a Noite restaura o mesmo checkpoint.
- Concluir a Noite grava o checkpoint do próximo Dia.
- Inventário, coletados e fatos consolidados são restaurados corretamente.
- Estado local de cena não é restaurado.
- Falha de gravação não destrói o checkpoint anterior.

### 21.6 Áudio

- Ambiente contínuo não reinicia ao alternar pontos da mesma zona.
- Crossfades não produzem cortes ou cliques não intencionais.
- Equipamentos persistentes continuam audíveis fora de seu ViewNode.
- Ameaças produzem sinais sem depender de GameObjects visuais ativos.
- ViewNode altera perspectiva sem exigir acústica 3D real.
- `TransitionProfile` não controla ambiente permanente.
- Feedback, interação e transição não duplicam o mesmo som.
- Modais e pausa aplicam a mixagem prevista.
- Sinais críticos permanecem inteligíveis sob a estética analog horror.
- Áudio de Dia termina de forma controlada e o de Noite entra sem corte acidental.

### 21.7 Arquitetura

- `GameSessionManager` é o único singleton.
- O singleton não mantém referências a objetos de cena.
- Managers locais são referenciados explicitamente.
- ScriptableObjects não armazenam progresso de runtime.
- Erros de conteúdo são diagnosticados sem deixar o jogo permanentemente bloqueado.

---

## 22. Invariantes finais

As seguintes regras não podem ser violadas por implementações específicas:

1. O jogador interage somente com o ViewNode apresentado.
2. Toda navegação passa pelo `NavigationManager`.
3. Toda transição bloqueia o gameplay até sua conclusão e margem final.
4. Reentrada não é substituída pela espera de 0,05 segundo.
5. Não existe cooldown genérico de hotspot.
6. O ViewNode é uma apresentação do estado, não a fonte de verdade do mundo.
7. Condições são atualizadas ao entrar e revalidadas antes da ação.
8. Alterações entre Dia e Noite passam pelo estado de trabalho do `GameSessionManager`.
9. O save em disco representa sempre o início de um Dia.
10. Sons contínuos, equipamentos e ameaças não dependem de filhos visuais desativados.
11. ViewNode define o áudio-alvo; link define a transição; `TransitionProfile` define somente o SFX.
12. Tilt e parallax não podem quebrar o alinhamento dos hotspots.
13. Nenhum erro de conteúdo pode deixar o `InputBlocker` permanentemente preso.
