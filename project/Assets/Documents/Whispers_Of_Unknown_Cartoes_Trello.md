# Whispers Of Unknown — Cartões de implementação no Trello

> Baseado na documentação **Arquitetura Final de Navegação, Hotspots e Áudio — versão 2.0**.  
> Total: **29 cartões**, organizados por fundação, vertical slices e integração final.

## Fluxo recomendado do quadro

1. Crie o cartão na coluna de disciplina indicada: `📋 Programação`, `🔈Sound Design`, `🖼️ Arte` ou `📌 Visão Geral`.
2. Ao iniciar o trabalho, mova para `🚧 Em Andamento`.
3. Ao concluir a implementação ou produção, mova para `👀 Revisão / Teste`.
4. Problemas confirmados durante a revisão geram cartões em `🐞 Bugs`.
5. Depois de cumprir todos os critérios, mova para `✅ Concluído`.
6. `💡 Ideias Futuras` recebe somente funcionalidades fora do escopo desta arquitetura.
7. `✍️ Narrativa` não recebe cartões deste pacote, pois o documento trata de infraestrutura e apresentação sistêmica.

## Marcos

- **Fundação:** cartões 01–04.
- **Vertical Slice 1 — Navegação:** cartões 05–10.
- **Vertical Slice 2 — Interação:** cartões 11–15.
- **Vertical Slice 3 — Ciclo e checkpoint:** cartões 16–18.
- **Vertical Slice 4 — Áudio:** cartões 19–25.
- **Polimento e estabilização:** cartões 26–29.

---

# Cartão 01 — Controla o roadmap da arquitetura

**Coluna inicial:** 📌 Visão Geral  
**Etiqueta:** Prioridade Alta

## Descrição

### Objetivo

Organiza a implementação da arquitetura de navegação, hotspots, checkpoint, câmera e áudio pelos vertical slices definidos.

### Critério de pronto

- Os cartões de implementação estão criados nas colunas correspondentes.
- Os cartões estão ordenados conforme os marcos e dependências gerais.
- O andamento de cada vertical slice está visível no quadro.
- Um marco só é considerado concluído depois de seu cartão de revisão.
- Mudanças de escopo são registradas antes de alterar os cartões em andamento.

## Checklist

- [ ] Os cartões de implementação estão criados nas colunas correspondentes.
- [ ] Os cartões estão ordenados conforme os marcos e dependências gerais.
- [ ] O andamento de cada vertical slice está visível no quadro.
- [ ] Um marco só é considerado concluído depois de seu cartão de revisão.
- [ ] Mudanças de escopo são registradas antes de alterar os cartões em andamento.

---

# Cartão 02 — Configura a infraestrutura-base das cenas

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta

## Descrição

### Objetivo

Configura as cenas espelhadas de Dia e Noite com os managers locais, referências obrigatórias, câmera compartilhada, EventSystem e definições de cena.

### Critério de pronto

- As cenas de Dia e Noite possuem uma raiz de gameplay padronizada.
- O `GameplaySceneController` referencia explicitamente os managers locais.
- O `GameplaySceneDefinition` identifica etapa, período e ViewNode inicial.
- O EventSystem usa `StandaloneInputModule` e Input Manager legado.
- A câmera de apresentação e os Canvases de gameplay e UI estão separados corretamente.
- O boot detecta referências obrigatórias ausentes sem deixar a cena bloqueada.

## Checklist

- [ ] As cenas de Dia e Noite possuem uma raiz de gameplay padronizada.
- [ ] O `GameplaySceneController` referencia explicitamente os managers locais.
- [ ] O `GameplaySceneDefinition` identifica etapa, período e ViewNode inicial.
- [ ] O EventSystem usa `StandaloneInputModule` e Input Manager legado.
- [ ] A câmera de apresentação e os Canvases de gameplay e UI estão separados corretamente.
- [ ] O boot detecta referências obrigatórias ausentes sem deixar a cena bloqueada.

---

# Cartão 03 — Implementa o estado global da sessão

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta

## Descrição

### Objetivo

Implementa o `GameSessionManager` como único singleton e mantém a cópia de trabalho do ciclo sem referências a objetos de cena.

### Critério de pronto

- Existe apenas uma instância persistente do `GameSessionManager`.
- O manager sobrevive às trocas de cena sem duplicação.
- Inventário, IDs coletados e fatos persistentes possuem estado de trabalho em memória.
- Slot, etapa e Dia atuais estão disponíveis para o fluxo global.
- A ferramenta selecionada é transitória e pode ser limpa na troca de período.
- O manager não mantém referências a ViewNodes, hotspots ou managers locais.

## Checklist

- [ ] Existe apenas uma instância persistente do `GameSessionManager`.
- [ ] O manager sobrevive às trocas de cena sem duplicação.
- [ ] Inventário, IDs coletados e fatos persistentes possuem estado de trabalho em memória.
- [ ] Slot, etapa e Dia atuais estão disponíveis para o fluxo global.
- [ ] A ferramenta selecionada é transitória e pode ser limpa na troca de período.
- [ ] O manager não mantém referências a ViewNodes, hotspots ou managers locais.

---

# Cartão 04 — Implementa o bloqueio da entrada de gameplay

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta

## Descrição

### Objetivo

Implementa o `InputBlocker` com contagem de motivos e separa o bloqueio do cenário da interação com UI autorizada.

### Critério de pronto

- O bloqueio aceita motivos independentes de boot, transição, modal, pausa, cutscene e encerramento.
- A entrada só é liberada quando todos os motivos ativos são removidos.
- Hotspots não processam hover, dwell ou clique durante bloqueio.
- Solicitações feitas durante bloqueio são descartadas sem fila.
- Dwell é cancelado e zerado quando o bloqueio começa.
- Modais continuam recebendo entrada enquanto os hotspots do cenário permanecem bloqueados.
- O desbloqueio marca hotspots sob o cursor como “exige saída”.

