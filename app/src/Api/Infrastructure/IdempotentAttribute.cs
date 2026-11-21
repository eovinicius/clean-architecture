using Microsoft.AspNetCore.Mvc;

namespace Api.Infrastructure;

// Opts an action into IdempotencyFilter; IdempotencyOperationTransformer documents the header and the 409.
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute : TypeFilterAttribute
{
    public IdempotentAttribute()
        : base(typeof(IdempotencyFilter))
    {
    }
}
