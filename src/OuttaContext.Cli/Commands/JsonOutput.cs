using System.Text.Encodings.Web;
using System.Text.Json;

namespace OuttaContext.Cli.Commands;

/// <summary>JSON options for --json output.</summary>
public static class JsonOutput
{
    public static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
