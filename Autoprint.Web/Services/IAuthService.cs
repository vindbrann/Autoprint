using Autoprint.Shared;
using Autoprint.Shared.DTOs;

namespace Autoprint.Web.Services
{
    public interface IAuthService
    {
        Task<LoginResponse?> Login(LoginRequest request);

        Task Logout();
    }
}