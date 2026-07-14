# Sistema de Services — Whispers Of Unknown

## 1. Objetivo

O sistema de Services centraliza funcionalidades globais ou de alto nível que possuem ciclo de vida próprio, como áudio, salvamento, navegação global e coordenação do loop do jogo.

A solução utiliza:

- `IService` como contrato de ciclo de vida;
- interfaces específicas para expor APIs públicas;
- implementações concretas dos Services;
- `ServiceLocator` para registro e resolução controlada;
- `GameBootstrapper` como Composition Root dos Services do Core;
- uma lista procedural e tipada de installers;
- `CoreServicesLifetime` para encerramento seguro;
- `RuntimeInitializeOnLoadMethod` para inicialização independente de cena;
- limpeza de estado estático compatível com Domain Reload desativado.

O objetivo não é transformar o `ServiceLocator` em um Singleton acessado por qualquer classe. Ele deve ser utilizado principalmente em Bootstrappers, Composition Roots, controladores de alto nível e ferramentas de diagnóstico.

Depois de resolver um Service, a dependência deve ser repassada explicitamente às classes que a utilizam sempre que possível.

---

## 2. Estado atual

O sistema está preparado, mas a inicialização automática de Services está deliberadamente desativada.

No `GameBootstrapper` existe:

```csharp
private static bool initializeServices = false;
```

A lista de installers também está vazia:

```csharp
private static readonly Action<GameObject>[] CoreServiceInstallers =
    new Action<GameObject>[]
    {
        // Nenhum serviço deve iniciar neste momento.
    };
```

Consequentemente, ao iniciar o Play Mode:

- nenhum Service é criado;
- nenhum Service é registrado;
- o `AudioService` permanece inativo;
- nenhum `AudioSource` é criado;
- o GameObject `[WHISPERS_CORE_SERVICES]` não é criado;
- o `CoreServicesLifetime` não é criado;
- o `ServiceLocator` apenas tem seu estado estático restaurado;
- o Console informa que a inicialização automática está desativada.

O Event Bus baseado em ScriptableObjects não depende de Services e continua funcionando normalmente.

---

## 3. Arquivos do sistema

### 3.1 Core

```text
Assets/_Project/Scripts/Core/
├── GameBootstrapper.cs
├── Bootstrap/
│   └── CoreServicesLifetime.cs
├── ServiceLocator/
│   ├── IService.cs
│   └── ServiceLocator.cs
└── Services/
    ├── IAudioService.cs
    └── AudioService.cs
```

### 3.2 Assembly

Todos esses arquivos pertencem ao assembly:

```text
Whispers.Core
```

O assembly `Whispers.Core` não pode referenciar `Whispers.Gameplay` ou `Whispers.UI`.

Por esse motivo, o `GameBootstrapper` registra apenas Services pertencentes ao Core. Services específicos da sessão de Gameplay serão responsabilidade de um `GameplayCompositionRoot` futuro.

---

## 4. Visão geral

```text
Unity inicia o Play Mode
        │
        ▼
GameBootstrapper.ResetStaticState()
        │
        ├── Limpa referências estáticas anteriores
        └── Suporta Domain Reload desativado
        │
        ▼
GameBootstrapper.InitializeCoreSystems()
        │
        ▼
if (initializeServices)
        │
        ├── false
        │   └── Não cria ou registra nenhum Service
        │
        └── true
            ├── Cria [WHISPERS_CORE_SERVICES]
            ├── Aplica DontDestroyOnLoad ao root
            ├── Adiciona CoreServicesLifetime
            └── Executa CoreServiceInstallers em ordem
                    │
                    ▼
             ServiceLocator.Register<T>()
                    │
                    ├── Valida duplicação
                    ├── Armazena o Service
                    └── Chama Initialize()
```

No encerramento:

```text
OnApplicationQuit() ou OnDestroy()
        │
        ▼
CoreServicesLifetime
        │
        ▼
ServiceLocator.Shutdown()
        │
        └── Chama Dispose() na ordem inversa ao registro
```

---

## 5. IService

Caminho:

```text
Assets/_Project/Scripts/Core/ServiceLocator/IService.cs
```

Namespace:

```csharp
Whispers.Core.ServiceLocator
```

Contrato:

```csharp
public interface IService
{
    bool IsInitialized { get; }

    void Initialize();

    void Dispose();
}
```

### 5.1 IsInitialized

Indica se o Service concluiu sua inicialização.

A propriedade permite:

- impedir inicializações duplicadas;
- validar o estado do Service em testes;
- ignorar chamadas de `Dispose()` quando o Service já estiver finalizado;
- diagnosticar uso prematuro.

Implementação recomendada:

```csharp
public bool IsInitialized { get; private set; }
```

### 5.2 Initialize

Responsável por:

- validar dependências;
- criar recursos internos;
- configurar componentes;
- realizar inscrições em eventos;
- preparar o Service para uso.

Deve ser idempotente:

```csharp
public void Initialize()
{
    if (IsInitialized)
    {
        return;
    }

    // Inicialização.

    IsInitialized = true;
}
```

### 5.3 Dispose

Responsável por:

- remover inscrições em eventos;
- interromper operações;
- limpar referências;
- liberar recursos;
- restaurar o estado interno.

Também deve ser idempotente:

```csharp
public void Dispose()
{
    if (!IsInitialized)
    {
        return;
    }

    // Encerramento.

    IsInitialized = false;
}
```

---

## 6. Contrato específico e implementação

Consumidores não devem depender diretamente da classe concreta de um Service. Cada Service deve expor uma interface específica.

Exemplo:

```text
IAudioService
      ▲
      │ implementa
      │
AudioService
```

Contrato:

```csharp
public interface IAudioService : IService
{
    bool IsVhsDistortionActive { get; }

    void PlaySfx(AudioClip clip, float volume = 1f);

    void SetVHSDistortion(bool active);
}
```

Implementação:

```csharp
public sealed class AudioService : MonoBehaviour, IAudioService
{
    // Implementação concreta.
}
```

Benefícios:

- menor acoplamento;
- substituição por mocks em testes;
- troca de implementação sem alterar consumidores;
- API pública menor;
- implementação concreta protegida de uso indevido.

---

## 7. ServiceLocator

Caminho:

```text
Assets/_Project/Scripts/Core/ServiceLocator/ServiceLocator.cs
```

Namespace:

```csharp
Whispers.Core.ServiceLocator
```

Como o namespace e a classe possuem o mesmo nome, recomenda-se um alias:

```csharp
using GlobalServices = Whispers.Core.ServiceLocator.ServiceLocator;
```

O `ServiceLocator` mantém internamente:

- um dicionário de Services por tipo de contrato;
- uma lista com a ordem de registro;
- o controle necessário para descarte reverso.

Ele não expõe contagem pública de Services.

### 7.1 Register<T>()

Exemplo:

```csharp
GlobalServices.Register<IAudioService>(audioService);
```

O tipo genérico informado é a chave do registro. Portanto, após registrar por `IAudioService`, o Service deve ser resolvido por `IAudioService`.

Correto:

```csharp
IAudioService audio = GlobalServices.Get<IAudioService>();
```

Incorreto:

```csharp
AudioService audio = GlobalServices.Get<AudioService>();
```

`Register<T>()` executa o seguinte fluxo:

1. Valida se a instância é válida.
2. Detecta referências destruídas de `UnityEngine.Object`.
3. Verifica se o contrato já está registrado.
4. Rejeita registros duplicados válidos.
5. Adiciona o Service ao dicionário.
6. Armazena a ordem de registro.
7. Chama `Initialize()`.
8. Se a inicialização falhar, remove o registro.
9. Tenta executar `Dispose()` para limpar uma inicialização parcial.
10. Propaga a exceção.

Registros duplicados não são substituídos silenciosamente. Uma duplicação é um erro de composição e lança `InvalidOperationException`.

### 7.2 Get<T>()

Usado para Services obrigatórios:

```csharp
IAudioService audio = GlobalServices.Get<IAudioService>();
```

Se o Service não estiver registrado, lança `InvalidOperationException` imediatamente.

Esse comportamento é intencional. A ausência de um Service obrigatório representa um erro de configuração e deve falhar no ponto correto, em vez de retornar `null` e causar uma `NullReferenceException` posteriormente.

### 7.3 TryGet<T>()

Usado apenas para dependências realmente opcionais:

```csharp
if (GlobalServices.TryGet(out IAudioService audio))
{
    audio.SetVHSDistortion(true);
}
```

Retorna `false` quando:

- o contrato não foi registrado;
- a instância foi destruída;
- a referência armazenada não é mais válida.

Não utilizar `TryGet<T>()` para esconder a ausência de um Service obrigatório.

### 7.4 IsRegistered<T>()

Verifica se existe uma instância válida registrada:

