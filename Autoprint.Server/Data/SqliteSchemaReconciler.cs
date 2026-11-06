using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Autoprint.Server.Data
{
    /// <summary>
    /// Remise à niveau générique d'une base SQLite existante à partir du modèle EF.
    /// Les anciennes versions créaient la base avec EnsureCreated, puis tentaient des migrations écrites pour SQL Server
    /// qui échouaient en silence : le schéma réel dépend donc de la version d'origine de la base. Ici, toute table ou
    /// colonne du modèle absente de la base est ajoutée (rien n'est jamais supprimé ni modifié).
    /// </summary>
    public static class SqliteSchemaReconciler
    {
        public static (int Tables, int Columns) Reconcile(ApplicationDbContext context, ILogger? logger)
        {
            int tables = 0, columns = 0;
            var createdTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var connection = context.Database.GetDbConnection();
            bool wasClosed = connection.State != System.Data.ConnectionState.Open;
            if (wasClosed) connection.Open();
            try
            {
                var createScript = SplitCreateScript(context.Database.GenerateCreateScript());

                // 1. Tables et colonnes du modèle absentes de la base
                foreach (var entity in context.Model.GetEntityTypes())
                {
                    var tableName = entity.GetTableName();
                    if (tableName == null || entity.GetViewName() != null) continue;
                    var table = StoreObjectIdentifier.Table(tableName, entity.GetSchema());

                    var existing = GetColumns(connection, tableName);
                    if (existing.Count == 0)
                    {
                        if (createScript.TryGetValue(tableName, out var statements))
                        {
                            foreach (var sql in statements) Execute(connection, sql);
                            logger?.LogWarning("Mise à niveau SQLite : table {Table} créée.", tableName);
                            createdTables.Add(tableName);
                            tables++;
                        }
                        continue;
                    }

                    // Valeurs initiales déclarées dans la classe (ex. SnmpPort = 161) : appliquées aux lignes existantes
                    object? sample = null;
                    try { sample = entity.ClrType.GetConstructor(Type.EmptyTypes) != null ? Activator.CreateInstance(entity.ClrType) : null; } catch { }

                    foreach (var property in entity.GetProperties())
                    {
                        var column = property.GetColumnName(table);
                        if (column == null || existing.Contains(column)) continue;

                        Execute(connection, $"ALTER TABLE \"{tableName}\" ADD COLUMN {ColumnDefinition(property, table, column, sample)};");
                        existing.Add(column);
                        logger?.LogWarning("Mise à niveau SQLite : colonne {Table}.{Column} ajoutée.", tableName, column);
                        columns++;
                    }
                }

                // 2. Données déplacées par les migrations (que les anciennes bases SQLite n'ont jamais reçues)
                MigrateLegacyData(connection, createdTables, logger);

                // 3. Anciennes colonnes obligatoires sans valeur par défaut que le modèle n'écrit plus :
                //    elles feraient échouer toute création de ligne (ex. Emplacements.CidrIpv4)
                DropBlockingObsoleteColumns(context, connection, logger);
            }
            finally
            {
                if (wasClosed) connection.Close();
            }
            return (tables, columns);
        }

        private static void MigrateLegacyData(System.Data.Common.DbConnection connection, HashSet<string> createdTables, ILogger? logger)
        {
            // Transition multi-réseaux (janvier 2026) : le réseau unique du lieu devient son réseau principal
            if (createdTables.Contains("EmplacementNetworks") && GetColumns(connection, "Emplacements").Contains("CidrIpv4"))
            {
                int n = Execute(connection, "INSERT INTO \"EmplacementNetworks\" (\"EmplacementId\", \"CidrIpv4\", \"Description\", \"IsPrimary\", \"DateModification\", \"EstSupprime\") " +
                    "SELECT \"Id\", \"CidrIpv4\", 'Réseau Principal', 1, strftime('%Y-%m-%d %H:%M:%f', 'now'), 0 FROM \"Emplacements\" " +
                    "WHERE \"CidrIpv4\" IS NOT NULL AND \"CidrIpv4\" <> '' AND \"CidrIpv4\" <> '0.0.0.0/0';");
                logger?.LogWarning("Mise à niveau SQLite : {Count} réseau(x) de lieu repris dans EmplacementNetworks.", n);
            }
        }

        private static void DropBlockingObsoleteColumns(ApplicationDbContext context, System.Data.Common.DbConnection connection, ILogger? logger)
        {
            foreach (var group in context.Model.GetEntityTypes().Where(e => e.GetTableName() != null && e.GetViewName() == null).GroupBy(e => e.GetTableName()!))
            {
                var tableName = group.Key;
                var table = StoreObjectIdentifier.Table(tableName, group.First().GetSchema());
                var modelColumns = new HashSet<string>(
                    group.SelectMany(e => e.GetProperties()).Select(p => p.GetColumnName(table)).OfType<string>(),
                    StringComparer.OrdinalIgnoreCase);

                var blocking = new List<string>();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"PRAGMA table_info(\"{tableName}\")";
                    using var reader = cmd.ExecuteReader();
                    // colonnes : cid, name, type, notnull, dflt_value, pk
                    while (reader.Read())
                    {
                        string name = reader.GetString(1);
                        bool notNull = reader.GetInt64(3) == 1;
                        bool hasDefault = !reader.IsDBNull(4);
                        bool pk = reader.GetInt64(5) > 0;
                        if (notNull && !hasDefault && !pk && !modelColumns.Contains(name)) blocking.Add(name);
                    }
                }
                foreach (var column in blocking)
                {
                    try
                    {
                        Execute(connection, $"ALTER TABLE \"{tableName}\" DROP COLUMN \"{column}\";");
                        logger?.LogWarning("Mise à niveau SQLite : ancienne colonne obligatoire {Table}.{Column} supprimée (plus utilisée).", tableName, column);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Mise à niveau SQLite : impossible de supprimer l'ancienne colonne {Table}.{Column} ; la création de lignes dans cette table échouera.", tableName, column);
                    }
                }
            }
        }

        private static string ColumnDefinition(IProperty property, StoreObjectIdentifier table, string column, object? sample)
        {
            string type = property.GetColumnType(table) ?? "TEXT";
            bool nullable = property.IsColumnNullable(table);
            if (nullable) return $"\"{column}\" {type} NULL";

            // SQLite exige une valeur par défaut pour ajouter une colonne NOT NULL à une table existante
            return $"\"{column}\" {type} NOT NULL DEFAULT {DefaultLiteral(property, sample)}";
        }

        private static string DefaultLiteral(IProperty property, object? sample)
        {
            var sqlDefault = property.GetDefaultValueSql();
            if (!string.IsNullOrWhiteSpace(sqlDefault) && !sqlDefault.Contains('(')) return sqlDefault;

            // Valeur par défaut configurée explicitement (HasDefaultValue), sinon valeur initiale déclarée dans la classe
            object? value = null;
            if (property.FindAnnotation(RelationalAnnotationNames.DefaultValue) != null) value = property.GetDefaultValue();
            else if (sample != null && property.PropertyInfo != null)
            {
                try { value = property.PropertyInfo.GetValue(sample); } catch { }
            }
            var converter = property.GetTypeMapping().Converter;
            if (value != null && converter != null) value = converter.ConvertToProvider(value);

            var clr = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            return value switch
            {
                bool b => b ? "1" : "0",
                string s => "'" + s.Replace("'", "''") + "'",
                DateTime d => "'" + d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "'",
                Guid g => "'" + g.ToString().ToUpperInvariant() + "'",
                Enum e => Convert.ToInt64(e, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
                IFormattable n => n.ToString(null, CultureInfo.InvariantCulture),
                // Aucune valeur connue : valeur neutre du type
                _ when clr == typeof(bool) || clr.IsEnum || IsNumeric(clr) => "0",
                _ when clr == typeof(DateTime) || clr == typeof(DateTimeOffset) => "'0001-01-01 00:00:00'",
                _ when clr == typeof(Guid) => "'00000000-0000-0000-0000-000000000000'",
                _ => "''"
            };
        }

        private static bool IsNumeric(Type t) => t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)
            || t == typeof(double) || t == typeof(float) || t == typeof(decimal) || t == typeof(uint) || t == typeof(ulong);

        /// <summary>Script de création EF découpé par table : CREATE TABLE puis ses index.</summary>
        private static Dictionary<string, List<string>> SplitCreateScript(string script)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in Regex.Split(script, @";\s*(?:\r?\n|$)"))
            {
                var sql = raw.Trim();
                if (sql.Length == 0) continue;
                var create = Regex.Match(sql, "^CREATE TABLE \"([^\"]+)\"", RegexOptions.IgnoreCase);
                var index = Regex.Match(sql, "^CREATE (?:UNIQUE )?INDEX \"[^\"]+\" ON \"([^\"]+)\"", RegexOptions.IgnoreCase);
                var table = create.Success ? create.Groups[1].Value : index.Success ? index.Groups[1].Value : null;
                if (table == null) continue; // données initiales (INSERT) : gérées par DbInitializer
                if (!result.TryGetValue(table, out var list)) result[table] = list = new List<string>();
                list.Add(sql + ";");
            }
            return result;
        }

        private static HashSet<string> GetColumns(System.Data.Common.DbConnection connection, string table)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info(\"{table.Replace("\"", "\"\"")}\")";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) columns.Add(reader.GetString(1));
            return columns;
        }

        private static int Execute(System.Data.Common.DbConnection connection, string sql)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            return cmd.ExecuteNonQuery();
        }
    }
}
