using System.ComponentModel.DataAnnotations;

namespace Autoprint.Shared
{
    public class SystemError : BaseEntity
    {
        public string Source { get; set; } = "";
        public string Message { get; set; } = "";
        public string StackTrace { get; set; } = "";
        public DateTime DateOccured { get; set; } = DateTime.UtcNow;
    }
}