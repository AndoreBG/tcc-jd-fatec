# Prólogo — Whispers from the Unknown

Texto de abertura: sete linhas, 972 caracteres no total, cerca de 54 segundos de leitura.

**Estado atual: parado.** A cena de cutscene que ia apresentar este texto foi retirada do
projeto (§23 do Progresso), então o prólogo está aqui como documento, pronto para ir para
outro lugar. As três opções naturais estão no fim do arquivo.

A voz é de quem está dentro da história, não a do documento de design. Nada aqui explica
o jogo; tudo aqui é o que um sobrevivente diria em voz baixa antes de sair do abrigo.

---

## Versão principal — 7 slides

**Slide 1**

> Em 2076, sobraram poucos de nós. O mundo que ficou aprendeu a não fazer barulho depois
> que escurece.

**Slide 2**

> As criaturas vieram primeiro. Andam como gente e o rosto quase convence, até você olhar
> demais. O resto do estrago foi a falta de tudo.

**Slide 3**

> De dia ainda dá para andar. Recolher o que serve, seguir rastro de quem passou, abrir
> porta que não está mais trancada.

**Slide 4**

> De noite não se atravessa o corredor. As paredes do abrigo seguram o frio e a chuva, e é
> só isso que elas seguram.

**Slide 5**

> Tem um sinal de rádio repetindo a mesma frase há dias. Uns juram que é uma colônia de
> gente viva. Outros dizem que é isca. É para lá que eu vou, porque é a única direção que
> sobrou.

**Slide 6**

> O que eu sei está espalhado em bilhete de gaveta, em nome riscado na parede, em
> transmissão que só começa depois da meia-noite. Nada disso foi escrito para ser
> entendido de uma vez.

**Slide 7**

> E quando a noite fica funda, o rádio pega outra voz. Militar, com hora marcada, falando
> com alguém que nunca respondeu. Ela nunca fala comigo.

---

## Ritmo de leitura

| Slide | Assunto | Caracteres | Tempo confortável (16 caracteres/s) |
|---|---|---|---|
| 1 | Abertura, o ano | 100 | 5.6s |
| 2 | As criaturas | 135 | 7.5s |
| 3 | O dia | 119 | 6.6s |
| 4 | A noite | 114 | 6.3s |
| 5 | O sinal | 181 | 10.1s |
| 6 | Os pedaços da história | 181 | 10.1s |
| 7 | A outra voz | 142 | 7.9s |

O `WorldCutsceneController` calcula a duração pelo tamanho da linha: o valor de
`secondsPerSlide` funciona como **piso**, e `charactersPerSecond` (16 por padrão) estica
os slides longos. Assim o slide 1 não fica nove segundos na tela por causa do slide 5.

Esse tempo é o orçamento total do slide: dentro dele acontece o typewriter, e o que sobra
vira espera com a linha completa. Um slide de 181 caracteres gasta 4s digitando e fica
mais 7s na tela pronta. Quem aperta E avança antes, como sempre.

Se preferir tempo fixo, desligue `autoSecondsFromText` e ajuste `secondsPerSlide` para
algo em torno de **10s** — é o que cabe no slide mais longo sem pressa.

---

## Versão curta — 4 slides

Para uma abertura de 30 segundos, corte o meio e deixe o gancho:

**1.**

> Em 2076, sobraram poucos de nós. O mundo que ficou aprendeu a não fazer barulho depois
> que escurece.

**2.**

> Tem um sinal de rádio repetindo a mesma frase há dias. Uns juram que é uma colônia de
> gente viva. Outros dizem que é isca. É para lá que eu vou, porque é a única direção que
> sobrou.

**3.**

> O que eu sei está espalhado em bilhete de gaveta, em nome riscado na parede, em
> transmissão que só começa depois da meia-noite. Nada disso foi escrito para ser
> entendido de uma vez.

**4.**

> E quando a noite fica funda, o rádio pega outra voz. Militar, com hora marcada, falando
> com alguém que nunca respondeu. Ela nunca fala comigo.

---

## Typewriter e aviso de pulo

O texto entra letra por letra, a **45 caracteres por segundo**. Cada slide é digitado e
depois fica parado na tela pelo tempo de leitura. O aviso no canto inferior direito diz:

> E para pular

**E** tem dois papéis, na ordem em que o jogador precisa deles:

| Momento | O que E faz |
|---|---|
| Linha ainda sendo digitada | completa a linha no mesmo instante |
| Linha já completa | avança para o próximo slide |
| Último slide | encerra a cutscene e carrega o Dia 1 |

Clique e Espaço fazem o mesmo, para quem não ler o aviso. Um único E que completa a linha
não avança de slide no mesmo toque: o mesmo aperto não pode valer duas coisas.

Se você quiser que **E encerre a cutscene inteira** de uma vez, em vez de só adiantar o
texto, ligue `E Skips Whole Cutscene` no `WorldCutsceneController`. Aí o primeiro E pula
tudo, com fade para o preto antes do Dia 1.

---

## Escolhas de escrita

**O ano entra como fato, não como ficha técnica.** "Em 2076, sobraram poucos de nós" diz a
data e o tom na mesma frase. A alternativa, "No ano de 2076, a humanidade...", soa a
relatório.

**Ninguém recebe nome.** Nem o protagonista, nem as criaturas, nem a colônia. Nomear
fecha a interpretação, e o jogo inteiro é sobre juntar pedaços.

**As criaturas ficam no quase.** "Andam como gente e o rosto quase convence, até você
olhar demais" entrega o horror sem descrever anatomia. O que o jogador imagina é pior.

**A escassez aparece como rotina, não como tragédia.** "Recolher o que serve" e "abrir
porta que não está mais trancada" são tarefas de quem já perdeu o espanto. É o mesmo tom
que o rádio usa nos Dias, onde instrução e medo vêm na mesma voz.

**O mistério central fica só encostado.** O slide 6 diz como a história é contada sem
citar documentos, monólogos ou cenário como categorias. O slide 7 planta a outra voz e
para: nem confirma ligação com a colônia, nem sugere que ela seja hostil. É o fio que o
jogo inteiro vai puxar.

**Nada de "o jogador".** Nem "você". O texto fala de um eu que já está no mundo, e deixa
o jogador entrar nele por conta própria.

---

## Onde este texto pode morar

A cutscene foi retirada, então o prólogo ainda não tem casa. Três caminhos, em ordem de
custo:

**1. VN de abertura na cena do Dia.** O mesmo desenho do rádio (§19): uma camada de UI que
cobre o gameplay, apresenta os slides com typewriter e o aviso do E, e devolve a sala. Não
precisa de cena nova nem de legenda de carregamento, e reaproveita tudo o que o
`RadioEventController` já faz. É o mais barato, porque metade do código existe.

**2. Texto na tela de carregamento.** O `LevelManager` já mostra "Dia 1" no centro. Daria
para trocar a legenda por uma ou duas linhas do prólogo, sorteadas ou por Dia. Custa pouco
e cobre o tempo morto do carregamento, mas só cabem frases curtas.

**3. Documento no abrigo.** O prólogo vira um `DOC_` na mesa do abrigo, que o jogador lê
quando quiser. Encaixa na narrativa fragmentada, mas quem não procurar nunca lê.

O texto foi escrito para os três: cada linha funciona em slide, em cartela e em página de
documento.
