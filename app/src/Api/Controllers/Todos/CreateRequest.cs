using System.Diagnostics.CodeAnalysis;
using Domain.Todos;

namespace Api.Controllers.Todos;

// A missing UserId stays Guid.Empty and is rejected by CreateTodoCommandValidator (NotEmpty);
// a missing Priority keeps the documented default, as in the contract before controllers.
[SuppressMessage("Major Code Smell", "S6964", Justification = "Under-posting is covered by the command validator.")]
public sealed class CreateRequest
{
    public Guid UserId { get; set; }
    public string Description { get; set; }
    public DateTime? DueDate { get; set; }
    public List<string> Labels { get; set; } = [];
    public Priority Priority { get; set; }
}
