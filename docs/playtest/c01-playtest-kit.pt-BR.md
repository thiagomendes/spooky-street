# Kit de teste em família do caso 01 (pt-BR)

Versão em português do roteiro do facilitador e da ficha de observação de `docs/playtest/c01-playtest-kit.md`
(a versão em inglês é a referência; em caso de dúvida, vale o que está lá). A ficha para imprimir é
`docs/playtest/c01-observation-sheet.pdf` (A4 paisagem, uma página por criança; imprima uma por criança, em
tamanho real, sem "ajustar à página"). Ela é gerada por `tools/playtest/make_observation_sheet.py`.

**O jogo está só em inglês por enquanto.** O apêndice A traz leituras literais das falas para crianças que ainda
não leem inglês.

**Nada deste kit é mostrado à criança.** A ficha cita as pistas e as respostas: mantenha-a fora da vista dela.

## 1. O que queremos aprender

O caso foi refeito "mistério primeiro" (D26): a criança junta as pistas e faz as deduções, e as dicas nunca dão a
resposta. Queremos ver, com crianças reais de 6 a 9 anos (e de 9 a 12 para os extras):

1. Elas criam as próprias teorias depois de ouvir a Bea e o Sr. Vale?
2. As pistas chamam a atenção (brilho, som, movimento) sem o rádio?
3. Elas usam os espaços de figurinhas do caderno depois de no máximo uma tentativa errada? O erro (as figurinhas
   balançam e voltam) desperta curiosidade, e não chateação?
4. Elas sentem o "aha!" quando o conjunto certo desenha o quadrinho?
5. No fim, conseguem explicar com as próprias palavras a entrada pelo lado da casa ou de onde vêm as batidas?
6. Elas querem continuar jogando?

O caso só é considerado aprovado por crianças depois que estas sessões forem observadas e registradas.

## 2. O que tem no build (confira antes da primeira sessão)

Jogue o build do TestFlight uma vez inteiro, de "New game" até o carimbo "Case closed", e confira:

- **Abertura** (cerca de 20 s, dá para pular): Candlebrook ao anoitecer, as caixas de mudança, a janela redonda
  acesa, a Bea acenando. O jogo começa na varanda.
- **Três batidas** na chegada: levantam a primeira pergunta no caderno (o disco do caderno balança).
- **Testemunhas:** Bea (praça, perto da bicicleta) e Sr. Vale (banco). Cada fala toca uma pequena animação com uma
  linha de texto em inglês (leitura híbrida, D23.1).
- **Figurinhas de pista:** 12 na bandeja (o brilho da Bea, a porta do Vale, a vista do banco, a porta emperrada, a
  fita de papel-alumínio = a pista falsa honesta, o pano prateado, o fio no peitoril, o chão que treme, a peça de
  madeira, o painel da coruja, a coruja de madeira, o braço solto) e o doce guardado como lembrança.
- **Três perguntas**, que aparecem quando o mundo as levanta, uma por página, com 2 ou 3 espaços vazios, uma bandeja
  compartilhada e um "?" para cada pista ainda não achada.
- **Conferência:** espaços cheios, uma pausa; errado: os espaços tremem, as figurinhas voltam balançando, a Pip
  pensa; certo: brilho laranja, o quadrinho com as figurinhas da própria criança, *Found it!*. A partir da 3ª
  tentativa errada as figurinhas que servem ficam; depois da 5ª a pista falsa é posta de lado.
- **Rádio (botão Hint):** um toque por nível para o passo atual: nível 1 um brilho e o olhar de um personagem,
  nível 2 a trilha de morcegos de papel, nível 3 uma mão transparente mostrando o que fazer. Dentro do caderno ele
  mostra a Pip pensando (duas figuras) e acende um "?" com o lugar da pista.
- **Movimentos:** andar, pular, segurar para pular mais alto, planar; pulo na parede e subir na borda em superfícies
  marcadas (dicas de uma vez só acima do botão Jump).
- **Casa:** a janela do sótão (virada de página para um interior maior), a June no sótão, o trinco, o corredor, a
  grade da oficina, o braço e a manivela, a coruja que se curva no telhado.
