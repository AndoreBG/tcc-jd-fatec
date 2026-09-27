# Whispers Of Unknown — Especificação de Implementação do Vertical Slice 4
## Áudio autorado por ViewNode, pontos de perigo e painel de DEBUG F8

> **Status:** infraestrutura implementada; validação/compilação no Unity pendente  
> **Branch de referência:** `test`  
> **Commit analisado:** `b4593a8`  
> **Unity:** `6000.3.18f1`  
> **Plataforma inicial:** PC  
> **Linguagem:** C#  
> **Escopo:** Vertical Slice 4 — Áudio, integração e debug em runtime

---

## 0. Como este documento deve ser usado por um AI agent

Este arquivo é o contrato técnico do VS4. O agente que for implementar deve:

1. Trabalhar somente na branch `test`.
2. Preservar as invariantes definidas em:
   - `Whispers_Of_Unknown_Arquitetura_Navegacao_Audio_v2.md`;
   - `Whispers_Of_Unknown_Cartoes_Trello.md`.
3. Manter compatibilidade com Unity `6000.3.18f1`.
4. Não criar um segundo singleton global de áudio.
5. Não mover a autoridade de regras de gameplay para `AudioSource`, `AudioClip` ou ScriptableObjects.
6. Não prender áudio persistente aos filhos visuais de um ViewNode.
7. Não alterar a lógica validada dos VS1, VS2 e VS3 sem necessidade explícita de integração.
8. Implementar primeiro a infraestrutura e as referências de clips; a produção dos assets finais é uma etapa separada. Não criar clips placeholder nesta etapa.
9. Atualizar a documentação e os assets de configuração sempre que um novo ID, grupo ou regra for criado.
10. Validar o comportamento em runtime, e não somente a compilação.

O documento descreve a arquitetura desejada, o modelo de dados, os pontos de integração, o painel F8, a ordem de implementação, os testes e os critérios de aceite.

---

# 1. Decisões confirmadas de produto e arquitetura

As decisões abaixo foram confirmadas antes da implementação.

## 1.1 Espacialização

O primeiro VS4 utilizará **espacialização 2D autorada por ViewNode**.

A direção do som será definida por dados do `ViewAudioProfile`, utilizando principalmente:

- `panStereo`;
- volume;
- distância percebida;
- oclusão/abafamento;
- filtro passa-baixa;
- reverberação;
- variações de pitch ou atraso quando necessário.

O movimento do mouse não deve modificar a direção principal de uma ameaça. O som deve continuar sendo percebido conforme a orientação autorada do mapa e do ViewNode atual.

Não é necessário utilizar acústica 3D real ou `AudioSource.spatialBlend = 1` no primeiro VS4.

## 1.2 Autoria por ViewNode

Cada `ViewAudioProfile` poderá configurar individualmente os pontos sonoros relevantes para aquele ViewNode.

Exemplo:

```text
__VN_CorredorFront
  window_sala: direita, pan +0.75

__VN_CorredorBack
  window_sala: esquerda e atrás, pan -0.70, mais abafado
```

O perfil não armazena referências diretas para objetos `AudioSource`. Ele armazena IDs lógicos e parâmetros fixos.

## 1.3 Entidades e pontos fixos

As entidades de perigo possuirão pontos fixos possíveis, chamados de `anchorId`.

Exemplo:

```text
window_sala
window_quarto
window_corredor
```

Uma entidade pode escolher em qual ponto aparecer, mas só pode estar em **um ponto por vez**.

Exemplo de estado válido:

```text
entity_monster_01
activeAnchorId: window_sala
```

Estado inválido:

```text
entity_monster_01
activeAnchorId: window_sala
activeAnchorId: window_quarto
```

A entidade pode trocar de ponto durante o gameplay, mas não se desloca continuamente pelo mapa no escopo inicial.

## 1.4 Ordem de produção

O primeiro objetivo é implementar o código e a infraestrutura:

- mixer;
- fontes de runtime;
- perfis acústicos;
- navegação;
- ameaças simuladas;
- pontos fixos;
- painel F8;
- diagnóstico.

Os clips finais e a produção sonora completa serão adicionados depois, utilizando a mesma infraestrutura.

---

# 2. Objetivo do Vertical Slice 4

O VS4 deve provar que o jogo possui uma arquitetura sonora contínua e autorável, integrada a:

- ViewNodes;
- navegação;
- transições visuais;
- interações;
- hotspots;
- Backpack;
- documentos;
- Hotbar;
- equipamentos persistentes;
- sinais de ameaça;
- mídia, rádio e fitas;
- pausa;
- troca Dia → Noite;
- painel de testes F8.

O VS4 não é apenas a reprodução de efeitos individuais. Ele deve provar que:

1. O ambiente não reinicia ao trocar de ViewNode da mesma zona.
2. A perspectiva de um ponto de perigo muda conforme o ViewNode atual.
3. Uma entidade pode ser testada em um ponto fixo por vez.
4. Equipamentos e ameaças não dependem de GameObjects visuais ativos.
5. O áudio de navegação é sincronizado com a transição visual.
6. A mixagem de modais e pausa é previsível.
7. Sons críticos continuam inteligíveis sob a ambiência.
8. Todo o áudio pode ser inspecionado e testado individualmente pelo DEBUG F8.

---

# 3. Escopo oficial dos cartões 19 a 25

## Cartão 19 — Estrutura de mixagem e regras

Implementar e documentar:

- grupos do `AudioMixer`;
- autoridade de cada tipo de som;
- regras de prioridade;
- regras de modal e pausa;
- separação entre ambiente, interação, equipamento, ameaça, media, transição e UI;
- prevenção de duplicidade sonora;
- orientação principal por ViewNode.

## Cartão 20 — Ambiente contínuo e perfis acústicos

Implementar:

- `SceneAudioController` local à cena;
- `ViewAudioProfile` como ScriptableObject;
- camadas contínuas de Dia e Noite;
- aplicação de perspectiva ao entrar em um ViewNode;
- fontes de áudio fora dos filhos visuais dos ViewNodes;
- fontes locais e persistentes;
- fallback e diagnóstico de perfil ausente.

