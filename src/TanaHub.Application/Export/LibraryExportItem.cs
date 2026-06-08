namespace TanaHub.Application.Export;

public sealed record LibraryExportItem(
    string MediaId,
    string Title,
    string Type,
    string Status,
    int Progress,
    int? Score);