```csharp
bool hasAudio = GlobalServices.IsRegistered<IAudioService>();
```

É adequado para diagnósticos e decisões de Composition Roots. Não deve ser utilizado para criar fluxos de Gameplay dependentes da presença acidental de um Service.

### 7.5 Unregister<T>()

Remove um Service específico:

```csharp
GlobalServices.Unregister<IAudioService>();
```

Por padrão, também chama `Dispose()`.

É possível remover sem descartar:

```csharp
GlobalServices.Unregister<IAudioService>(dispose: false);
```

A remoção sem descarte deve ser rara e possuir propriedade de ciclo de vida claramente definida por outro objeto.

### 7.6 Shutdown()

Finaliza todos os Services registrados:

```csharp
GlobalServices.Shutdown();
```

Os Services são descartados na ordem inversa ao registro.

Exemplo de registro:

```text
1. AudioService
2. SaveService
3. GameLoopService
```

Ordem de descarte:

```text
1. GameLoopService.Dispose()
2. SaveService.Dispose()
3. AudioService.Dispose()
```

Isso permite que um Service consumidor seja finalizado antes de sua dependência.

Se um `Dispose()` lançar uma exceção, o erro é registrado, mas o encerramento dos demais Services continua.

### 7.7 ClearAll()

`ClearAll()` foi mantido como alias de compatibilidade:

```csharp
GlobalServices.ClearAll();
```

Seu comportamento atual é chamar `Shutdown()`.

Para código novo, prefira:

```csharp
GlobalServices.Shutdown();
```

### 7.8 ResetStaticState()

Método interno utilizado pelo `GameBootstrapper` durante:

```csharp
RuntimeInitializeLoadType.SubsystemRegistration
```

Ele limpa coleções estáticas sem tentar acessar objetos de uma execução anterior.

Não deve ser utilizado por código de Gameplay.

---

## 8. GameBootstrapper

Caminho:

```text
Assets/_Project/Scripts/Core/GameBootstrapper.cs
```

Namespace:

```csharp
Whispers.Core
```

O `GameBootstrapper` é o Composition Root do Core.

Responsabilidades:

- restaurar o estado estático no início da execução;
- decidir se a inicialização automática está ativa;
- criar o root persistente quando necessário;
- instalar Services Core de forma procedural;
- tratar falhas durante a composição;
- não depender de Gameplay ou UI.

### 8.1 Controle por código

A inicialização é controlada por:

```csharp
private static bool initializeServices = false;
```

Esse campo:

- não é serializado;
- não aparece no Inspector;
- deve ser alterado diretamente no código;
- utiliza um `if/else` simples no início da execução.

Com `false`, nenhum Service é instalado.

Com `true`, a lista de installers é executada.

### 8.2 Lista procedural

A lista utiliza delegates tipados:

```csharp
private static readonly Action<GameObject>[] CoreServiceInstallers =
    new Action<GameObject>[]
    {
        // Installers.
    };
```

Não são utilizados:

- `MonoScript`;
- reflexão baseada em nome de arquivo;
- listas serializadas de scripts;
- busca de tipos em assemblies;
- `GameObject.Find`;
- `FindObjectOfType`.

O arquivo `.cs` é uma unidade de código-fonte e não deve ser usado como dependência de runtime. A lista referencia tipos C# já compilados.

### 8.3 AddComponentAndRegister

O helper procedural recebe:

- o contrato público;
- a implementação concreta;
- o root onde o componente será criado.

Exemplo:

```csharp
root => AddComponentAndRegister<IAudioService, AudioService>(root),
```

O helper:

1. Adiciona a implementação com `AddComponent<TImplementation>()`.
2. Valida se a implementação atende ao contrato.
3. Destrói o componente se o contrato for inválido.
4. Registra o Service pelo contrato correto.
5. Delega a inicialização ao `ServiceLocator.Register<T>()`.

O helper atual é específico para implementações `MonoBehaviour`.

Services C# puros poderão utilizar outro installer ou ser construídos manualmente dentro do Composition Root. Essa extensão ainda não foi implementada e só deve ser adicionada quando existir um caso real.

### 8.4 Root persistente

Quando a inicialização estiver ativa, o Bootstrapper cria:

```text
[WHISPERS_CORE_SERVICES]
```

O root recebe:

```csharp
UnityEngine.Object.DontDestroyOnLoad(root);
```

Os Services individuais não devem chamar `DontDestroyOnLoad()`.

A persistência é propriedade do root, não de cada componente.

