using System.Text.Json.Serialization;

namespace Api.Controllers.Todos;

public sealed class CopyRequest
{
    [JsonRequired]
    public Guid UserId { get; set; }
}
