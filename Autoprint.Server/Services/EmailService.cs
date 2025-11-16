using System.Net;
using System.Net.Mail;
using Autoprint.Server.Data; // Pour accéder à la BDD
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Services
{
    public class EmailService : IEmailService
    {
        // On injecte le Service Provider pour récupérer le DbContext à la demande
        // (C'est une bonne pratique pour éviter les conflits de Scope dans les services Singleton/Scoped)
        private readonly IServiceProvider _serviceProvider;

        public EmailService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        // 1. Méthode de Test (Celle utilisée par la page Paramètres)
        public async Task SendTestEmailAsync(string host, int port, string user, string pass, bool ssl, string from, string to)
        {
            await SendSmtpMail(host, port, user, pass, ssl, from, to, "[Autoprint] Test Config", "<h1>Ceci est un email de test.</h1><p>Configuration valide !</p>");
        }

        // 2. Méthode de Production (Celle utilisée par l'appli plus tard)
        public async Task SendEmailAsync(string to, string subject, string htmlMessage)
        {
            // On crée un scope pour récupérer la config fraîche en BDD
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var settings = await context.ServerSettings.ToListAsync();

                // On récupère les valeurs (avec des valeurs par défaut si vide)
                string host = GetVal(settings, "SmtpHost");
                int port = int.Parse(GetVal(settings, "SmtpPort", "25"));
                string user = GetVal(settings, "SmtpUser");
                string pass = GetVal(settings, "SmtpPass");
                bool ssl = bool.Parse(GetVal(settings, "SmtpEnableSsl", "false"));
                string from = GetVal(settings, "SmtpFromAddress", "noreply@autoprint.local");

                if (string.IsNullOrEmpty(host)) throw new Exception("Serveur SMTP non configuré.");

                await SendSmtpMail(host, port, user, pass, ssl, from, to, subject, htmlMessage);
            }
        }

        // Méthode privée générique pour éviter de dupliquer le code SMTP
        private async Task SendSmtpMail(string host, int port, string user, string pass, bool ssl, string from, string to, string subject, string body)
        {
            using var client = new SmtpClient(host, port);
            client.EnableSsl = ssl;

            // Gestion Auth
            if (!string.IsNullOrEmpty(user))
            {
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(user, pass);
            }

            var message = new MailMessage
            {
                From = new MailAddress(from, "Autoprint"),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(to);

            await client.SendMailAsync(message);
        }

        private string GetVal(List<Autoprint.Shared.ServerSetting> list, string key, string def = "")
            => list.FirstOrDefault(s => s.Key == key)?.Value ?? def;
    }
}