## Checklist

- [ ] O bloqueio aceita motivos independentes de boot, transição, modal, pausa, cutscene e encerramento.
- [ ] A entrada só é liberada quando todos os motivos ativos são removidos.
- [ ] Hotspots não processam hover, dwell ou clique durante bloqueio.
- [ ] Solicitações feitas durante bloqueio são descartadas sem fila.
- [ ] Dwell é cancelado e zerado quando o bloqueio começa.
- [ ] Modais continuam recebendo entrada enquanto os hotspots do cenário permanecem bloqueados.
- [ ] O desbloqueio marca hotspots sob o cursor como “exige saída”.

---

# Cartão 05 — Implementa o ciclo de vida dos ViewNodes

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta

## Descrição

### Objetivo

Implementa ViewNodes como raízes permanentes com filhos visuais e funcionais controlados, mantendo apenas um ponto de visão logicamente apresentado.

### Critério de pronto

- Cada ViewNode possui `ViewNodeController` e `ViewNodeDefinition` com ID estável.
- Todas as raízes permanecem ativas e somente os filhos do ViewNode apresentado são habilitados.
- `OnNodeExit` ocorre antes da desativação dos filhos.
- O destino é preparado sem raycast antes de `OnNodeEnter`.
- Estado visual, condições, câmera e áudio são resolvidos durante a entrada.
- ViewNodes não apresentados não executam lógica visual ou interativa.
- O boot apresenta somente o ViewNode inicial configurado.

## Checklist

- [ ] Cada ViewNode possui `ViewNodeController` e `ViewNodeDefinition` com ID estável.
- [ ] Todas as raízes permanecem ativas e somente os filhos do ViewNode apresentado são habilitados.
- [ ] `OnNodeExit` ocorre antes da desativação dos filhos.
- [ ] O destino é preparado sem raycast antes de `OnNodeEnter`.
- [ ] Estado visual, condições, câmera e áudio são resolvidos durante a entrada.
- [ ] ViewNodes não apresentados não executam lógica visual ou interativa.
- [ ] O boot apresenta somente o ViewNode inicial configurado.

---

# Cartão 06 — Implementa a base comum dos hotspots

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta

## Descrição

### Objetivo

Implementa o `HotspotBase` com detecção de ponteiro, bloqueio, reentrada, modos de ativação e políticas simples de repetição.

### Critério de pronto

- Hotspots usam regiões `RectTransform` e o EventSystem configurado.
- A base reconhece entrada, saída e clique do cursor.
- Os modos hover imediato, dwell e clique são representáveis.
- O primeiro vertical slice executa corretamente o modo hover imediato.
- A regra “exige saída” só é liberada por saída física do cursor.
- Movimento de câmera ou parallax não satisfaz sozinho a reentrada.
- Existem apenas as políticas uso único e repetível, sem cooldown genérico.

## Checklist

- [ ] Hotspots usam regiões `RectTransform` e o EventSystem configurado.
- [ ] A base reconhece entrada, saída e clique do cursor.
- [ ] Os modos hover imediato, dwell e clique são representáveis.
- [ ] O primeiro vertical slice executa corretamente o modo hover imediato.
- [ ] A regra “exige saída” só é liberada por saída física do cursor.
- [ ] Movimento de câmera ou parallax não satisfaz sozinho a reentrada.
- [ ] Existem apenas as políticas uso único e repetível, sem cooldown genérico.

---

# Cartão 07 — Implementa a navegação e as transições básicas

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta

## Descrição

### Objetivo

Implementa a troca validada de ViewNodes com bloqueio, ocultação, ponto de troca, revelação e margem pós-transição.

### Critério de pronto

- `NavigationHotspot` solicita e `NavigationManager` executa a navegação.
- Destino ausente, igual ao atual ou inválido é descartado com warning.
- Solicitações concorrentes são descartadas e não existe fila.
- Corte seco e fade possuem ocultação, ponto de troca e revelação definidos.
- O ViewNode só é substituído no ponto de troca do perfil.
- O input permanece bloqueado durante toda a transição e por 0,05 segundo em tempo não escalado.
- Hotspots sob o cursor exigem reentrada depois do desbloqueio.
- Uma falha restaura ou mantém o ViewNode anterior e sempre libera o bloqueio.

## Checklist

- [ ] `NavigationHotspot` solicita e `NavigationManager` executa a navegação.
- [ ] Destino ausente, igual ao atual ou inválido é descartado com warning.
- [ ] Solicitações concorrentes são descartadas e não existe fila.
- [ ] Corte seco e fade possuem ocultação, ponto de troca e revelação definidos.
- [ ] O ViewNode só é substituído no ponto de troca do perfil.
- [ ] O input permanece bloqueado durante toda a transição e por 0,05 segundo em tempo não escalado.
- [ ] Hotspots sob o cursor exigem reentrada depois do desbloqueio.
- [ ] Uma falha restaura ou mantém o ViewNode anterior e sempre libera o bloqueio.

---

# Cartão 08 — Integra a câmera compartilhada e o parallax

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Média

## Descrição

### Objetivo

Integra a câmera compartilhada com perfil por ViewNode, movimento pelo mouse, recentralização, shake e parallax em camadas.

### Critério de pronto

- Existe uma única câmera de apresentação para todos os ViewNodes.
- `ViewCameraProfile` controla pan, roll, suavização, dead zone, zoom e intensidade de parallax.
- Camadas recebem multiplicadores diferentes sem quebrar a composição.
- Hotspots de objetos acompanham a transformação de suas camadas visuais.
- UI global e cursor permanecem estáveis.
- A resposta ao mouse é suspensa durante ocultação e retomada após revelação.
- O perfil do destino é aplicado no ponto de troca.
- Os limites definidos não revelam bordas vazias.

