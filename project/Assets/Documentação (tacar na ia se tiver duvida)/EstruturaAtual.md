Assets/
└── _Project/                              ◄ Prefixo: separa de Assets externos/pacotes
    │
    ├── Art/                               ◄ Arte visual (2D, sprites, shaders)
    │   ├── Backgrounds/                   ◄ Fundos das telas fixas (ViewNodes)
    │   ├── Entities/                      ◄ Arte das 4 ameaças
    │   ├── Items/                         ◄ Ícones e sprites de itens
    │   ├── UI/                            ◄ Sprites de HUD e menus
    │   └── VHS/                           ◄ Texturas e shaders de ruído/distorção analógica
    │
    ├── Audio/                             ◄ Consumido pelo AudioService
    │   ├── Ambience/                      ◄ Loops de ambiente (fonte "ambient")
    │   ├── Entities/                      ◄ SFX das ameaças
    │   ├── Music/                         ◄ Trilha
    │   ├── Radio/                         ◄ Rádio e fitas corrompidas
    │   └── SFX/                           ◄ SFX genéricos (cliques, portas) — fonte "sfx"
    │
    ├── Prefabs/                           ◄ Prefabs instanciáveis (Object Pooling depois)
    │   ├── Gameplay/                      ◄ Elementos de gameplay
    │   ├── Hotspots/                      ◄ Hotspots clicáveis
    │   ├── UI/                            ◄ Elementos de interface
    │   └── VFX/                           ◄ Efeitos visuais recorrentes
    │
    ├── Scenes/
    │   ├── Boot.unity                     ◄ Primeira cena do Build Profile (ver nota 1)
    │   ├── MainMenu.unity
    │   ├── Gameplay_House.unity           ◄ Cena principal de gameplay
    │   └── Development/
    │       └── Playground.unity           ◄ FORA do Build Profile — testes manuais
    │
    ├── ScriptableObjects/                 ◄ Apenas instâncias .asset (nunca código)
    │   ├── Configs/                       ◄ Configurações de design (ItemDefinitionSO, etc.)
    │   ├── Events/                        ◄ Event Channels (Void/Float)
    │   │   └── Debug/                     ◄ EVT_Debug_Void, EVT_OnActionsChanged, ...
    │   ├── Items/                         ◄ ItemDefinitionSO
    │   ├── Variables/                     ◄ Runtime Variables observáveis
    │   │                                      VAR_CurrentDay, VAR_NightTimeRemaining,
    │   │                                      VAR_ActionsRemaining
    │   └── ViewNodes/                     ◄ ViewNodeSO (pontos de visão)
    │
    ├── Resources/                         ◄ ATENÇÃO: carregado via Resources.Load (ver nota 2)
    │   └── GameLoop/
    │       └── GameLoopConfig.asset       ◄ Config do loop consumida pelo GameLoopService
    │
    └── Scripts/
        ├── Core/                          ◄ [Whispers.Core.asmdef] — não referencia Gameplay/UI
        │   ├── Bootstrap/                 ◄ GameBootstrapper.cs, CoreServicesLifetime.cs
        │   ├── Events/                    ◄ VoidEventChannelSO, VoidEventListener, FloatEventChannelSO
        │   ├── ServiceLocator/            ◄ IService.cs, ServiceLocator.cs
        │   ├── Services/                  ◄ Serviços globais do Core
        │   │   ├── GameLoop/              ◄ FSM global + orçamento de ações (Etapa 1)
        │   │   │   ├── States/            ◄ DayState, NightState, NightResolutionState
        │   │   │   ├── GamePhase.cs
        │   │   │   ├── IGameLoopState.cs
        │   │   │   ├── IGameLoopService.cs
        │   │   │   ├── GameLoopConfigSO.cs
        │   │   │   └── GameLoopService.cs
        │   │   ├── IAudioService.cs
        │   │   └── AudioService.cs
        │   └── Variables/                 ◄ ObservableFloatSO, ObservableIntSO
        │
        ├── Gameplay/                      ◄ [Whispers.Gameplay.asmdef] → referencia Core
        │   ├── Bootstrap/                 ◄ GameplayCompositionRoot (futuro)
        │   ├── Entities/                  ◄ 4 ameaças (cada com sua FSM)
        │   │   ├── Predator/              ◄ ataca portas → tábuas
        │   │   ├── Voyeur/                ◄ janelas → vigilância
        │   │   ├── Exhauster/             ◄ lampião da escadaria
        │   │   └── Tricker/              ◄ quarto → ameaça psicológica
        │   ├── House/                     ◄ Estrutura e defesa da casa
        │   │   ├── Navigation/            ◄ ViewNodes e navegação point-and-click
        │   │   ├── Hotspots/              ◄ Hotspots clicáveis
        │   │   ├── Defense/               ◄ Portas, tábuas, janelas, HouseDefenseSystem
        │   │   └── Lamp/                  ◄ Lampião da escadaria (combustível/carga)
        │   └── Player/                    ◄ Tudo do jogador
        │       ├── Inventory/             ◄ Inventário e limite da mochila
        │       ├── Interaction/           ◄ Interação/clique
        │       └── Tools/                 ◄ Lanterna de dínamo, martelo, tábuas, óleo
        │
        ├── UI/                            ◄ [Whispers.UI.asmdef] → refs Core (+ Gameplay p/ ler estado)
        │   ├── HUD/                       ◄ HUD minimalista (cronômetro, ações, combustível)
        │   └── Menus/                     ◄ MainMenu, pausa, configurações
        │
        └── Development/                   ◄ [Whispers.Development.asmdef]
            └── Diagnostics/               ◄ Smoke tests: VoidEventSmokeTest, GameLoopDiagnostics
