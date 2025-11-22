namespace Autoprint.Shared.Enums
{
    public enum PrinterStatus
    {
        // 0 : Tout va bien, synchro OK
        Synchronized = 0,

        // 1 : Importée ou Créée, mais manque le Modèle ou le Lieu (Bloquant)
        ImportedNeedsFix = 1,

        // 2 : Prête (Données OK), attend d'être créée sur Windows
        PendingCreation = 2,

        // 3 : Modifiée dans l'appli, attend d'être mise à jour sur Windows
        PendingUpdate = 3,

        // 4 : Marqué pour suppression sur Windows
        PendingDelete = 4,

        // 99 : Erreur technique
        SyncError = 99
    }
}