## Checklist

- [ ] Existe uma única câmera de apresentação para todos os ViewNodes.
- [ ] `ViewCameraProfile` controla pan, roll, suavização, dead zone, zoom e intensidade de parallax.
- [ ] Camadas recebem multiplicadores diferentes sem quebrar a composição.
- [ ] Hotspots de objetos acompanham a transformação de suas camadas visuais.
- [ ] UI global e cursor permanecem estáveis.
- [ ] A resposta ao mouse é suspensa durante ocultação e retomada após revelação.
- [ ] O perfil do destino é aplicado no ponto de troca.
- [ ] Os limites definidos não revelam bordas vazias.

---

# Cartão 09 — Prepara uma composição visual para parallax

**Coluna inicial:** 🖼️ Arte  
**Etiqueta:** Prioridade Média

## Descrição

### Objetivo

Prepara as telas do vertical slice em camadas compatíveis com pan, tilt, parallax e overscan.

### Critério de pronto

- Cada tela possui separação coerente entre fundo, arquitetura, objetos, primeiro plano e overlays necessários.
- As camadas possuem margem suficiente para o deslocamento máximo previsto.
- Objetos interativos estão isolados quando precisam de hotspot móvel ou estado alternativo.
- A composição não revela áreas vazias durante os limites de câmera aprovados.
- As variações de estado reutilizam camadas locais em vez de duplicar a tela inteira quando possível.
- A nomenclatura dos assets identifica ViewNode, camada e estado.

## Checklist

- [ ] Cada tela possui separação coerente entre fundo, arquitetura, objetos, primeiro plano e overlays necessários.
- [ ] As camadas possuem margem suficiente para o deslocamento máximo previsto.
- [ ] Objetos interativos estão isolados quando precisam de hotspot móvel ou estado alternativo.
- [ ] A composição não revela áreas vazias durante os limites de câmera aprovados.
- [ ] As variações de estado reutilizam camadas locais em vez de duplicar a tela inteira quando possível.
- [ ] A nomenclatura dos assets identifica ViewNode, camada e estado.

---

# Cartão 10 — Valida o Vertical Slice 1 de navegação

**Coluna inicial:** 👀 Revisão / Teste  
**Etiqueta:** Prioridade Alta

## Descrição

### Objetivo

Valida o fluxo completo entre dois ou mais ViewNodes com hover imediato, transição, câmera e proteção contra reativação acidental.

### Critério de pronto

- Apenas o ViewNode apresentado recebe raycasts e entrada.
- Hover imediato realiza uma única troca por entrada física do cursor.
- Manter o cursor parado não causa navegação em cadeia.
- Transição bloqueia o input, troca no ponto correto e libera após 0,05 segundo.
- Solicitações concorrentes não produzem fila ou estado inválido.
- Parallax mantém arte e hotspot alinhados.
- Boot, transição e desbloqueio aplicam corretamente a reentrada.
- Nenhum caminho de erro deixa o `InputBlocker` preso.

## Checklist

- [ ] Apenas o ViewNode apresentado recebe raycasts e entrada.
- [ ] Hover imediato realiza uma única troca por entrada física do cursor.
- [ ] Manter o cursor parado não causa navegação em cadeia.
- [ ] Transição bloqueia o input, troca no ponto correto e libera após 0,05 segundo.
- [ ] Solicitações concorrentes não produzem fila ou estado inválido.
- [ ] Parallax mantém arte e hotspot alinhados.
- [ ] Boot, transição e desbloqueio aplicam corretamente a reentrada.
- [ ] Nenhum caminho de erro deixa o `InputBlocker` preso.

---

# Cartão 11 — Implementa o estado de cena e as condições

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta

## Descrição

### Objetivo

Implementa o `SceneRuntimeState`, as condições reutilizáveis e a atualização integral de apresentação ao entrar em cada ViewNode.

### Critério de pronto

- `SceneRuntimeState` armazena flags temporárias e emite eventos de alteração.
- Condições ScriptableObject não armazenam estado de runtime ou referências transitórias da cena.
- Existem condições iniciais para item, flag, fato persistente, período e interação concluída.
- Cada hotspot aceita política Todas ou Qualquer sem grupos aninhados.
- ViewNode reavalia todas as condições antes de habilitar raycasts.
- Somente o ViewNode apresentado reage a mudanças por eventos.
- Toda ação revalida suas condições imediatamente antes da execução.
- Perda de condição durante dwell cancela e zera o progresso.

## Checklist

- [ ] `SceneRuntimeState` armazena flags temporárias e emite eventos de alteração.
- [ ] Condições ScriptableObject não armazenam estado de runtime ou referências transitórias da cena.
- [ ] Existem condições iniciais para item, flag, fato persistente, período e interação concluída.
- [ ] Cada hotspot aceita política Todas ou Qualquer sem grupos aninhados.
- [ ] ViewNode reavalia todas as condições antes de habilitar raycasts.
- [ ] Somente o ViewNode apresentado reage a mudanças por eventos.
- [ ] Toda ação revalida suas condições imediatamente antes da execução.
- [ ] Perda de condição durante dwell cancela e zera o progresso.

---

# Cartão 12 — Implementa interações e resultados

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta

## Descrição

### Objetivo

Implementa `InteractionHotspot`, definições de interação e resultados executados pelos managers responsáveis.

### Critério de pronto

- `InteractionHotspot` solicita ações ao `InteractionManager`.
- Uma ação em execução descarta segunda solicitação sem usar cooldown.
- Resultados podem entregar/remover item, registrar coleta, alterar flag, registrar fato e trocar visual.
- Navegação e encerramento de período são solicitados aos managers correspondentes.
- Estado visível em vários ViewNodes é registrado no `SceneRuntimeState`.
- A ação é considerada comprometida depois da validação final.
- Uso único e repetível funcionam conforme a definição.
- Feedback local não se torna requisito para concluir a regra de gameplay.

