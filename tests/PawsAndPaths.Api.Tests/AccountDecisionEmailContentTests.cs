using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Tests;

public class AccountDecisionEmailContentTests
{
    [Fact]
    public void DeclinedEmail_IsPoliteAndExplainsPossibleReasons()
    {
        var message = AccountDecisionEmailContent.CreateDeclined(
            "customer@example.test", "Taylor & <Pup>", "owner@example.test", "Princess Dog Walker");

        Assert.Equal("About your Princess Dog Walker account", message.Subject);
        Assert.Contains("We are genuinely sorry", message.TextBody);
        Assert.DoesNotContain("Demo Owner is genuinely sorry", message.TextBody);
        Assert.Contains("does not currently provide service in your area", message.TextBody);
        Assert.DoesNotContain("suitable match", message.TextBody);
        Assert.Contains("Taylor &amp; &lt;Pup&gt;", message.HtmlBody);
        Assert.DoesNotContain("Taylor & <Pup>", message.HtmlBody);
    }
}
