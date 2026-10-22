namespace SalsCaterSuite.UnitTests.Api;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.WebApi.Controllers;
using Xunit;

/// <summary>
/// DEC-037 regression guard for the frozen login contract. The controller now reaches the
/// identity store only through <see cref="IAuthenticationRepository"/>, so the HTTP-visible
/// behaviour can be pinned here with a stub — no database, no Identity, no host:
/// <list type="bullet">
/// <item><c>400 { "message": "Username and password are required." }</c></item>
/// <item><c>401 { "message": "Invalid credentials or account is inactive." }</c> (missing or inactive account)</item>
/// <item><c>401 { "message": "Invalid credentials." }</c> (wrong password)</item>
/// <item><c>200 { token, userId, userName, fullName, roles }</c></item>
/// </list>
/// </summary>
public class AuthControllerTests
{
    private const string SecretKey = "unit-test-signing-key-that-is-long-enough-for-hs256";

    private static AuthController CreateController(
        AuthenticationAttempt attempt,
        StubAuthenticationRepository? repository = null)
    {
        repository ??= new StubAuthenticationRepository { Result = attempt };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = SecretKey,
                ["Jwt:Issuer"] = "SalsCaterSuite.Tests",
                ["Jwt:Audience"] = "SalsCaterSuite.Tests.Client"
            })
            .Build();

        return new AuthController(repository, configuration);
    }

    private static object? ValueOf(IActionResult result) => result switch
    {
        ObjectResult objectResult => objectResult.Value,
        _ => null
    };

    private static string Serialize(object? value) =>
        JsonSerializer.Serialize(value);

    [Theory]
    [InlineData("", "secret")]
    [InlineData("   ", "secret")]
    [InlineData("alice", "")]
    [InlineData("alice", "   ")]
    public async Task Login_WhenUsernameOrPasswordIsBlank_Returns400WithTheFrozenMessage(
        string username,
        string password)
    {
        // Arrange
        var controller = CreateController(AuthenticationAttempt.ForInvalidPassword());

        // Act
        var result = await controller.Login(new LoginRequest(username, password));

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(400);
        Serialize(ValueOf(badRequest)).Should().Be("{\"message\":\"Username and password are required.\"}");
    }

    [Fact]
    public async Task Login_WhenUserIsUnknownOrInactive_Returns401WithTheInactiveAccountMessage()
    {
        // Arrange
        var controller = CreateController(AuthenticationAttempt.ForUnavailableUser());

        // Act
        var result = await controller.Login(new LoginRequest("ghost", "whatever"));

        // Assert
        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorized.StatusCode.Should().Be(401);
        Serialize(ValueOf(unauthorized))
            .Should().Be("{\"message\":\"Invalid credentials or account is inactive.\"}");
    }

    [Fact]
    public async Task Login_WhenPasswordIsWrong_Returns401WithThePlainCredentialsMessage()
    {
        // Arrange
        var controller = CreateController(AuthenticationAttempt.ForInvalidPassword());

        // Act
        var result = await controller.Login(new LoginRequest("alice", "wrong"));

        // Assert
        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorized.StatusCode.Should().Be(401);
        Serialize(ValueOf(unauthorized))
            .Should().Be("{\"message\":\"Invalid credentials.\"}",
                "a wrong password must never be confused with an inactive account");
    }

    [Fact]
    public async Task Login_WhenCredentialsVerify_Returns200WithTokenAndIdentityFields()
    {
        // Arrange
        var user = new AuthenticatedUser(
            UserId: "user-1",
            UserName: "alice",
            FullName: "Reyes, Alice M.",
            IsActive: true,
            Roles: new[] { "Administrator", "Manager" });

        var controller = CreateController(AuthenticationAttempt.ForAuthenticatedUser(user));

        // Act
        var result = await controller.Login(new LoginRequest("alice", "correct"));

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);

        using var body = JsonDocument.Parse(Serialize(ValueOf(ok)));
        var root = body.RootElement;

        root.EnumerateObject().Select(p => p.Name)
            .Should().BeEquivalentTo(new[] { "token", "userId", "userName", "fullName", "roles" });

        root.GetProperty("token").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("userId").GetString().Should().Be("user-1");
        root.GetProperty("userName").GetString().Should().Be("alice");
        root.GetProperty("fullName").GetString().Should().Be("Reyes, Alice M.");
        root.GetProperty("roles").EnumerateArray().Select(r => r.GetString())
            .Should().BeEquivalentTo(new[] { "Administrator", "Manager" });
    }

    [Fact]
    public async Task Login_IssuesAJwtCarryingTheIdentityAndRoleClaimsTheHostValidates()
    {
        // Arrange — Program.cs validates issuer/audience/signature and pins ClaimTypes.Role
        // as the role claim type, so those claims are part of the contract.
        var user = new AuthenticatedUser(
            UserId: "user-1",
            UserName: "alice",
            FullName: "Reyes, Alice M.",
            IsActive: true,
            Roles: new[] { "Administrator" });

        var controller = CreateController(AuthenticationAttempt.ForAuthenticatedUser(user));

        // Act
        var result = await controller.Login(new LoginRequest("alice", "correct"));
        var token = JsonDocument.Parse(Serialize(ValueOf(result))).RootElement.GetProperty("token").GetString();

        // Assert
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Issuer.Should().Be("SalsCaterSuite.Tests");
        jwt.Audiences.Should().ContainSingle().Which.Should().Be("SalsCaterSuite.Tests.Client");
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == "user-1");
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == "alice");
        jwt.Claims.Should().Contain(c => c.Type == "fullName" && c.Value == "Reyes, Alice M.");
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Administrator");
        jwt.ValidTo.Should().BeAfter(DateTime.UtcNow, "the token must not be issued already expired");
    }

    private sealed class StubAuthenticationRepository : IAuthenticationRepository
    {
        public AuthenticationAttempt Result { get; init; } =
            AuthenticationAttempt.ForInvalidPassword();

        public string? LastUserName { get; private set; }

        public Task<AuthenticationAttempt> AuthenticateAsync(
            string userName,
            string password,
            CancellationToken cancellationToken = default)
        {
            LastUserName = userName;
            return Task.FromResult(Result);
        }
    }
}
