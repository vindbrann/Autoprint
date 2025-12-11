using Autoprint.Shared;

namespace Autoprint.Server.Services
{
    public interface INamingService
    {
        string GenererNom(Imprimante imp, string template);
        Task<string> GenererNomAsync(Imprimante imp);
    }
}