## Cartão 21 — Conteúdo de ambiente

Preparar ou suportar:

- ambiente-base;
- exterior;
- estrutura;
- eletricidade;
- detalhes locais;
- sons aleatórios;
- loops sem emendas;
- variações com prevenção de repetição;
- limite de simultaneidade;
- regras de não mascaramento de sinais mecânicos.

No primeiro estágio, os campos de clip ficam disponíveis para atribuição no Inspector. Por decisão de escopo, nenhum clip placeholder é criado nesta etapa.

## Cartão 22 — Integração com navegação

Implementar:

- `NavigationLinkDefinition` com modo de áudio;
- modos `Keep`, `Crossfade`, `Immediate` e `Special`;
- aplicação do áudio no ponto de troca do ViewNode;
- SFX do `TransitionProfile` separado do ambiente permanente;
- sincronização entre áudio e `TransitionController`;
- uso de tempo não escalado para transições e margem final.

## Cartão 23 — Continuidade e estados especiais

Implementar suporte a:

- equipamentos persistentes;
- ameaças independentes da apresentação visual;
- perspectiva por ponto fixo;
- mixagem de Backpack e documentos;
- pausa;
- saída controlada da cena;
- áudio opcional atravessando a troca global.

## Cartão 24 — Sinais e media

Preparar suporte para:

- sinais leve, próximo e crítico;
- som de funcionamento e falha de equipamentos;
- rádio;
- fitas;
- SFX de corte, fade e glitch;
- falsos positivos deliberados;
- roteamento correto para mixer;
- variações suficientes.

## Cartão 25 — Validação do VS4

Validar:

- continuidade;
- crossfade;
- perspectiva por ViewNode;
- pontos de perigo;
- persistência de equipamentos;
- sinais sem GameObjects visuais ativos;
- sincronização visual/sonora;
- ausência de duplicidade;
- mixagem de modais e pausa;
- inteligibilidade;
- troca Dia → Noite.

---

# 4. Auditoria da branch antes desta entrega

A branch de referência possuía a base dos VS1, VS2 e VS3, mas o VS4 ainda não estava implementado. O estado após a implementação está registrado no Apêndice A.

## 4.1 Componentes já existentes

- `GameplaySceneController`;
- `GameplaySceneDefinition`;
- `InputBlocker`;
- `SceneRuntimeState`;
- `NavigationManager`;
- `TransitionController`;
- `TransitionProfile`;
- `ViewNodeController`;
- `ViewNodeDefinition`;
- `InteractionManager`;
- `InteractionDefinition`;
- `HotspotFeedbackProfile`;
- `ModalUIController`;
- `BackpackController`;
- `HotbarController`;
- `GameSessionManager`;
- `CycleTestUI` com abertura em F8.

## 4.2 Recursos de áudio já existentes

`GameplaySceneController` possui um `AudioSource` de feedback e o método:

```csharp
public void PlayFeedback(AudioClip clip)
```

`InteractionDefinition` possui um `AudioClip sfx`.

`HotspotFeedbackProfile` possui clips para:

- hover;
- ativação;
- bloqueio;
- falha.

Esses recursos eram somente uma base de feedback na auditoria inicial; o runtime VS4 agora os mantém como fallback e os encaminha pelo `SceneAudioController` quando disponível.

## 4.3 Lacunas observadas na auditoria inicial

Na referência analisada, ainda não existiam:

- `SceneAudioController`;
- `ViewAudioProfile`;
- `NavigationLinkDefinition`;
- pontos de áudio estáveis;
- estado runtime de entidades sonoras;
- `AudioMixer` do projeto;
- grupos de mixer;
- ambiente contínuo;
- crossfade entre perfis;
- mixagem de modal;
- mixagem de pausa;
- catálogo de debug;
- aba de áudio no F8.

Na auditoria inicial, `ViewNodeDefinition` possuía somente perfil de câmera; a implementação adicionou o perfil de áudio.

Na auditoria inicial, `TransitionProfile` possuía somente configuração visual de corte/fade; a implementação adicionou SFX e momento de reprodução.

Na auditoria inicial, `NavigationHotspot` possuía destino e perfil de transição diretamente; a implementação adicionou `NavigationLinkDefinition` e modo de áudio.

`GameplaySceneController.PrepareLocalShutdown()` já indica que a saída controlada do áudio pertence ao VS4.

---

# 5. Princípios obrigatórios

## 5.1 Um único singleton global

O único singleton global continua sendo:

```text
GameSessionManager
```

Não criar:

- `AudioManager` singleton;
- `GlobalAudioManager`;
- `PersistentAudioManager` separado.

`SceneAudioController` será local à cena.

## 5.2 ScriptableObjects sem estado de runtime

Os seguintes assets contêm somente dados fixos:

- `ViewAudioProfile`;
- `AudioAnchorDefinition`;
- `TransitionProfile`;
- `NavigationLinkDefinition`, caso no futuro seja convertido em asset;
- catálogo de clips.

Eles não podem armazenar:

- `AudioSource` tocando;
- coroutine;
- estado de ameaça;
- entidade ativa;
- tempo decorrido;
- fonte atual;
- referência transitória de cena.

## 5.3 Fonte lógica não é fonte física

Separar sempre:

```text
emitterId  = quem produz o som
anchorId   = onde o som está no mapa
signalId   = qual sinal/clip foi solicitado
AudioSource = voz física de runtime
```

Exemplo:

```text
emitterId: entity_monster_01
anchorId: window_sala
signalId: threat_critical
AudioSource: ThreatSource_01
```

## 5.4 Áudio persistente fora dos ViewNodes

Não colocar fontes persistentes dentro de:

```text
__VN_Sala/Content
__VN_CorredorFront/Content
__VN_CorredorBack/Content
```

Os conteúdos dos ViewNodes podem ser desativados. O áudio contínuo, os equipamentos e as ameaças devem sobreviver a isso.

## 5.5 Sem cooldown de áudio

