namespace DocMng;

internal sealed record ExtractedInvoice(
    string? VendorName,
    string? CustomerName,
    string? InvoiceNumber,
    DateTime? InvoiceDate,
    DateTime? DueDate,
    string? PurchaseOrder,
    decimal? Subtotal,
    decimal? TotalTax,
    decimal? InvoiceTotal,
    string? CurrencyCode,
    IReadOnlyList<ExtractedInvoiceLine> Lines);

internal sealed record ExtractedInvoiceLine(
    string? Description,
    decimal? Quantity,
    string? Unit,
    decimal? UnitPrice,
    decimal? Amount,
    string? ProductCode,
    string? Tax);

internal sealed record ProcessingLogEntry(
    DateTimeOffset TimestampUtc,
    string FileName,
    string FileHash,
    string Status,
    string? Message);
