# Invoice document processor

Console application for analyzing PDF invoices with Azure AI Document Intelligence, displaying the extracted fields, and saving normalized data and the complete SDK analysis result to SQL Server.

## Requirements

- .NET 9 SDK for running the application locally.
- Docker Desktop on Windows in Linux container mode for the SQL Server test database.
- An Azure AI Document Intelligence resource with the invoice model available.
- SQL Server Management Studio (SSMS), if you want to inspect the local database.

The application uses the official `Azure.AI.DocumentIntelligence` SDK and `Microsoft.Data.SqlClient`. The configured model defaults to `prebuilt-invoice`.

## Security setup

The API key previously included in the original prompt must be treated as compromised. Revoke or rotate it in Azure before running the application. Do not copy it into source code, this repository, command history, logs, or documentation.

Set these variables in the same PowerShell window where you run the application. Replace each placeholder locally; never commit real values:

```powershell
$env:DOCUMENT_INTELLIGENCE_ENDPOINT = "https://<your-resource>.cognitiveservices.azure.com/"
$env:DOCUMENT_INTELLIGENCE_API_KEY = "<new-key-after-rotation>"
$env:DOCUMENT_INTELLIGENCE_MODEL_ID = "prebuilt-invoice"
$env:SQLSERVER_HOST = "localhost"
$env:SQLSERVER_PORT = "1433"
$env:SQLSERVER_DATABASE = "InvoiceDb"
$env:SQLSERVER_USER = "sa"
$env:SQLSERVER_PASSWORD = "<local-sql-password>"
$env:SQLSERVER_TRUST_SERVER_CERTIFICATE = "true"
```

Alternatively, supply `SQLSERVER_CONNECTION_STRING` instead of the individual SQL settings. The application uses `SqlConnectionStringBuilder` for the individual settings, so special characters in the password are handled correctly. The API key and SQL password remain outside versioned files. For production SQL Server, configure a trusted certificate and set `SQLSERVER_TRUST_SERVER_CERTIFICATE` to `false`.

## Start the SQL Server test container

1. Copy `.env.example` to `.env`, edit it, and set `MSSQL_SA_PASSWORD` to a unique local password that meets SQL Server's password policy. The `.env` file is ignored by Git.

   ```powershell
   Copy-Item .env.example .env
   notepad .env
   ```
2. Optionally change `SQLSERVER_HOST_PORT` if port 1433 is already in use.
3. From the project directory, build both images and start the services:

   ```powershell
   docker build -f Dockerfile.sqlserver -t docmng-sqlserver .
   docker build -f Dockerfile -t docmng-database-init .
   docker compose up -d
   docker compose ps
   docker compose logs database-init
   ```

Compose starts the official SQL Server 2022 Developer container, waits for its TCP health check, then runs the `database-init` one-shot service. SQL Server can accept TCP connections before its first-start initialization has finished, so the initializer retries transient startup login/network errors for up to about two minutes. The custom SQL Server image copies `Database/Schema.sql` into the container; the initialization service applies the same idempotent schema through the application repository. The scripts are not assumed to run merely because they were copied into the SQL Server image.

The host port is `1433` by default and is bound to the local machine only. In SSMS, connect to `127.0.0.1,1433` using SQL Server Authentication, login `sa`, the local password from `.env`, and database `InvoiceDb`. For a different mapped port, use `127.0.0.1,<SQLSERVER_HOST_PORT>`.

SQL data persists in the `sqlserver_data` Docker volume. To stop the services without deleting data:

```powershell
docker compose down
```

To permanently erase the test database and its data, explicitly remove the volume:

```powershell
docker compose down --volumes
```

## Process invoices

Create these folders under the application's working directory (the application creates missing folders):

```text
Docs\
  Invoices\
  Archive\
  Errors\
  Logs\
```

Place PDF files in `Docs\Invoices`, then, from the project directory and in a PowerShell session with all configuration variables set, run:

```powershell
dotnet restore
dotnet run
```

The root folder defaults to `Docs` relative to the current working directory. To change it for a run, set `$env:DOCS_ROOT` to the desired path. The app scans `.pdf` files sequentially, computes a SHA-256 hash, submits each document to `prebuilt-invoice`, displays recognized invoice fields and line items, then saves the result.

On successful analysis and SQL transaction, the PDF moves to `Archive`. Files that fail analysis or persistence move to `Errors`. Existing destination names are never overwritten; a hash suffix is added when necessary. If the SQL commit succeeds but the archive move fails, the file stays in `Invoices`; on the next run the hash lookup recognizes the committed invoice and retries only the move, avoiding another service call or duplicate invoice.

The complete serialized `AnalyzeResult` model is stored in `JsonImportLog.RawJson`. `Invoice` and `InvoiceLine` hold queryable normalized values. `DocumentIntelligenceCallLog` stores each completed service attempt, its duration, status code where available, and failure message. Text logs are written as one JSON Lines entry per PDF under `Docs\Logs`, without invoice field values or credentials.

## Database schema

`Database/Schema.sql` creates the following objects idempotently:

- `Invoice`: normalized invoice fields and unique source-file hash.
- `InvoiceLine`: line items with a foreign key to `Invoice`.
- `JsonImportLog`: complete analysis JSON and import outcomes.
- `DocumentIntelligenceCallLog`: service call outcomes and durations.

The schema is applied automatically at app startup. To run only database initialization after starting Compose:

```powershell
docker compose run --rm database-init
```

## Build and troubleshooting

```powershell
dotnet build
```

If SQL Server is not ready, wait for `docker compose ps` to show the service as healthy and inspect `docker compose logs sqlserver database-init`. If the Azure call fails, verify the resource endpoint, rotated API key, resource availability, network access, and model ID. Database and service exceptions are written to the per-file log; if SQL Server is unavailable, the application still writes the failure to its local text log when the filesystem is writable.

The application currently processes one PDF at a time and stores the full result JSON in SQL Server. Run it from a trusted local environment and protect both the database volume and generated logs, since invoice documents can contain sensitive business data.