- **Final:** na varanda, o show da coruja da tia Kit; a Kit pergunta "So, detective, what happened here?" e abre o
  caderno se ainda houver pergunta aberta; o carimbo precisa das três perguntas resolvidas; depois a cena da
  revelação (cerca de 28 s, montada com as figurinhas da criança).
- **Som:** batidas, chão que treme, pano tremulando, sons do caderno, vozes de "balbucio", música.
- **Helper:** em Settings, atrás do portão dos pais, **desligado** por padrão. Deixe desligado.

Diferenças conhecidas em relação ao design (para não interpretar errado):

- O nível 1 ainda faz brilhar o próprio alvo e o nível 3 pode mostrar o "Use". Anote o nível de cada toque no rádio
  e conte "deduziu sozinha" só nas colocações do caderno.
- A figurinha da coruja de madeira chega com a reverência da coruja. A pista bônus do cata-vento, a demonstração da
  mão arrastando, as paradas de capítulo e o resumo em figuras no "Continue" ainda não estão no build.

## 3. Quem joga

- **Crianças:** cerca de cinco: três de 6 a 9 anos e duas de 9 a 12. Menos também serve; anote quantas.
- **Uma criança por vez.** Irmãos esperam em outro cômodo; quem assiste aprende as respostas. Se duas crianças
  insistirem em jogar juntas, tudo bem, mas marque "dupla" e não conte para a meta.
- **Um responsável por criança**, presente a sessão toda, sentado atrás dela e também em silêncio (mesmas regras do
  facilitador, §5).
- Anote se a criança lê inglês (sim / um pouco / não).
- Não faça a sessão com uma criança doente, cansada ou chateada naquele dia.

## 4. Consentimento e privacidade

- **Pergunte primeiro ao responsável, depois à criança.** Explique: "Estamos testando um jogo para crianças. Estamos
  observando o jogo, não o seu filho. Ele pode parar quando quiser, e nada do que ele fizer está errado." O
  responsável concorda em voz alta ou por escrito; a criança é convidada ("Quer experimentar um jogo novo?") e pode
  dizer não.
- **A criança pode parar a qualquer momento**, sem dar motivo. Encerre com carinho: "Obrigado, detetive!".
- **Não grave rosto nem voz** a menos que o responsável concorde explicitamente, para aquela sessão, sabendo o que
  será gravado e quem vai ver. O padrão é só anotação no papel.
  - Se o responsável aceitar uma gravação de tela, use a gravação de tela do iOS com o **microfone desligado** (só o
    jogo). Fica no aparelho do dono, serve para o relatório e é apagada em até duas semanas.
  - Nunca fotografe nem filme a criança.
- **Sem nomes nas fichas.** Use um código (C1–C5), a idade e o nível de leitura. A lista código → nome fica em papel,
  separada das fichas, e é destruída depois do relatório.
- **O repositório é público.** Só um resumo anônimo entra no repositório (códigos, idades, contagens, frases sem
  nomes). Nunca envie fichas, gravações, fotos, nomes, escolas ou qualquer coisa que identifique uma família.
- **O jogo não coleta nada** (sem contas, sem analytics). Não instale nenhum app de rastreio ou gravação.

## 5. Preparação

- **Aparelho:** o iPhone do dono com o build do TestFlight. Anote na ficha a versão e o build do canto da tela de
  título. Bateria acima de 80 %, outros apps fechados.
- **Foco / Não Perturbe ligado.** Opcional: Acesso Guiado (clique triplo) para manter a criança no app.
- **Som ligado:** chave de silencioso desligada, volume em cerca de 60 %, pelo alto-falante (o facilitador também
  precisa ouvir as batidas). Sem fones.
- **Começo do zero para cada criança:** na tela de título, toque **New game** (confirme se pedir). Confira em
  Settings (portão dos pais): **Helper desligado**.
- **Um cômodo silencioso**, boa luz sem reflexo na tela, cadeira ou sofá confortável, celular na horizontal nas
  mãos da criança. Sem TV, sem outras crianças.
- **Posição:** o facilitador ao lado e um pouco atrás da criança, vendo a tela sem que ela veja a ficha. O
  responsável atrás.
- **Na mesa:** a ficha impressa (uma por criança) numa prancheta, caneta, cronômetro silencioso, água.
- **Cronômetro:** zere quando a criança pega o celular e anote os tempos como mm:ss a partir daí.

