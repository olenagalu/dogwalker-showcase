using System.Net;
using MimeKit;

namespace PawsAndPaths.Api.Services;

public interface IWelcomeEmailSender
{
    Task SendAsync(string recipientEmail, string recipientName, CancellationToken cancellationToken = default);
}

public sealed class WelcomeEmailSender(IConfiguration configuration, ILogger<WelcomeEmailSender> logger)
    : IWelcomeEmailSender
{
    public async Task SendAsync(string recipientEmail, string recipientName,
        CancellationToken cancellationToken = default)
    {
        // Showcase: no network transport or delivery of any kind.
        await Task.CompletedTask;
    }
}

public static class WelcomeEmailContent
{
    public static MimeMessage Create(string recipientEmail, string recipientName,
        string fromAddress, string fromName)
    {
        var safeName = WebUtility.HtmlEncode(recipientName);
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = "Welcome to Princess Dog Walker!";
        message.Body = new BodyBuilder
        {
            TextBody = $"Hi {recipientName},\n\nWelcome to Princess Dog Walker! You can add your dogs and request an available service whenever you’re ready.\n\nWith love and happy tails,\nDemo Owner\nPrincess Dog Walker\n202-555-0100",
            HtmlBody = $$"""
                <!doctype html>
                <html lang="en">
                <body style="margin:0;background:#fff7fb;font-family:Arial,sans-serif;color:#43283a">
                  <div style="max-width:600px;margin:0 auto;padding:32px 24px">
                    <div style="background:#ffffff;border:1px solid #f3cfdf;border-radius:20px;padding:32px">
                      <p style="margin:0 0 8px;color:#b23a72;font-weight:700">PRINCESS DOG WALKER ✨</p>
                      <h1 style="margin:0 0 20px;font-size:28px;color:#7c2852">Welcome, {{safeName}}!</h1>
                      <p style="font-size:16px;line-height:1.6">Your account has been created. You can add your dogs and request an available service whenever you’re ready.</p>
                      <p style="font-size:16px;line-height:1.6">We’re so happy to have you and your pup here. 🐾</p>
                      <p style="margin:28px 0 0;line-height:1.6">With love and happy tails,<br><strong>Demo Owner</strong><br>Princess Dog Walker<br><a href="tel:202-555-0100" style="color:#b23a72">202-555-0100</a></p>
                    </div>
                  </div>
                </body>
                </html>
                """
        }.ToMessageBody();
        return message;
    }
}
