using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;

namespace SalsCaterSuite.WebApi.Controllers;

/// <summary>
/// Login endpoint. The controller performs no database access (DEC-037): credential
/// verification goes through the Application's <see cref="IAuthenticationRepository"/>, and
/// JWT creation — which is pure token composition, not persistence — stays here in the
/// WebApi layer.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationRepository _authenticationRepository;
    private readonly IConfiguration _configuration;

    public AuthController(
        IAuthenticationRepository authenticationRepository,
        IConfiguration configuration)
    {
        _authenticationRepository = authenticationRepository;
        _configuration = configuration;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Username and password are required." });
        }

        var attempt = await _authenticationRepository.AuthenticateAsync(request.Username, request.Password);

        if (attempt.Status == AuthenticationStatus.UserUnavailable)
        {
            return Unauthorized(new { message = "Invalid credentials or account is inactive." });
        }

        if (attempt.Status != AuthenticationStatus.Succeeded || attempt.User is null)
        {
            return Unauthorized(new { message = "Invalid credentials." });
        }

        var user = attempt.User;
        var token = GenerateJwtToken(user);

        return Ok(new
        {
            token,
            userId = user.UserId,
            userName = user.UserName,
            fullName = user.FullName,
            roles = user.Roles
        });
    }

    private string GenerateJwtToken(AuthenticatedUser user)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var secretKey = Encoding.UTF8.GetBytes(jwtSection["SecretKey"]!);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId),
            new(ClaimTypes.Name, user.UserName),
            new("fullName", user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(secretKey),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSection["Issuer"],
            audience: jwtSection["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public record LoginRequest(string Username, string Password);