## Checklist

- [ ] `InteractionHotspot` solicita ações ao `InteractionManager`.
- [ ] Uma ação em execução descarta segunda solicitação sem usar cooldown.
- [ ] Resultados podem entregar/remover item, registrar coleta, alterar flag, registrar fato e trocar visual.
- [ ] Navegação e encerramento de período são solicitados aos managers correspondentes.
- [ ] Estado visível em vários ViewNodes é registrado no `SceneRuntimeState`.
- [ ] A ação é considerada comprometida depois da validação final.
- [ ] Uso único e repetível funcionam conforme a definição.
- [ ] Feedback local não se torna requisito para concluir a regra de gameplay.

---

# Cartão 13 — Implementa inventário e UI modal

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta

## Descrição

### Objetivo

Implementa o inventário e os modais sem permitir que a UI interaja acidentalmente com hotspots do cenário.

### Critério de pronto

- O inventário apresenta IDs, quantidades, ícones e descrições a partir de `ItemDefinition`.
- Abrir modal adiciona motivo de bloqueio e cancela dwell.
- Hotspots do cenário ficam bloqueados enquanto controles do modal funcionam.
- Fechar modal remove somente seu próprio motivo de bloqueio.
- Hotspots sob o cursor exigem saída e nova entrada após o fechamento.
- `InventoryPanel` permite selecionar e cancelar ferramenta.
- `DocumentPanel` apresenta documentos e mídia sem depender do ViewNode permanecer ativo.
- Abrir modal não altera automaticamente o `timeScale`.

## Checklist

- [ ] O inventário apresenta IDs, quantidades, ícones e descrições a partir de `ItemDefinition`.
- [ ] Abrir modal adiciona motivo de bloqueio e cancela dwell.
- [ ] Hotspots do cenário ficam bloqueados enquanto controles do modal funcionam.
- [ ] Fechar modal remove somente seu próprio motivo de bloqueio.
- [ ] Hotspots sob o cursor exigem saída e nova entrada após o fechamento.
- [ ] `InventoryPanel` permite selecionar e cancelar ferramenta.
- [ ] `DocumentPanel` apresenta documentos e mídia sem depender do ViewNode permanecer ativo.
- [ ] Abrir modal não altera automaticamente o `timeScale`.

---

# Cartão 14 — Implementa ferramentas, dwell e feedback de hotspot

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Média

## Descrição

### Objetivo

Implementa o uso de ferramentas, hover com permanência e feedback padronizado sem adicionar cooldown genérico.

### Critério de pronto

- `ToolHotspot` consulta a ferramenta selecionada e valida compatibilidade.
- Sucesso consome carga somente quando a definição determinar.
- Falha apresenta mensagem genérica, não consome e mantém a seleção.
- Destaque de alvos válidos respeita a opção da ferramenta e permanece desligado por padrão.
- Dwell usa tempo escalado e zera ao sair, bloquear ou perder condição.
- `HotspotFeedbackProfile` controla cursor, progresso, hover e sons abstratos.
- `GlobalHotspotSettings` contém dwell padrão e margem pós-transição, sem campo de cooldown.
- Repetição depende de nova ação deliberada ou reentrada.

## Checklist

- [ ] `ToolHotspot` consulta a ferramenta selecionada e valida compatibilidade.
- [ ] Sucesso consome carga somente quando a definição determinar.
- [ ] Falha apresenta mensagem genérica, não consome e mantém a seleção.
- [ ] Destaque de alvos válidos respeita a opção da ferramenta e permanece desligado por padrão.
- [ ] Dwell usa tempo escalado e zera ao sair, bloquear ou perder condição.
- [ ] `HotspotFeedbackProfile` controla cursor, progresso, hover e sons abstratos.
- [ ] `GlobalHotspotSettings` contém dwell padrão e margem pós-transição, sem campo de cooldown.
- [ ] Repetição depende de nova ação deliberada ou reentrada.

---

# Cartão 15 — Valida o Vertical Slice 2 de interação

**Coluna inicial:** 👀 Revisão / Teste  
**Etiqueta:** Prioridade Alta

## Descrição

### Objetivo

Valida condições, estado compartilhado, inventário, modais, ferramentas, dwell e repetição em um fluxo jogável.

### Critério de pronto

- Alterar inventário ou flag atualiza os hotspots do ViewNode apresentado.
- Retornar a um ViewNode resolve condições alteradas enquanto ele estava oculto.
- Um objeto compartilhado apresenta o mesmo estado em diferentes pontos de visão.
- Interações não executam duas vezes por clique ou hover residual.
- Uso de ferramenta válido e inválido produz os resultados esperados.
- Modal bloqueia o cenário sem bloquear seus próprios controles.
- Dwell cancela corretamente em saída, bloqueio e perda de condição.
- Não existe comportamento dependente de cooldown genérico.

## Checklist

- [ ] Alterar inventário ou flag atualiza os hotspots do ViewNode apresentado.
- [ ] Retornar a um ViewNode resolve condições alteradas enquanto ele estava oculto.
- [ ] Um objeto compartilhado apresenta o mesmo estado em diferentes pontos de visão.
- [ ] Interações não executam duas vezes por clique ou hover residual.
- [ ] Uso de ferramenta válido e inválido produz os resultados esperados.
- [ ] Modal bloqueia o cenário sem bloquear seus próprios controles.
- [ ] Dwell cancela corretamente em saída, bloqueio e perda de condição.
- [ ] Não existe comportamento dependente de cooldown genérico.

---

# Cartão 16 — Implementa fatos persistentes e a transferência Dia → Noite

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta

## Descrição

### Objetivo

