-- =====================================================================================
-- Correctif de base Autoprint : SQLite 1.1.0 (janvier 2026) -> prête pour l'installeur 1.1.2.0
-- Généré le 06/11/2026. À appliquer UNE SEULE FOIS, site arrêté, sur une base 1.1.0 sans
-- historique de migrations (table __EFMigrationsHistory absente).
-- Effet : schéma identique à une base 1.1.2 + les 4 migrations de la 1.1.2 marquées appliquées,
-- pour que « Autoprint.Server.exe --migrate-only » de l'installeur 1.1.2.0 n'ait rien à faire.
-- =====================================================================================
PRAGMA foreign_keys = OFF;
BEGIN TRANSACTION;

-- 1. Réseaux multiples des lieux (migration 20260122171454_TransitionMultiReseau)
CREATE TABLE "EmplacementNetworks" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_EmplacementNetworks" PRIMARY KEY AUTOINCREMENT,
    "EmplacementId" INTEGER NOT NULL,
    "CidrIpv4" TEXT NOT NULL,
    "Description" TEXT NULL,
    "IsPrimary" INTEGER NOT NULL,
    "DateModification" TEXT NOT NULL,
    "ModifiePar" TEXT NULL,
    "EstSupprime" INTEGER NOT NULL,
    CONSTRAINT "FK_EmplacementNetworks_Emplacements_EmplacementId" FOREIGN KEY ("EmplacementId") REFERENCES "Emplacements" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_EmplacementNetworks_EmplacementId" ON "EmplacementNetworks" ("EmplacementId");

-- Le réseau unique de chaque lieu devient son réseau principal (même règle que la migration d'origine)
INSERT INTO "EmplacementNetworks" ("EmplacementId", "CidrIpv4", "Description", "IsPrimary", "DateModification", "EstSupprime")
SELECT "Id", "CidrIpv4", 'Réseau Principal', 1, strftime('%Y-%m-%d %H:%M:%f', 'now'), 0
FROM "Emplacements"
WHERE "CidrIpv4" IS NOT NULL AND "CidrIpv4" <> '' AND "CidrIpv4" <> '0.0.0.0/0';

ALTER TABLE "Emplacements" DROP COLUMN "CidrIpv4";

-- 2. Historique des migrations : les 4 migrations de la 1.1.2 sont déjà reflétées dans le schéma
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES
    ('20251211102108_InitialCreate', '10.0.0'),
    ('20251224143308_AddedDiscoveryModule', '10.0.0'),
    ('20260116155730_AddModifieParColumn', '10.0.0'),
    ('20260122171454_TransitionMultiReseau', '10.0.0');

COMMIT;
PRAGMA foreign_keys = ON;
PRAGMA integrity_check;
