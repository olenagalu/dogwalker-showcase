using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Tests;

public class WelcomeEmailContentTests
{
    [Fact]
    public async Task Sender_DoesNotFailRegistrationWhenSmtpIsNotConfigured()
    {
        var configuration = new ConfigurationBuilder().Build();
        var sender = new WelcomeEmailSender(configuration, NullLogger<WelcomeEmailSender>.Instance);

        await sender.SendAsync("customer@example.test", "Sam Taylor");
    }

    [Fact]
    public void Create_IncludesRecipientAndBusinessDetails()
    {
        var message = WelcomeEmailContent.Create(
            "customer@example.test", "Sam Taylor", "owner@example.test", "Princess Dog Walker");

        Assert.Equal("Welcome to Princess Dog Walker!", message.Subject);
        Assert.Equal("customer@example.test", message.To.Mailboxes.Single().Address);
        Assert.Contains("Sam Taylor", message.HtmlBody);
        Assert.Contains("202-555-0100", message.TextBody);
        Assert.Contains("request an available service", message.TextBody);
        Assert.DoesNotContain("review your service area", message.TextBody);
    }

    [Fact]
    public void Create_HtmlEncodesCustomerName()
    {
        var message = WelcomeEmailContent.Create(
            "customer@example.test", "<script>alert('hi')</script>",
            "owner@example.test", "Princess Dog Walker");

        Assert.DoesNotContain("<script>", message.HtmlBody);
        Assert.Contains("&lt;script&gt;", message.HtmlBody);
    }
}
