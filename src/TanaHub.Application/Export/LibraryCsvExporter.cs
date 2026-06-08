using System.Text;

namespace TanaHub.Application.Export;

public static class LibraryCsvExporter
{
    public static string Export(IEnumerable<LibraryExportItem> items)
    {
        var csv = new StringBuilder();
        csv.AppendLine("MediaId,Title,Type,Status,Progress,Score");

        foreach (var item in items)
        {
            csv.Append(Escape(item.MediaId)).Append(',')
                .Append(Escape(item.Title)).Append(',')
                .Append(Escape(item.Type)).Append(',')
                .Append(Escape(item.Status)).Append(',')
                .Append(item.Progress).Append(',')
                .Append(item.Score?.ToString() ?? string.Empty)
                .AppendLine();
        }

        return csv.ToString();
    }

    private static string Escape(string value)
    {
        if (!value.ContainsAny([',', '"', '\r', '\n']))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
