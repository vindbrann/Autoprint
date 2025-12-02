using Autoprint.Server.Data;
using Autoprint.Shared;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;

namespace Autoprint.Server.Services
{
    public class AuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly JsonSerializerOptions _jsonOptions;

        public AuditService(ApplicationDbContext context)
        {
            _context = context;
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };
        }

        // Méthode standard pour les entités simples (Lieux, Marques, Pilotes)
        public async Task LogUpdateAsync<T>(
            int entityId,
            T newEntity,
            string actionCode,
            string? userName,
            string level = "INFO",
            params string[] includes) where T : class
        {
            IQueryable<T> query = _context.Set<T>().AsNoTracking();

            foreach (var include in includes)
            {
                query = query.Include(include);
            }

            var oldEntity = await query
                .Where(e => EF.Property<int>(e, "Id") == entityId)
                .FirstOrDefaultAsync();

            string oldJson = "{}";
            if (oldEntity != null)
            {
                oldJson = JsonSerializer.Serialize(oldEntity, _jsonOptions);
            }

            string newJson = JsonSerializer.Serialize(newEntity, _jsonOptions);
            string resourceName = GetSafeEntityName(newEntity, entityId);

            AddLog(actionCode, $"Modification de l'entité : {typeof(T).Name} (ID: {entityId})", userName, level, resourceName, oldJson, newJson);
        }

        // NOUVEAU : Méthode flexible pour Users et Roles (Snapshots manuels)
        public void LogCustomAudit(
            string actionCode,
            string details,
            string? userName,
            string resourceName,
            object? oldObj,
            object? newObj,
            string level = "INFO")
        {
            string oldJson = oldObj != null ? JsonSerializer.Serialize(oldObj, _jsonOptions) : "";
            string newJson = newObj != null ? JsonSerializer.Serialize(newObj, _jsonOptions) : "";

            AddLog(actionCode, details, userName, level, resourceName, oldJson, newJson);
        }

        public void LogAction(string actionCode, string details, string? userName, string level = "INFO", string resourceName = "")
        {
            AddLog(actionCode, details, userName, level, resourceName);
        }

        private void AddLog(string action, string details, string? user, string level, string resource, string oldVal = "", string newVal = "")
        {
            _context.AuditLogs.Add(new AuditLog
            {
                Action = action,
                Details = details,
                Utilisateur = user ?? "System",
                Niveau = level,
                DateAction = DateTime.UtcNow,
                ResourceName = resource,
                OldValues = oldVal,
                NewValues = newVal
            });
        }

        private string GetSafeEntityName(object entity, int id)
        {
            try
            {
                var type = entity.GetType();
                var prop = type.GetProperty("Nom") ?? type.GetProperty("NomAffiche");
                if (prop != null)
                {
                    var val = prop.GetValue(entity);
                    if (val != null) return val.ToString()!;
                }
            }
            catch { }
            return id.ToString();
        }
    }
}