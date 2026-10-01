using System.Globalization;
using System.Net;
using MimeKit;

namespace PawsAndPaths.Api.Services;

public record ContactNotification(string Name, string Email, string Message);

public record AccountApprovalNotification(
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    string ServiceArea,
    string ServiceAddress);

public record BookingNotification(
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    string ServiceAddress,
    string DogName,
    string DogBreed,
    string ServiceName,
    DateOnly Date,
    DateOnly? EndDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    decimal Price,
    string SpecialInstructions);

public record DogRegistrationNotification(
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    string ServiceAddress,
    string DogName,
    string Breed,
    int? Age,
    string CareInstructions,
    string BehavioralNotes,
    string MedicalNotes);

public interface IOwnerNotificationEmailSender
{
    Task SendAccountApprovalRequestAsync(AccountApprovalNotification notification,
        CancellationToken cancellationToken = default);
    Task SendContactMessageAsync(ContactNotification notification, CancellationToken cancellationToken = default);
    Task SendBookingRequestAsync(BookingNotification notification, CancellationToken cancellationToken = default);
    Task SendDogRegistrationAsync(DogRegistrationNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed class OwnerNotificationEmailSender(
    IConfiguration configuration,
    ILogger<OwnerNotificationEmailSender> logger) : IOwnerNotificationEmailSender
{
    public Task SendAccountApprovalRequestAsync(
        AccountApprovalNotification notification, CancellationToken cancellationToken = default) =>
        SendAsync(OwnerNotificationEmailContent.CreateAccountApprovalRequest(
            notification,
            OwnerEmail,
            FromAddress,
            configuration["Email:FromName"] ?? "Princess Dog Walker",
            $"{(configuration["PublicBaseUrl"] ?? "http://localhost:5095").TrimEnd('/')}/owner.html#owner-customers"), cancellationToken);

    public Task SendContactMessageAsync(
        ContactNotification notification, CancellationToken cancellationToken = default) =>
        SendAsync(OwnerNotificationEmailContent.CreateContactMessage(
            notification,
            OwnerEmail,
            FromAddress,
            configuration["Email:FromName"] ?? "Princess Dog Walker"), cancellationToken);

    public Task SendBookingRequestAsync(
        BookingNotification notification, CancellationToken cancellationToken = default) =>
        SendAsync(OwnerNotificationEmailContent.CreateBookingRequest(
            notification,
            OwnerEmail,
            FromAddress,
            configuration["Email:FromName"] ?? "Princess Dog Walker",
            $"{(configuration["PublicBaseUrl"] ?? "http://localhost:5095").TrimEnd('/')}/owner.html#owner-messages"), cancellationToken);

    public Task SendDogRegistrationAsync(
        DogRegistrationNotification notification, CancellationToken cancellationToken = default) =>
        SendAsync(OwnerNotificationEmailContent.CreateDogRegistration(
            notification,
            OwnerEmail,
            FromAddress,
            configuration["Email:FromName"] ?? "Princess Dog Walker",
            $"{(configuration["PublicBaseUrl"] ?? "http://localhost:5095").TrimEnd('/')}/owner.html#owner-customers"), cancellationToken);

    private string OwnerEmail => configuration["Owner:Email"] ?? "owner@example.test";
    private string FromAddress => configuration["Email:FromAddress"]
        ?? configuration["Email:SmtpUsername"]
        ?? OwnerEmail;

    private async Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        // Showcase: no network transport or delivery of any kind.
        await Task.CompletedTask;
    }
}

public static class OwnerNotificationEmailContent
{
    public static MimeMessage CreateAccountApprovalRequest(
        AccountApprovalNotification notification, string ownerEmail, string fromAddress, string fromName,
        string? ownerDashboardUrl = null)
    {
        var safe = new
        {
            Name = WebUtility.HtmlEncode(notification.CustomerName),
            Email = WebUtility.HtmlEncode(notification.CustomerEmail),
            Phone = WebUtility.HtmlEncode(notification.CustomerPhone),
            Area = WebUtility.HtmlEncode(notification.ServiceArea),
            Address = WebUtility.HtmlEncode(notification.ServiceAddress)
        };
        var message = CreateBase(ownerEmail, fromAddress, fromName, notification.CustomerEmail,
            $"Account approval needed: {notification.CustomerName}");
        var dashboardText = string.IsNullOrWhiteSpace(ownerDashboardUrl)
            ? string.Empty
            : $"\n\nReview this account: {ownerDashboardUrl}";
        var dashboardHtml = string.IsNullOrWhiteSpace(ownerDashboardUrl)
            ? string.Empty
            : $"<p><a href=\"{WebUtility.HtmlEncode(ownerDashboardUrl)}\">Open the owner dashboard to review this account</a></p>";
        message.Body = new BodyBuilder
        {
            TextBody = $"A customer account is waiting for approval.\n\nName: {notification.CustomerName}\nEmail: {notification.CustomerEmail}\nPhone: {notification.CustomerPhone}\nService area: {notification.ServiceArea}\nService address: {notification.ServiceAddress}{dashboardText}",
            HtmlBody = $$"""
                <h1>Account approval needed</h1>
                <p><strong>Name:</strong> {{safe.Name}}<br>
                <strong>Email:</strong> {{safe.Email}}<br>
                <strong>Phone:</strong> {{safe.Phone}}<br>
                <strong>Service area:</strong> {{safe.Area}}<br>
                <strong>Service address:</strong> {{safe.Address}}</p>
                {{dashboardHtml}}
                """
        }.ToMessageBody();
        return message;
    }

    public static MimeMessage CreateContactMessage(
        ContactNotification notification, string ownerEmail, string fromAddress, string fromName)
    {
        var safeName = WebUtility.HtmlEncode(notification.Name);
        var safeEmail = WebUtility.HtmlEncode(notification.Email);
        var safeMessage = WebUtility.HtmlEncode(notification.Message).Replace("\n", "<br>");
        var message = CreateBase(ownerEmail, fromAddress, fromName, notification.Email,
            $"New website message from {notification.Name}");
        message.Body = new BodyBuilder
        {
            TextBody = $"New message from the Princess Dog Walker website\n\nName: {notification.Name}\nEmail: {notification.Email}\n\nMessage:\n{notification.Message}",
            HtmlBody = $$"""
                <h1>New website message</h1>
                <p><strong>From:</strong> {{safeName}}<br><strong>Email:</strong> {{safeEmail}}</p>
                <p><strong>Message:</strong><br>{{safeMessage}}</p>
                """
        }.ToMessageBody();
        return message;
    }

    public static MimeMessage CreateBookingRequest(
        BookingNotification notification, string ownerEmail, string fromAddress, string fromName,
        string? ownerDashboardUrl = null)
    {
        var dateText = notification.EndDate is null
            ? notification.Date.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)
            : $"{notification.Date:MMMM d, yyyy} through {notification.EndDate:MMMM d, yyyy}";
        var timeText = notification.EndDate is null
            ? $"{notification.StartTime:h:mm tt}–{notification.EndTime:h:mm tt}"
            : $"Check-in {notification.StartTime:h:mm tt}; checkout {notification.EndTime:h:mm tt}";
        var notes = string.IsNullOrWhiteSpace(notification.SpecialInstructions)
            ? "None provided"
            : notification.SpecialInstructions;
        var breed = string.IsNullOrWhiteSpace(notification.DogBreed)
            ? "Not provided"
            : notification.DogBreed;
        var safe = new
        {
            Customer = WebUtility.HtmlEncode(notification.CustomerName),
            Email = WebUtility.HtmlEncode(notification.CustomerEmail),
            Phone = WebUtility.HtmlEncode(notification.CustomerPhone),
            Address = WebUtility.HtmlEncode(notification.ServiceAddress),
            Dog = WebUtility.HtmlEncode(notification.DogName),
            Breed = WebUtility.HtmlEncode(breed),
            Service = WebUtility.HtmlEncode(notification.ServiceName),
            Date = WebUtility.HtmlEncode(dateText),
            Time = WebUtility.HtmlEncode(timeText),
            Notes = WebUtility.HtmlEncode(notes).Replace("\n", "<br>")
        };
        var message = CreateBase(ownerEmail, fromAddress, fromName, notification.CustomerEmail,
            $"New booking request: {notification.ServiceName} for {notification.DogName}");
        var dashboardText = string.IsNullOrWhiteSpace(ownerDashboardUrl)
            ? string.Empty
            : $"\n\nApprove or decline this request: {ownerDashboardUrl}";
        var dashboardHtml = string.IsNullOrWhiteSpace(ownerDashboardUrl)
            ? string.Empty
            : $"<p><a href=\"{WebUtility.HtmlEncode(ownerDashboardUrl)}\">Open the owner dashboard to approve or decline</a></p>";
        message.Body = new BodyBuilder
        {
            TextBody = $"New booking request\n\nCustomer: {notification.CustomerName}\nEmail: {notification.CustomerEmail}\nPhone: {notification.CustomerPhone}\nService address: {notification.ServiceAddress}\nDog: {notification.DogName}\nBreed: {breed}\nService: {notification.ServiceName}\nDate: {dateText}\nTime: {timeText}\nPrice: {notification.Price:C}\nStatus: Pending\nSpecial instructions: {notes}{dashboardText}",
            HtmlBody = $$"""
                <h1>New booking request</h1>
                <p><strong>Customer:</strong> {{safe.Customer}}<br>
                <strong>Email:</strong> {{safe.Email}}<br>
                <strong>Phone:</strong> {{safe.Phone}}<br>
                <strong>Service address:</strong> {{safe.Address}}</p>
                <p><strong>Dog:</strong> {{safe.Dog}}<br>
                <strong>Breed:</strong> {{safe.Breed}}<br>
                <strong>Service:</strong> {{safe.Service}}<br>
                <strong>Date:</strong> {{safe.Date}}<br>
                <strong>Time:</strong> {{safe.Time}}<br>
                <strong>Price:</strong> {{notification.Price:C}}<br>
                <strong>Status:</strong> Pending</p>
                <p><strong>Special instructions:</strong><br>{{safe.Notes}}</p>
                {{dashboardHtml}}
                """
        }.ToMessageBody();
        return message;
    }

    public static MimeMessage CreateDogRegistration(
        DogRegistrationNotification notification, string ownerEmail, string fromAddress, string fromName,
        string? ownerDashboardUrl = null)
    {
        static string Value(string value) => string.IsNullOrWhiteSpace(value) ? "Not provided" : value;
        var age = notification.Age?.ToString(CultureInfo.InvariantCulture) ?? "Not provided";
        var safe = new
        {
            Customer = WebUtility.HtmlEncode(notification.CustomerName),
            Email = WebUtility.HtmlEncode(notification.CustomerEmail),
            Phone = WebUtility.HtmlEncode(notification.CustomerPhone),
            Address = WebUtility.HtmlEncode(notification.ServiceAddress),
            Dog = WebUtility.HtmlEncode(notification.DogName),
            Breed = WebUtility.HtmlEncode(Value(notification.Breed)),
            Age = WebUtility.HtmlEncode(age),
            Care = WebUtility.HtmlEncode(Value(notification.CareInstructions)).Replace("\n", "<br>"),
            Behavior = WebUtility.HtmlEncode(Value(notification.BehavioralNotes)).Replace("\n", "<br>"),
            Medical = WebUtility.HtmlEncode(Value(notification.MedicalNotes)).Replace("\n", "<br>")
        };
        var message = CreateBase(ownerEmail, fromAddress, fromName, notification.CustomerEmail,
            $"New dog registered: {notification.DogName} for {notification.CustomerName}");
        var dashboardText = string.IsNullOrWhiteSpace(ownerDashboardUrl) ? string.Empty : $"\n\nView customer: {ownerDashboardUrl}";
        var dashboardHtml = string.IsNullOrWhiteSpace(ownerDashboardUrl) ? string.Empty
            : $"<p><a href=\"{WebUtility.HtmlEncode(ownerDashboardUrl)}\">Open the owner dashboard to view this customer</a></p>";
        message.Body = new BodyBuilder
        {
            TextBody = $"A customer registered a dog.\n\nCustomer: {notification.CustomerName}\nEmail: {notification.CustomerEmail}\nPhone: {notification.CustomerPhone}\nService address: {notification.ServiceAddress}\nDog: {notification.DogName}\nBreed: {Value(notification.Breed)}\nAge: {age}\nCare instructions: {Value(notification.CareInstructions)}\nBehavioral notes: {Value(notification.BehavioralNotes)}\nMedical / allergy information: {Value(notification.MedicalNotes)}{dashboardText}",
            HtmlBody = $$"""
                <h1>New dog registered</h1>
                <p><strong>Customer:</strong> {{safe.Customer}}<br>
                <strong>Email:</strong> {{safe.Email}}<br>
                <strong>Phone:</strong> {{safe.Phone}}<br>
                <strong>Service address:</strong> {{safe.Address}}</p>
                <p><strong>Dog:</strong> {{safe.Dog}}<br>
                <strong>Breed:</strong> {{safe.Breed}}<br>
                <strong>Age:</strong> {{safe.Age}}</p>
                <p><strong>Care instructions:</strong><br>{{safe.Care}}</p>
                <p><strong>Behavioral notes:</strong><br>{{safe.Behavior}}</p>
                <p><strong>Medical / allergy information:</strong><br>{{safe.Medical}}</p>
                {{dashboardHtml}}
                """
        }.ToMessageBody();
        return message;
    }

    private static MimeMessage CreateBase(
        string ownerEmail, string fromAddress, string fromName, string replyTo, string subject)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(MailboxAddress.Parse(ownerEmail));
        message.ReplyTo.Add(MailboxAddress.Parse(replyTo));
        message.Subject = subject;
        return message;
    }
}
