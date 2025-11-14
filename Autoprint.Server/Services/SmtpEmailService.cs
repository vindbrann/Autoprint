using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Autoprint.Server.Data;

namespace Autoprint.Server.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly ApplicationDbContext _context;

        public SmtpEmailService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task SendEmailAsync(string to, string subject, string htmlMessage)
        {
            // 1. Récupération de la configuration fraiche depuis la BDD
            var settings = await _context.ServerSettings.ToDictionaryAsync(s => s.Key, s => s.Value);

            // Helper pour lire sans crasher si la clé manque
            string GetConfig(string key) => settings.ContainsKey(key) ? settings[key] : "";

            var host = GetConfig("SmtpHost");
            var port = int.Parse(GetConfig("SmtpPort")); // Assure-toi que ce n'est pas vide en BDD
            var user = GetConfig("SmtpUser");
            var pass = GetConfig("SmtpPass");
            var enableSsl = bool.Parse(GetConfig("SmtpEnableSsl"));
            var fromAddr = GetConfig("SmtpFromAddress");
            var displayName = GetConfig("SmtpDisplayName");

            // 2. Construction du client SMTP
            using (var client = new SmtpClient(host, port))
            {
                client.EnableSsl = enableSsl;

                // Gestion de l'authentification
                if (bool.Parse(GetConfig("SmtpAuthRequired")))
                {
                    client.Credentials = new NetworkCredential(user, pass);
                }

                // 3. Création du message
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromAddr, displayName),
                    Subject = subject,
                    Body = htmlMessage,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(to);

                // 4. Envoi
                await client.SendMailAsync(mailMessage);
            }
        }
    }
}