### 8.5 Falha de composição

Se um installer lançar uma exceção:

1. `ServiceLocator.Shutdown()` é chamado.
2. Services já inicializados são descartados.
3. O root é destruído.
4. A referência estática ao root é limpa.
5. Uma `InvalidOperationException` é lançada com a causa original.

O jogo não deve continuar silenciosamente com apenas parte da infraestrutura inicializada.

---

## 9. CoreServicesLifetime

Caminho:

```text
Assets/_Project/Scripts/Core/Bootstrap/CoreServicesLifetime.cs
```

Namespace:

```csharp
Whispers.Core
```

O componente é adicionado ao root quando a inicialização automática está ativa.

Responsabilidades:

- chamar `ServiceLocator.Shutdown()` no encerramento da aplicação;
- utilizar `OnDestroy()` como fallback;
- impedir Shutdown duplicado com um booleano interno;
- notificar o `GameBootstrapper` quando o root for destruído.

Fluxo:

```text
OnApplicationQuit()
        │
        ▼
Shutdown()
        │
        ├── Verifica se já encerrou
        ├── ServiceLocator.Shutdown()
        └── Notifica GameBootstrapper
```

`OnDestroy()` executa o mesmo fluxo caso o root seja destruído manualmente ou durante o encerramento do Play Mode.

---

## 10. Domain Reload

A Unity permite desativar o Domain Reload para acelerar a entrada no Play Mode. Nesse caso, campos estáticos podem manter valores de uma execução anterior.

O sistema protege o estado estático com:

```csharp
[RuntimeInitializeOnLoadMethod(
    RuntimeInitializeLoadType.SubsystemRegistration)]
```

Esse estágio executa antes da inicialização principal e chama:

```csharp
GlobalServices.ResetStaticState();
```

Também restaura a referência estática do root.

O objetivo é garantir o mesmo comportamento com:

- Domain Reload ativado;
- Domain Reload desativado.

O reset de `SubsystemRegistration` não substitui `Dispose()`. O encerramento normal continua sendo responsabilidade de `CoreServicesLifetime` e dos Composition Roots proprietários.

---

## 11. Como adicionar um novo Service Core

### Passo 1 — Criar o contrato

Exemplo de caminho:

```text
Assets/_Project/Scripts/Core/Services/ISaveService.cs
```

```csharp
using Whispers.Core.ServiceLocator;

namespace Whispers.Core.Services
{
    public interface ISaveService : IService
    {
        void Save();
        void Load();
    }
}
```

### Passo 2 — Criar a implementação

Exemplo:

```text
Assets/_Project/Scripts/Core/Services/SaveService.cs
```

```csharp
using UnityEngine;

namespace Whispers.Core.Services
{
    public sealed class SaveService : MonoBehaviour, ISaveService
    {
        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            IsInitialized = true;
        }

        public void Save()
        {
            if (!IsInitialized)
            {
                return;
            }

            // Implementação futura.
        }

        public void Load()
        {
            if (!IsInitialized)
            {
                return;
            }

            // Implementação futura.
        }

        public void Dispose()
        {
            if (!IsInitialized)
            {
                return;
            }

            IsInitialized = false;
        }
    }
}
```

### Passo 3 — Adicionar à lista

```csharp
private static readonly Action<GameObject>[] CoreServiceInstallers =
    new Action<GameObject>[]
    {
        root => AddComponentAndRegister<ISaveService, SaveService>(root),
    };
```

### Passo 4 — Ativar a inicialização

```csharp
private static bool initializeServices = true;
```

### Passo 5 — Respeitar a ordem de dependência

Dependências devem aparecer antes de seus consumidores.

Exemplo:

```csharp
private static readonly Action<GameObject>[] CoreServiceInstallers =
    new Action<GameObject>[]
    {
        root => AddComponentAndRegister<IAudioService, AudioService>(root),
        root => AddComponentAndRegister<ISaveService, SaveService>(root),
    };
```

Se `SaveService` depender de `AudioService`, o áudio deve ser registrado primeiro.

### Passo 6 — Resolver no Composition Root ou controlador

```csharp
IAudioService audio = GlobalServices.Get<IAudioService>();
ISaveService save = GlobalServices.Get<ISaveService>();
```

Depois disso, passe as interfaces explicitamente às classes consumidoras.

---

## 12. AudioService atual

Arquivos:

```text
Assets/_Project/Scripts/Core/Services/IAudioService.cs
Assets/_Project/Scripts/Core/Services/AudioService.cs
```