A margem pós-transição de `0,05` segundo bloqueia input. Ela não cria cooldown de áudio.

---

# 6. Arquitetura de runtime

## 6.1 Hierarquia conceitual

```text
Gameplay
├── GameplaySceneController
├── SceneAudioController
│   ├── ContinuousSources
│   ├── LocalSources
│   ├── EquipmentSources
│   ├── ThreatSources
│   ├── MediaSources
│   ├── TransitionSources
│   ├── UISources
│   └── OneShotPool
├── NavigationManager
├── TransitionController
├── InputBlocker
├── InteractionManager
├── ModalUIController
├── BackpackController
├── HotbarController
└── CycleTestUI
```

A hierarquia física pode ser organizada em GameObjects filhos do objeto `Gameplay`, mas não dentro das raízes de ViewNodes.

## 6.2 Responsabilidades do SceneAudioController

`SceneAudioController` é responsável por:

- inicializar fontes;
- iniciar ambiente da cena;
- manter loops contínuos;
- aplicar `ViewAudioProfile`;
- aplicar overrides por `anchorId`;
- iniciar e parar fontes locais;
- registrar equipamentos persistentes;
- registrar ameaças ativas;
- tocar sinais de ameaça;
- controlar pools de one-shots;
- aplicar crossfade;
- controlar mixagem;
- receber comandos de debug;
- fornecer diagnóstico;
- encerrar o áudio da cena com segurança.

Ele não deve decidir:

- se uma entidade realmente apareceu;
- se o jogador completou uma interação;
- se uma ameaça é verdadeira no gameplay;
- se o Dia terminou.

Essas decisões pertencem aos sistemas de gameplay. O controller apresenta o estado recebido.

---

# 7. Modelo de dados

## 7.1 Enums recomendados

```csharp
public enum AudioTransitionMode
{
    Keep,
    Crossfade,
    Immediate,
    Special
}
```

```csharp
public enum AudioDirection
{
    Center,
    Left,
    Right,
    Front,
    Behind,
    LeftBehind,
    RightBehind
}
```

```csharp
public enum AudioDistance
{
    Near,
    Medium,
    Far
}
```

```csharp
public enum ThreatAudioState
{
    Inactive,
    Light,
    Near,
    Critical
}
```

Os nomes podem ser adaptados à convenção existente, mas os significados devem ser preservados.

## 7.2 AudioAnchorDefinition

Representa um ponto fixo do mapa.

Campos mínimos:

```text
id                 ID estável
 displayName       Nome de autoria/debug
category           Window, Door, Room, Exterior, Other
worldPosition      Opcional, apenas para referência/debug
```

Exemplos:

```text
window_sala
window_quarto
window_corredor
```

O ID não deve depender do nome temporário do GameObject.

## 7.3 AudioPointPerspective

Representa como um ponto é ouvido a partir de um ViewNode.

Campos mínimos:

```text
anchorId
isAudible
direction
distance
pan
volumeMultiplier
occlusion
lowPassFrequency
reverbSend
```

Regras:

- `pan` varia de `-1` a `1`;
- `-1` representa esquerda;
- `0` representa centro;
- `1` representa direita;
- `Behind`, `LeftBehind` e `RightBehind` são convenções autoradas aplicadas por filtros, volume, reverb e pan;
- ausência de entrada usa fallback documentado;
- pontos críticos sem entrada explícita devem gerar warning.

## 7.4 ViewAudioProfile

`ViewAudioProfile` é um ScriptableObject de configuração fixa.

Campos mínimos recomendados:

```text
id
zoneId
baseAmbienceMultiplier
interferenceMultiplier
layerPerspectives[]
pointPerspectives[]
localLayers[]
defaultPerspective
```

`pointPerspectives[]` é a parte que permite configurar individualmente os perigos e entidades.

Exemplo conceitual:

```text
Profile: AUDIO_VN_CorredorFront
Zone: corredor

window_sala:
  direction: Right
  distance: Medium
  pan: 0.75
  volume: 0.85
  occlusion: 0.15

window_quarto:
  direction: Left
  distance: Far
  pan: -0.60
  volume: 0.55
  occlusion: 0.65
```

O perfil não deve conter referência a `AudioSource`.

## 7.5 Estado runtime de entidade sonora

Estrutura mínima:

```text
entityId
activeAnchorId
threatState
isActive
currentSignalId
```

Regras:

- `entityId` é único dentro da cena;
- `activeAnchorId` pode ser vazio quando a entidade está inativa;
- uma entidade possui no máximo um `activeAnchorId`;
- alterar o anchor não deve criar duplicidade de entidade;
- o áudio deve atualizar a perspectiva sem depender da ativação de um GameObject visual.

O primeiro VS4 pode receber esse estado pelo `AudioDebugController`. Depois, o sistema real de ameaças poderá chamar a mesma API.

## 7.6 NavigationLinkDefinition

Deve ser uma classe `[Serializable]`, conforme a arquitetura documentada.

Campos mínimos:

```text
destinationId
transitionProfile
audioMode
specialAudioId, se necessário
```

A definição deve ser usada pelo `NavigationHotspot`, substituindo ou encapsulando os campos atuais de destino e transição.

Se um link não definir `audioMode`, utilizar fallback da cena ou configuração padrão.

## 7.7 TransitionProfile

Adicionar suporte a:

```text
transitionSfx
transitionSfxTiming
```

O timing pode ser:

```text
OnTransitionStart
OnHideStart
OnSwap
OnRevealStart
OnTransitionEnd
```

O `TransitionProfile` continua sem controlar o ambiente permanente. Ele define apenas o efeito visual, o SFX e o timing da transição.

---

# 8. Organização dos AudioSources

O sistema final terá múltiplos `AudioSource`s, mas não um por ViewNode.

## 8.1 Fontes contínuas

Exemplos:

```text
Ambience_Day
Ambience_Night
Exterior
Structure
Electricity
```

Essas fontes:

- normalmente usam loop;
- ficam fora dos conteúdos dos ViewNodes;
- não reiniciam ao trocar de perspectiva;
- recebem apenas novos parâmetros.

