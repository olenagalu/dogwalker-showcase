using MimeKit;

namespace PawsAndPaths.Api.Services;

public interface IAssistantInvitationEmailSender
{
    Task SendAsync(string email, string token, CancellationToken ct);
}

public class AssistantInvitationEmailSender(IConfiguration configuration) : IAssistantInvitationEmailSender
{
    public async Task SendAsync(string email, string token, CancellationToken ct)
    {
        // Showcase: no network transport or delivery of any kind.
        await Task.CompletedTask;
    }
}
