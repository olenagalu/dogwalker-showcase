using System.Net;
using MimeKit;

namespace PawsAndPaths.Api.Services;

public interface IPasswordResetEmailSender
{
    Task SendAsync(string recipientEmail, string recipientName, string verificationCode,
        CancellationToken cancellationToken = default);
}

public sealed class PasswordResetEmailSender(IConfiguration configuration, ILogger<PasswordResetEmailSender> logger)
    : IPasswordResetEmailSender
{
    public async Task SendAsync(string recipientEmail, string recipientName, string verificationCode,
        CancellationToken cancellationToken = default)
    {
        // Showcase: no network transport or delivery of any kind.
        await Task.CompletedTask;
    }
}
