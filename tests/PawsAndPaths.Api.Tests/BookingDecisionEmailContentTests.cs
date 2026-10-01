using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Tests;

public class BookingDecisionEmailContentTests
{
    [Fact]
    public void DeclinedEmail_IsPoliteAndExplainsServiceAreaAndContactOptions()
    {
        var message = BookingDecisionEmailContent.CreateDeclined(
            new BookingDeclinedNotification(
                "customer@example.test", "Taylor & <Pup>", "Buddy", "Dog walk",
                new DateOnly(2026, 10, 2)),
            "sender@example.test", "Princess Dog Walker", "owner@example.test", "202-555-0100");

        Assert.Equal("About your Princess Dog Walker booking request", message.Subject);
        Assert.Contains("We are genuinely sorry", message.TextBody);
        Assert.Contains("scheduling conflict", message.TextBody);
        Assert.DoesNotContain("service in your area", message.TextBody);
        Assert.Contains("Please do not reply to this automated email", message.TextBody);
        Assert.Contains("contact Demo Owner directly", message.TextBody);
        Assert.Contains("owner@example.test", message.TextBody);
        Assert.Contains("Taylor &amp; &lt;Pup&gt;", message.HtmlBody);
        Assert.DoesNotContain("Taylor & <Pup>", message.HtmlBody);
    }

    [Fact]
    public void CustomDeclinedEmail_UsesOwnerSubjectAndSafelyEncodesMessage()
    {
        var message = BookingDecisionEmailContent.CreateCustomDeclined(
            new BookingDeclinedNotification(
                "customer@example.test", "Taylor", "Buddy", "Dog walk",
                new DateOnly(2026, 10, 2)),
            "sender@example.test", "Princess Dog Walker", "A personal update",
            "Hi Taylor,\n<script>alert('no')</script> Please call me.");

        Assert.Equal("A personal update", message.Subject);
        Assert.Contains("<script>", message.TextBody);
        Assert.DoesNotContain("<script>", message.HtmlBody);
        Assert.Contains("&lt;script&gt;", message.HtmlBody);
        Assert.Contains("<br>", message.HtmlBody);
    }
}