Implementa fatos persistentes e mantém em memória as consequências do Dia necessárias para configurar a Noite.

### Critério de pronto

- Fatos persistentes possuem IDs estáveis e significado separado de itens coletados.
- Interações do Dia podem registrar fatos na cópia de trabalho do ciclo.
- Inventário, coletados e fatos atravessam a troca de cena em memória.
- `SceneRuntimeState` do Dia é descartado e não atravessa automaticamente.
- A cena da Noite lê o estado de trabalho e aplica suas condições iniciais.
- A ferramenta selecionada é limpa antes de carregar a Noite.
- A passagem Dia → Noite não sobrescreve o checkpoint em disco.

## Checklist

- [ ] Fatos persistentes possuem IDs estáveis e significado separado de itens coletados.
- [ ] Interações do Dia podem registrar fatos na cópia de trabalho do ciclo.
- [ ] Inventário, coletados e fatos atravessam a troca de cena em memória.
- [ ] `SceneRuntimeState` do Dia é descartado e não atravessa automaticamente.
- [ ] A cena da Noite lê o estado de trabalho e aplica suas condições iniciais.
- [ ] A ferramenta selecionada é limpa antes de carregar a Noite.
- [ ] A passagem Dia → Noite não sobrescreve o checkpoint em disco.

---

# Cartão 17 — Implementa o checkpoint diário e o ciclo global

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta

## Descrição

### Objetivo

Implementa o save como checkpoint do início do Dia e consolida um novo checkpoint somente após a conclusão da Noite.

### Critério de pronto

- `GameSaveData` registra versão, slot, etapa, Dia, inventário, coletados, fatos e metadados.
- Novo jogo cria um checkpoint válido antes de iniciar o primeiro Dia.
- Carregar um slot sempre abre o início do Dia registrado.
- Sair ou falhar durante Dia ou Noite descarta a cópia de trabalho não consolidada.
- Concluir a Noite prepara e grava o checkpoint do próximo Dia.
- O próximo Dia só é carregado depois de uma gravação bem-sucedida.
- Falha de save preserva o checkpoint anterior e permite tentar novamente.
- A UI avisa que sair retorna ao início do Dia atual.

## Checklist

- [ ] `GameSaveData` registra versão, slot, etapa, Dia, inventário, coletados, fatos e metadados.
- [ ] Novo jogo cria um checkpoint válido antes de iniciar o primeiro Dia.
- [ ] Carregar um slot sempre abre o início do Dia registrado.
- [ ] Sair ou falhar durante Dia ou Noite descarta a cópia de trabalho não consolidada.
- [ ] Concluir a Noite prepara e grava o checkpoint do próximo Dia.
- [ ] O próximo Dia só é carregado depois de uma gravação bem-sucedida.
- [ ] Falha de save preserva o checkpoint anterior e permite tentar novamente.
- [ ] A UI avisa que sair retorna ao início do Dia atual.

---

# Cartão 18 — Valida o ciclo completo e o rollback diário

**Coluna inicial:** 👀 Revisão / Teste  
**Etiqueta:** Prioridade Alta

## Descrição

### Objetivo

Valida o ciclo Dia → Noite → próximo Dia e confirma o retorno ao checkpoint diário em todas as saídas antecipadas.

### Critério de pronto

- Item coletado no Dia fica disponível na Noite da mesma sessão.
- Fato criado no Dia altera corretamente uma condição da Noite.
- O checkpoint em disco permanece igual durante Dia → Noite.
- Fechar o jogo durante o Dia retorna ao início daquele Dia.
- Fechar ou falhar durante a Noite também retorna ao início daquele Dia.
- Concluir a Noite cria um checkpoint do próximo Dia com os dados consolidados.
- Item consumido não reaparece quando seu ID coletado já está consolidado.
- Um save interrompido ou inválido não destrói o checkpoint anterior.

## Checklist

- [ ] Item coletado no Dia fica disponível na Noite da mesma sessão.
- [ ] Fato criado no Dia altera corretamente uma condição da Noite.
- [ ] O checkpoint em disco permanece igual durante Dia → Noite.
- [ ] Fechar o jogo durante o Dia retorna ao início daquele Dia.
- [ ] Fechar ou falhar durante a Noite também retorna ao início daquele Dia.
- [ ] Concluir a Noite cria um checkpoint do próximo Dia com os dados consolidados.
- [ ] Item consumido não reaparece quando seu ID coletado já está consolidado.
- [ ] Um save interrompido ou inválido não destrói o checkpoint anterior.

---

# Cartão 19 — Define a estrutura de mixagem e as regras de áudio

**Coluna inicial:** 🔈Sound Design  
**Etiqueta:** Prioridade Alta

## Descrição

### Objetivo

Define a hierarquia do mixer, a autoridade de cada tipo de som e as regras de inteligibilidade da experiência sonora.

### Critério de pronto

- O mixer possui grupos para Ambience, Threats, Interactions, Equipment, Media, Transition, UI e Music.
- ViewNode define o áudio-alvo, link define a mudança e `TransitionProfile` define somente o SFX.
- Sons contínuos, locais, persistentes, de ameaça, media, UI e transição possuem escopos documentados.
- Sinais críticos possuem prioridade sobre ruídos decorativos.
- Sons de feedback, interação e transição não duplicam a mesma resposta.
- Pausa e modais possuem regras de mixagem distintas.
- A orientação sonora principal depende do ViewNode, não do movimento do mouse.

## Checklist

- [ ] O mixer possui grupos para Ambience, Threats, Interactions, Equipment, Media, Transition, UI e Music.
- [ ] ViewNode define o áudio-alvo, link define a mudança e `TransitionProfile` define somente o SFX.
- [ ] Sons contínuos, locais, persistentes, de ameaça, media, UI e transição possuem escopos documentados.
- [ ] Sinais críticos possuem prioridade sobre ruídos decorativos.
- [ ] Sons de feedback, interação e transição não duplicam a mesma resposta.
- [ ] Pausa e modais possuem regras de mixagem distintas.
- [ ] A orientação sonora principal depende do ViewNode, não do movimento do mouse.

