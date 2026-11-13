# Sviluppo applicazione per l'elaborazione delle fatture

Agisci come un senior .NET developer con esperienza in Microsoft Azure, Azure AI Document Intelligence, SQL Server e Docker.

## Contesto

Esamina la soluzione e il progetto C# esistenti prima di modificarli. È un'applicazione console .NET 9 sviluppata con Visual Studio 2022. Mantieni le convenzioni e la struttura esistenti quando sono adeguate e usa gli SDK e le immagini ufficiali Microsoft, compatibili e supportati.

L'applicazione deve elaborare fatture in formato PDF, comprese quelle scansionate, usando Azure AI Document Intelligence e il modello predefinito per le fatture (`prebuilt-invoice`).

## Requisiti funzionali

1. Leggi i file con estensione `.pdf` (senza distinzione tra maiuscole e minuscole) presenti in `Docs\Invoices`.
2. Invia ogni documento ad Azure AI Document Intelligence e recupera il risultato completo dell'analisi.
3. Rendi visibili nella console i dati principali della fattura e le relative righe. Gestisci in modo chiaro i campi assenti o non riconosciuti.
4. Salva nel database SQL Server:
   - il JSON completo restituito dal servizio;
   - i dati strutturati principali della fattura e le righe, così da poterli interrogare senza analizzare nuovamente il JSON.
5. Dopo il completamento corretto dell'analisi e del salvataggio nel database, sposta il PDF in `Docs\Archive`.
6. Se l'elaborazione o il salvataggio falliscono, sposta il PDF in `Docs\Errors` e registra il motivo. Evita sovrascritture e perdita di documenti se si verificano errori durante gli spostamenti.
7. Scrivi un log testuale con una riga per ciascun PDF elaborato, indicando almeno nome del file, esito e, in caso di errore, una descrizione utile. Non includere chiavi, stringhe di connessione o dati fattura sensibili.
8. Rendi il processo sicuro rispetto a riavvii e riesecuzioni, evitando duplicazioni dei dati. Gestisci esplicitamente i casi in cui il salvataggio SQL riesca ma lo spostamento del file fallisca, o viceversa.

La struttura delle cartelle è:

```text
Docs\
  Invoices\
  Archive\
  Errors\
```

Il percorso della cartella principale `Docs` può essere hardcoded per questa prima versione, ma deve essere dichiarato in un unico punto facilmente individuabile e modificabile. Crea le sottocartelle mancanti all'avvio, se appropriato.

## Modello dati e registrazione degli esiti

Progetta le tabelle SQL necessarie per memorizzare sia i dati estratti sia le tracce di elaborazione. Includi almeno:

- una tabella principale delle fatture con i dati salienti e i vincoli/indici appropriati;
- una tabella delle righe fattura con chiave esterna verso la fattura;
- una tabella di log dei JSON importati, contenente il JSON completo, il file di origine, date, esito e riferimenti alle entità correlate;
- una tabella di log dei tentativi di chiamata a Document Intelligence, con data/ora, modello, durata, esito e dettagli dell'errore o codice di stato disponibili.

Definisci tipi, precisioni per importi e quantità, chiavi, relazioni, nullabilità e indici in modo coerente con i dati restituiti dal modello. Non registrare segreti o payload sensibili nei log delle chiamate. Assicura una strategia d'idempotenza (ad esempio tramite hash del file o un identificativo stabile) e descrivi come vengono gestite le nuove elaborazioni dello stesso documento.

## SQL Server e Docker Desktop

Prepara un ambiente di test eseguibile su Windows con Docker Desktop, usando la modalità Linux container e un'immagine SQL Server ufficiale Microsoft adatta ai test.

Produci:

- un `Dockerfile` per l'immagine SQL Server personalizzata che includa gli script SQL necessari;
- gli script SQL per creare database, tabelle, relazioni e indici;
- un `docker-compose.yml` (o `compose.yaml`) per avviare il servizio;
- la mappatura sull'host della porta SQL Server necessaria per connettersi con SQL Server Management Studio (SSMS), configurabile se opportuno;
- health check e istruzioni di avvio, attesa della disponibilità e arresto.