## 6. Roteiro do facilitador

Regra de ouro: **entregue o celular depois do título, fique em silêncio e nunca dê dica nem resposta.** Você é um
ouvinte curioso, não um professor. Rosto simpático e neutro, aconteça o que acontecer. Não balance a cabeça para a
pista certa nem franza a testa para a errada.

### 6.1 Boas-vindas (diga isto, cerca de 2 minutos)

> "Oi! Este é um jogo novo chamado Spooky Street. Não tem jeito errado de jogar. Eu quero ver como as crianças
> jogam, então vou ficar quietinho e anotar coisas sobre o **jogo**, não sobre você. Se quiser, pode falar em voz
> alta: o que você está pensando, o que você gosta, o que acha esquisito. Pode parar quando quiser. Pronto? Toque em
> New game."

Entregue o celular e ligue o cronômetro.

### 6.2 Durante o jogo: o que você pode dizer

Só frases neutras, de vez em quando (no máximo uma a cada poucos minutos), nunca no meio de uma cena:

- "O que você está pensando?"
- "O que você está reparando?"
- "Me conta sobre isso."
- "O que você acha que aconteceu?" (**diga uma única vez, logo depois que a criança ouvir a Bea e o Sr. Vale**;
  anote a resposta palavra por palavra.)
- "Hmm." / "Tá." / "Entendi." (como ouvinte)
- Quando a criança pedir ajuda ou perguntar o que fazer: **"Não sei, o que você acha?"** ou "O que você poderia
  tentar?"
- Quando perguntar se está certo: "O que você acha?"
- Quando resolver algo: um sorriso, um "Hum-hum!". Nada de "Muito bem, acertou!" antes de o jogo mostrar.

### 6.3 Durante o jogo: o que você nunca diz nem faz