O `AudioService`:

- implementa `IAudioService`;
- é um `MonoBehaviour`;
- possui inicialização e descarte idempotentes;
- cria uma fonte para ambiente quando necessário;
- cria uma fonte exclusiva para SFX;
- configura as fontes como áudio 2D;
- utiliza `PlayOneShot()` para SFX;
- limita o volume entre zero e um;
- interrompe fontes durante `Dispose()`;
- limpa o clip ambiente;
- mantém o estado inicial da distorção VHS;
- ainda não implementa o processamento analógico definitivo.

O `AudioService` não chama `DontDestroyOnLoad()`. Quando for ativado, ele será filho do root persistente criado pelo `GameBootstrapper`.

Estado atual:

```text
Implementado: sim
Registrado: não
Inicializado automaticamente: não
Presente na lista de installers: não
```

Para ativá-lo futuramente:

```csharp
private static bool initializeServices = true;

private static readonly Action<GameObject>[] CoreServiceInstallers =
    new Action<GameObject>[]
    {
        root => AddComponentAndRegister<IAudioService, AudioService>(root),
    };
```

---

## 13. Services de Gameplay

O `GameBootstrapper` pertence ao assembly `Whispers.Core` e não pode referenciar tipos de `Whispers.Gameplay`.

Portanto, Services como estes não devem ser registrados no `GameBootstrapper`:

- `NavigationService`;
- `GameLoopService`;
- `InventoryService`;
- `HouseDefenseService`;
- `EntityDirectorService`.

Eles serão registrados por:

```text
Assets/_Project/Scripts/Gameplay/Bootstrap/GameplayCompositionRoot.cs
```

Esse Composition Root ainda não foi implementado.

Responsabilidades futuras:

- criar Services da sessão de Gameplay;
- resolver dependências Core necessárias;
- registrar Services de Gameplay;
- manter a propriedade dos Services criados;
- remover e descartar apenas os Services da sessão ao sair do Gameplay.

Um `GameplayCompositionRoot` não deve chamar `ServiceLocator.Shutdown()` indiscriminadamente caso existam Services Core ativos, pois isso encerraria todo o registro global. Ele deverá rastrear e remover apenas os contratos que possui, preferencialmente em ordem inversa à instalação.

---

## 14. Comunicação entre Services

Services não devem depender diretamente de implementações concretas.

Preferir:

```csharp
public sealed class ExampleController
{
    private readonly IAudioService audioService;

    public ExampleController(IAudioService audioService)
    {
        this.audioService = audioService;
    }
}
```

Evitar:

```csharp
public sealed class ExampleController
{
    public void Execute()
    {
        AudioService audio =
            GlobalServices.Get<AudioService>();
    }
}
```

Para notificações entre módulos independentes, utilizar ScriptableObject Event Channels.

Exemplo:

```text
GameLoopService
    └── Publica OnNightStarted
            ├── UI reage
            ├── áudio reage
            └── Entity Director reage
```

O publicador não precisa conhecer os consumidores.

---

## 15. Regras de uso

### Permitido

- Resolver interfaces em Bootstrappers e Composition Roots.
- Registrar implementações por contratos específicos.
- Utilizar `Get<T>()` para dependências obrigatórias.
- Utilizar `TryGet<T>()` para dependências realmente opcionais.
- Passar dependências por construtor ou método de configuração.
- Manter `Initialize()` e `Dispose()` idempotentes.
- Registrar dependências antes de seus consumidores.
- Utilizar Event Channels para comunicação desacoplada.

### Evitar

- Chamar `ServiceLocator.Get<T>()` em toda classe de Gameplay.
- Registrar Services pelo tipo concreto quando existe uma interface pública.
- Substituir silenciosamente um registro existente.
- Criar novos Singletons.
- Utilizar `GameObject.Find` para localizar Services.
- Utilizar `FindObjectOfType` em runtime.
- Chamar `DontDestroyOnLoad()` em cada Service.
- Acessar UI diretamente a partir de Gameplay.
- Colocar regras de domínio dentro do Bootstrapper.
- Colocar toda a lógica do jogo em um único Service.
- Referenciar `UnityEditor` ou `UnityEditorInternal` em código de runtime.
- Utilizar `MonoScript` como cadastro de Services para builds.
- Ignorar falhas de inicialização.
- Esquecer unsubscribe no `Dispose()`.

---

## 16. Testes recomendados

### 16.1 Inicialização desativada

Com:

