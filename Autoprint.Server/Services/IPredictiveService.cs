using System.Collections.Generic;
using Autoprint.Shared;

namespace Autoprint.Server.Services
{
    public interface IPredictiveService
    {
        int? PredictDaysRemaining(List<TonerHistory> history);
    }
}