## 8.2 Fontes locais

Usadas para:

- goteira;
- lâmpada;
- objetos próximos;
- detalhes de primeiro plano.

Podem usar fontes reutilizáveis com fade curto.

## 8.3 Fontes de equipamentos

Exemplos:

```text
RadioSource
GeneratorSource
VentilationSource
AlarmSource
```

Devem continuar tocando quando a representação visual do equipamento estiver fora do ViewNode atual.

## 8.4 Fontes de ameaças

Usar uma fonte por entidade persistente ou um pool controlado para sinais temporários.

O pool deve controlar:

- limite de simultaneidade;
- prioridade;
- prevenção de interrupção de sinais críticos;
- reutilização de fontes;
- finalização de sinais anteriores.

## 8.5 Fontes de mídia

Manter fontes separadas para rádio e fitas quando forem necessários filtros ou mixagens diferentes.

## 8.6 OneShot e pools

`PlayOneShot` pode ser usado para feedbacks simples, mas não deve ser a única estratégia.

Usar pool quando for necessário:

- fade independente;
- prioridade;
- controle de pan individual;
- múltiplos sons simultâneos;
- interrupção seletiva.

---

# 9. AudioMixer

Criar um asset de mixer, por exemplo em:

```text
Assets/_Project/Audio/AUDIO_VS4.mixer
```

Hierarquia obrigatória:

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

Parâmetros expostos recomendados:

```text
AmbienceVolume
ThreatsVolume
InteractionsVolume
EquipmentVolume
MediaVolume
RadioVolume
TapesVolume
TransitionVolume
UIVolume
MusicVolume
```

Snapshots recomendados:

```text
Normal
Modal
MediaFocus
Paused
Transition
```

A mixagem deve ser controlada pelo `SceneAudioController` ou por um componente interno dele. Não criar um segundo manager global somente para mixer.

Não utilizar `AudioListener.pause` como regra geral para abrir modais. A pausa deve ser aplicada por mixagem e pelas regras de geração de sinais.

---

# 10. Ciclo de vida do áudio

## 10.1 Boot da cena

Sequência recomendada:

1. `GameplaySceneController` valida referências obrigatórias.
2. `SceneAudioController` é localizado pela referência explícita da cena.
3. O controller inicializa mixer, fontes e pools.
4. O ambiente-base do período é iniciado.
5. `NavigationManager` resolve o ViewNode inicial.
6. O `ViewAudioProfile` inicial é aplicado.
7. Hotspots e condições são avaliados.
8. O áudio é liberado após o ViewNode e a cena estarem coerentes.
9. `InputBlockReason.Boot` é removido.

Se um clip estiver ausente, gerar warning e continuar. Falha de áudio não pode deixar o `InputBlocker` permanentemente preso.

## 10.2 Entrada em um ViewNode

Durante a entrada:

1. input continua bloqueado;
2. destino é preparado;
3. `ViewNodeDefinition.audioProfile` é obtido;
4. camadas contínuas recebem a política do link;
5. perspectivas dos anchors ativos são resolvidas;
6. fontes locais entram ou saem;
7. equipamentos recebem nova perspectiva;
8. ameaças recebem nova perspectiva;
9. `OnNodeEnter` é emitido depois da resolução;
10. a transição visual continua;
11. a margem final de input é aplicada.

## 10.3 Modo Keep

- não reinicia loops;
- não troca clips contínuos desnecessariamente;
- interpola volume, pan, filtro, reverb e oclusão;
- mantém equipamentos e ameaças ativos;
- fontes locais podem entrar ou sair.

## 10.4 Modo Crossfade

- reduz o áudio atual;
- introduz o destino gradualmente;
- evita corte ou clique;
- mantém sinais críticos conforme prioridade;
- termina junto ou imediatamente após a revelação visual.

## 10.5 Modo Immediate

- troca no ponto autorado;
- é intencionalmente abrupto;
- apropriado para monitor, fita, corte ou evento específico.

## 10.6 Modo Special

- chama comportamento explicitamente autorado;
- não deve criar lógica genérica escondida no hotspot;
- deve ser identificável no diagnóstico.

---

# 11. Pontos de perigo e entidades

## 11.1 Regra de resolução

Para cada entidade ativa:

```text
entityId
    ↓
activeAnchorId
    ↓
ViewAudioProfile atual
    ↓
AudioPointPerspective do anchor
    ↓
parâmetros de runtime
```

## 11.2 Exemplo obrigatório de validação

Criar ou configurar pelo menos três pontos fixos:

```text
window_sala
window_quarto
window_corredor
```

Criar uma entidade de teste:

```text
entity_debug_threat
```

Permitir selecionar somente um ponto por vez.

Testar:

1. entidade em `window_sala` no `__VN_CorredorFront`;
2. entidade em `window_sala` no `__VN_CorredorBack`;
3. entidade em `window_quarto` no mesmo VN;
4. mudança de anchor sem duplicar a fonte;
5. entidade inativa sem emissão de sinal;
6. estado `Light`, `Near` e `Critical`.

## 11.3 Estado não visual

O sinal deve continuar funcionando mesmo se:

- o GameObject da janela estiver desativado;
- o ViewNode da origem estiver fora de apresentação;
- o objeto visual da entidade não existir;
- o jogador estiver em outro cômodo.

---

# 12. Modais, pausa e Hotbar

## 12.1 Backpack

Ao abrir:

- input de gameplay recebe `Modal`;
- ambiência pode reduzir;
- UI permanece audível;
- áudio de ameaça segue a regra de gameplay definida;
- a abertura pode tocar feedback no grupo `UI`.

Ao fechar:

- mixagem anterior é restaurada;
- o cenário exige reentrada conforme a regra atual;
- nenhum áudio persistente deve reiniciar.

Durante `ToolDrag`, não aplicar automaticamente a mesma mixagem de pausa. O arraste bloqueia hotspots, mas é um estado próprio.

## 12.2 Documentos

Ao abrir documento:

- aplicar snapshot ou mixagem `Modal`;
- manter UI ativa;
- destacar `Media` somente se o conteúdo for mídia;
- restaurar a mixagem ao fechar.

## 12.3 Pausa F8

O painel F8 adiciona `InputBlockReason.Pause`, mas não deve alterar `Time.timeScale`.

A UI de debug continua podendo solicitar reprodução explícita. Esse comando é uma ação autorizada de debug e não representa um novo sinal de gameplay.

A pausa não deve gerar novos sinais críticos enquanto o jogador não puder reagir.

## 12.4 Hotbar

A Hotbar não interage com hotspots.

A lanterna é apresentação. Sons associados à seleção da lanterna devem ser classificados como `UI` ou `Equipment`, conforme o significado do som, sem alterar condições ou navegação.

---

# 13. Integração com os scripts existentes

## 13.1 `GameplaySceneController.cs`

Adicionar:

```csharp
[SerializeField] private SceneAudioController sceneAudioController;
```

Expor:

```csharp
public SceneAudioController Audio => sceneAudioController;
```

No boot:

- validar referência;
- inicializar áudio-base;
- aplicar o perfil inicial após a resolução do ViewNode.

Em `PrepareLocalShutdown()`:

- fechar fontes locais;
- iniciar fade da cena;
- finalizar ou transferir áudio permitido;
- só então permitir o fluxo do `GameSessionManager`.

O método antigo `PlayFeedback(AudioClip)` deve ser mantido temporariamente por compatibilidade ou redirecionado para o grupo correto do `SceneAudioController`.

## 13.2 `ViewNodeDefinition.cs`

Adicionar:

```csharp
public ViewAudioProfile audioProfile;
```

Não adicionar estado de runtime.

## 13.3 `ViewNodeController.cs`

Manter responsabilidade visual e de hotspots.

A resolução do áudio deve ocorrer no fluxo de navegação, preferencialmente por `NavigationManager` e `SceneAudioController`, para que o modo do link seja respeitado.

## 13.4 `NavigationManager.cs`

Alterar `TransitionRoutine()` para:

1. iniciar bloqueio;
2. iniciar SFX/timing da transição;
3. iniciar política de áudio do link;
4. executar ocultação;
5. chamar `SwapNode()`;
6. aplicar perfil do destino;
7. executar revelação;
8. finalizar crossfade;
9. manter margem de `0,05` segundo;
10. remover bloqueio.

## 13.5 `NavigationHotspot.cs`

Adicionar ou encapsular os dados em `NavigationLinkDefinition`.

O hotspot continua somente solicitando navegação. Ele não deve tocar áudio diretamente nem aplicar perfil.

## 13.6 `TransitionProfile.cs`

Adicionar SFX e timing do SFX.

Não adicionar volume de ambiente ou regras de mixagem permanente.

## 13.7 `InteractionManager.cs`

Continuar sendo a autoridade das interações.

Ao tocar `InteractionDefinition.sfx`, encaminhar para o `SceneAudioController` no grupo `Interactions`, evitando usar diretamente a fonte genérica de feedback.

## 13.8 `HotspotBase.cs`

Feedbacks devem ser encaminhados ao grupo `UI`.

O som de hover, quando utilizado, deve ser tocado somente uma vez por entrada válida, respeitando bloqueio e reentrada.

Não tocar simultaneamente feedback de hover e som diegético equivalente sem regra explícita.

## 13.9 `ModalUIController.cs`

Notificar o controller de áudio ao:

- abrir documento;
- fechar documento;
- entrar em mixagem modal;
- restaurar mixagem;
- encerrar imediatamente na troca de período.

## 13.10 `BackpackController.cs`

Notificar eventos de:

- abertura;
- fechamento;
- início de arraste;
- saída do modal;
- cancelamento;
- finalização do drop.

A seleção de ferramenta continua sendo responsabilidade do sistema existente.

## 13.11 `GameSessionManager.cs`

Não transformar o singleton em autoridade do áudio de cena.

Somente adicionar uma fonte persistente global se um SFX específico precisar atravessar a troca Dia/Noite. Essa fonte deve ser limitada a esse uso.

## 13.12 `CycleTestUI.cs`

Atualizar o painel de VS3 para incluir a aba `Áudio`.

Manter:

- tecla F8;
- bloqueio de pausa;
- IMGUI;
- funcionamento em Development/Editor.

---

# 14. Painel de DEBUG F8

## 14.1 Objetivo

O painel deve permitir testar todos os áudios individualmente sem precisar executar todo o fluxo do jogo.

Ele deve chamar o caminho real do sistema:

```text
CycleTestUI
    ↓
AudioDebugPanel
    ↓
SceneAudioController
    ↓
AudioSource / AudioMixer
```

Não implementar testes chamando `AudioSource.Play()` diretamente na UI.

## 14.2 Estrutura das abas

```text
F8 — DEBUG
├── Ciclo
├── Estado
├── Áudio
└── Diagnóstico
```

A aba `Áudio` deve possuir pelo menos:

```text
Biblioteca
Pontos de perigo
ViewNode/Perspectiva
Mixer
Transições
```

## 14.3 Biblioteca de áudio

Listar por categoria:

- Ambience;
- Equipment;
- Threats;
- Interactions;
- Media;
- Transition;
- UI;
- Music.

Cada item deve permitir:

```text
Play
Stop
Restart
Loop on/off
Volume
Pan
```

Exibir:

- ID;
- nome;
- categoria;
- clip;
- grupo do mixer;
- duração;
- fonte em uso;
- estado de reprodução.

## 14.4 Teste de pontos de perigo

Controles mínimos:

```text
Entity: [entity_debug_threat]
Anchor: [window_sala]
State: [Inactive / Light / Near / Critical]

[Simular entidade]
[Remover entidade]
[Reproduzir sinal]
```

A interface deve impedir ou corrigir automaticamente a existência de dois anchors ativos para a mesma entidade.

## 14.5 Visualização da perspectiva resolvida

Exibir:

```text
ViewNode atual
Anchor atual
Direção
Pan
Volume
Distância
Oclusão
Filtro
Reverb
AudioSource utilizado
```