---

# Cartão 20 — Implementa o ambiente contínuo e os perfis acústicos

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta

## Descrição

### Objetivo

Implementa o `SceneAudioController` e os `ViewAudioProfile`s sem reiniciar o ambiente contínuo a cada troca de ViewNode.

### Critério de pronto

- `SceneAudioController` é local à cena e não cria novo singleton.
- A cena possui ambiente-base contínuo de Dia ou Noite.
- `ViewAudioProfile` define zona, volume, pan, filtros, reverberação e camadas locais.
- Trocar entre ViewNodes da mesma zona mantém os loops e altera apenas a perspectiva.
- Sons locais entram e saem por comportamento controlado.
- O perfil acústico do destino é aplicado no ponto de troca da navegação.
- Perfis ScriptableObject não armazenam fontes tocando ou estado de runtime.

## Checklist

- [ ] `SceneAudioController` é local à cena e não cria novo singleton.
- [ ] A cena possui ambiente-base contínuo de Dia ou Noite.
- [ ] `ViewAudioProfile` define zona, volume, pan, filtros, reverberação e camadas locais.
- [ ] Trocar entre ViewNodes da mesma zona mantém os loops e altera apenas a perspectiva.
- [ ] Sons locais entram e saem por comportamento controlado.
- [ ] O perfil acústico do destino é aplicado no ponto de troca da navegação.
- [ ] Perfis ScriptableObject não armazenam fontes tocando ou estado de runtime.

---

# Cartão 21 — Produz os ambientes sonoros do vertical slice

**Coluna inicial:** 🔈Sound Design  
**Etiqueta:** Prioridade Média

## Descrição

### Objetivo

Produz as camadas de ambiente, detalhes locais e variações acústicas necessárias para testar os ViewNodes do vertical slice.

### Critério de pronto

- Existe uma camada-base contínua apropriada ao período testado.
- Existem camadas separadas para exterior, estrutura, eletricidade e detalhes locais necessários.
- ViewNodes do mesmo ambiente possuem perspectivas distinguíveis sem reiniciar os loops.
- Sons aleatórios possuem intervalo, limite de simultaneidade e prevenção de repetição imediata.
- Ruídos decorativos não mascaram os sinais mecânicos do teste.
- Assets possuem nomenclatura e categoria de mixer definidas.
- Loops não apresentam emendas, estalos ou diferenças abruptas não intencionais.

## Checklist

- [ ] Existe uma camada-base contínua apropriada ao período testado.
- [ ] Existem camadas separadas para exterior, estrutura, eletricidade e detalhes locais necessários.
- [ ] ViewNodes do mesmo ambiente possuem perspectivas distinguíveis sem reiniciar os loops.
- [ ] Sons aleatórios possuem intervalo, limite de simultaneidade e prevenção de repetição imediata.
- [ ] Ruídos decorativos não mascaram os sinais mecânicos do teste.
- [ ] Assets possuem nomenclatura e categoria de mixer definidas.
- [ ] Loops não apresentam emendas, estalos ou diferenças abruptas não intencionais.

---

# Cartão 22 — Integra o áudio às transições de navegação

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta

## Descrição

### Objetivo

Integra os modos Manter, Crossfade, Imediato e Especial às fases visuais das transições de ViewNode.

### Critério de pronto

- Cada `NavigationLinkDefinition` pode escolher um modo de áudio ou fallback.
- Manter preserva loops e interpola somente perspectiva.
- Crossfade reduz o ambiente atual e introduz o destino sem corte abrupto.
- Imediato troca o áudio de forma intencional no ponto configurado.
- Especial delega comportamento autorado ao `SceneAudioController`.
- O SFX do `TransitionProfile` toca no momento definido sem controlar o ambiente permanente.
- O áudio do destino entra sincronizado com ocultação, troca e revelação.
- A margem de 0,05 segundo afeta apenas o input, não cria cooldown de áudio.

## Checklist

- [ ] Cada `NavigationLinkDefinition` pode escolher um modo de áudio ou fallback.
- [ ] Manter preserva loops e interpola somente perspectiva.
- [ ] Crossfade reduz o ambiente atual e introduz o destino sem corte abrupto.
- [ ] Imediato troca o áudio de forma intencional no ponto configurado.
- [ ] Especial delega comportamento autorado ao `SceneAudioController`.
- [ ] O SFX do `TransitionProfile` toca no momento definido sem controlar o ambiente permanente.
- [ ] O áudio do destino entra sincronizado com ocultação, troca e revelação.
- [ ] A margem de 0,05 segundo afeta apenas o input, não cria cooldown de áudio.

---

# Cartão 23 — Implementa a continuidade e os estados especiais do áudio

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Alta

## Descrição

### Objetivo

Implementa áudio persistente de equipamentos e ameaças, mixagem de modais e pausa e passagem controlada entre cenas.

### Critério de pronto

- Rádio, gerador, exaustor, telefone e alarmes podem continuar ao trocar de ViewNode.
- Sons persistentes não dependem dos filhos visuais desativados.
- Ameaças emitem sinais a partir de seu estado lógico, independentemente do ViewNode ativo.
- O ViewNode atual aplica direção, distância e abafamento autorados aos sinais.
- Inventário, documentos e media aplicam mixagem própria sem bloquear a UI.
- Pausa impede novos sinais críticos enquanto o jogador não pode reagir.
- O áudio da cena faz saída controlada antes do carregamento global.
- Um SFX que atravesse a carga usa o fluxo persistente existente sem novo singleton.

