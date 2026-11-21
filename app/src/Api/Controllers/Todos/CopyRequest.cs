using System.Diagnostics.CodeAnalysis;

namespace Api.Controllers.Todos;

// A missing UserId stays Guid.Empty and is rejected by CopyTodoCommandValidator (NotEmpty).
[SuppressMessage("Major Code Smell", "S6964", Justification = "Under-posting is covered by the command validator.")]
public sealed class CopyRequest
{
    public Guid UserId { get; set; }
}
