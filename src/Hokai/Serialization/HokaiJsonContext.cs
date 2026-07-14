using System.Text.Json;
using System.Text.Json.Serialization;
using Hokai.Models;

namespace Hokai.Serialization;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true)]
[JsonSerializable(typeof(List<EndpointConfig>))]
[JsonSerializable(typeof(List<CheckResult>))]
internal sealed partial class HokaiJsonContext : JsonSerializerContext
{
}
