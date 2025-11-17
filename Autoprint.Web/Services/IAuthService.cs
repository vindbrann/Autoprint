using Autoprint.Shared;

namespace Autoprint.Web.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> Login(LoginRequest loginRequest);
        Task Logout();
    }
}