Exemplo:

```text
ViewNode: __VN_CorredorBack
Anchor: window_sala
Direção: LeftBehind
Pan: -0.70
Volume: 0.65
Low-pass: 2800 Hz
Oclusão: 0.55
Fonte: ThreatSource_01
```

## 14.6 Preview de ViewNode

Permitir selecionar um ViewNode e aplicar apenas o perfil de áudio para comparação.

Esse preview não deve:

- trocar o ViewNode visual sem solicitação explícita;
- alterar save;
- alterar inventário;
- alterar fatos;
- criar progresso;
- deixar o input bloqueado.

## 14.7 Mixer

Exibir sliders temporários para os grupos principais e botões:

```text
Mix normal
Mix modal
Mix media
Mix pausa
Mix transição
Restaurar
```

As alterações de debug devem ser restauráveis.

## 14.8 Transições

Permitir selecionar:

- `TransitionProfile`;
- modo de áudio;
- ViewNode de destino.

Botões:

```text
Executar somente SFX
Executar preview de áudio
Executar transição visual + áudio
```

## 14.9 Catálogo de debug

Criar um catálogo ou registro de debug para garantir que todos os clips testáveis estejam listados.

Campos recomendados:

```text
id
category
clip
mixerGroup
isLoop
defaultVolume
associatedAnchorId
associatedEmitterId
description
```

O catálogo é de autoria/debug. A reprodução continua sendo responsabilidade do `SceneAudioController`.

## 14.10 Build final

O painel F8 pode ficar restrito a:

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
```

Na build de produção:

- não exibir painel;
- não aceitar comandos de debug;
- não incluir lógica de alteração manual de entidades;
- não alterar o fluxo normal do jogo.

---

# 15. API mínima do SceneAudioController

A API exata pode ser refinada, mas deve cobrir os seguintes comportamentos:

```csharp
public void Initialize(GameplaySceneDefinition sceneDefinition);

public void ApplyViewAudioProfile(
    ViewNodeDefinition viewNode,
    AudioTransitionMode mode);

public void BeginSceneExit();
public void CompleteSceneExit();

public void PlayUi(AudioClip clip);
public void PlayInteraction(AudioClip clip);
public void PlayTransition(AudioClip clip);

public void SetThreatState(
    string entityId,
    string anchorId,
    ThreatAudioState state);

public void ClearThreatState(string entityId);
public void PlayThreatSignal(string entityId, string signalId);

