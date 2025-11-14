namespace Autoprint.Server.Services
{
    public interface IEmailService
    {
        // Envoie un email simple (HTML supporté)
        Task SendEmailAsync(string to, string subject, string htmlMessage);
    }
}