```csharp
private static bool initializeServices = false;
```

Validar:

- não existe `[WHISPERS_CORE_SERVICES]`;
- não existe `AudioService` em runtime;
- não existem `AudioSource` criados pelo Bootstrapper;
- nenhum log de registro de Service é emitido;
- o Event Bus continua funcionando.

### 16.2 Inicialização ativada

Ao ativar Services futuramente, validar:

- existe apenas um root;
- cada contrato é registrado uma única vez;
- cada `Initialize()` executa uma vez;
- Services são resolvidos pela interface;
- o root sobrevive a mudanças de cena;
- Services não criam roots persistentes próprios.

### 16.3 Encerramento

Validar:

- `Dispose()` executa ao sair do Play Mode;
- o encerramento acontece na ordem inversa ao registro;
- nenhuma inscrição permanece ativa;
- uma exceção em um Service não impede o descarte dos demais.

### 16.4 Domain Reload

Executar os testes com:

- Reload Domain ativado;
- Reload Domain desativado.

Em ambos os casos, validar:

- nenhum registro duplicado;
- nenhuma referência destruída;
- nenhum root residual;
- comportamento idêntico entre execuções.

### 16.5 Falha de inicialização

Criar temporariamente um Service de teste cujo `Initialize()` lança uma exceção.

Validar:

- o registro incompleto é removido;
- `Dispose()` é tentado;
- Services anteriores são descartados;
- o root é destruído;
- o erro original aparece como causa da falha crítica.

---

## 17. Problemas comuns

### Service não encontrado

Erro esperado:

```text
O serviço IExampleService não está registrado.
```

Verificar:

1. `initializeServices` está ativo?
2. O installer foi adicionado à lista correta?
3. O Service foi registrado pelo contrato esperado?
4. A resolução usa a mesma interface usada no registro?
5. O `Initialize()` lançou uma exceção?

### Registro duplicado

Erro esperado:

```text
Já existe um serviço registrado para IExampleService.
```

Não substituir o registro. Localizar o Bootstrapper ou Composition Root duplicado.

### MonoBehaviour não pode ser anexado

Verificar:

- o script está em um assembly de runtime válido;
- o assembly não possui `includePlatforms: Editor`;
- o caminho não está dentro de uma pasta chamada `Editor`;
- o script não depende de `UnityEditor`;
- não existem erros de compilação.

### Object ambíguo

Quando um arquivo importa `System` e `UnityEngine`, usar:

```csharp
using UObject = UnityEngine.Object;
```

E chamar:

```csharp
UObject.DontDestroyOnLoad(root);
UObject.Destroy(root);
```

### AudioSource nulo

O `AudioService` é criado proceduralmente, portanto referências serializadas podem começar nulas. Sua implementação atual cria as fontes necessárias durante `Initialize()`.

### Service permanece entre execuções

Verificar:

- `ResetStaticState()` está sendo chamado em `SubsystemRegistration`;
- o Domain Reload está configurado como esperado;
- o root possui `CoreServicesLifetime`;
- `Dispose()` não está lançando uma exceção não tratada internamente.

---

## 18. Resumo do ciclo de vida

```text
CRIAÇÃO
GameBootstrapper
    └── Cria root persistente
        └── Executa installers
            └── ServiceLocator.Register<T>()
                └── IService.Initialize()

USO
Composition Root ou controlador
    └── ServiceLocator.Get<Interface>()
        └── Dependência repassada explicitamente

REMOÇÃO INDIVIDUAL
Composition Root
    └── ServiceLocator.Unregister<Interface>()
        └── IService.Dispose()

ENCERRAMENTO GLOBAL
CoreServicesLifetime
    └── ServiceLocator.Shutdown()
        └── IService.Dispose() em ordem inversa

NOVA EXECUÇÃO
SubsystemRegistration
    └── ResetStaticState()
```

---

## 19. Situação final atual

O sistema de Services está arquitetado e implementado, mas permanece inativo por decisão do projeto.

```text
ServiceLocator: implementado
IService: implementado
GameBootstrapper: implementado
Lista procedural: implementada e vazia
Controle booleano: false
CoreServicesLifetime: implementado, mas não instanciado
IAudioService: implementado
AudioService: implementado, mas não registrado
Contagem pública de Services: removida
Suporte a Domain Reload: implementado
Inicialização automática de Services: desativada
```

Nenhum Service deve ser ativado até existir uma decisão explícita para alterar o booleano e adicionar seu installer à lista procedural.
