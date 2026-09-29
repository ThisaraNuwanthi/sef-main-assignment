using System.Text.Json;
using System.Text.Json.Serialization;

namespace SkcaEnrol.Tests.Infrastructure;

/// <summary>Same JSON settings as the API (camelCase, enums as strings).</summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
