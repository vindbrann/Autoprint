using System.ComponentModel.DataAnnotations;
using Autoprint.Shared.Enums;

namespace Autoprint.Shared.DTOs
{
    public class AdRoleMappingDto
    {
        public int Id { get; set; }
        public string AdIdentifier { get; set; } = string.Empty;
        public AdMappingType MappingType { get; set; }
        public int RoleId { get; set; }
    }

    public class CreateAdMappingDto
    {
        [Required]
        public string AdIdentifier { get; set; } = string.Empty;

        [Required]
        public AdMappingType MappingType { get; set; }

        [Required]
        public int RoleId { get; set; }
    }
    public class AdSearchResultDto
    {
        public string Name { get; set; } = string.Empty;
        public string SamAccountName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public AdMappingType Type { get; set; }
        public string? DistinguishedName { get; set; }
        /// <summary>Filtre LDAP prêt à l'emploi (DN échappé, RFC 4515) pour le filtre de connexion AD.</summary>
        public string? MemberOfFilter { get; set; }
    }
}