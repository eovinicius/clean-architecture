using System.Reflection;
using System.Runtime.CompilerServices;
using Application.Abstractions.Messaging;
using FluentValidation;
using Shouldly;
using Web.Api.Endpoints;

namespace ArchitectureTests.Conventions;

public sealed class ConventionTests : BaseTest
{
    private static readonly Type[] HandlerInterfaces =
    [
        typeof(ICommandHandler<>),
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>)
    ];

    [Fact]
    public void Handlers_Should_BeInternalAndSealed()
    {
        // Arrange
        Type[] handlers = [.. ConcreteTypes(ApplicationAssembly.GetTypes())
            .Where(type => type.GetInterfaces().Any(IsHandlerInterface))];
        handlers.ShouldNotBeEmpty();

        // Act
        string[] violations = [.. handlers.Where(type => !IsInternalAndSealed(type)).Select(type => type.FullName!)];

        // Assert
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Endpoints_Should_BeInternalAndSealed()
    {
        // Arrange
        Type[] endpoints = [.. ConcreteTypes(PresentationAssembly.GetTypes())
            .Where(type => type.IsAssignableTo(typeof(IEndpoint)))];
        endpoints.ShouldNotBeEmpty();

        // Act
        string[] violations = [.. endpoints.Where(type => !IsInternalAndSealed(type)).Select(type => type.FullName!)];

        // Assert
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Validators_Should_HaveNameEndingWithValidator()
    {
        // Arrange
        Type[] validators = [.. ConcreteTypes(ApplicationAssembly.GetTypes())
            .Where(type => type.BaseType is { IsGenericType: true } baseType &&
                baseType.GetGenericTypeDefinition() == typeof(AbstractValidator<>))];
        validators.ShouldNotBeEmpty();

        // Act
        string[] violations = [.. validators
            .Where(type => !type.Name.EndsWith("Validator", StringComparison.Ordinal))
            .Select(type => type.FullName!)];

        // Assert
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Validators_Should_BeInternalAndSealed()
    {
        // Arrange
        Type[] validators = [.. Validators().Where(type => !LegacyPublicValidators.Contains(type.Name))];
        validators.ShouldNotBeEmpty();

        // Act
        string[] violations = [.. validators.Where(type => !IsInternalAndSealed(type)).Select(type => type.FullName!)];

        // Assert
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Requests_Should_NotCarryUserId()
    {
        // Arrange
        Type[] requests = [.. ConcreteTypes(ApplicationAssembly.GetTypes())
            .Where(type => type.GetInterfaces().Any(IsRequestInterface))
            .Where(type => !LegacyUserIdRequests.Contains(type.Name))];
        requests.ShouldNotBeEmpty();

        // Act
        string[] violations = [.. requests
            .Where(type => type.GetProperty("UserId") is not null)
            .Select(type => type.FullName!)];

        // Assert
        violations.ShouldBeEmpty();
    }

    [Fact] // ADR-0004
    public void DomainClasses_Should_NotHavePublicSetters()
    {
        // Arrange
        Type[] domainClasses = [.. ConcreteTypes(DomainAssembly.GetTypes()).Where(type => !IsRecord(type))];
        domainClasses.ShouldNotBeEmpty();

        // Act
        string[] violations = [.. domainClasses
            .SelectMany(type => type.GetProperties())
            .Where(property => property.SetMethod is { IsPublic: true })
            .Select(property => $"{property.DeclaringType!.FullName}.{property.Name}")];

        // Assert
        violations.ShouldBeEmpty();
    }

    [Fact] // ADR-0004
    public void Classes_Should_BeSealed_WhenNotInherited()
    {
        // Arrange
        Type[] types = [.. ProductionAssemblies
            .SelectMany(assembly => assembly.GetTypes().Where(type => !IsGenerated(type, assembly)))
            .Where(type => type.Namespace?.EndsWith(".Migrations", StringComparison.Ordinal) != true)];
        HashSet<Type> baseTypes = [.. types
            .Select(type => type.BaseType)
            .OfType<Type>()
            .Select(type => type.IsGenericType ? type.GetGenericTypeDefinition() : type)];
        Type[] concreteTypes = [.. ConcreteTypes(types)
            .Where(type => !baseTypes.Contains(type))
            .Where(type => !LegacyPublicValidators.Contains(type.Name))];
        concreteTypes.ShouldNotBeEmpty();

        // Act
        string[] violations = [.. concreteTypes.Where(type => !type.IsSealed).Select(type => type.FullName!)];

        // Assert
        violations.ShouldBeEmpty();
    }

    private static readonly Assembly[] ProductionAssemblies =
    [
        SharedKernelAssembly,
        DomainAssembly,
        ApplicationAssembly,
        InfrastructureAssembly,
        PresentationAssembly
    ];

    // Template legacy (.claude/rules/60-security.md): the identity comes from IUserContext, never from the request.
    // Don't add to this list; remove an entry when its slice is migrated.
    private static readonly string[] LegacyUserIdRequests =
    [
        "CreateTodoCommand",
        "CopyTodoCommand",
        "GetTodosQuery",
        "GetUserByIdQuery"
    ];

    // Template legacy (.claude/rules/10-architecture.md). Don't add to this list.
    private static readonly string[] LegacyPublicValidators =
    [
        "CreateTodoCommandValidator",
        "CopyTodoCommandValidator"
    ];

    private static readonly Type[] RequestInterfaces =
    [
        typeof(ICommand),
        typeof(ICommand<>),
        typeof(IQuery<>)
    ];

    private static IEnumerable<Type> Validators() =>
        ConcreteTypes(ApplicationAssembly.GetTypes())
            .Where(type => type.BaseType is { IsGenericType: true } baseType &&
                baseType.GetGenericTypeDefinition() == typeof(AbstractValidator<>));

    private static bool IsRequestInterface(Type type) =>
        RequestInterfaces.Contains(type.IsGenericType ? type.GetGenericTypeDefinition() : type);

    // Compiler and source-generator output (closures, file-local types, the top-level-statements Program)
    // is not ours to seal; generated EF migrations are excluded by namespace.
    private static bool IsGenerated(Type type, Assembly assembly) =>
        type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false) ||
        type.FullName!.Contains('<', StringComparison.Ordinal) ||
        type == assembly.EntryPoint?.DeclaringType;

    // Records get a compiler-generated "<Clone>$" method; their positional properties are init-only by design.
    private static bool IsRecord(Type type) => type.GetMethod("<Clone>$") is not null;

    private static IEnumerable<Type> ConcreteTypes(IEnumerable<Type> types) =>
        types.Where(type => type is { IsClass: true, IsAbstract: false });

    private static bool IsHandlerInterface(Type type) =>
        type.IsGenericType && HandlerInterfaces.Contains(type.GetGenericTypeDefinition());

    private static bool IsInternalAndSealed(Type type) =>
        type.IsSealed && (type.IsNested ? type.IsNestedAssembly : type.IsNotPublic);
}