public void SetModalMix(bool active);
public void SetMediaFocusMix(bool active);
public void SetPausedMix(bool active);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public void DebugPlayAudio(string audioId);
public void DebugStopAudio(string audioId);
public void DebugSetEntityAnchor(string entityId, string anchorId);
public void DebugSetThreatState(string entityId, ThreatAudioState state);
public void DebugApplyProfile(string viewNodeId);
public AudioDebugSnapshot GetDebugSnapshot();
#endif
```

Regras:

- os métodos de produção não devem depender da UI F8;
- os métodos de debug devem usar as mesmas fontes, grupos e regras de prioridade;
- comandos inválidos devem gerar warning sem lançar exceção fatal;
- falha de áudio nunca deve prender o `InputBlocker`.

---

# 16. Organização de arquivos recomendada

## Scripts novos

```text
Assets/_Project/Scripts/Audio/
├── AudioEnums.cs
├── AudioAnchorDefinition.cs
├── AudioPointPerspective.cs
├── AudioRuntimeSource.cs
├── AudioThreatRuntimeState.cs
├── ViewAudioProfile.cs
└── SceneAudioController.cs
```

```text
Assets/_Project/Scripts/Navigation/
└── NavigationLinkDefinition.cs
```

```text
Assets/_Project/Scripts/Debug/
├── AudioDebugController.cs
├── AudioDebugPanel.cs
└── AudioDebugSnapshot.cs
```

Os nomes podem ser adaptados se a convenção final do projeto exigir, mas as responsabilidades devem permanecer separadas.

## Assets novos

```text
Assets/_Project/SO/Audio/
├── ViewAudioProfiles/
├── Anchors/
├── AudioDebugCatalog.asset
└── AudioMixer/
```

```text
Assets/_Project/Audio/Clips/
├── Ambience/
├── Equipment/
├── Threats/
├── Interactions/
├── Media/
├── Transition/
└── UI/
```

Nesta etapa não são criados clips temporários ou placeholder; os campos ficam vazios para receber clips reais pelo Inspector.

---

# 17. Ordem recomendada de implementação

## Fase 1 — Base de dados

1. Criar enums de áudio.
2. Criar `AudioAnchorDefinition`.
3. Criar `AudioPointPerspective`.
4. Criar `ViewAudioProfile`.
5. Adicionar `audioProfile` a `ViewNodeDefinition`.
6. Criar IDs iniciais dos pontos fixos.

## Fase 2 — Mixer e runtime

1. Criar `AudioMixer`.
2. Criar grupos e snapshots.
3. Criar `SceneAudioController`.
4. Criar fontes contínuas.
5. Criar pool de one-shots.
6. Implementar diagnóstico de fonte.
7. Inicializar no boot da cena.

## Fase 3 — Perspectiva por ViewNode

1. Aplicar perfil inicial.
2. Implementar modo `Keep`.
3. Implementar modo `Crossfade`.
4. Implementar modo `Immediate`.
5. Implementar fallback.
6. Validar troca de corredor front/back.

## Fase 4 — Navegação e transição

1. Criar `NavigationLinkDefinition`.
2. Integrar modo de áudio ao link.
3. Expandir `TransitionProfile`.
4. Sincronizar SFX com fases visuais.
5. Testar erro e liberação do bloqueio.

## Fase 5 — Entidades e pontos de perigo

1. Criar estado runtime da entidade.
2. Implementar `activeAnchorId` único.
3. Implementar sinal leve/próximo/crítico.
4. Aplicar perspectiva por anchor.
5. Validar entidade na janela da Sala.
6. Validar troca para as outras janelas.

## Fase 6 — Modais e ciclo global

1. Integrar `ModalUIController`.
2. Integrar `BackpackController`.
3. Integrar pausa F8.
4. Implementar fade de encerramento da cena.
5. Validar Dia → Noite.
6. Validar fonte global opcional, se necessária.

## Fase 7 — DEBUG F8

1. Separar a aba de áudio do `CycleTestUI`.
2. Criar catálogo.
3. Listar clips individualmente.
4. Adicionar controles de Play/Stop/Loop.
5. Adicionar teste de entidade/anchor.
6. Adicionar visualização de perspectiva.
7. Adicionar controles de mixer.
8. Adicionar preview de transição.
9. Adicionar diagnóstico e warnings.

## Fase 8 — Conteúdo e aceite

1. Atribuir os clips reais nos campos já preparados.
2. Adicionar variações.
3. Ajustar volumes e filtros.
4. Validar inteligibilidade.
5. Executar checklist dos Cartões 19–25.
6. Documentar problemas encontrados.

---

# 18. Validação e testes

## 18.1 Testes de dados

Validar no Editor/boot:

- IDs de anchors duplicados;
- ViewAudioProfile ausente;
- perfil sem zona;
- `anchorId` desconhecido;
- pan fora do intervalo;
- clip sem categoria;
- grupo de mixer ausente;
- link sem modo e sem fallback;
- TransitionProfile sem SFX quando exigido;
- ponto crítico sem perspectiva configurada.

Warnings devem informar:

```text
cena
ViewNode
anchorId
entityId
campo inválido
```

## 18.2 Testes de continuidade

1. Iniciar `__VN_CorredorFront`.
2. Confirmar que o ambiente está tocando.
3. Navegar para `__VN_CorredorBack`.
4. Confirmar que o loop não reiniciou.
5. Confirmar que pan/filtros foram interpolados.
6. Confirmar que não houve clique.

## 18.3 Testes de ponto de perigo

1. Ativar `entity_debug_threat` em `window_sala`.
2. Testar no `__VN_CorredorFront`.
3. Confirmar direção direita.
4. Ir ao `__VN_CorredorBack`.
5. Confirmar direção esquerda/atrás.
6. Mudar para `window_quarto`.
7. Confirmar que não existem duas fontes ativas para a mesma entidade.
8. Desativar entidade.
9. Confirmar silêncio do sinal.

## 18.4 Testes de entidade fora da tela

- desativar o GameObject visual do ponto;
- manter a entidade lógica ativa;
- confirmar que o sinal continua audível;
- trocar ViewNode;
- confirmar nova perspectiva.

## 18.5 Testes de mixagem

Validar:

- Backpack reduz ambiência conforme regra;
- documento restaura mix ao fechar;
- mídia recebe foco quando aplicável;
- UI continua audível;
- pausa não gera novos sinais críticos;
- transição continua usando tempo não escalado;
- F8 não altera `Time.timeScale`.

## 18.6 Testes de duplicidade

Confirmar que um mesmo acontecimento não é reproduzido por mais de uma origem:

- feedback de hotspot;
- som de interação;
- SFX de transição;
- `UnityEvent` autorado;
- debug panel.

## 18.7 Testes de troca Dia → Noite

1. Iniciar áudio do Dia.
2. Solicitar encerramento.
3. Confirmar fade/saída controlada.
4. Carregar Noite.
5. Confirmar entrada gradual do novo ambiente.
6. Confirmar ausência de corte acidental.
7. Confirmar que nenhum controller global duplicado foi criado.

---

# 19. Critérios de aceite do VS4

O VS4 será considerado pronto quando todos os itens abaixo forem verdadeiros:

- [ ] Existe mixer com os grupos definidos.
- [ ] Existe `SceneAudioController` local em cada cena de gameplay.
- [ ] Existe ambiente contínuo de teste.
- [ ] O ambiente não reinicia ao alternar ViewNodes da mesma zona.
- [ ] Existe `ViewAudioProfile` associado aos ViewNodes necessários.
- [ ] Perfis configuram pontos individualmente por `anchorId`.
- [ ] Uma ameaça pode estar em somente um ponto por vez.
- [ ] A entidade pode mudar de ponto sem duplicar sua fonte.
- [ ] O som da janela da Sala é direita no `__VN_CorredorFront`.
- [ ] O mesmo som é esquerda/atrás no `__VN_CorredorBack`.
- [ ] Equipamento persistente continua audível fora do seu ViewNode visual.
- [ ] Ameaça funciona sem GameObject visual ativo.
- [ ] Modos `Keep`, `Crossfade`, `Immediate` e `Special` possuem fallback válido.
- [ ] SFX da transição está separado do ambiente permanente.
- [ ] Transição visual e sonora usam o mesmo ponto de troca.
- [ ] Não há cliques ou cortes não intencionais.
- [ ] Interações são roteadas para `Interactions`.
- [ ] Feedbacks são roteados para `UI`.
- [ ] Modais aplicam a mixagem correta.
- [ ] Pausa não gera sinais críticos novos indevidamente.
- [ ] F8 possui aba de áudio.
- [ ] Todos os clips registrados podem ser testados individualmente.
- [ ] Pontos e entidades podem ser simulados pelo F8.
- [ ] O F8 exibe a perspectiva resolvida.
- [ ] Debug não altera save, inventário ou fatos.
- [ ] Debug não fica disponível na build de produção.
- [ ] Falha de clip ou perfil gera warning sem travar o jogo.
- [ ] `InputBlocker` é liberado em todos os caminhos de sucesso e erro.
- [ ] Não existe um segundo singleton global de áudio.

---

# 20. Fora do escopo inicial

Não implementar no primeiro VS4, salvo solicitação explícita:

- acústica 3D real completa;
- cálculo automático de raycast acústico;
- sistema de navegação de entidades;
- IA completa de ameaça;
- streaming de áudio por Addressables;
- descarregamento dinâmico de clips;
- sistema musical completo;
- localização multilíngue de mídia;
- legendas finais de acessibilidade;
- editor customizado avançado para perfis;
- banco externo de áudio;
- gravação de debug em save.

Esses itens podem ser adicionados depois que o slice comprovar a arquitetura principal.

---

# 21. Regras finais para implementação por AI agent

1. Não criar `AudioManager` singleton.
2. Não colocar `AudioSource` persistente em filhos de ViewNode.
3. Não armazenar estado de runtime em `ViewAudioProfile`.
4. Não usar o mouse para determinar a direção principal de ameaças.
5. Não usar volume como único indicador de distância/perigo.
6. Não reiniciar ambiente ao trocar ViewNode quando o modo for `Keep`.
7. Não criar dois estados ativos para a mesma entidade.
8. Não tocar diretamente clips pela UI de debug.
9. Não modificar save com comandos de debug.
10. Não usar `AudioListener.pause` como substituto da mixagem de modal/pausa.
11. Não transformar `GameSessionManager` em manager de áudio de cena.
12. Não permitir que erro de áudio prenda o `InputBlocker`.
13. Não misturar feedback de UI com som diegético de interação.
14. Não fazer o `TransitionProfile` controlar ambiente permanente.
15. Não avançar para produção final de assets antes de validar a infraestrutura; nesta etapa os clips permanecem como referências vazias e serão atribuídos pelo Inspector.

---

# 22. Resultado esperado

Ao final da implementação, deverá ser possível executar o seguinte fluxo em runtime:

1. Pressionar `F8`.
2. Abrir a aba `Áudio`.
3. Selecionar `entity_debug_threat`.
4. Selecionar `window_sala`.
5. Selecionar o estado `Critical`.
6. Reproduzir o sinal.
7. Confirmar que ele é ouvido à direita no `__VN_CorredorFront`.
8. Fechar o painel.
9. Navegar para `__VN_CorredorBack`.
10. Reabrir o F8.
11. Reproduzir novamente o mesmo sinal.
12. Confirmar que ele é ouvido à esquerda e atrás.
13. Mudar a entidade para `window_quarto`.
14. Confirmar que o perfil acústico muda sem duplicar a entidade.
15. Testar mixagem de modal e pausa.
16. Testar transição para outro ViewNode.
17. Encerrar o Dia.
18. Confirmar fade-out do áudio do Dia e entrada gradual do áudio da Noite.

Esse fluxo representa a prova mínima de que o sistema de áudio do projeto está integrado, autorável, diagnosticável e pronto para receber o conteúdo sonoro final.

---

# Apêndice A — Estado da implementação nesta entrega

A infraestrutura descrita neste documento foi implementada na branch `test`, mantendo o
`CycleTestUI` existente e sem criar um painel F8 paralelo.

## A.1 Runtime e integração

- `SceneAudioController` é local à cena e cria fontes em `__AudioRuntime`, fora dos filhos visuais dos ViewNodes.
- As fontes são separadas por `AudioSourceRole` e roteadas para o mixer `AUDIO_VS4`.
- Há suporte a camadas contínuas, fontes persistentes, one-shots, baixo-pass, reverb, snapshots e fade de saída.
- A aplicação de `ViewAudioProfile` ocorre no ponto de troca do `NavigationManager`.
- Links podem usar `Keep`, `Crossfade`, `Immediate` ou `Special`; as cenas de desenvolvimento contêm amostras configuradas para `vn_quarto` (`Keep`), `vn_sala` (`Crossfade`), `vn_corredorfront` (`Immediate`) e `vn_corredorback` (`Special`, ID `transition_glitch`).
- SFX de `TransitionProfile` podem ser disparados nos momentos `OnTransitionStart`, `OnHideStart`, `OnSwap`, `OnRevealStart` ou `OnTransitionEnd`.
- Ameaças e equipamentos têm contrato mínimo de runtime (`SetThreatState`, `SetPersistentEmitter`) sem implementar a IA ou o gameplay completo.

## A.2 Dados e assets criados

- `Assets/_Project/Audio/AUDIO_VS4.mixer`: grupos `World`, `Ambience`, `Threats`, `Interactions`, `Equipment`, `Media`, `Radio`, `Tapes`, `Transition`, `UI` e `Music`, com snapshots `Normal`, `Modal`, `MediaFocus`, `Paused` e `Transition`.
- Três `AudioAnchorDefinition`: `window_sala`, `window_quarto` e `window_corredor`.
- Nove `ViewAudioProfile`, todos associados aos nove `ViewNodeDefinition` existentes.
- `AUDIO_DebugCatalog` com IDs de ambiente, ameaça, interação, rádio, fita, transição e UI. Os campos `AudioClip` estão intencionalmente vazios.
- `TRANS_Corte` e `TRANS_Fade` contêm os campos de SFX temporizado, também sem clips atribuídos.

## A.3 DEBUG F8

A aba `Áudio` foi adicionada ao `CycleTestUI` existente. Ela permite:

- inspecionar ViewNode, perfil, zona, mix e vozes ativas;
- executar/parar entradas do `AudioDebugCatalog`;
- selecionar `window_sala`, `window_quarto` ou `window_corredor` para uma entidade de teste;
- alternar os estados `Light`, `Near`, `Critical` e `Inactive`;
- reproduzir um sinal selecionado na entidade;
- aplicar um perfil de áudio sem trocar o ViewNode visual;
- alternar snapshots de mixagem.

Com o catálogo sem clips, o painel deve mostrar `SEM CLIP` e os botões de reprodução devem
retornar falha controlada, sem criar som sintético ou asset placeholder.

## A.4 Validação pendente no editor Unity

O ambiente desta entrega não possui o executável do Unity. Portanto, ainda é necessário abrir
o projeto com `6000.3.18f1` e validar:

1. importação do `AUDIO_VS4.mixer` e dos subassets de grupo/snapshot;
2. compilação sem erros;
3. referências do `SceneAudioController` nas duas cenas;
4. abertura da aba Áudio pelo F8 em `playground_day` e `playground_night`;
5. atribuição posterior dos clips reais pelo Inspector;
6. ajustes finais de volume e snapshots após ouvir o material produzido.
