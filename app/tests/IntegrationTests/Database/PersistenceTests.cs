using Domain.Users;
using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.Database;

public sealed class PersistenceTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Theory]
    [InlineData("todo_items", "description", 500)]
    [InlineData("users", "email", 320)]
    [InlineData("users", "first_name", 100)]
    [InlineData("users", "last_name", 100)]
    public async Task Schema_Should_LimitTextColumnLength(string table, string column, int maxLength)
    {
        // Arrange
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Act
        int? length = await context.Database
            .SqlQuery<int?>($"""
                SELECT character_maximum_length AS "Value"
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = {table} AND column_name = {column}
                """)
            .SingleAsync();

        // Assert
        length.ShouldBe(maxLength);
    }

    [Fact]
    public void DbContext_Should_RetryOnTransientFailures()
    {
        // Arrange
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Act
        bool retriesOnFailure = context.Database.CreateExecutionStrategy().RetriesOnFailure;

        // Assert
        retriesOnFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveChanges_Should_DispatchDomainEventsWithoutCancellation()
    {
        // Arrange
        using IServiceScope scope = Factory.Services.CreateScope();
        DbContextOptions<ApplicationDbContext> options =
            scope.ServiceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>();

        var dispatcher = new RecordingDomainEventsDispatcher();
        await using var context = new ApplicationDbContext(options, dispatcher);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = UniqueEmail(),
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "hash"
        };
        user.Raise(new UserRegisteredDomainEvent(user.Id, user.Email));
        context.Users.Add(user);

        using var requestCancellation = new CancellationTokenSource();

        // Act
        await context.SaveChangesAsync(requestCancellation.Token);

        // Assert
        dispatcher.DispatchedEvents.ShouldHaveSingleItem().ShouldBeOfType<UserRegisteredDomainEvent>();
        dispatcher.ReceivedToken.CanBeCanceled.ShouldBeFalse();
    }

    private sealed class RecordingDomainEventsDispatcher : IDomainEventsDispatcher
    {
        public List<IDomainEvent> DispatchedEvents { get; } = [];

        public CancellationToken ReceivedToken { get; private set; }

        public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
        {
            DispatchedEvents.AddRange(domainEvents);
            ReceivedToken = cancellationToken;

            return Task.CompletedTask;
        }
    }
}
