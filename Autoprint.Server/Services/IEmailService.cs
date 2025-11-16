namespace Autoprint.Server.Services
{
    public interface IEmailService
    {
        // Méthode existante (Production : utilise la config en BDD)
        Task SendEmailAsync(string to, string subject, string htmlMessage);

        // Nouvelle méthode (Test : utilise les paramètres fournis en arguments)
        Task SendTestEmailAsync(string host, int port, string user, string pass, bool ssl, string from, string to);
    }
}