- **Nunca toque no celular** nem aponte para a tela, e não deixe o responsável fazer isso.
- Nunca diga um lugar ou objeto para onde ir ("tenta a cerca viva", "olha a casa da árvore", "e a bicicleta?").
- Nunca explique uma pista ou um depoimento ("a Bea viu a capa da June", "o prateado é da June", "a fita é uma
  pegadinha").
- Nunca junte figurinhas, nunca diga qual figurinha vai em qual pergunta, nunca conte ("falta uma").
- Nunca fale do rádio, do caderno ou de um botão pelo nome, a não ser que a criança pergunte o que é; aí diga
  "O que você acha que ele faz?"
- Não explique os controles além das dicas do próprio jogo. (Se a criança não conseguir se mexer depois de um
  minuto, pode dizer uma vez "Tenta mexer o dedão do lado esquerdo." e anotar.)
- Nunca diga "rápido", "quase", "tá perto", "não, isso não", nem suspire. Nenhum elogio ou decepção que mostre se
  uma escolha está certa.
- Nunca traduza **mais** do que as palavras da tela (ver §6.5).

### 6.4 Quando a criança empaca ou fica chateada

Anote o tempo em que ela empacou. Então:

1. **Até 3 minutos empacada mas tranquila:** fique quieto. Explorar faz parte.
2. **Depois de uns 3 minutos, ou frustração visível:** uma frase neutra: "O que você está pensando?" ou "O que você
   poderia tentar?". Marque "P" na ficha.
3. **Depois de uns 5 minutos, ou se ela disser que quer desistir:** "Tem alguma coisa no jogo que poderia ajudar?"
   (ainda sem citar botão ou lugar). Marque "P2".
4. **Chateada, quase chorando, ou querendo parar:** pare. "Obrigado, você foi um ótimo detetive! Vamos parar por
   aqui." Anote o momento e o motivo. Nunca insista.

Se a criança ficar com medo (não deveria: o jogo é "assustadinho", nunca assustador), pause, acalme e anote o que
causou: isso é um defeito de design.

### 6.5 Crianças que ainda não leem inglês (leitura híbrida, D23.1)

O jogo tem um só modo de leitura para todos: cada fala toca uma pequena animação que carrega o sentido, com uma
linha curta de texto em inglês embaixo. A ideia é que quem ainda não lê entenda pelas figuras. Então:

- **Por padrão, não leia nem traduza.** Primeiro veja se as figuras bastam; isso é parte do teste. Marque "só
  figuras" no cabeçalho se a criança não pediu leitura.
- **Se a criança perguntar "o que está escrito?"**, leia a linha uma vez, **literalmente**, em português, com voz
  neutra, sem palavras a mais, sem destacar palavras de pista e sem explicar. Use a tabela do apêndice A, para que
  todas as famílias ouçam as mesmas palavras. Marque "R" (lido em voz alta) na linha da ficha.
- Nunca leia as palavras de resposta do caderno antes de a criança resolver aquela pergunta (elas só aparecem
  depois de um conjunto certo, de qualquer forma).
- Se a criança lê um pouco de inglês, deixe-a tentar; ajude só com a palavra que ela perguntar.
- Anote cada pedido de leitura: muitos pedidos na mesma fala mostram que a figura não está dando conta.

### 6.6 Limite de tempo e fim do jogo

- Deixe jogar até o carimbo "Case closed" e o fim da cena de revelação, ou **40 minutos**, o que vier primeiro. Aos
  35 minutos diga "Mais uns minutinhos e a gente para." Aos 40, pare com carinho, mesmo no meio de uma pergunta.
- Depois do carimbo a criança pode continuar explorando. Anote se ela **pede** para continuar antes de você dizer
  qualquer coisa: é o sinal de "quer continuar".

## 7. Tempo (por criança, cerca de 45 minutos)

| Parte | Minutos |
|---|---|
| Boas-vindas, consentimento, conferência | 5 |
| Jogo (esperado 17–30 para uma criança pequena jogando pela primeira vez, 12–18 para uma leitora mais velha) | até 40 |
| Perguntas do fim | 5–10 |
| No dia seguinte: pergunta de lembrança, feita pelo responsável em casa | 2 |

Deixe 10–15 minutos entre crianças para preparar o celular (New game, Helper desligado) e terminar as anotações.

## 8. Perguntas do fim da sessão

Pergunte com calma, uma de cada vez, e anote palavra por palavra. Não corrija nada, não acrescente pistas, aceite
"não sei".

1. "Me conta o que aconteceu na história." (lembrança: preste atenção na entrada pelo lado e na origem das batidas)
2. "Como a June entrou na casa?" (só se a 1 não respondeu)
3. "O que fazia as batidas?" (só se a 1 não respondeu)
4. "Quem te ajudou?"
5. "Qual foi a parte que você mais gostou?"
6. "Teve alguma coisa confusa? Alguma coisa assustadora demais?"
7. "O que você achou do caderno e das figurinhas?"
8. "Você usou o rádio? O que ele fez?"
9. "Você gostaria de jogar mais Spooky Street outro dia?"

Para o responsável (se possível longe da criança):

1. "Alguma coisa te preocupou ou pareceu não ser adequada para seu filho?"
2. "A leitura foi demais, na medida, ou nem precisou?"
3. "Você confiaria neste jogo no celular do seu filho? Por quê?"
4. "O que você mudaria?"

**No dia seguinte** (o responsável pergunta em casa e anota): "Me conta do jogo que você jogou ontem? O que
aconteceu?"

## 9. Ficha de observação

Uma ficha por criança. A criança pode fazer o caso em qualquer ordem: preencha as linhas quando os momentos
acontecerem e escreva a ordem na coluna "#" se for diferente. O PDF para imprimir junta numa página só os momentos
que costumam acontecer juntos.

**Cabeçalho:** código · idade · lê inglês (sim / um pouco / não) · só figuras? · build (versão · build) · data ·
hora de início · dupla? · responsável presente · mudanças na preparação.

**Colunas:**

- **Tempo**: mm:ss desde o início.
- **O que a criança fez**: a ação, em poucas palavras (caminho, o que tocou, o que tentou).
- **Empacou? (min)**: quanto tempo sem progresso antes deste momento; P / P2 se você fez uma pergunta neutra.
- **Nível da dica**: toques no rádio neste momento: 1, 2, 3, ou M (Pip pensando, no caderno). Em branco se nenhum.
- **Falou em voz alta**: as palavras da criança, ao pé da letra se possível; R se você leu uma fala.
- **Emoção**: A alegria · C confusão · F frustração · T tédio (circule uma ou duas; em branco se neutra).
- **Deduziu sozinha?**: só na pergunta da teoria e nas três perguntas do caderno: S (colocação dela, sem rádio naquela
  pergunta), M (depois da Pip pensando), T (tentativa e erro, muitos conjuntos rápidos), N (não resolveu).

| # | Momento (o que o jogo mostra) | Tempo | O que a criança fez | Empacou? (min) | Nível da dica | Falou em voz alta | Emoção A/C/F/T | Deduziu sozinha? |
|---|---|---|---|---|---|---|---|---|
| 1 | Cena de abertura: assistiu ou pulou? | | | | | | | — |
| 2 | Chegada na varanda, primeiro movimento (para onde?) | | | | | | | — |
| 3 | Primeira batida: as três batidas; reage ou olha para a casa? | | | | | | | — |
| 4 | Primeiro balanço do disco do caderno (pergunta nova): percebeu? abriu? | | | | | | | — |
| 5 | Fala com a Bea (figurinha: o brilho da Bea) | | | | | | | — |
| 6 | Fala com o Sr. Vale (figurinha: a porta do Vale) | | | | | | | — |
| 7 | O facilitador pergunta uma vez: "O que você acha que aconteceu?" (anote a teoria) | | | | | | | S/N teoria |
| 8 | Senta no banco (figurinha: a vista do banco) | | | | | | | — |
| 9 | Tenta a porta da frente (figurinha: porta emperrada) | | | | | | | — |
| 10 | Bicicleta da Bea: fita de papel-alumínio (figurinha, a pista falsa) | | | | | | | — |
| 11 | Acha o quintal lateral (tempo desde o início) | | | | | | | — |
| 12 | Pano prateado no corrimão da rampa: o brilho chamou sem o rádio? (figurinha) | | | | | | | — |
| 13 | Casa da árvore e ponte; apareceram as dicas de pular ou planar? | | | | | | | — |
| 14 | Peitoril do sótão: fio prateado (figurinha) | | | | | | | — |
| 15 | Entra pela janela do sótão (virada de página), encontra a June | | | | | | | — |
| 16 | Primeiro uso do caderno com duas ou mais figurinhas: arrasta ou toca sem ajuda? | | | | | | | — |
| 17 | "How did June get in?" (Como a June entrou?): tentativas, usou a pista falsa?, rosto no balanço | | | | | | | S/M/T/N |
| 18 | Levanta o trinco com a June, a porta da frente abre | | | | | | | — |
| 19 | O corredor: o chão treme (figurinha) | | | | | | | — |
| 20 | Grade da oficina: a peça de madeira (figurinha) | | | | | | | — |
| 21 | Painel da coruja (figurinha) | | | | | | | — |
| 22 | "Where are the knocks coming from?" (De onde vêm as batidas?): tentativas, rosto | | | | | | | S/M/T/N |
| 23 | Braço solto: girou a manivela antes de encaixar? encaixou o braço? (figurinha) | | | | | | | — |
| 24 | Gira a manivela: a coruja se curva no telhado (figurinha: coruja de madeira) | | | | | | | — |
| 25 | "What should the knocks move?" (O que as batidas deveriam mover?): tentativas, rosto | | | | | | | S/M/T/N |
| 26 | Varanda: o show da coruja da Kit; a Kit pergunta "So, detective?" | | | | | | | — |
| 27 | Carimbo "Case closed", cena da revelação: assistiu ou pulou? reação às próprias figurinhas? | | | | | | | — |
| 28 | Página do caso: "x of y clues" (anote x e y) | | | | | | | — |
| 29 | O doce guardado pelo Sr. Vale (bônus) | | | | | | | — |
| 30 | Exploração livre: pede para continuar antes de alguém perguntar? | | | | | | | — |
| D1 | Rádio, nível 1 (brilho e olhar): cada toque, tempo e passo | | | | 1 | | | — |
| D2 | Rádio, nível 2 (trilha de morcegos): cada toque, tempo e passo | | | | 2 | | | — |
| D3 | Rádio, nível 3 (mão transparente): cada toque, tempo e passo | | | | 3 | | | — |
| D4 | Rádio no caderno (Pip pensando e um "?" com o lugar): cada toque, qual pergunta | | | | M | | | — |
| X | Perdeu a câmera ou o controle, caiu, se assustou, quis parar | | | | | | | — |

**Contagens no fim:** pistas achadas antes do primeiro toque no rádio ___ de 12 · total de toques no rádio ___ ·
conjuntos errados por pergunta (entrada ___ batidas ___ mover ___) · a pista falsa enganou? sim/não · se corrigiu
sozinha? sim/não · pedidos de leitura ___ · tempo de jogo ___ min.

## 10. Critérios de sucesso (checklist)

Por criança:

- [ ] Terminou o caso (carimbo "Case closed") com **no máximo uma dica pedida** no total.
- [ ] Resolveu "Como a June entrou?" com **no máximo um toque no rádio**.
- [ ] Não desistiu no caderno.
- [ ] Achou **pelo menos 70 %** das pistas de uma pergunta antes de qualquer toque no rádio.
- [ ] Mostrou um "aha!" (palavras, gesto, "Eu sabia!") numa colocação certa.
- [ ] Recebeu os erros com curiosidade, não com frustração.
- [ ] Explicou com as próprias palavras a entrada pelo lado **ou** a origem das batidas (fim da sessão).
- [ ] Pediu para continuar jogando numa parada.
- [ ] No dia seguinte: recontou o que aconteceu e quem ajudou.
- [ ] Teve uma teoria própria depois da Bea e do Sr. Vale (qualquer teoria vale).
- [ ] A pista falsa: enganou e ela se corrigiu, ou não enganou (as duas servem; anote qual).

Meta do grupo (todas as crianças juntas):

- [ ] **4 de 5** terminam com no máximo uma dica pedida e explicam a entrada pelo lado ou a origem das batidas.
- [ ] **Pelo menos 3 de 5** resolvem "Como a June entrou?" com no máximo um toque no rádio.
- [ ] **Ninguém** desiste no caderno.
- [ ] **4 de 5** pedem para continuar.
- [ ] A maioria das perguntas resolvidas tem um "aha!".
- [ ] Os responsáveis acham o jogo confiável, sem precisar perguntar.
- [ ] Qualquer confusão vista em **duas** sessões vai para a lista de revisão de design.
- [ ] (Se houver segunda sessão) **3 de 5** vão atrás dos morcegos de papel ou de lugares antigos sem ninguém pedir.

## 11. Depois das sessões

1. No mesmo dia, complete cada ficha: contagens, checklist e três linhas de "o que me surpreendeu".
2. Escreva um resumo anônimo em `docs/playtest/c01-results-<data>.md` (em inglês, a língua dos documentos do
   repositório): número de crianças e idades, níveis de leitura, contagens do checklist, confusões repetidas e em que
   momento, frases sem nomes, e as limitações (amostra pequena, ambiente familiar, o facilitador é o desenvolvedor,
   build só em inglês).
3. Não diga que o caso foi aprovado por crianças se a meta não foi atingida em sessões observadas.
4. Destrua a lista código → nome e qualquer gravação de tela depois do resumo.

## Apêndice A. Leituras literais das falas (só se a criança pedir)

Leia **exatamente** isto, uma vez, com voz neutra. Não explique, não repita com outra entonação, não acrescente
nada. As legendas entre colchetes descrevem um som ou o que se vê. As respostas do caderno (última parte) só
aparecem depois que a criança acerta.

| Na tela (inglês) | Leitura literal |
|---|---|
| [Three knocks from the blue house] | [Três batidas vindas da casa azul] |
| Something silver flashed up high, by the side of the house! | Uma coisa prateada brilhou lá no alto, do lado da casa! |
| Up high, over the hedge. Then it was gone! | Lá no alto, por cima da cerca viva. Depois sumiu! |
| The door's open now? That silver flash was long before. | A porta está aberta agora? Aquele brilho prateado foi muito antes. |
| That front door? I could see it from my bench. It stayed shut. | Aquela porta da frente? Eu via ela do meu banco. Ficou fechada. |
| Sit down and see for yourself. | Senta aqui e vê você mesma. |
| Not one creak from that door all evening. | Nem um rangido daquela porta a noite toda. |
| It was shut when I looked. | Estava fechada quando eu olhei. |
| It's open now, though. Well, well. | Mas agora está aberta. Ora, ora. |
| Crinkly, shiny foil. | Papel-alumínio amassadinho e brilhante. |
| Soft silver cloth, caught on a splinter. | Pano prateado macio, preso numa farpa. |
| A silver thread on the sill. | Um fio prateado no peitoril. |
| It won't budge. | Não se mexe. |
| Pip! You found my secret way in! Did you hear the knocks too? | Pip! Você achou meu caminho secreto! Você também ouviu as batidas? |
| Pip! You found my secret way in, and nobody showed you! Did you hear the knocks too? | Pip! Você achou meu caminho secreto, e ninguém te mostrou! Você também ouviu as batidas? |
| It sticks. Give me a hand? | Ela emperra. Me dá uma mão? |
| The latch is down by the front door. Lift it with me? | O trinco fica lá embaixo, na porta da frente. Levanta comigo? |
| There! It just needed two hands. | Pronto! Só precisava de duas mãos. |
| And you got the front door open already? It always sticks for me. | E você já abriu a porta da frente? Comigo ela sempre emperra. |
| I came down to find the knocks. Listen… | Eu desci para achar as batidas. Escuta… |
| [The floor shakes: knock, knock… knock] | [O chão treme: toc, toc… toc] |
| [A wooden piece lifts three times behind the owl panel.] | [Uma peça de madeira sobe três vezes atrás do painel da coruja.] |
| A painted owl, wings folded. | Uma coruja pintada, de asas fechadas. |
| [Tap. The striped arm swings in the air.] | [Tec. O braço listrado balança no ar.] |
| [Click!] | [Clique!] |
| [Three knocks. Outside, the owl bows three times.] | [Três batidas. Lá fora, a coruja se curva três vezes.] |
| Come on, let's watch from the porch! | Vem, vamos assistir da varanda! |
| First show of the night. Best seats are on the porch. | Primeiro show da noite. Os melhores lugares são na varanda. |
| So, detective, what happened here? | E então, detetive, o que aconteceu aqui? |
| How did June get in? | Como a June entrou? |
| Where are the knocks coming from? | De onde vêm as batidas? |
| What should the knocks move? | O que as batidas deveriam mover? |
| Bea: a silver flash, up high. | Bea: um brilho prateado, lá no alto. |
| Mr. Vale: the front door stayed shut. | Sr. Vale: a porta da frente ficou fechada. |
| From the bench: the door, and the hedge. | Do banco: a porta, e a cerca viva. |
| The foil streamer on Bea's bike. | A fita de papel-alumínio na bicicleta da Bea. |
| Soft silver cloth on a splinter. | Pano prateado macio numa farpa. |
| A silver thread on the attic sill. | Um fio prateado no peitoril do sótão. |
| The front door won't budge. | A porta da frente não se mexe. |
| The floorboards tremble with the knocks. | As tábuas do chão tremem com as batidas. |
| Behind the grille, a wooden piece lifts three times. | Atrás da grade, uma peça de madeira sobe três vezes. |
| A painted owl on the workshop panel. | Uma coruja pintada no painel da oficina. |
| Kit's wooden owl, outside the house. | A coruja de madeira da Kit, do lado de fora da casa. |
| A striped arm, hanging loose. | Um braço listrado, pendurado solto. |
| Found it! | Achei! |
| Case closed | Caso encerrado |
| *Only after the child solves that question:* Not by the front door: up high on the hidden side, in at the attic window. | Não pela porta da frente: lá no alto, no lado escondido, pela janela do sótão. |
| *Only after solving:* Under the floor! A wooden piece lifts and knocks. | Debaixo do chão! Uma peça de madeira sobe e bate. |
| *Only after solving:* An owl! The loose striped arm should make it bow. | Uma coruja! O braço listrado solto deveria fazer ela se curvar. |

Dicas do rádio: se a criança pedir para ler uma dica, leia também literalmente (exemplos: "A breeze keeps tugging
toward the side yard." = "Uma brisa fica puxando para o quintal lateral."; "Listen low down in the hall." = "Escuta
lá embaixo, no corredor."). A dica já foi pedida pela criança: anote o nível e leia sem acrescentar nada.