## Checklist

- [ ] Rádio, gerador, exaustor, telefone e alarmes podem continuar ao trocar de ViewNode.
- [ ] Sons persistentes não dependem dos filhos visuais desativados.
- [ ] Ameaças emitem sinais a partir de seu estado lógico, independentemente do ViewNode ativo.
- [ ] O ViewNode atual aplica direção, distância e abafamento autorados aos sinais.
- [ ] Inventário, documentos e media aplicam mixagem própria sem bloquear a UI.
- [ ] Pausa impede novos sinais críticos enquanto o jogador não pode reagir.
- [ ] O áudio da cena faz saída controlada antes do carregamento global.
- [ ] Um SFX que atravesse a carga usa o fluxo persistente existente sem novo singleton.

---

# Cartão 24 — Produz sinais de ameaça, equipamentos e media

**Coluna inicial:** 🔈Sound Design  
**Etiqueta:** Prioridade Alta

## Descrição

### Objetivo

Produz a linguagem sonora de ameaças, equipamentos, rádio, fitas e transições sem comprometer a leitura mecânica.

### Critério de pronto

- Cada sinal crítico testado possui assinatura reconhecível por ritmo, timbre e direção.
- Estados leve, próximo e crítico não dependem somente de volume.
- Equipamentos possuem sons de funcionamento, falha e confirmação coerentes.
- Rádio e fitas possuem processamento analógico distinto e inteligível.
- Transições possuem SFX compatíveis com corte, fade e glitch VHS.
- Falsos positivos são deliberados e não idênticos por acidente às pistas críticas.
- Assets estão roteados para os grupos corretos e possuem variações suficientes.
- Os sinais continuam reconhecíveis sob a ambiência do vertical slice.

## Checklist

- [ ] Cada sinal crítico testado possui assinatura reconhecível por ritmo, timbre e direção.
- [ ] Estados leve, próximo e crítico não dependem somente de volume.
- [ ] Equipamentos possuem sons de funcionamento, falha e confirmação coerentes.
- [ ] Rádio e fitas possuem processamento analógico distinto e inteligível.
- [ ] Transições possuem SFX compatíveis com corte, fade e glitch VHS.
- [ ] Falsos positivos são deliberados e não idênticos por acidente às pistas críticas.
- [ ] Assets estão roteados para os grupos corretos e possuem variações suficientes.
- [ ] Os sinais continuam reconhecíveis sob a ambiência do vertical slice.

---

# Cartão 25 — Valida o Vertical Slice 4 de áudio

**Coluna inicial:** 👀 Revisão / Teste  
**Etiqueta:** Prioridade Alta

## Descrição

### Objetivo

Valida continuidade, sincronização, inteligibilidade e autoridade do áudio durante navegação, gameplay, modais e troca de cenas.

### Critério de pronto

- Ambientes não reiniciam ao alternar ViewNodes da mesma zona.
- Crossfades não produzem cortes ou cliques não intencionais.
- Equipamentos continuam audíveis fora de seu ViewNode visual.
- Sinais de ameaça funcionam sem GameObjects visuais ativos.
- Transição visual e sonora usam o mesmo ponto de troca.
- Feedback, interação e transição não duplicam o mesmo som.
- Modais e pausa aplicam o comportamento de mixagem esperado.
- Sinais críticos permanecem identificáveis sob ruído e efeitos analógicos.
- Dia termina e Noite começa sem corte acidental de áudio.

## Checklist

- [ ] Ambientes não reiniciam ao alternar ViewNodes da mesma zona.
- [ ] Crossfades não produzem cortes ou cliques não intencionais.
- [ ] Equipamentos continuam audíveis fora de seu ViewNode visual.
- [ ] Sinais de ameaça funcionam sem GameObjects visuais ativos.
- [ ] Transição visual e sonora usam o mesmo ponto de troca.
- [ ] Feedback, interação e transição não duplicam o mesmo som.
- [ ] Modais e pausa aplicam o comportamento de mixagem esperado.
- [ ] Sinais críticos permanecem identificáveis sob ruído e efeitos analógicos.
- [ ] Dia termina e Noite começa sem corte acidental de áudio.

---

# Cartão 26 — Define a apresentação visual das transições VHS

**Coluna inicial:** 🖼️ Arte  
**Etiqueta:** Prioridade Média

## Descrição

### Objetivo

Define o aspecto visual de fade, ruído, glitch e corrupção VHS usado nas transições e eventos de câmera.

### Critério de pronto

- Corte, fade e glitch possuem referências visuais aprovadas.
- O momento de maior ocultação identifica claramente o ponto de troca do ViewNode.
- Intensidades leves, médias e fortes estão definidas.
- Efeitos preservam a leitura de elementos críticos quando não são usados para ocultação.
- Texturas, máscaras e overlays necessários estão preparados para integração em URP.
- A UI crítica permanece legível ou separada do efeito quando necessário.

## Checklist

- [ ] Corte, fade e glitch possuem referências visuais aprovadas.
- [ ] O momento de maior ocultação identifica claramente o ponto de troca do ViewNode.
- [ ] Intensidades leves, médias e fortes estão definidas.
- [ ] Efeitos preservam a leitura de elementos críticos quando não são usados para ocultação.
- [ ] Texturas, máscaras e overlays necessários estão preparados para integração em URP.
- [ ] A UI crítica permanece legível ou separada do efeito quando necessário.

---

# Cartão 27 — Implementa os efeitos VHS nas transições URP

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Média

## Descrição

### Objetivo

Implementa os efeitos visuais VHS dos `TransitionProfile`s e integra seus parâmetros às fases de ocultação e revelação.

### Critério de pronto

