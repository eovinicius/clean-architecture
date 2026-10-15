using Application.Abstractions.Messaging;
using FluentValidation;
using Shouldly;
using Web.Api.Endpoints;

namespace ArchitectureTests.Conventions;

public class ConventionTests : BaseTest
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

    private static IEnumerable<Type> ConcreteTypes(IEnumerable<Type> types) =>
        types.Where(type => type is { IsClass: true, IsAbstract: false });

    private static bool IsHandlerInterface(Type type) =>
        type.IsGenericType && HandlerInterfaces.Contains(type.GetGenericTypeDefinition());

    private static bool IsInternalAndSealed(Type type) =>
        type.IsSealed && (type.IsNested ? type.IsNestedAssembly : type.IsNotPublic);
}
