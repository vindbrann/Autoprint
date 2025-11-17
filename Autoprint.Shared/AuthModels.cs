namespace Autoprint.Shared
{
    // Ce que le client envoie
    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    // Ce que le serveur répond
    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty; // Le fameux JWT
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new(); // Pour l'UI (cacher les menus)
    }
}