Assicurati che gli script vengano effettivamente eseguiti all'avvio: non dare per scontato che l'immagine ufficiale esegua automaticamente gli script SQL solo perché copiati nel container. Implementa un meccanismo affidabile e idempotente, ad esempio un servizio di inizializzazione che attende SQL Server e poi applica gli script, oppure un entrypoint documentato. Non esporre la porta del database pubblicamente oltre quanto necessario per i test locali.

Configura la password dell'utente amministrativo SQL tramite variabile d'ambiente o file `.env` locale non versionato. Fornisci un `.env.example` con soli placeholder e istruzioni per creare la configurazione locale; non inserire password reali nei file. Usa un volume persistente per i dati del database e spiega come cancellarlo solo quando si desidera azzerare l'ambiente di test.

## Configurazione e sicurezza

- Non inserire mai chiavi API, password o stringhe di connessione in chiaro nel codice, nei file di configurazione versionabili, nella documentazione o nei log.
- Leggi endpoint e chiave di Azure AI Document Intelligence da configurazione esterna (ad esempio variabili d'ambiente o .NET User Secrets in sviluppo). Mantieni `prebuilt-invoice` come modello predefinito.
- Leggi la stringa di connessione SQL Server da configurazione esterna.
- Anche se la richiesta originale propone di hardcodare temporaneamente i dati sensibili, non inserirli nel codice o in file versionabili: predisponi da subito la parametrizzazione tramite configurazione esterna.
- La chiave API era stata riportata in chiaro nel prompt originale: considerala compromessa e non riutilizzarla; indica di revocarla/ruotarla prima di eseguire l'applicazione.
- Non usare valori segreti reali negli esempi: usa nomi di variabili e placeholder chiaramente fittizi.

## Aspetti tecnici

- Usa il pacchetto SDK ufficiale Microsoft per Azure AI Document Intelligence e le API appropriate per analizzare fatture PDF, comprese quelle scansionate.
- Usa una soluzione Microsoft supportata per l'accesso a SQL Server, coerente con il progetto esistente e con .NET 9.
- Gestisci errori di rete, risposte non valide, errori del servizio, errori del database e problemi del filesystem. Non nascondere le eccezioni né segnalare un successo se il salvataggio non è riuscito.
- Evita di caricare inutilmente tutti i PDF in memoria; elabora i documenti in modo affidabile e limita la concorrenza se introduci elaborazione parallela.
- Mantieni il codice e i nomi tecnici (classi, proprietà, tabelle, colonne e configurazioni) in inglese. La documentazione richiesta sotto deve essere in italiano.
- Aggiungi o aggiorna test mirati per i comportamenti introdotti, se il progetto dispone di un'impostazione di test adeguata.

## Modalità di lavoro

1. Esamina prima la soluzione, il progetto, il codice esistente e le dipendenze; non presumere che il progetto sia vuoto.
2. Prima di apportare modifiche, poni domande brevi e specifiche solo per le informazioni mancanti che impediscono una scelta corretta. Non chiedere né richiedere di incollare segreti: usa nomi di variabili d'ambiente o placeholder. Se una scelta non è bloccante, adotta un valore predefinito ragionevole, dichiaralo e procedi.
3. Implementa la soluzione completa, mantenendo le modifiche circoscritte ai requisiti.
4. Esegui i controlli pertinenti disponibili (build e test mirati) e correggi eventuali problemi introdotti.
5. Riassumi le modifiche, le decisioni e le assunzioni, i comandi necessari per configurare ed eseguire l'applicazione e l'esito dei controlli.

## Documentazione da produrre

Genera una documentazione dettagliata in italiano in formato Markdown, includendo almeno:

- prerequisiti e servizi Azure da predisporre;
- configurazione sicura dell'endpoint, della chiave e della connessione a SQL Server, con nomi e comandi di esempio privi di segreti reali;
- struttura del database e comandi o procedura per creare/aggiornare lo schema;
- contenuto del Dockerfile, inizializzazione SQL, avvio di Docker Compose, porta per SSMS e persistenza dei dati;
- struttura delle cartelle e flusso di elaborazione;
- comandi per compilare, configurare ed eseguire l'applicazione;
- formato dei log, gestione degli errori, idempotenza, riesecuzione e limitazioni note.

Sii preciso e tecnico: spiega i passaggi necessari senza includere credenziali reali o valori segreti.
