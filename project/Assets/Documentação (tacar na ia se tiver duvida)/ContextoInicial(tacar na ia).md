# Contexto do projeto — Whispers Of Unknown

## Diretrizes para o assistente

Atue como Tech Lead sênior especializado em Unity e C#. Responda sempre em português do Brasil.

Seja objetivo, prático e opinativo, priorizando:

- desacoplamento;
- testabilidade;
- escalabilidade;
- clareza arquitetural;
- código pronto para produção.

Antes de alterar um arquivo existente, consulte sua implementação atual no repositório. O repositório é a fonte de verdade quando houver divergência entre este documento e o código.

Não reescreva a infraestrutura Core sem justificativa técnica, análise de impacto, plano de migração e índice de confiança superior a 95%.

## Repositório

Repositório principal:

<++[https://github.com/AndoreBG/tcc-jd-fatec/tree/main/project](https://github.com/AndoreBG/tcc-jd-fatec/tree/main/project)++>

## Ambiente técnico

- Engine: Unity 6000.3.18f1
- Plataforma inicial: PC
- Linguagem: C#

Todo código novo deve ser compatível com essa versão da Unity.

# Projeto

Nome: **Whispers Of Unknown**

Whispers Of Unknown é um survival horror point-and-click para PC, desenvolvido em Unity.

O jogo utiliza primeira pessoa simulada por meio de telas 2D fixas. O jogador navega entre pontos de visão e interage com o cenário usando hotspots. Não existe movimentação 3D livre nem combate direto tradicional.

O gameplay é dividido em ciclos:

- **Dia:** exploração, coleta de recursos, descoberta de pistas e preparação.
- **Noite:** defesa da casa, interpretação de sinais, manutenção de equipamentos e gerenciamento simultâneo de ameaças.

As decisões tomadas durante o dia devem afetar diretamente as condições da noite.

O jogo possui estética analog horror/VHS, baixa fidelidade, ruído, distorções, rádio e fitas corrompidas. O foco é tensão contínua, escassez, vulnerabilidade e incerteza, evitando dependência excessiva de jumpscares.

# Referências mecânicas e direção de gameplay

Whispers Of Unknown possui identidade visual, narrativa, entidades, ambientação e progressão próprias. Entretanto, parte importante de sua estrutura mecânica é inspirada em **Five Nights at Freddy’s (FNAF)**, principalmente no gerenciamento de atenção, na navegação por pontos fixos e na manutenção preventiva de ameaças.

A inspiração deve ser utilizada como referência de comportamento e experiência. Não copiar personagens, interface, layout, sons, animações, timings ou elementos visuais específicos da franquia.

## Navegação por hotspots

- A navegação acontece entre telas e pontos de visão fixos.
- Hotspots de navegação podem ser ativados quando o jogador posiciona ou mantém o cursor sobre regiões específicas da tela.
- Essa navegação por hover é inspirada no uso de regiões sensíveis ao cursor em FNAF.
- O jogador não controla um personagem em um ambiente 3D livre.
- A troca de ViewNodes deve ser rápida, responsiva e acompanhada por feedback visual ou sonoro.
- Hotspots de navegação por hover devem ser tecnicamente diferentes de hotspots de interação por clique.
- O sistema deve evitar transições acidentais por meio de tempo mínimo de permanência, cooldown ou bloqueio durante transições.

Categorias previstas:

- **Navigation Hotspot:** troca o ViewNode, preferencialmente por hover.
- **Interaction Hotspot:** examina, coleta ou utiliza um objeto, normalmente por clique.
- **Tool Hotspot:** aplica uma ferramenta sobre um elemento do ambiente.
- **Conditional Hotspot:** fica disponível somente quando condições de Gameplay são atendidas.

## Lanterna

- A lanterna é inspirada no papel da lanterna em FNAF como ferramenta ativa de observação, confirmação de ameaças e gerenciamento de risco.
- A lanterna não funciona como uma arma tradicional.
- Seu uso permite verificar áreas escuras, revelar sinais e reagir a determinadas ameaças.
- A lanterna de Whispers Of Unknown utiliza um sistema próprio de dínamo e energia limitada.
- Recarregar ou utilizar a lanterna consome tempo e atenção, deixando outros pontos da casa temporariamente sem supervisão.
- A lanterna deve aumentar a capacidade de observação sem eliminar a vulnerabilidade do jogador.
- Sua implementação pertence ao Gameplay e não deve ser incorporada diretamente ao sistema de navegação.

## Lampião e Exhauster

- A manutenção do Exhauster é mecanicamente inspirada na caixa de música associada à Puppet em FNAF.
- O jogador precisa retornar periodicamente à escadaria para verificar e manter o lampião.
- O lampião funciona como uma tarefa de manutenção preventiva que compete com as demais ameaças pela atenção do jogador.
- Quando o jogador negligencia o lampião, o Exhauster avança progressivamente.
- A manutenção não elimina permanentemente a ameaça; apenas controla ou atrasa sua progressão.
- Quanto maior o tempo sem manutenção, maior deve ser o risco e mais intensos devem ser os sinais sonoros e visuais.
- O sistema deve obrigar o jogador a abandonar temporariamente portas, janelas, quarto ou outras tarefas.
- A identidade própria do projeto substitui a caixa de música por um lampião associado à escadaria, ao sótão e ao Exhauster.

A implementação deve separar:

- `LampSystem`: combustível, carga, estado e manutenção do lampião.
- `ExhausterStateMachine`: avanço e comportamento da entidade.
- `LampConfigSO`: dados de configuração.
- Event Channels: notificações entre sistemas.
- Runtime Variables: combustível, carga ou nível de risco observável.
- Views e áudio: apresentação dos sinais, sem controle das regras de domínio.

O `LampSystem` não deve chamar diretamente a entidade, e a entidade não deve conhecer a UI. A comunicação deve acontecer por interfaces, eventos ou estado observável.

## Economia de atenção

A principal referência de FNAF para Whispers Of Unknown é a economia de atenção.

Durante a noite, o jogador deve decidir constantemente entre:

- observar portas;
- verificar janelas;
- investigar o quarto;
- manter o lampião;
- recarregar ou utilizar a lanterna;
- interpretar sinais sonoros;
- trocar de ViewNode;
- administrar recursos defensivos.

O jogador não deve conseguir supervisionar todas as ameaças ao mesmo tempo.

Cada ação exige tempo, muda o foco de observação e cria uma janela de vulnerabilidade em outro ponto da casa. A dificuldade deve surgir principalmente da sobreposição desses sistemas, e não apenas do aumento artificial de velocidade das entidades.

# Entidades

Existem quatro ameaças principais.

## 1. Predator

- Ataca as portas.
- É contido com tábuas e reforços.

## 2. Voyeur

- Atua pelas janelas.
- Exige vigilância visual e sonora.

## 3. Exhauster

- Está ligado ao lampião da escadaria e ao acesso para o sótão.
- Avança quando o lampião perde carga ou combustível.
- Seu loop é inspirado mecanicamente na caixa de música da Puppet em FNAF.
- O jogador precisa retornar periodicamente à escadaria para manter o lampião.
- A manutenção apenas atrasa ou controla a ameaça; não elimina o Exhauster.
- Quanto maior a negligência, mais intensos devem ser seus sinais sonoros e visuais.
- Compete diretamente com portas, janelas, quarto e lanterna pela atenção do jogador.

## 4. Tricker

- Atua principalmente no quarto.
- É uma ameaça psicológica e incerta.
- Exige observação e investigação.

As entidades devem competir pelo tempo, pelos recursos e pela atenção do jogador.

# Direção de arquitetura

O projeto utiliza uma arquitetura híbrida baseada em:

- Services para sistemas globais e orquestração;
- Service Locator leve para registro e resolução controlada;
- ScriptableObject Event Channels para comunicação desacoplada;
- Runtime Variables observáveis em ScriptableObjects;
- ScriptableObjects de configuração para dados de design;
- classes C# puras para regras de domínio;
- MonoBehaviours apenas para integração com Unity, cenas, Inspector, áudio e apresentação.

Não utilizar Singleton Hell ou managers globais fortemente acoplados.

A arquitetura deve separar:

- **Data:** configurações em ScriptableObjects.
- **Domain:** regras em classes C# independentes da Unity quando possível.
- **Services:** ciclo de vida, coordenação e acesso a sistemas globais.
- **Controllers:** conexão entre domínio, cena e input.
- **Views:** apresentação visual, sonora e interface.
- **Composition Roots:** criação, registro e conexão das dependências.

# Services

Todo Service deve implementar `IService`:

- `bool IsInitialized`;
- `Initialize()`;
- `Dispose()`.

`Initialize()` e `Dispose()` devem ser idempotentes, ou seja, seguros contra chamadas repetidas.

Services planejados ou existentes:

- `AudioService`;
- `GameLoopService`;
- `NavigationService`;
- `InventoryService`;
- `HouseDefenseService`;
- `EntityDirectorService`;
- `SaveService`.

Services devem:

- possuir responsabilidade bem definida;
- controlar ciclo de vida e orquestração;
- expor uma API pública pequena e clara;
- ser acessados preferencialmente por interfaces;
- publicar eventos em vez de chamar diretamente módulos independentes;
- liberar inscrições e recursos em `Dispose()`;
- ser registrados em ordem de dependência;
- ser descartados na ordem inversa ao registro.

O Service Locator deve ser utilizado principalmente em:

- Bootstrappers;
- Composition Roots;
- controladores de alto nível;
- ferramentas de diagnóstico.

Não espalhar chamadas a `ServiceLocator.Get<T>()` por toda a lógica do jogo. Depois da resolução inicial, as dependências devem ser repassadas explicitamente sempre que possível.

Services Core podem ser registrados pelo `GameBootstrapper`.

Services de Gameplay serão registrados futuramente por um `GameplayCompositionRoot`, evitando que `Whispers.Core` dependa de `Whispers.Gameplay`.

# Estado atual do GameBootstrapper

O `GameBootstrapper` existe e é executado automaticamente antes do carregamento da primeira cena.

A inicialização automática está habilitada:

```csharp
private static bool initializeServices = true;
```

Esse booleano é privado, não serializado e alterado diretamente no código.

O controle utiliza um `if/else` simples:

- `false`: não cria o root e não inicializa Services.
- `true`: cria o root e executa os installers configurados.

A lista procedural contém atualmente apenas o `GameLoopService`:

```csharp
private static readonly Action<GameObject>[] CoreServiceInstallers =
    new Action<GameObject>[]
    {
        root => AddComponentAndRegister<IGameLoopService, GameLoopService>(root),
    };
```

Estado atual:

- O `GameLoopService` é criado, registrado e inicializado.
- O GameObject `[WHISPERS_CORE_SERVICES]` é criado.
- `DontDestroyOnLoad` é aplicado ao root.
- O `CoreServicesLifetime` é criado.
- O `AudioService` não é criado nem registrado porque não está na lista.
- Nenhum `AudioSource` é criado atualmente.

Para ativar o `AudioService` futuramente:

```csharp
root => AddComponentAndRegister<IAudioService, AudioService>(root),
```

Não utilizar `MonoScript` ou listas serializadas de scripts para registrar Services. `MonoScript` pertence ao ambiente do Editor. A lista procedural deve trabalhar com tipos C# compilados e delegates tipados.

# Service Locator

Arquivo:

```text
Assets/_Project/Scripts/Core/ServiceLocator/ServiceLocator.cs
```

API atual:

- `Register<T>()`;
- `Get<T>()`;
- `TryGet<T>()`;
- `IsRegistered<T>()`;
- `Unregister<T>()`;
- `Shutdown()`;
- `ClearAll()`;
- `ResetStaticState()` interno.

Comportamentos definidos:

- `Register<T>()` registra e inicializa o Service.
- Registros duplicados não são substituídos silenciosamente.
- Um registro duplicado lança `InvalidOperationException`.
- Falhas durante `Initialize()` removem o registro incompleto.
- `Get<T>()` lança `InvalidOperationException` quando um Service obrigatório não existe.
- `TryGet<T>()` deve ser usado apenas para Services realmente opcionais.
- `Unregister<T>()` pode remover e descartar um Service específico.
- `Shutdown()` descarta Services na ordem inversa ao registro.
- Exceções durante `Dispose()` são registradas sem impedir o descarte dos demais Services.
- Referências para Unity Objects destruídos são identificadas mesmo quando armazenadas por uma interface.
- `ResetStaticState()` suporta Play Mode com Domain Reload desativado.
- `ClearAll()` foi mantido como alias de compatibilidade para `Shutdown()`.
- Não existe contagem pública ou log de quantidade de Services.
- O código de runtime não depende de `UnityEditor` ou `UnityEditorInternal`.

Namespace:

```text
Whispers.Core.ServiceLocator
```

Como o namespace e a classe possuem o mesmo nome, utilizar alias quando necessário:

```csharp
using GlobalServices = Whispers.Core.ServiceLocator.ServiceLocator;
```

# Ciclo de vida dos Services

Arquivo:

```text
Assets/_Project/Scripts/Core/Bootstrap/CoreServicesLifetime.cs
```

Namespace atual:

```text
Whispers.Core
```

Responsabilidades:

- chamar `ServiceLocator.Shutdown()` em `OnApplicationQuit()`;
- usar `OnDestroy()` como fallback;
- notificar o `GameBootstrapper` quando o root for destruído;
- evitar chamadas duplicadas de `Shutdown()`.

O `CoreServicesLifetime` somente é criado quando `initializeServices` está `true`.

Como a inicialização está habilitada, o componente aparece durante o Play Mode no GameObject `[WHISPERS_CORE_SERVICES]`.

O `GameBootstrapper` utiliza:

```text
RuntimeInitializeLoadType.SubsystemRegistration
```

para limpar referências estáticas e suportar execuções com Domain Reload ativado ou desativado.

# Serviço de áudio

Arquivos:

```text
Assets/_Project/Scripts/Core/Services/IAudioService.cs
Assets/_Project/Scripts/Core/Services/AudioService.cs
```

`IAudioService` é o contrato público e estende `IService`.

API inicial:

- `bool IsInitialized`;
- `bool IsVhsDistortionActive`;
- `PlaySfx(AudioClip, float)`;
- `SetVHSDistortion(bool)`;
- `Initialize()`;
- `Dispose()`.

O `AudioService`:

- é um MonoBehaviour;
- implementa `IAudioService`;
- é idempotente;
- cria duas fontes de áudio quando inicializado: uma para ambiente e outra para SFX;
- interrompe e limpa as fontes em `Dispose()`;
- não chama `DontDestroyOnLoad` individualmente;
- depende do root criado pelo `GameBootstrapper` para persistência;
- ainda não possui processamento analógico definitivo;
- atualmente apenas controla o estado inicial da distorção VHS;
- receberá AudioMixer e filtros no futuro Analog Audio Engine.

Apesar de implementado e previamente validado, o `AudioService` não é iniciado atualmente porque não existe installer para ele em `CoreServiceInstallers`.

Consumidores devem resolver `IAudioService`, nunca o tipo concreto `AudioService`.

# Serviço de Game Loop

O `GameLoopService` está implementado, registrado e ativo.

Arquivos no namespace `Whispers.Core.GameLoop`:

```text
Assets/_Project/Scripts/Core/Services/GameLoop/GamePhase.cs
Assets/_Project/Scripts/Core/Services/GameLoop/IGameLoopState.cs
Assets/_Project/Scripts/Core/Services/GameLoop/IGameLoopService.cs
Assets/_Project/Scripts/Core/Services/GameLoop/GameLoopConfigSO.cs
Assets/_Project/Scripts/Core/Services/GameLoop/GameLoopService.cs
Assets/_Project/Scripts/Core/Services/GameLoop/States/DayState.cs
Assets/_Project/Scripts/Core/Services/GameLoop/States/NightState.cs
Assets/_Project/Scripts/Core/Services/GameLoop/States/NightResolutionState.cs
```

Características:

- É um MonoBehaviour com FSM embutida e `Update()` próprio.
- A lógica de cada fase vive em classes separadas que implementam `IGameLoopState`.
- O Service orquestra, realiza transições e publica eventos.
- Implementa `IGameLoopService`.
- Possui `Initialize()` e `Dispose()` idempotentes.
- Possui guarda contra reentrância por meio de transição pendente resolvida no fim do `Update()`.
- Permanece em `[WHISPERS_CORE_SERVICES]`.
- Resolve sua configuração com `Resources.Load("GameLoop/GameLoopConfig")`.
- Se o asset não existir, usa padrões embutidos: noite de 120 segundos e 10 ações diurnas.
- Sem o asset de configuração, não publica os Event Channels configuráveis nem atualiza Runtime Variables externas.

## Fases

`GamePhase` possui:

- `None`;
- `Day`;
- `Night`;
- `NightResolution`.

## Regras do ciclo

### Dia

- Não é cronometrado.
- Dura enquanto houver orçamento de ações.
- O limite é definido por `DayActionLimit`.
- Cada interação consome ações por `PerformAction(int cost = 1)`.
- Quando o orçamento chega a zero, ocorre a transição automática `Day → Night`.

### Noite

- É cronometrada.
- `NightState.Tick()` decrementa `NightTimeRemaining`.
- Quando o tempo chega a zero, ocorre a transição para `NightResolution`.

### Resolução da noite

- Possui uma janela curta de resolução.
- Ao terminar, avança para `Day`.
- Incrementa `CurrentDay`.

## API pública

`IGameLoopService` expõe:

- `GamePhase CurrentPhase`;
- `int CurrentDay`;
- `float NightTimeRemaining`;
- `int DayActionLimit`;
- `int ActionsRemaining`;
- `bool CanPerformAction`;
- `void StartGame()` para `None → Day`;
- `void EndDay()` para `Day → Night` manual;
- `bool CanAfford(int cost)`;
- `bool PerformAction(int cost = 1)`.

Regras de `PerformAction`:

- retorna `true` quando o custo é debitado;
- custo zero representa ação gratuita;
- custo maior que o saldo retorna `false`;
- o saldo nunca fica negativo.

Mapeamento sugerido de custos:

- `0`: examinar ou observar;
- `1`: deslocar ou pegar item;
- `2`: construir reforço.

Consumidores devem resolver `IGameLoopService`, nunca `GameLoopService` diretamente.

## Configuração

O código de configuração utiliza `GameLoopConfigSO` e espera um asset em:

```text
Assets/_Project/Resources/GameLoop/GameLoopConfig.asset
```

Caminho de carregamento:

```text
GameLoop/GameLoopConfig
```

Campos previstos:

### Event Channels

- `OnDayStarted`;
- `OnNightStarted`;
- `OnNightCompleted`;
- `OnActionsChanged`.

### Runtime Variables

- `CurrentDay` (`ObservableIntSO`);
- `NightTimeRemaining` (`ObservableFloatSO`);
- `ActionsRemaining` (`ObservableIntSO`).

### Timing

- Night Duration;
- Resolution Duration;
- Starting Day;
- Auto Start On Initialize.

Manter `Auto Start On Initialize` como `false` em produção.

O código de `GameLoopConfigSO` existe. Os assets de configuração, eventos e Runtime Variables devem ser criados e vinculados no Editor. Enquanto estiverem ausentes, o Service utiliza seus padrões internos.

## Diagnóstico

Arquivo:

```text
Assets/_Project/Scripts/Development/Diagnostics/GameLoopDiagnostics.cs
```

O diagnóstico é utilizado na cena Playground para validar o loop Dia/Noite.

# Comunicação entre sistemas

Sistemas independentes devem se comunicar por ScriptableObject Event Channels.

Eventos previstos ou existentes:

- `OnDayStarted`;
- `OnNightStarted`;
- `OnNightCompleted`;
- `OnActionsChanged`;
- `OnViewChanged`;
- `OnInventoryChanged`;
- `OnDoorDamaged`;
- `OnLampFuelChanged`;
- `OnEntityStateChanged`.

Estados globais observáveis devem utilizar Runtime Variables em ScriptableObjects.

Exemplos:

- `CurrentDay`;
- `NightTimeRemaining`;
- `ActionsRemaining`;
- `LampFuel`;
- `AvailableBoards`;
- `PlayerCondition`.

Runtime Variables devem restaurar seu valor inicial em `OnEnable()`, evitando que alterações feitas durante o Play Mode permaneçam no Editor.

Dados de configuração devem utilizar ScriptableObjects específicos:

- `ViewNodeSO`;
- `ItemDefinitionSO`;
- `EntityConfigSO`;
- `NightConfigSO`;
- `LootTableSO`;
- `GameLoopConfigSO`.

Configurações e estados de runtime não devem ser misturados no mesmo objeto.

# Event Bus validado

O Event Bus baseado em ScriptableObjects foi integrado e validado na Unity 6000.3.18f1.

Arquivos:

```text
Assets/_Project/Scripts/Core/Events/VoidEventChannelSO.cs
Assets/_Project/Scripts/Core/Events/VoidEventListener.cs
Assets/_Project/Scripts/Core/Events/FloatEventChannelSO.cs
```

O `VoidEventChannelSO` possui:

- `RaiseEvent()`;
- `Subscribe()`;
- `Unsubscribe()`.

O `FloatEventChannelSO` segue o mesmo padrão de funcionamento, mas trafega um valor `float` (`RaiseEvent(float)` e `Subscribe(UnityAction<float>)`).

O `VoidEventListener`:

- é um MonoBehaviour;
- escuta um `VoidEventChannelSO`;
- encaminha o evento para um UnityEvent;
- permite respostas configuradas pelo Inspector;
- realiza subscribe e unsubscribe conforme seu ciclo de vida.

Asset de diagnóstico:

```text
Assets/_Project/ScriptableObjects/Events/Debug/EVT_Debug_Void.asset
```

Smoke test:

```text
Assets/_Project/Scripts/Development/Diagnostics/VoidEventSmokeTest.cs
```

Fluxo validado:

```text
VoidEventSmokeTest
→ VoidEventChannelSO
→ VoidEventListener
→ UnityEvent
→ VoidEventSmokeTest.OnEventReceived()
```

Também foi validado que:

- o evento é recebido uma única vez;
- desativar o listener interrompe o recebimento;
- reativar o listener não duplica inscrições;
- entrar e sair do Play Mode não gera inscrições residuais.

# Runtime Variables

Arquivos existentes:

```text
Assets/_Project/Scripts/Core/Variables/ObservableFloatSO.cs
Assets/_Project/Scripts/Core/Variables/ObservableIntSO.cs
```

Ambos possuem:

- valor inicial;
- propriedade `Value`;
- `Subscribe()`;
- `Unsubscribe()`;
- `ResetValue()`;
- reset em `OnEnable()`.

Ainda devem ser criados testes específicos para validar as Runtime Variables com as configurações de Enter Play Mode da Unity 6.

# Assembly Definitions

Os Assembly Definitions estão configurados e compilando corretamente.

Arquivos:

```text
Assets/_Project/Scripts/Core/Whispers.Core.asmdef
Assets/_Project/Scripts/Gameplay/Whispers.Gameplay.asmdef
Assets/_Project/Scripts/UI/Whispers.UI.asmdef
Assets/_Project/Scripts/Development/Whispers.Development.asmdef
```

Dependências:

## Whispers.Core

- Não referencia Gameplay ou UI.

## Whispers.Gameplay

- Referencia `Whispers.Core`.

## Whispers.UI

- Referencia `Whispers.Core`.
- Pode referenciar `Whispers.Gameplay` quando necessário.

## Whispers.Development

- Referencia Core, Gameplay e UI.
- É utilizado para ferramentas e smoke tests.
- Possui define constraint `UNITY_EDITOR`.
- Não utiliza `includePlatforms: Editor`, pois isso impediria anexar MonoBehaviours de diagnóstico a GameObjects.

Fluxo permitido:

```text
Core ← Gameplay ← UI
```

Development pode observar todas as camadas apenas para diagnóstico.

Regra obrigatória: a UI pode conhecer Gameplay, mas Gameplay nunca pode conhecer ou chamar diretamente a UI.

# Estrutura de pastas

Estrutura atual e planejada:

```text
Assets/
└── _Project/
    ├── Art/
    │   ├── Backgrounds/
    │   ├── Entities/
    │   ├── Items/
    │   ├── UI/
    │   └── VHS/
    ├── Audio/
    │   ├── Ambience/
    │   ├── Entities/
    │   ├── Music/
    │   ├── Radio/
    │   └── SFX/
    ├── Prefabs/
    │   ├── Gameplay/
    │   ├── Hotspots/
    │   ├── UI/
    │   └── VFX/
    ├── Resources/
    │   └── GameLoop/
    │       └── GameLoopConfig.asset
    ├── Scenes/
    │   ├── Development/
    │   │   └── playground.unity
    │   ├── boot.unity
    │   ├── level_1.unity
    │   ├── level_2.unity
    │   ├── level_3.unity
    │   └── menu_main.unity
    ├── ScriptableObjects/
    │   ├── Configs/
    │   ├── Events/
    │   │   └── Debug/
    │   │       └── EVT_Debug_Void.asset
    │   ├── Items/
    │   ├── Variables/
    │   └── ViewNodes/
    └── Scripts/
        ├── Core/
        │   ├── Whispers.Core.asmdef
        │   ├── Bootstrap/
        │   │   └── CoreServicesLifetime.cs
        │   ├── Events/
        │   │   ├── VoidEventChannelSO.cs
        │   │   ├── VoidEventListener.cs
        │   │   └── FloatEventChannelSO.cs
        │   ├── ServiceLocator/
        │   │   ├── IService.cs
        │   │   └── ServiceLocator.cs
        │   ├── Services/
        │   │   ├── GameLoop/
        │   │   │   ├── States/
        │   │   │   │   ├── DayState.cs
        │   │   │   │   ├── NightState.cs
        │   │   │   │   └── NightResolutionState.cs
        │   │   │   ├── GamePhase.cs
        │   │   │   ├── IGameLoopState.cs
        │   │   │   ├── IGameLoopService.cs
        │   │   │   ├── GameLoopConfigSO.cs
        │   │   │   └── GameLoopService.cs
        │   │   ├── IAudioService.cs
        │   │   └── AudioService.cs
        │   ├── Variables/
        │   │   ├── ObservableFloatSO.cs
        │   │   └── ObservableIntSO.cs
        │   └── GameBootstrapper.cs
        ├── Development/
        │   ├── Whispers.Development.asmdef
        │   └── Diagnostics/
        │       ├── VoidEventSmokeTest.cs
        │       └── GameLoopDiagnostics.cs
        ├── Gameplay/
        │   ├── Whispers.Gameplay.asmdef
        │   ├── Bootstrap/
        │   ├── Entities/
        │   │   ├── Predator/
        │   │   ├── Voyeur/
        │   │   ├── Exhauster/
        │   │   └── Tricker/
        │   ├── House/
        │   │   ├── Navigation/
        │   │   ├── Hotspots/
        │   │   ├── Defense/
        │   │   └── Lamp/
        │   └── Player/
        │       ├── Inventory/
        │       ├── Interaction/
        │       └── Tools/
        └── UI/
            ├── Whispers.UI.asmdef
            ├── HUD/
            ├── Menus/
            └── Views/
```

O arquivo `GameLoopConfig.asset` representa o local esperado para a configuração. Caso ainda não tenha sido criado no Editor, o `GameLoopService` utiliza os padrões internos.

A cena Playground:

- é utilizada apenas para testes manuais;
- está fora do Build Profile;
- contém o GameObject `TestListener`;
- possui `VoidEventListener` e `VoidEventSmokeTest`;
- pode abrigar o `GameLoopDiagnostics` para validar o ciclo Dia/Noite.

# Namespaces

Os namespaces devem acompanhar as responsabilidades:

- `Whispers.Core`;
- `Whispers.Core.Events`;
- `Whispers.Core.GameLoop`;
- `Whispers.Core.Services`;
- `Whispers.Core.ServiceLocator`;
- `Whispers.Core.Variables`;
- `Whispers.Development.Diagnostics`;
- `Whispers.Gameplay`;
- `Whispers.Gameplay.Bootstrap`;
- `Whispers.Gameplay.Entities`;
- `Whispers.Gameplay.Entities.Predator`;
- `Whispers.Gameplay.Entities.Voyeur`;
- `Whispers.Gameplay.Entities.Exhauster`;
- `Whispers.Gameplay.Entities.Tricker`;
- `Whispers.Gameplay.House`;
- `Whispers.Gameplay.House.Navigation`;
- `Whispers.Gameplay.House.Hotspots`;
- `Whispers.Gameplay.House.Defense`;
- `Whispers.Gameplay.House.Lamp`;
- `Whispers.Gameplay.Player`;
- `Whispers.Gameplay.Player.Inventory`;
- `Whispers.Gameplay.Player.Interaction`;
- `Whispers.Gameplay.Player.Tools`;
- `Whispers.UI`.

O `CoreServicesLifetime` utiliza atualmente o namespace `Whispers.Core`, apesar de estar fisicamente na pasta `Core/Bootstrap`.

Não utilizar links, URLs ou marcação Markdown dentro de namespaces.

# Estado da Etapa 0

Concluído e validado:

- projeto executando na Unity 6000.3.18f1;
- estrutura inicial de pastas;
- Assembly Definitions;
- dependências entre Core, Gameplay e UI;
- Event Bus com `VoidEventChannelSO`;
- subscribe e unsubscribe sem duplicação;
- smoke test na cena Playground;
- `ServiceLocator` endurecido;
- registro por interfaces;
- inicialização e descarte idempotentes;
- descarte reverso dos Services;
- proteção contra registros duplicados;
- suporte a Domain Reload desativado;
- `CoreServicesLifetime`;
- `IAudioService`;
- implementação inicial do `AudioService`;
- `GameBootstrapper` procedural;
- remoção da contagem pública de Services;
- remoção de dependências de `UnityEditor` no runtime.

# Estado da Etapa 1

Concluído e validado:

- `GamePhase`: None, Day, Night e NightResolution;
- `IGameLoopState` e estados Day, Night e NightResolution;
- `IGameLoopService` e `GameLoopService`;
- FSM embutida em MonoBehaviour com lógica separada nos estados;
- transições com guarda contra reentrância;
- dia controlado por orçamento de ações;
- `PerformAction(int cost)` com custo variável;
- `CanAfford(int cost)`;
- noite cronometrada;
- janela de resolução da noite;
- `GameLoopConfigSO`;
- suporte aos Event Channels do Game Loop;
- suporte às Runtime Variables do Game Loop;
- installer do `GameLoopService` no `GameBootstrapper`;
- `GameLoopDiagnostics` para a cena Playground.

Estado deliberado atual:

- início automático dos Services habilitado;
- `GameLoopService` registrado e ativo;
- `AudioService` implementado, mas inativo por não possuir installer;
- assets de configuração, Event Channels e Runtime Variables do Game Loop devem ser criados e vinculados no Editor;
- enquanto esses assets estiverem ausentes, o Service utiliza padrões embutidos.

# Backlog principal

1. Game Loop e máquina de estados global — **concluído na Etapa 1**.
2. Navegação por ViewNodes e hotspots de navegação por hover.
3. Hotspots de navegação, interação, ferramentas e condições de disponibilidade.
4. Inventário e limite da mochila.
5. Exploração diurna e sistema de loot.
6. Ferramentas: lanterna de dínamo, martelo, tábuas e óleo.
7. House Defense System.
8. Entity Director.
9. FSM do Predator.
10. FSM do Voyeur.
11. FSM do Exhauster integrada à manutenção preventiva do lampião, inspirada no princípio da caixa de música da Puppet em FNAF.
12. FSM do Tricker.
13. Temporizador e progressão das noites — **parcial**: cronômetro e progressão de dia já existem no `GameLoopService`; faltam regras de progressão de dificuldade.
14. Sistema de áudio analógico.
15. Controlador visual VHS/analog horror.
16. Save/Load.
17. UI/HUD minimalista.
18. Configurações e acessibilidade.

# Padrão para novas entregas

Ao implementar qualquer sistema:

- informar o caminho exato de cada arquivo;
- informar o namespace de cada classe;
- entregar código C# completo e compilável;
- considerar Unity 6000.3.18f1;
- consultar o repositório antes de substituir arquivos existentes;
- explicar a responsabilidade de cada componente;
- explicar a configuração no Inspector;
- listar os ScriptableObjects que precisam ser criados;
- mostrar o fluxo de execução;
- informar como o sistema se comunica com os demais;
- incluir checklist de integração e testes;
- não reescrever a infraestrutura Core sem justificativa técnica e confiança superior a 95%;
- considerar FNAF como referência mecânica para economia de atenção, navegação por hotspots, lanterna e manutenção preventiva do Exhauster;
- preservar a identidade própria de Whispers Of Unknown;
- diferenciar hotspots de navegação por hover e hotspots de interação por clique;
- garantir que a lanterna seja uma ferramenta de observação e risco, não uma arma convencional;
- manter `LampSystem` e `ExhausterStateMachine` desacoplados;
- fazer portas, janelas, quarto, lampião, lanterna e entidades disputarem o tempo e a atenção do jogador.

Evitar:

- `GameObject.Find`;
- `FindObjectOfType` em runtime;
- `GetComponent` repetitivo;
- dependências diretas entre Gameplay e UI;
- Singletons adicionais;
- God Objects;
- managers excessivamente grandes;
- alocações recorrentes por frame;
- LINQ em caminhos críticos;
- Coroutines sem controle explícito de ciclo de vida;
- eventos sem unsubscribe;
- referências a `UnityEditor` ou `UnityEditorInternal` em assemblies de runtime;
- registros silenciosamente substituídos no `ServiceLocator`;
- cópia de personagens, interface, layout, sons, animações ou timings específicos de FNAF.

Utilizar Object Pooling para efeitos e objetos recorrentes, principalmente para evitar picos de garbage collection que possam quebrar a imersão.

Preservar essas decisões arquiteturais nas próximas implementações. Caso uma mudança estrutural seja necessária, explicar primeiro a motivação, os impactos e o plano de migração.