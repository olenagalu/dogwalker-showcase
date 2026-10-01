using System.Net;
using MimeKit;

namespace PawsAndPaths.Api.Services;

public interface IAccountDecisionEmailSender
{
    Task SendDeclinedAsync(string recipientEmail, string recipientName,
        CancellationToken cancellationToken = default);
}

public sealed class AccountDecisionEmailSender(
    IConfiguration configuration,
    ILogger<AccountDecisionEmailSender> logger) : IAccountDecisionEmailSender
{
    public Task SendDeclinedAsync(string recipientEmail, string recipientName,
        CancellationToken cancellationToken = default) =>
        SendAsync(AccountDecisionEmailContent.CreateDeclined, recipientEmail, recipientName, cancellationToken);

    private async Task SendAsync(
        Func<string, string, string, string, MimeMessage> createMessage,
        string recipientEmail,
        string recipientName,
        CancellationToken cancellationToken)
    {
        // Showcase: no network transport or delivery of any kind.
        await Task.CompletedTask;
    }
}

public static class AccountDecisionEmailContent
{
    public static MimeMessage CreateDeclined(string recipientEmail, string recipientName,
        string fromAddress, string fromName)
    {
        var safeName = WebUtility.HtmlEncode(recipientName);
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = "About your Princess Dog Walker account";
        message.Body = new BodyBuilder
        {
            TextBody = $"Hi {recipientName},\n\nThank you so much for reaching out to Princess Dog Walker. We are genuinely sorry, but Princess Dog Walker does not currently provide service in your area, so we’re unable to accept booking requests for this account.\n\nThank you for understanding. We wish you and your pet all the very best.\n\nWarmly,\nDemo Owner\nPrincess Dog Walker\n202-555-0100",
            HtmlBody = $$"""
                <!doctype html>
                <html lang="en"><body style="margin:0;background:#fff7fb;font-family:Arial,sans-serif;color:#43283a">
                  <div style="max-width:600px;margin:0 auto;padding:32px 24px"><div style="background:#fff;border:1px solid #f3cfdf;border-radius:20px;padding:32px">
                    <p style="margin:0 0 8px;color:#b23a72;font-weight:700">PRINCESS DOG WALKER</p>
                    <h1 style="margin:0 0 20px;font-size:26px;color:#7c2852">Hi {{safeName}},</h1>
                    <p style="font-size:16px;line-height:1.6">Thank you so much for reaching out to Princess Dog Walker.</p>
                    <p style="font-size:16px;line-height:1.6">We are genuinely sorry, but Princess Dog Walker does not currently provide service in your area, so we’re unable to accept booking requests for this account.</p>
                    <p style="font-size:16px;line-height:1.6">Thank you for understanding. We wish you and your pet all the very best.</p>
                    <p style="margin:28px 0 0;line-height:1.6">Warmly,<br><strong>Demo Owner</strong><br>Princess Dog Walker<br><a href="tel:202-555-0100" style="color:#b23a72">202-555-0100</a></p>
                  </div></div>
                </body></html>
                """
        }.ToMessageBody();
        return message;
    }
}
