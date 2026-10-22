using Microsoft.AspNetCore.Mvc;
using TenantSubscriptionApi.Contracts.Requests;
using TenantSubscriptionApi.Contracts.Responses;
using TenantSubscriptionApi.Services.Auth;

namespace TenantSubscriptionApi.Controllers;

// No [Authorize] here — logging in is, by definition, the one thing you can do
// before you have a token.
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var token = await _authService.LoginAsync(request.Email, request.Password);
        return Ok(new LoginResponse(token));
    }
}
