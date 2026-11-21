using Domain.Todos;

namespace Api.Controllers.Todos;

public sealed class CreateRequest
{
    public Guid? UserId { get; set; }
    public string Description { get; set; }
    public DateTime? DueDate { get; set; }
    public List<string> Labels { get; set; } = [];
    public Priority? Priority { get; set; }
}
