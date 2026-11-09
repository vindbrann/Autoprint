namespace Autoprint.Shared
{
    // Politique de mot de passe des comptes locaux (audit #13) : partagée entre l'API et la console
    public static class PasswordPolicy
    {
        public const int MinLength = 12;
        public const string MinLengthMessage = "Le mot de passe doit faire au moins 12 caractères.";
    }
}
