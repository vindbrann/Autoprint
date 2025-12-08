using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Security.Cryptography.X509Certificates;
using WixToolset.Dtf.WindowsInstaller;
using System.Windows.Forms;

namespace Autoprint.Setup.Actions
{
    public class CustomActions
    {
        /// <summary>
        /// Teste la connexion SQL Server avec les paramètres fournis par l'UI WiX.
        /// </summary>
        [CustomAction]
        public static ActionResult TestSqlConnection(Session session)
        {
            // Reset des statuts
            session["DB_VALID"] = "0";
            session["DB_ERROR_MESSAGE"] = "";

            session.Log("AUTOPRINT_LOG: Début du test de connexion SQL...");

            // 1. Récupération des propriétés (variables) saisies dans l'interface WiX
            string server = session["SQL_SERVER"];     // Ex: localhost\SQLEXPRESS
            string user = session["SQL_USER"];         // Ex: sa
            string password = session["SQL_PASSWORD"]; // Ex: Pa$$w0rd

            // "1" = Authentification Windows, "0" ou vide = SQL Auth
            string useIntegrated = session["SQL_USE_INTEGRATED"];

            // 2. Construction de la chaîne de connexion
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
            builder.DataSource = server;
            builder.InitialCatalog = "master"; // On teste sur master car notre BDD n'existe peut-être pas
            builder.ConnectTimeout = 2; // On ne veut pas figer l'installeur 30 secondes

            if (useIntegrated == "1")
            {
                builder.IntegratedSecurity = true;
            }
            else
            {
                builder.IntegratedSecurity = false;
                builder.UserID = user;
                builder.Password = password;
            }

            // 3. Tentative de connexion
            try
            {
                using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
                {
                    session.Log($"AUTOPRINT_LOG: Tentative de connexion à {server}...");
                    connection.Open();

                    // Si ça ne plante pas ici, c'est gagné
                    session.Log("AUTOPRINT_LOG: Connexion réussie !");
                    session["DB_VALID"] = "1"; // Le signal pour WiX de passer à la page suivante
                }
            }
            catch (Exception ex)
            {
                session.Log($"AUTOPRINT_LOG: Échec de connexion. {ex.Message}");
                session["DB_VALID"] = "0";

                // Affichage de la Popup Erreur
                MessageBox.Show(
                    $"La connexion au serveur SQL a échoué.\n\nDétails : {ex.Message}",
                    "Erreur de Connexion",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }

            return ActionResult.Success;
        }
    }
}