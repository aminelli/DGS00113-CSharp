# Prompt: rilevamento targhe con Azure AI Vision OCR e .NET 9

Agisci come un consulente e sviluppatore esperto di .NET, C# e Azure AI. Rispondi in italiano, salvo per il codice e gli identificatori tecnici, che devono essere in inglese.

## Obiettivo

Progetta e realizza un'applicazione **console .NET** compatibile con **Windows 11** e **Visual Studio 2022** che analizzi le immagini presenti direttamente in una cartella locale e determini se ciascuna immagine contiene la targa di un veicolo, in particolare camion e rimorchi. Il percorso della cartella deve essere configurabile. Analizza solo i file al livello superiore: **non** elaborare le sottocartelle. I formati da supportare sono **JPEG e PNG**.

Il servizio richiesto è **Azure AI Vision OCR**: l'applicazione deve inviare le immagini a una risorsa Azure AI Vision e usare il relativo SDK ufficiale Microsoft, nella versione attualmente supportata e compatibile con l'API OCR scelta. L'autenticazione richiesta avviene tramite **endpoint e key** della risorsa. Non sostituire il servizio o il metodo di autenticazione con un prodotto o una modalità diversi senza prima spiegare il motivo e ottenere conferma.

Il framework richiesto è **.NET 9** (non definirlo LTS). Verifica su fonti ufficiali Microsoft lo stato di supporto alla data di esecuzione e la versione minima di Visual Studio 2022 necessaria per sviluppare e compilare il progetto. Se .NET 9 è fuori supporto, oppure l'ambiente Visual Studio 2022 indicato non è compatibile, segnala il conflitto e chiedi se mantenere il requisito oppure passare a una versione/framework supportato. Non cambiare versione autonomamente e non iniziare l'implementazione finché la decisione non è stata presa.

Il volume è da pilot: non sono richieste ottimizzazioni specifiche per grandi lotti. Preferisci un'elaborazione sequenziale semplice e affidabile, salvo che la documentazione o i limiti del servizio richiedano diversamente.

L'output richiesto è **JSON**. Definisci e documenta una struttura consistente per i risultati per immagine e per il riepilogo del lotto; come impostazione predefinita, scrivi un file JSON valido in una posizione configurabile e stampa a console il riepilogo e gli eventuali errori diagnostici senza corrompere il JSON.

## Analisi preliminare e domande

Prima di scrivere codice o creare file:

1. Esamina la soluzione e l'ambiente disponibili, se hai accesso al repository e al terminale. Riutilizza le convenzioni e gli strumenti già presenti.
2. Verifica nella documentazione ufficiale Microsoft lo stato di supporto di .NET 9, il servizio Azure AI Vision OCR, l'API OCR appropriata e il relativo SDK. Indica i link alle fonti consultate e le eventuali differenze tra OCR generico e riconoscimento specializzato di targhe.
3. Considera Azure AI Vision OCR un vincolo esplicito, non una scelta da rifare. Valuta però se le sue capacità sono sufficienti per individuare targhe nelle immagini in esame; non promettere riconoscimento ALPR/LPR specializzato né accuratezza garantita. Se il requisito non è tecnicamente soddisfacibile con il servizio richiesto, descrivi il limite e chiedi conferma prima di proporre una sostituzione o un servizio aggiuntivo.
4. Individua le informazioni mancanti che cambiano in modo significativo architettura, comportamento o costi. Raccoglile in un'unica serie concisa di domande e attendi le risposte prima di iniziare l'implementazione.
5. Per le scelte non bloccanti, proponi valori predefiniti ragionevoli e dichiarali esplicitamente; non moltiplicare le domande per dettagli facilmente configurabili.

Le scelte operative principali sono già definite: cartella non ricorsiva, JPEG/PNG, output JSON, uso pilota, Windows 11, Visual Studio 2022 e autenticazione tramite endpoint e key. Non chiedere nuovamente queste informazioni. Chiedi chiarimenti solo per aspetti ancora mancanti che cambino significativamente l'architettura o il comportamento. La compatibilità e il supporto di .NET 9 sono un punto di conferma obbligatorio qualora la verifica ufficiale rilevi un conflitto.

## Requisiti di implementazione

Dopo aver ricevuto le risposte necessarie:

- Crea un'applicazione console C# per la versione .NET confermata, semplice da configurare e avviare da Visual Studio 2022 e da terminale su Windows 11.
- Usa Azure AI Vision OCR e il relativo SDK ufficiale Microsoft. Scegli l'API OCR supportata più adatta alle immagini e documenta la scelta, la versione dell'SDK, eventuali limiti di disponibilità regionale e le istruzioni Microsoft pertinenti. Non usare SDK o API deprecati senza dichiararlo e motivarlo.
- Rendi configurabili il percorso della cartella di input e quello del file JSON di output; non inserire percorsi macchina specifici nel codice.
- Enumera solo i file JPEG e PNG presenti direttamente nella cartella, senza ricorsione. Tratta le estensioni senza distinzione tra maiuscole e minuscole e documenta il comportamento per file non supportati.
- Elabora le immagini sequenzialmente per il pilot, rispettando comunque rate limit, dimensioni massime e altri limiti documentati dal servizio.
- Per ogni immagine, usa il testo e le informazioni restituite dall'OCR per produrre un risultato chiaro che distingua almeno tra targa rilevata, targa non rilevata ed errore di elaborazione. Spiega in modo trasparente come viene determinato il rilevamento: non confondere il semplice riconoscimento di testo nell'immagine con una classificazione certa di una sequenza come targa. Conserva o esporta solo i dati utili all'output concordato.
- Produci JSON valido e serializzato con le API .NET, includendo almeno nome/percorso relativo del file, stato, eventuale testo OCR utile e dettagli dell'errore senza dati sensibili. Includi un riepilogo con conteggi e assicurati che un errore su un'immagine non impedisca di registrare gli altri risultati. Distingui gli errori applicativi (ad esempio cartella o output non validi) dagli errori per singolo file.
- Gestisci cartella inesistente o vuota, formati non supportati, file illeggibili, errori di rete e risposte del servizio senza interrompere inutilmente l'intero lotto.
- Usa operazioni asincrone dove appropriato, messaggi diagnostici comprensibili e un riepilogo finale dei risultati.
- Usa endpoint e key come richiesto, ma non inserire chiavi o altri segreti nel codice, nel JSON di output o nei file versionati. Configurali tramite variabili d'ambiente o .NET User Secrets in sviluppo e descrivi come impostarli su Windows 11. Spiega la rotazione e la protezione della key; non stampare il valore nei log.
- Evita database, servizi aggiuntivi e infrastruttura cloud non necessari ai requisiti.
- Mantieni nomi di classi, metodi, variabili, file tecnici e configurazioni in inglese.
- Segnala chiaramente che il rilevamento OCR può avere falsi positivi o falsi negativi e non equivale necessariamente a un sistema ALPR/LPR certificato. Non presentare il risultato come identificazione certa.

## Documentazione e consegna

Fornisci una guida dettagliata in **italiano**, in Markdown, che includa:

1. architettura adottata e motivazione dell'uso di Azure AI Vision OCR, con fonti Microsoft e limiti del riconoscimento targhe;
2. prerequisiti e versioni richieste di .NET 9 e Visual Studio 2022, chiarendo il modello e lo stato di supporto verificato con fonti ufficiali;
3. creazione o configurazione delle risorse Azure e dei permessi necessari;
4. configurazione sicura delle credenziali;
5. comandi PowerShell esatti per creare, compilare, eseguire e verificare il progetto, oltre alle istruzioni per aprirlo e avviarlo in Visual Studio 2022;
6. struttura dei file e descrizione del flusso di elaborazione;
7. schema JSON dei risultati, esempio realistico, configurazione dei percorsi e gestione degli errori;
8. limiti noti, considerazioni su costi, privacy e dati delle targhe.

Realizza una soluzione completa e verificabile, non limitarti a frammenti di codice. Includi test o verifiche pratiche proporzionati al progetto e riporta con precisione eventuali passaggi che richiedono credenziali o risorse Azure non disponibili nell'ambiente.

## Criteri di completamento

Considera il lavoro concluso quando:

- il progetto compila;
- la versione .NET e l'API OCR usate sono quelle concordate e risultano documentate;
- è compatibile con la versione confermata di Visual Studio 2022 e con Windows 11;
- vengono elaborati solo JPEG e PNG nella cartella selezionata, senza scansione delle sottocartelle;
- input e output sono configurabili e l'output è JSON valido;
- ogni immagine produce un esito distinguibile, inclusi gli errori;
- endpoint e key sono configurabili senza esporre segreti nel codice, nei log o nell'output;
- la guida consente a un altro sviluppatore di predisporre l'ambiente ed eseguire l'applicazione;
- i limiti del servizio e del riconoscimento sono documentati senza promesse di accuratezza non verificate.
