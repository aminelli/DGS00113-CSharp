# PlateOcr

Applicazione console C# che invia a **Azure AI Vision Image Analysis 4.0 OCR (Read)** le immagini JPEG/PNG contenute direttamente in una cartella. Salva un report JSON locale con l'esito e gli eventuali candidati che una semplice euristica considera plausibili targhe.

> **Limiti importanti**
>
> - L'OCR estrae testo: non è un riconoscitore ALPR/LPR e non certifica che il testo sia una targa. La regola euristica usata qui cerca sequenze alfanumeriche ASCII di 6-10 caratteri (anche composte unendo fino a tre token OCR), con almeno una lettera e una cifra. Può produrre falsi positivi e falsi negativi; non è specifica per un paese.
> - Microsoft ha annunciato il ritiro del servizio Image Analysis API (incluse le versioni 4.0 e 3.2) il **25 settembre 2028**. L'app usa 4.0 perché richiesto, ma per un progetto di durata maggiore pianificare una migrazione prima di tale data.
> - È stato confermato di mantenere .NET 9 e Visual Studio 2022. .NET 9 è STS, non LTS, e la policy Microsoft consultata indica il **10 novembre 2026** come data di fine supporto. La data coincide con quella di verifica: per un uso successivo occorre pianificare il passaggio a un runtime supportato e installare l'ultima patch disponibile.

## Requisiti

- Windows 11.
- .NET 9 SDK. Il feature band SDK 9.0.1xx richiede Visual Studio 2022 17.12; il feature band 9.0.3xx richiede Visual Studio 2022 17.14. In questo ambiente sono presenti SDK 9.0.312 e toolset Visual Studio 17.14.
- Visual Studio 2022 con workload **Sviluppo desktop .NET**, oppure il .NET SDK da terminale.
- Una risorsa Azure **Computer Vision / Azure AI Vision** con endpoint e key. La disponibilità del servizio dipende dalla regione; verificare la documentazione e la disponibilità della risorsa nella regione scelta.

Versioni e stato di supporto verificati il 10 novembre 2026:

- [Policy e lifecycle di .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- [Versioning SDK .NET, MSBuild e Visual Studio](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs)
- [Compatibilità di Visual Studio 2022](https://learn.microsoft.com/en-us/visualstudio/releases/2022/compatibility)

## Configurazione Azure

1. Nel portale Azure crea una risorsa **Computer Vision** nella sottoscrizione e nella regione desiderate.
2. Dalla risorsa copia **Endpoint** e una **Key**.
3. Non inserire la key nel codice, in `appsettings.json`, nel report o nei file versionati. L'app legge le variabili d'ambiente `VISION_ENDPOINT` e `VISION_KEY`.
4. La key è un segreto condiviso: proteggila, non stamparla, limita chi può leggerla e ruotala periodicamente. La guida Microsoft consiglia di custodire le API key in modo sicuro e di valutarne la gestione tramite Key Vault.

La risorsa e l'uso del servizio possono comportare costi o limiti di quota. Controllare il piano tariffario, la quota e le condizioni applicabili prima di elaborare immagini reali.

## Build ed esecuzione da PowerShell

Dalla cartella del repository:

```powershell
dotnet --list-sdks
dotnet restore .\PlateOcr\PlateOcr.csproj
dotnet build .\PlateOcr\PlateOcr.csproj --configuration Release
dotnet test .\PlateOcr.sln --configuration Release
```

Imposta i segreti solo per la sessione PowerShell corrente (le assegnazioni non vengono salvate nei file):

```powershell
$env:VISION_ENDPOINT = "https://<nome-risorsa>.cognitiveservices.azure.com/"
$env:VISION_KEY = "<key-della-risorsa>"
```

Prepara la cartella `images` nella cartella corrente e avvia l'app:

```powershell
dotnet run --project .\PlateOcr\PlateOcr.csproj -- --input .\images --output .\results.json
```

In alternativa, specifica un percorso diverso per input e output. I percorsi relativi sono risolti rispetto alla cartella di lavoro corrente.

Per sviluppare in Visual Studio:

1. Apri `PlateOcr.sln` dalla cartella del repository.
2. Configura `VISION_ENDPOINT` e `VISION_KEY` nell'ambiente del processo di Visual Studio (oppure avvia Visual Studio da una sessione PowerShell che le ha già impostate).
3. Seleziona il progetto `PlateOcr` come progetto di avvio e configura gli argomenti applicazione. Se la working directory è la cartella `PlateOcr`, usa `--input ..\images --output ..\results.json` per puntare alle cartelle del repository; altrimenti adatta i percorsi alla working directory.
4. Avvia con **F5** o **Ctrl+F5**.

Il repository non include le immagini, endpoint, key o una risorsa Azure; la chiamata end-to-end richiede che siano configurati dall'utente.

## Uso

```text
PlateOcr [--input <directory>] [--output <json-file-or-directory>] [--help]
```

- `--input`: cartella di input; predefinita `.\images`.
- `--output`: percorso di un file JSON o di una cartella esistente. Per una cartella, il report viene salvato come `results_yyyyMMdd_HHmmss.json` usando data e ora locali. Per creare una cartella non ancora esistente, termina il percorso con `\`, ad esempio `--output '.\JSON\'`. Senza `--output` il percorso predefinito è `.\JSON\results_yyyyMMdd_HHmmss.json`; la cartella viene creata automaticamente. Se il file risultante esiste già, viene sovrascritto.
- Sono considerati solo `.jpg`, `.jpeg` e `.png` (senza distinzione tra maiuscole/minuscole), direttamente nella cartella. Le sottocartelle e gli altri formati sono ignorati.
- L'elaborazione è sequenziale. Un errore su un'immagine viene registrato nel report e non impedisce l'elaborazione delle successive.
- Le diagnostiche e il riepilogo vanno su `stderr`; il report viene scritto come JSON UTF-8 nel percorso indicato.
- Codici di uscita: `0` completamento senza errori per immagine; `1` una o più immagini non elaborate; `2` errore di configurazione o di scrittura del report.

Esempio indicativo:

```json
{
  "generatedAtUtc": "2026-11-10T10:00:00+00:00",
  "summary": {
    "totalImages": 2,
    "detected": 1,
    "notDetected": 1,
    "errors": 0
  },
  "results": [
    {
      "fileName": "truck-01.jpg",
      "status": "detected",
      "plateCandidates": ["AB123CD"],
      "error": null
    },
    {
      "fileName": "trailer-02.png",
      "status": "notDetected",
      "plateCandidates": [],
      "error": null
    }
  ]
}
```

`detected` indica solo che l'OCR ha restituito una sequenza che corrisponde all'euristica sopra descritta. Un risultato `notDetected` non dimostra l'assenza di una targa. Un `error` include un messaggio generalizzato senza la key o l'endpoint.
Il JSON include soltanto il nome del file, non il suo percorso locale assoluto.

## Implementazione e verifiche

- Target framework: `net9.0`.
- SDK NuGet ufficiale: [`Azure.AI.Vision.ImageAnalysis` 1.0.0](https://www.nuget.org/packages/Azure.AI.Vision.ImageAnalysis).
- API: `ImageAnalysisClient.AnalyzeAsync(..., VisualFeatures.Read)`.
- I byte dell'immagine vengono inviati dal client locale al servizio Azure; il report resta locale. Considerare la sensibilità delle immagini e dei dati OCR prima dell'uso.
- I casi di input (help, cartella vuota/inesistente, parametri mancanti, filtri di estensione) si possono verificare senza chiamate OCR. Per verificare le risposte OCR serve una risorsa Azure configurata.
- I test automatizzati verificano il filtro JPEG/PNG non ricorsivo e l'euristica sui candidati: `dotnet test .\PlateOcr.sln`.

Fonti Microsoft:

- [Quickstart Image Analysis 4.0 per .NET](https://learn.microsoft.com/en-us/azure/ai-services/computer-vision/quickstarts-sdk/image-analysis-client-library-40?tabs=csharp)
- [Panoramica di Image Analysis e funzionalità Read OCR](https://learn.microsoft.com/en-us/azure/ai-services/computer-vision/overview-image-analysis?tabs=4-0)
- [Requisiti degli input e regioni supportate](https://learn.microsoft.com/en-us/azure/ai-services/computer-vision/overview-image-analysis?tabs=4-0#input-requirements)
- [Fine vita e opzioni di migrazione di Image Analysis](https://learn.microsoft.com/en-us/azure/ai-services/computer-vision/migration-options)
- [Best practice per le API key](https://learn.microsoft.com/en-us/azure/key-vault/general/apps-api-keys-secrets)
