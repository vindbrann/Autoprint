namespace Autoprint.Server.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string htmlMessage);

        Task SendTestEmailAsync(string host, int port, string user, string pass, bool ssl, string from, string to);
    }
}