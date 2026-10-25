using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SharedKernel;
using Testcontainers.PostgreSql;

namespace IntegrationTests.Database;

// Runs on its own database: the shared factory is always fully migrated, so it cannot hold pre-migration data.
public sealed class EmailConfirmationMigrationTests : IAsyncLifetime
{
    private const string MigrationBefore = "20261018213333_Add_Todo_Archived_Fields";

    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:17.6")
        .WithDatabase("clean-architecture-migrations")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public Task InitializeAsync() => _dbContainer.StartAsync();

    public Task DisposeAsync() => _dbContainer.DisposeAsync().AsTask();

    [Fact] // SPEC-0003 AC-15
    public async Task AddEmailConfirmation_Should_ConfirmExistingUsers_AndLeaveNewOnesUnconfirmed()
    {
        // Arrange
        await using ApplicationDbContext context = CreateContext();
        IMigrator migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(MigrationBefore);

        var existingUserId = Guid.NewGuid();
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO public.users (id, email, first_name, last_name, password_hash)
            VALUES ({existingUserId}, 'existing@example.com', 'Existing', 'User', 'hash')
            """);

        // Act
        await migrator.MigrateAsync();

        var newUserId = Guid.NewGuid();
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO public.users (id, email, first_name, last_name, password_hash, registered_at)
            VALUES ({newUserId}, 'new@example.com', 'New', 'User', 'hash', now())
            """);

        // Assert
        UserRow existing = await ReadUserAsync(context, existingUserId);
        existing.EmailConfirmedAt.ShouldNotBeNull();
        existing.RegisteredAt.ShouldBeGreaterThan(DateTime.UnixEpoch);

        UserRow created = await ReadUserAsync(context, newUserId);
        created.EmailConfirmedAt.ShouldBeNull();
    }

    private ApplicationDbContext CreateContext()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                _dbContainer.GetConnectionString(),
                npgsqlOptions => npgsqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, "public"))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ApplicationDbContext(options, new NoOpDomainEventsDispatcher());
    }

    private static Task<UserRow> ReadUserAsync(ApplicationDbContext context, Guid userId) =>
        context.Database
            .SqlQuery<UserRow>($"""
                SELECT email_confirmed_at AS "EmailConfirmedAt", registered_at AS "RegisteredAt"
                FROM public.users
                WHERE id = {userId}
                """)
            .SingleAsync();

    private sealed record UserRow(DateTime? EmailConfirmedAt, DateTime RegisteredAt);

    private sealed class NoOpDomainEventsDispatcher : IDomainEventsDispatcher
    {
        public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
