using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Autoprint.Server.Data
{
    /// <summary>
    /// Création / mise à niveau du schéma au démarrage (backlog B1).
    /// - SQL Server : migrations EF (les colonnes oubliées par certaines migrations sont rattrapées par DbInitializer).
    /// - SQLite : les migrations sont écrites pour SQL Server (nvarchar, datetime2…) et échouent ; le schéma est créé
    ///   directement à partir du modèle, puis mis à niveau par DbInitializer.
    /// </summary>
    public static class DatabaseSchema
    {
        public static void Ensure(ApplicationDbContext context, ILogger? logger = null)
        {
            if (context.Database.IsSqlite())
            {
                context.Database.EnsureCreated();

                // Base créée par une version précédente dont la tentative de migration a échoué :
                // elle ne contient que les tables techniques d'EF et EnsureCreated la considère comme existante.
                if (!SqliteTableExists(context, "ServerSettings"))
                {
                    logger?.LogWarning("Base SQLite incomplète détectée : création des tables à partir du modèle.");
                    context.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS \"__EFMigrationsLock\";");
                    context.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS \"__EFMigrationsHistory\";");
                    context.GetService<IRelationalDatabaseCreator>().CreateTables();
                }
                return;
            }

            try
            {
                context.Database.Migrate();
            }
            catch (Exception ex)
            {
                // Comportement historique conservé (base créée sans migrations), mais l'erreur n'est plus masquée
                logger?.LogError(ex, "Échec des migrations EF : tentative de création du schéma à partir du modèle.");
                context.Database.EnsureCreated();
            }
        }

        private static bool SqliteTableExists(ApplicationDbContext context, string table)
        {
            var connection = context.Database.GetDbConnection();
            bool wasClosed = connection.State != System.Data.ConnectionState.Open;
            if (wasClosed) connection.Open();
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
                var p = cmd.CreateParameter();
                p.ParameterName = "$name";
                p.Value = table;
                cmd.Parameters.Add(p);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
            finally
            {
                if (wasClosed) connection.Close();
            }
        }
    }
}
