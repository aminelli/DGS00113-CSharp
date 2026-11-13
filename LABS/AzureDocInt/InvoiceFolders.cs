namespace DocMng;

internal sealed record InvoiceFolders(string Invoices, string Archive, string Errors, string Logs)
{
    public static InvoiceFolders Create(string docsRoot)
    {
        var root = Path.GetFullPath(docsRoot);
        var folders = new InvoiceFolders(
            Path.Combine(root, "Invoices"),
            Path.Combine(root, "Archive"),
            Path.Combine(root, "Errors"),
            Path.Combine(root, "Logs"));

        Directory.CreateDirectory(folders.Invoices);
        Directory.CreateDirectory(folders.Archive);
        Directory.CreateDirectory(folders.Errors);
        Directory.CreateDirectory(folders.Logs);
        return folders;
    }
}
