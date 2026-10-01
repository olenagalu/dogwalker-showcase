using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.Services;

// Query on every authenticated request; JWT role/status claims alone cannot revoke access.
public sealed class AccountTokenEvents(UserManager<AppUser> users) : JwtBearerEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var user = await users.GetUserAsync(context.Principal!);
        if (user is null || user.AssistantStatus == AssistantAccountStatus.Frozen)
            context.Fail("This account is unavailable.");
    }
}
