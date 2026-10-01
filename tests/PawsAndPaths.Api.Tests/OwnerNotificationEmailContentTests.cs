using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Tests;

public class OwnerNotificationEmailContentTests
{
    [Fact]
    public async Task Sender_DoesNotFailWhenSmtpIsNotConfigured()
    {
        var configuration = new ConfigurationBuilder().Build();
        var sender = new OwnerNotificationEmailSender(
            configuration, NullLogger<OwnerNotificationEmailSender>.Instance);

        await sender.SendContactMessageAsync(
            new ContactNotification("Sam Taylor", "customer@example.test", "Can Demo Owner help?"));
    }

    [Fact]
    public void ContactMessage_IsSentToOwnerWithCustomerReplyTo()
    {
        var message = OwnerNotificationEmailContent.CreateContactMessage(
            new ContactNotification("Sam Taylor", "customer@example.test", "Can Demo Owner help?"),
            "owner@example.test", "sender@example.test", "Princess Dog Walker");

        Assert.Equal("owner@example.test", message.To.Mailboxes.Single().Address);
        Assert.Equal("customer@example.test", message.ReplyTo.Mailboxes.Single().Address);
        Assert.Contains("Sam Taylor", message.Subject);
        Assert.Contains("Can Demo Owner help?", message.TextBody);
    }

    [Fact]
    public void AccountApprovalRequest_IncludesCustomerAndReviewLink()
    {
        var message = OwnerNotificationEmailContent.CreateAccountApprovalRequest(
            new AccountApprovalNotification(
                "Sam Taylor", "customer@example.test", "202-555-0100", "Demo City", "123 Main St"),
            "owner@example.test", "sender@example.test", "Princess Dog Walker",
            "http://localhost:5095/owner.html#owner-customers");

        Assert.Equal("owner@example.test", message.To.Mailboxes.Single().Address);
        Assert.Equal("customer@example.test", message.ReplyTo.Mailboxes.Single().Address);
        Assert.Contains("Account approval needed", message.Subject);
        Assert.Contains("Demo City", message.TextBody);
        Assert.Contains("123 Main St", message.TextBody);
        Assert.Contains("owner.html#owner-customers", message.TextBody);
    }

    [Fact]
    public void BookingRequest_IncludesBookingAndCustomerDetails()
    {
        var message = OwnerNotificationEmailContent.CreateBookingRequest(
            new BookingNotification(
                "Sam Taylor", "customer@example.test", "202-555-0100", "123 Main St", "Buddy", "Labrador", "Overnight stay",
                new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 4),
                new TimeOnly(22, 0), new TimeOnly(9, 0), 95m, "Needs medication"),
            "owner@example.test", "sender@example.test", "Princess Dog Walker",
            "http://localhost:5095/owner.html#owner-messages");

        Assert.Equal("owner@example.test", message.To.Mailboxes.Single().Address);
        Assert.Equal("customer@example.test", message.ReplyTo.Mailboxes.Single().Address);
        Assert.Contains("Overnight stay", message.Subject);
        Assert.Contains("Buddy", message.TextBody);
        Assert.Contains("Labrador", message.TextBody);
        Assert.Contains("123 Main St", message.TextBody);
        Assert.Contains("October 2, 2026 through October 4, 2026", message.TextBody);
        Assert.Contains("Needs medication", message.TextBody);
        Assert.Contains("owner.html#owner-messages", message.TextBody);
        Assert.Contains("approve or decline", message.HtmlBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DogRegistration_IncludesAddressAndCareDetailsButNoPassword()
    {
        var message = OwnerNotificationEmailContent.CreateDogRegistration(
            new DogRegistrationNotification(
                "Sam Taylor", "customer@example.test", "202-555-0100", "123 Main St",
                "Buddy", "Labrador", 4, "Two cups daily", "Friendly", "Peanut allergy"),
            "owner@example.test", "sender@example.test", "Princess Dog Walker",
            "http://localhost:5095/owner.html#owner-customers");

        Assert.Equal("owner@example.test", message.To.Mailboxes.Single().Address);
        Assert.Equal("customer@example.test", message.ReplyTo.Mailboxes.Single().Address);
        Assert.Contains("Buddy", message.Subject);
        Assert.Contains("123 Main St", message.TextBody);
        Assert.Contains("Two cups daily", message.TextBody);
        Assert.Contains("Peanut allergy", message.TextBody);
        Assert.DoesNotContain("password", message.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("owner.html#owner-customers", message.TextBody);
    }

    [Fact]
    public void HtmlContent_EncodesCustomerProvidedValues()
    {
        var message = OwnerNotificationEmailContent.CreateContactMessage(
            new ContactNotification("<script>alert('hi')</script>", "customer@example.test", "<b>hello</b>"),
            "owner@example.test", "sender@example.test", "Princess Dog Walker");

        Assert.DoesNotContain("<script>", message.HtmlBody);
        Assert.DoesNotContain("<b>hello</b>", message.HtmlBody);
        Assert.Contains("&lt;script&gt;", message.HtmlBody);
        Assert.Contains("&lt;b&gt;hello&lt;/b&gt;", message.HtmlBody);
    }
}
