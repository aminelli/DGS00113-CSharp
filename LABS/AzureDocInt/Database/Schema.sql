IF OBJECT_ID(N'dbo.Invoice', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Invoice
    (
        InvoiceId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Invoice PRIMARY KEY,
        SourceFileName NVARCHAR(260) NOT NULL,
        SourceFileHash CHAR(64) NOT NULL,
        VendorName NVARCHAR(500) NULL,
        CustomerName NVARCHAR(500) NULL,
        InvoiceNumber NVARCHAR(200) NULL,
        InvoiceDate DATE NULL,
        DueDate DATE NULL,
        PurchaseOrder NVARCHAR(200) NULL,
        Subtotal DECIMAL(18,2) NULL,
        TotalTax DECIMAL(18,2) NULL,
        InvoiceTotal DECIMAL(18,2) NULL,
        CurrencyCode NVARCHAR(10) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Invoice_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_Invoice_SourceFileHash UNIQUE (SourceFileHash)
    );
END;

IF OBJECT_ID(N'dbo.InvoiceLine', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InvoiceLine
    (
        InvoiceLineId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InvoiceLine PRIMARY KEY,
        InvoiceId BIGINT NOT NULL,
        LineNumber INT NOT NULL,
        Description NVARCHAR(1000) NULL,
        Quantity DECIMAL(18,4) NULL,
        Unit NVARCHAR(100) NULL,
        UnitPrice DECIMAL(18,4) NULL,
        Amount DECIMAL(18,2) NULL,
        ProductCode NVARCHAR(200) NULL,
        Tax NVARCHAR(100) NULL,
        CONSTRAINT FK_InvoiceLine_Invoice FOREIGN KEY (InvoiceId)
            REFERENCES dbo.Invoice(InvoiceId) ON DELETE CASCADE,
        CONSTRAINT UQ_InvoiceLine_Invoice_LineNumber UNIQUE (InvoiceId, LineNumber)
    );
END;

IF OBJECT_ID(N'dbo.JsonImportLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.JsonImportLog
    (
        JsonImportLogId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_JsonImportLog PRIMARY KEY,
        InvoiceId BIGINT NULL,
        SourceFileName NVARCHAR(260) NOT NULL,
        SourceFileHash CHAR(64) NOT NULL,
        RawJson NVARCHAR(MAX) NULL,
        Status NVARCHAR(30) NOT NULL,
        ErrorMessage NVARCHAR(2000) NULL,
        ImportedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_JsonImportLog_ImportedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_JsonImportLog_Invoice FOREIGN KEY (InvoiceId)
            REFERENCES dbo.Invoice(InvoiceId)
    );
END;

IF OBJECT_ID(N'dbo.DocumentIntelligenceCallLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentIntelligenceCallLog
    (
        DocumentIntelligenceCallLogId BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_DocumentIntelligenceCallLog PRIMARY KEY,
        SourceFileName NVARCHAR(260) NOT NULL,
        SourceFileHash CHAR(64) NOT NULL,
        ModelId NVARCHAR(100) NOT NULL,
        StartedAtUtc DATETIME2(3) NOT NULL,
        DurationMilliseconds BIGINT NOT NULL,
        Succeeded BIT NOT NULL,
        HttpStatusCode INT NULL,
        ErrorMessage NVARCHAR(2000) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_DocumentIntelligenceCallLog_CreatedAtUtc DEFAULT SYSUTCDATETIME()
    );
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Invoice_InvoiceNumber' AND object_id = OBJECT_ID(N'dbo.Invoice')
)
BEGIN
    CREATE INDEX IX_Invoice_InvoiceNumber ON dbo.Invoice(InvoiceNumber);
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_JsonImportLog_SourceFileHash' AND object_id = OBJECT_ID(N'dbo.JsonImportLog')
)
BEGIN
    CREATE INDEX IX_JsonImportLog_SourceFileHash ON dbo.JsonImportLog(SourceFileHash, ImportedAtUtc DESC);
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DocumentIntelligenceCallLog_SourceFileHash' AND object_id = OBJECT_ID(N'dbo.DocumentIntelligenceCallLog')
)
BEGIN
    CREATE INDEX IX_DocumentIntelligenceCallLog_SourceFileHash
        ON dbo.DocumentIntelligenceCallLog(SourceFileHash, CreatedAtUtc DESC);
END;
