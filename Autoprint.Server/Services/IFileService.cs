namespace Autoprint.Server.Services
{
    public interface IFileService
    {
        // Cette méthode prend un fichier envoyé par le web, le sauvegarde et retourne (Chemin, Checksum)
        Task<(string chemin, string checksum)> SaveFileAsync(IFormFile file);
    }
}