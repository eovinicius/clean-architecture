using SharedKernel;

namespace Application.UnitTests.Abstractions;

public sealed class ResultTests
{
    private static readonly Error NotFound = Error.NotFound("Tests.NotFound", "Not found");

    [Fact]
    public void ImplicitConversion_Should_CreateFailure_WhenErrorIsReturnedAsResult()
    {
        // Act
        Result result = NotFound;

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(NotFound);
    }

    [Fact]
    public void ImplicitConversion_Should_CreateFailure_WhenErrorIsReturnedAsGenericResult()
    {
        // Act
        Result<Guid> result = NotFound;

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(NotFound);
        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void ImplicitConversion_Should_KeepDerivedError_WhenValidationErrorIsReturned()
    {
        // Arrange
        var validationError = new ValidationError([Error.Problem("Name", "Name is required")]);

        // Act
        Result<Guid> result = validationError;

        // Assert
        result.Error.ShouldBeSameAs(validationError);
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void ImplicitConversion_Should_Throw_WhenErrorIsNull()
    {
        // Arrange
        Error? error = null;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => (Result)error!);
        Should.Throw<ArgumentNullException>(() => (Result<Guid>)error!);
    }

    [Fact]
    public void ImplicitConversion_Should_CreateSuccess_WhenValueIsReturned()
    {
        // Arrange
        var value = Guid.NewGuid();

        // Act
        Result<Guid> result = value;

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(value);
    }

    [Fact]
    public void ImplicitConversion_Should_CreateNullValueFailure_WhenValueIsNull()
    {
        // Arrange
        string? value = null;

        // Act
        Result<string> result = value;

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(Error.NullValue);
    }
}
