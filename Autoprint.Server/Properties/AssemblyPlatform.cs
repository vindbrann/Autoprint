// Le serveur Autoprint ne fonctionne que sous Windows (IIS, spouleur d'impression, Active Directory, DPAPI) :
// déclaré pour tout l'assembly, ce qui lève les avertissements CA1416 sur les services réservés à Windows.
[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows")]
