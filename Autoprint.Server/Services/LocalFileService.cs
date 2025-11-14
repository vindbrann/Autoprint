using System.Security.Cryptography;
using System.IO; // Nécessaire pour Path et FileStream

namespace Autoprint.Server.Services
{
    public class LocalFileService : IFileService
    {
        private readonly IWebHostEnvironment _env;

        public LocalFileService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<(string chemin, string checksum)> SaveFileAsync(IFormFile file)
        {
            // 1. Définir le dossier de stockage
            // Si WebRootPath est null (cas API), on le force vers le dossier wwwroot manuel
            string webRootPath = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");

            // CORRECTION ICI : on utilise bien la variable "webRootPath" définie juste au-dessus
            var uploadFolder = Path.Combine(webRootPath, "drivers");

            // Si le dossier n'existe pas, on le crée
            if (!Directory.Exists(uploadFolder))
                Directory.CreateDirectory(uploadFolder);

            // 2. Générer un nom de fichier unique
            var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            var filePath = Path.Combine(uploadFolder, uniqueFileName);

            // 3. Sauvegarder le fichier
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 4. Calcul du Hash SHA256 pour la sécurité
            string checksum;
            using (var stream = File.OpenRead(filePath))
            {
                using (var sha256 = SHA256.Create())
                {
                    var hashBytes = await sha256.ComputeHashAsync(stream);
                    checksum = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }
            }

            // Retourne le chemin relatif et le hash
            return ($"/drivers/{uniqueFileName}", checksum);
        }
    }
}