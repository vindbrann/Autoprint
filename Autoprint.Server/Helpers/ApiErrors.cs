namespace Autoprint.Server.Helpers
{
    /// <summary>
    /// Messages d'erreur renvoyés à la console sans détail technique interne (audit #15, CWE-209).
    /// Le détail est journalisé côté serveur avec une référence courte que l'utilisateur peut communiquer.
    /// </summary>
    public static class ApiErrors
    {
        public static string LogAndDescribe(HttpContext context, Exception ex, string userMessage)
        {
            var reference = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Autoprint.ApiErrors");
            logger.LogError(ex, "[Réf. {Reference}] {Message} ({Path})", reference, userMessage, context.Request.Path.Value);
            return $"{userMessage} (réf. {reference} : détails dans les journaux du serveur)";
        }
    }
}
