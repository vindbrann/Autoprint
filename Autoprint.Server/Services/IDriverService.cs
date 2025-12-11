using Autoprint.Shared;
using Autoprint.Shared.DTOs;

namespace Autoprint.Server.Services
{
    public interface IDriverService
    {
        Task<BatchResult> SynchroniserPilotesAsync();
    }
}