- Os efeitos são aplicados pela estratégia URP definida para composição fullscreen.
- `TransitionProfile` controla tipo, duração, intensidade e parâmetros necessários.
- O ponto de troca pode ocorrer sob ocultação suficiente.
- Corte e fade continuam disponíveis como fallbacks seguros.
- O efeito não atinge UI crítica quando ela deve permanecer legível.
- A câmera e o parallax não entram em conflito com a composição fullscreen.
- Erro ou ausência do efeito não mantém o input bloqueado.
- O custo do efeito é medido no hardware-alvo do projeto.

## Checklist

- [ ] Os efeitos são aplicados pela estratégia URP definida para composição fullscreen.
- [ ] `TransitionProfile` controla tipo, duração, intensidade e parâmetros necessários.
- [ ] O ponto de troca pode ocorrer sob ocultação suficiente.
- [ ] Corte e fade continuam disponíveis como fallbacks seguros.
- [ ] O efeito não atinge UI crítica quando ela deve permanecer legível.
- [ ] A câmera e o parallax não entram em conflito com a composição fullscreen.
- [ ] Erro ou ausência do efeito não mantém o input bloqueado.
- [ ] O custo do efeito é medido no hardware-alvo do projeto.

---

# Cartão 28 — Implementa diagnóstico e realiza profiling

**Coluna inicial:** 📋 Programação  
**Etiqueta:** Prioridade Média

## Descrição

### Objetivo

Implementa validações de conteúdo e mede memória, renderização, entrada e áudio antes da produção em escala.

### Critério de pronto

- O boot valida managers, câmera, EventSystem, cena e ViewNode inicial.
- IDs duplicados, destinos inválidos e perfis obrigatórios ausentes geram warnings claros.
- Sobreposição é verificada apenas entre hotspots simultâneos do mesmo ViewNode.
- Áudio persistente anexado a filhos desativáveis é sinalizado durante validação ou revisão.
- Todo warning identifica cena, ViewNode, hotspot ou asset responsável.
- Uso de memória das texturas e camadas é medido com todos os ViewNodes da cena carregados.
- Custo de parallax, VHS, raycasts e mixer é registrado.
- A necessidade de carregamento dinâmico só é aberta como novo cartão se o orçamento for excedido.

## Checklist

- [ ] O boot valida managers, câmera, EventSystem, cena e ViewNode inicial.
- [ ] IDs duplicados, destinos inválidos e perfis obrigatórios ausentes geram warnings claros.
- [ ] Sobreposição é verificada apenas entre hotspots simultâneos do mesmo ViewNode.
- [ ] Áudio persistente anexado a filhos desativáveis é sinalizado durante validação ou revisão.
- [ ] Todo warning identifica cena, ViewNode, hotspot ou asset responsável.
- [ ] Uso de memória das texturas e camadas é medido com todos os ViewNodes da cena carregados.
- [ ] Custo de parallax, VHS, raycasts e mixer é registrado.
- [ ] A necessidade de carregamento dinâmico só é aberta como novo cartão se o orçamento for excedido.

---

# Cartão 29 — Valida a integração final da arquitetura

**Coluna inicial:** 👀 Revisão / Teste  
**Etiqueta:** Prioridade Alta

## Descrição

### Objetivo

Valida todos os invariantes da arquitetura em um ciclo jogável com navegação, interação, áudio, transição e checkpoint.

### Critério de pronto

- Apenas o ViewNode apresentado recebe entrada e representa o estado atual corretamente.
- Navegação, interação e ferramenta permanecem tecnicamente distintas.
- Nenhum fluxo depende de cooldown genérico.
- Reentrada funciona após boot, transição, modal, pausa e desbloqueio.
- Condições são atualizadas ao entrar e revalidadas antes da ação.
- Dia influencia a Noite pela cópia de trabalho sem alterar antecipadamente o checkpoint.
- Carregar o slot sempre retorna ao início do Dia consolidado.
- Áudio contínuo, equipamentos e ameaças independem dos filhos visuais.
- Tilt e parallax não quebram hotspots nem revelam bordas vazias.
- Erros de conteúdo não deixam entrada, transição ou save em estado irrecuperável.
- O vertical slice respeita os limites aprovados de memória e desempenho.
- Todos os critérios pendentes geram cartões de bug ou ajustes antes da conclusão.

## Checklist

- [ ] Apenas o ViewNode apresentado recebe entrada e representa o estado atual corretamente.
- [ ] Navegação, interação e ferramenta permanecem tecnicamente distintas.
- [ ] Nenhum fluxo depende de cooldown genérico.
- [ ] Reentrada funciona após boot, transição, modal, pausa e desbloqueio.
- [ ] Condições são atualizadas ao entrar e revalidadas antes da ação.
- [ ] Dia influencia a Noite pela cópia de trabalho sem alterar antecipadamente o checkpoint.
- [ ] Carregar o slot sempre retorna ao início do Dia consolidado.
- [ ] Áudio contínuo, equipamentos e ameaças independem dos filhos visuais.
- [ ] Tilt e parallax não quebram hotspots nem revelam bordas vazias.
- [ ] Erros de conteúdo não deixam entrada, transição ou save em estado irrecuperável.
- [ ] O vertical slice respeita os limites aprovados de memória e desempenho.
- [ ] Todos os critérios pendentes geram cartões de bug ou ajustes antes da conclusão.

---

# Ordem resumida de execução

| Ordem | Cartões | Marco |
|---:|---|---|
| 1 | 01–04 | Roadmap e fundação |
| 2 | 05–10 | Vertical Slice 1 — navegação, câmera e parallax |
| 3 | 11–15 | Vertical Slice 2 — condições, interação, inventário e ferramentas |
| 4 | 16–18 | Vertical Slice 3 — Dia/Noite e checkpoint diário |
| 5 | 19–25 | Vertical Slice 4 — arquitetura e conteúdo de áudio |
| 6 | 26–29 | VHS, profiling e validação final |
