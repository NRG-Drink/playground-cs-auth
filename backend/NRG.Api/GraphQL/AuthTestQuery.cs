using HotChocolate.Authorization;
using System.Security.Claims;

namespace NRG.Api.GraphQL;

[QueryType]
public partial class AuthTestQuery
{
    public record HelloData(string Hello, DateTime Timestamp);

    [AllowAnonymous]
    public async Task<HelloData> HelloWorld()
    {
        return new HelloData("Hello World!", DateTime.UtcNow);
    }

    [Authorize]
    public async Task<HelloData> HelloUser(ClaimsPrincipal claimsPrincipal)
    {
        var userId = claimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        return new HelloData($"Hello user {userId}!", DateTime.UtcNow);
    }

    [Authorize(Roles = ["Admin"])]
    public async Task<HelloData> HelloAdmin(ClaimsPrincipal claimsPrincipal)
    {
        var userId = claimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        return new HelloData($"Hello admin {userId}!", DateTime.UtcNow);
    }

    [Authorize(Roles = ["Staff"])]
    public async Task<HelloData> HelloStaff(ClaimsPrincipal claimsPrincipal)
    {
        var userId = claimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        return new HelloData($"Hello staff {userId}!", DateTime.UtcNow);
    }

    [Authorize(Roles = ["Staff", "Admin"])]
    public async Task<HelloData> HelloEmployee(ClaimsPrincipal claimsPrincipal)
    {
        var userId = claimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        return new HelloData($"Hello employee {userId}!", DateTime.UtcNow);
    }
}
