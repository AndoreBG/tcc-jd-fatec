> Repositório com o objetivo de armazenar o projeto prático do TCC (Trabalho de Conclusão de Curso) de Jogos Digitais da FATEC Americana "Ministro Ralph Biasi"

# Whispers Of Unknown
![Unity](https://img.shields.io/badge/Unity-2D-black?style=for-the-badge&logo=unity)
![Status](https://img.shields.io/badge/Status-Em%20Desenvolvimento-yellow?style=for-the-badge)
![Gênero](https://img.shields.io/badge/Gêneros-Surival%20Horror%20|%20Psychological%20Horror-red?style=for-the-badge)

## 🎮 Visão Geral

**Whispers Of Unknown** é um jogo de **terror de sobrevivência point-and-click**, desenvolvido na **Unity** para PC.

O jogo utiliza uma perspectiva de **primeira pessoa simulada**, com **telas fixas 2D** e interação baseada em **hotspots clicáveis**.

O jogador está isolado em uma casa, em um mundo distópico/pós-apocalíptico, onde experimentos militares deram origem a entidades híbridas entre **corpos humanos e máquinas**.

<br>

## 📁 Estrutura de Pastas do Projeto

```
Assets/_Project/
├── Art/                Arte visual: backgrounds, sprites de entidades/itens/UI e efeitos analógicos
│   ├── Backgrounds/
│   ├── Entities/
│   ├── Items/
│   ├── UI/
│   └── VHS/
├── Audio/              Áudio: ambience, music e SFX
│   ├── Ambience/
│   ├── Entities/
│   ├── Music/
│   ├── Radio/
│   └── SFX/
├── Prefabs/            Prefabs instanciáveis de gameplay, hotspots, UI e VFX
│   ├── Gameplay/
│   ├── Hotspots/
│   ├── UI/
│   └── VFX/
├── Scenes/             Boot, MainMenu e Development
│   ├── Boot.unity
│   ├── MainMenu.unity
│   └── Development/
├── ScriptableObjects/  Assets .asset: configs, events, items, variables, viewnodes
│   ├── Configs/
│   ├── Events/
│   ├── Items/
│   ├── Variables/
│   └── ViewNodes/
├── Resources/          Assets carregados via Resources.Load
│   └── GameLoop/
└── Scripts/
    ├── Core/                 [Whispers.Core]
    │   ├── Bootstrap/
    │   ├── Events/
    │   ├── ServiceLocator/
    │   ├── Services/
    │   └── Variables/
    ├── Gameplay/             [Whispers.Gameplay]
    │   ├── Bootstrap/
    │   ├── Entities/         4 ameaças, cada uma com sua FSM
    │   │   ├── Predator/
    │   │   ├── Voyeur/
    │   │   ├── Exhauster/
    │   │   └── Tricker/
    │   ├── House/            Estrutura e defesa da casa
    │   │   ├── Navigation/
    │   │   ├── Hotspots/
    │   │   ├── Defense/
    │   │   └── Lamp/
    │   └── Player/           Inventário, ferramentas e interação
    │       ├── Inventory/
    │       ├── Interaction/
    │       └── Tools/
    ├── UI/                   [Whispers.UI]
    │   ├── HUD/
    │   └── Menus/
    └── Development/          [Whispers.Development]
        └── Diagnostics/
```

<br>

## 📖 Narrativa

O mundo foi destruído ou profundamente alterado por experimentos militares que misturaram tecnologia e corpos humanos.

Esses experimentos criaram criaturas violentas e instáveis.

A história não é contada de forma direta. O jogador descobre fragmentos através de:

- Objetos  
- Documentos  
- Transmissões de rádio  
- Mudanças no ambiente  
- Sons  
- Pistas visuais  

A narrativa é construída gradualmente pelo próprio jogador.

<br>

## 🧠 Tom e Identidade

O jogo combina elementos de:

- Terror psicológico  
- Survival horror  
- Analog horror  

Características estéticas:

- Baixa fidelidade visual  
- Ruído e distorções  
- Estética VHS / transmissão corrompida  
- Sensação de registro antigo (rádio, fita, gravação perdida)  

A narrativa é fragmentada, focada em:

- Isolamento  
- Vulnerabilidade  
- Tensão constante  

O medo não vem de jumpscares excessivos, mas de:

- Sons estranhos  
- Ameaças simultâneas  
- Sensação de perda de controle  

<br>

## 🔁 Estrutura Principal

O jogo funciona em ciclos de **Dia e Noite**.

### ☀️ Durante o Dia

O jogador pode:

- Explorar áreas externas  
- Coletar recursos  
- Encontrar pistas narrativas  
- Se preparar para a noite  

### 🌑 Durante a Noite

O jogador deve:

- Defender a casa  
- Escutar sinais sonoros  
- Administrar recursos limitados  
- Lidar com múltiplas ameaças simultâneas  

> O que você faz durante o dia influencia diretamente suas chances de sobreviver à noite.

<br>

## ⚙️ Mecânicas Principais

- Interação por clique em hotspots  
- Navegação por telas fixas  
- Inventário em formato de mochila  
- Capacidade limitada de itens  
- Coleta e gerenciamento de recursos  
- Reforço de portas com tábuas  
- Uso de lanterna de dínamo  
- Manutenção de um lampião na escadaria  
- Defesa da casa durante a noite  
- Leitura de sinais sonoros e visuais  
- Ameaças simultâneas com prioridades conflitantes  

A interface é **minimalista**, incentivando o jogador a prestar atenção no ambiente.

<br>

## 👁️ Entidades

Quatro entidades principais pressionam o jogador de formas diferentes:

### 🩸 Predator
- Atua nas **portas**
- Pode forçar entrada
- Exige reforço com tábuas

### 👁️ Voyeur
- Atua nas **janelas**
- Requer vigilância constante
- Depende de sinais visuais e sonoros

### 🔥 Exhauster
- Ligado ao **lampião da escadaria**
- Avança caso o lampião não seja mantido carregado

### 🌀 Tricker
- Atua no **quarto**
- Presença mais psicológica e incerta
- Exige investigação e atenção gradual
