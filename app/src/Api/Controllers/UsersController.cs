using System.Net.Mime;
using Api.Controllers.Users;
using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Users;
using Application.Users.ConfirmEmail;
using Application.Users.GetById;
using Application.Users.GetCurrent;
using Application.Users.Login;
using Application.Users.Refresh;
using Application.Users.Register;
using Application.Users.RequestPasswordReset;
using Application.Users.ResendEmailConfirmation;
using Application.Users.ResetPassword;
using Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SharedKernel;

namespace Api.Controllers;

[ApiController]
[Route("users")]
[Tags(Tags.Users)]
[Authorize]
public sealed class UsersController : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicies.Authentication)]
    [EndpointSummary("Register a new user")]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, MediaTypeNames.Application.ProblemJson)]
    public async Task<IResult> Register(
        RegisterRequest request,
        [FromServices] ICommandHandler<RegisterUserCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var command = new RegisterUserCommand(
            request.Email,
            request.FirstName,
            request.LastName,
            request.Password);

        Result<Guid> result = await handler.Handle(command, cancellationToken);

        return result.Match(id => Results.Created($"users/{id}", id), CustomResults.Problem);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicies.Authentication)]
    [EndpointSummary("Log in and get an access and refresh token")]
    [ProducesResponseType<AccessTokensResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, MediaTypeNames.Application.ProblemJson)]
    public async Task<IResult> Login(
        LoginRequest request,
        [FromServices] ICommandHandler<LoginUserCommand, AccessTokensResponse> handler,
        CancellationToken cancellationToken)
    {
        var command = new LoginUserCommand(request.Email, request.Password);

        Result<AccessTokensResponse> result = await handler.Handle(command, cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicies.Authentication)]
    [EndpointSummary("Exchange a refresh token for a new token pair")]
    [ProducesResponseType<AccessTokensResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    public async Task<IResult> RefreshToken(
        RefreshTokenRequest request,
        [FromServices] ICommandHandler<RefreshTokenCommand, AccessTokensResponse> handler,
        CancellationToken cancellationToken)
    {
        var command = new RefreshTokenCommand(request.RefreshToken);

        Result<AccessTokensResponse> result = await handler.Handle(command, cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [HttpPost("confirm-email")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicies.Authentication)]
    [EndpointSummary("Confirm the email address with the token sent by email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    public async Task<IResult> ConfirmEmail(
        ConfirmEmailRequest request,
        [FromServices] ICommandHandler<ConfirmEmailCommand> handler,
        CancellationToken cancellationToken)
    {
        var command = new ConfirmEmailCommand(request.Token);

        Result result = await handler.Handle(command, cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [HttpPost("resend-email-confirmation")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicies.Authentication)]
    [EndpointSummary("Send a new email confirmation token")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    public async Task<IResult> ResendEmailConfirmation(
        ResendEmailConfirmationRequest request,
        [FromServices] ICommandHandler<ResendEmailConfirmationCommand> handler,
        CancellationToken cancellationToken)
    {
        var command = new ResendEmailConfirmationCommand(request.Email);

        Result result = await handler.Handle(command, cancellationToken);

        return result.Match(() => Results.Accepted(), CustomResults.Problem);
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicies.Authentication)]
    [EndpointSummary("Send a password reset token by email")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    public async Task<IResult> ForgotPassword(
        ForgotPasswordRequest request,
        [FromServices] ICommandHandler<RequestPasswordResetCommand> handler,
        CancellationToken cancellationToken)
    {
        var command = new RequestPasswordResetCommand(request.Email);

        Result result = await handler.Handle(command, cancellationToken);

        return result.Match(() => Results.Accepted(), CustomResults.Problem);
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicies.Authentication)]
    [EndpointSummary("Choose a new password with the token sent by email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    public async Task<IResult> ResetPassword(
        ResetPasswordRequest request,
        [FromServices] ICommandHandler<ResetPasswordCommand> handler,
        CancellationToken cancellationToken)
    {
        var command = new ResetPasswordCommand(request.Token, request.NewPassword);

        Result result = await handler.Handle(command, cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [HttpGet("me")]
    [EndpointSummary("Get the current user")]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<IResult> GetCurrent(
        [FromServices] IQueryHandler<GetCurrentUserQuery, CurrentUserResponse> handler,
        CancellationToken cancellationToken)
    {
        Result<CurrentUserResponse> result = await handler.Handle(new GetCurrentUserQuery(), cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [HttpGet("{userId:guid}")]
    [HasPermission(Permissions.UsersAccess)]
    [EndpointSummary("Get a user by id")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<IResult> GetById(
        Guid userId,
        [FromServices] IQueryHandler<GetUserByIdQuery, UserResponse> handler,
        CancellationToken cancellationToken)
    {
        var query = new GetUserByIdQuery(userId);

        Result<UserResponse> result = await handler.Handle(query, cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }
}
