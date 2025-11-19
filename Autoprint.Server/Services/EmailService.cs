using System.Net;
using System.Net.Mail;
using Autoprint.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Services
{
    public class EmailService : IEmailService
    {
        private readonly IServiceProvider _serviceProvider;

        public EmailService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task SendTestEmailAsync(string host, int port, string user, string pass, bool ssl, string from, string to)
        {
            // Validation basique pour éviter les crashs bêtes
            if (string.IsNullOrEmpty(host)) throw new Exception("Le champ Serveur SMTP est vide.");
            if (string.IsNullOrEmpty(from)) throw new Exception("Le champ Expéditeur est vide.");
            if (string.IsNullOrEmpty(to)) throw new Exception("Le champ Destinataire est vide.");

            await SendSmtpMail(host, port, user, pass, ssl, from, to,
                "[Autoprint] Test de configuration",
                "<h3>Configuration réussie !</h3><p>Le serveur Autoprint communique bien avec votre serveur de messagerie.</p>");
        }

        public async Task SendEmailAsync(string to, string subject, string htmlMessage)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var settings = await context.ServerSettings.ToListAsync();

                string host = GetVal(settings, "SmtpHost");
                // Si pas de config, on ne fait rien (pas d'erreur)
                if (string.IsNullOrEmpty(host)) return;

                int port = int.Parse(GetVal(settings, "SmtpPort", "25"));
                string user = GetVal(settings, "SmtpUser");
                string pass = GetVal(settings, "SmtpPass");
                bool ssl = bool.Parse(GetVal(settings, "SmtpEnableSsl", "false"));
                string from = GetVal(settings, "SmtpFromAddress", "noreply@autoprint.local");

                await SendSmtpMail(host, port, user, pass, ssl, from, to, subject, htmlMessage);
            }
        }

        private async Task SendSmtpMail(string host, int port, string user, string pass, bool ssl, string from, string to, string subject, string body)
        {
            // On utilise une instanciation classique pour maximiser la compatibilité
            using (var client = new SmtpClient())
            {
                client.Host = host;
                client.Port = port;
                client.EnableSsl = ssl;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.Timeout = 30000; 

                // Configuration de l'authentification
                client.UseDefaultCredentials = false;

                if (!string.IsNullOrEmpty(user))
                {
                    // Cas avec Login/Mot de passe
                    client.Credentials = new NetworkCredential(user, pass);
                }
                else
                {
                    // Cas Anonyme (Important pour ton port 25)
                    client.Credentials = null;
                }

                var message = new MailMessage
                {
                    From = new MailAddress(from, "Autoprint"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };
                message.To.Add(to);

                try
                {
                    await client.SendMailAsync(message);
                }
                catch (SmtpException smtpEx)
                {
                    // C'est ICI qu'on récupère la vraie info
                    string realError = smtpEx.Message;

                    // Si une erreur interne existe (ex: "Connection refused"), on la prend
                    if (smtpEx.InnerException != null)
                    {
                        realError += " | DÉTAIL: " + smtpEx.InnerException.Message;
                    }

                    throw new Exception($"Erreur SMTP : {realError}");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Erreur générale : {ex.Message}");
                }
            }
        }

        private string GetVal(List<Autoprint.Shared.ServerSetting> list, string key, string def = "")
            => list.FirstOrDefault(s => s.Key == key)?.Value ?? def;
    }
}