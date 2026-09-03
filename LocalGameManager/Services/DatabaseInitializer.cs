using Microsoft.EntityFrameworkCore;

namespace LocalGameManager.Services;

public sealed class DatabaseInitializer(AppPaths paths)
{
    public void Initialize()
    {
        paths.EnsureDirectories();
        using var context = new LibraryDbContext(paths);
        context.Database.EnsureCreated();
        EnsureGamesColumns(context);
        EnsureEnumTextStorage(context);
        EnsureStoreGamesTable(context);
        EnsureStoreGameMediaTable(context);
    }

    private static void EnsureStoreGamesTable(LibraryDbContext context)
    {
        var connection = context.Database.GetDbConnection(); connection.Open();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS "StoreGames" ("Id" INTEGER NOT NULL CONSTRAINT "PK_StoreGames" PRIMARY KEY AUTOINCREMENT, "StoreId" TEXT COLLATE NOCASE NOT NULL, "OriginalName" TEXT NOT NULL, "StoreUrl" TEXT NOT NULL, "CoverUrl" TEXT NULL, "CoverContent" BLOB NULL, "IsFavorite" INTEGER NOT NULL, "ClubName" TEXT NULL, "Authors" TEXT NULL, "Illustrators" TEXT NULL, "VoiceActors" TEXT NULL, "Description" TEXT NULL, "ReleaseDate" TEXT NULL, "WorkForms" TEXT NULL, "AddedAtUtc" TEXT NOT NULL, "DetailsFetchedAtUtc" TEXT NULL);
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_StoreGames_StoreId" ON "StoreGames" ("StoreId");
                """;
            command.ExecuteNonQuery();
            using var columns = connection.CreateCommand();
            columns.CommandText = "PRAGMA table_info(\"StoreGames\");";
            using var reader = columns.ExecuteReader();
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read()) existing.Add(reader.GetString(1));
            reader.Close();
            foreach (var (name, type) in new[] { ("TranslatedName", "TEXT NULL"), ("CategoryTags", "TEXT NULL"), ("FileSize", "TEXT NULL"), ("SupportedLanguages", "TEXT NULL"), ("AgeRating", "TEXT NULL"), ("SalesCount", "TEXT NULL"), ("StoreRating", "TEXT NULL"), ("IsAnimated", "INTEGER NOT NULL DEFAULT 0"), ("HasVoice", "INTEGER NOT NULL DEFAULT 0") })
            {
                if (existing.Contains(name)) continue;
                using var add = connection.CreateCommand(); add.CommandText = $"ALTER TABLE \"StoreGames\" ADD COLUMN \"{name}\" {type};"; add.ExecuteNonQuery();
            }
        }
        finally { connection.Close(); }
    }

    private static void EnsureStoreGameMediaTable(LibraryDbContext context)
    {
        var connection = context.Database.GetDbConnection(); connection.Open();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS "StoreGameMedia" ("Id" INTEGER NOT NULL CONSTRAINT "PK_StoreGameMedia" PRIMARY KEY AUTOINCREMENT, "StoreGameId" INTEGER NOT NULL, "SortOrder" INTEGER NOT NULL, "Content" BLOB NOT NULL, CONSTRAINT "FK_StoreGameMedia_StoreGames_StoreGameId" FOREIGN KEY ("StoreGameId") REFERENCES "StoreGames" ("Id") ON DELETE CASCADE);
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_StoreGameMedia_StoreGameId_SortOrder" ON "StoreGameMedia" ("StoreGameId", "SortOrder");
                """;
            command.ExecuteNonQuery();
        }
        finally { connection.Close(); }
    }

    private static void EnsureGamesColumns(LibraryDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        connection.Open();
        try
        {
            using var readColumns = connection.CreateCommand();
            readColumns.CommandText = "PRAGMA table_info(\"Games\");";
            using var reader = readColumns.ExecuteReader();
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
            {
                columns.Add(reader.GetString(1));
            }
            reader.Close();
            foreach (var (name, sqlType) in new[] { ("VersionNumber", "TEXT NULL"), ("ClubName", "TEXT NULL"), ("Authors", "TEXT NULL"), ("Illustrators", "TEXT NULL"), ("VoiceActors", "TEXT NULL"), ("MetadataUpdatedAtUtc", "TEXT NULL") })
            {
                if (columns.Contains(name)) continue;
                using var addColumn = connection.CreateCommand();
                addColumn.CommandText = $"ALTER TABLE \"Games\" ADD COLUMN \"{name}\" {sqlType};";
                addColumn.ExecuteNonQuery();
            }
        }
        finally
        {
            connection.Close();
        }
    }

    private static void EnsureEnumTextStorage(LibraryDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        connection.Open();
        try
        {
            var tagType = GetColumnType(connection, "Tags", "Type");
            var mediaKind = GetColumnType(connection, "GameMedia", "Kind");
            if (string.Equals(tagType, "TEXT", StringComparison.OrdinalIgnoreCase) && string.Equals(mediaKind, "TEXT", StringComparison.OrdinalIgnoreCase)) return;
            using (var foreignKeys = connection.CreateCommand()) { foreignKeys.CommandText = "PRAGMA foreign_keys = OFF;"; foreignKeys.ExecuteNonQuery(); }
            using var migrate = connection.CreateCommand();
            migrate.CommandText = """
                BEGIN;
                CREATE TABLE IF NOT EXISTS "Tags_new" ("Id" INTEGER NOT NULL CONSTRAINT "PK_Tags" PRIMARY KEY AUTOINCREMENT, "Name" TEXT COLLATE NOCASE NOT NULL, "Type" TEXT NOT NULL);
                INSERT INTO "Tags_new" ("Id", "Name", "Type") SELECT "Id", "Name", CASE "Type" WHEN 0 THEN 'Club' WHEN 1 THEN 'Author' WHEN 2 THEN 'WorkForm' WHEN 3 THEN 'Preference' WHEN 4 THEN 'Item' WHEN 5 THEN 'Character' WHEN 6 THEN 'Clothing' WHEN 7 THEN 'Plot' WHEN 8 THEN 'Gameplay' WHEN 9 THEN 'Appearance' WHEN 10 THEN 'Grotesque' ELSE "Type" END FROM "Tags";
                DROP TABLE "Tags";
                ALTER TABLE "Tags_new" RENAME TO "Tags";
                CREATE UNIQUE INDEX "IX_Tags_Type_Name" ON "Tags" ("Type", "Name");
                CREATE TABLE IF NOT EXISTS "GameMedia_new" ("Id" INTEGER NOT NULL CONSTRAINT "PK_GameMedia" PRIMARY KEY AUTOINCREMENT, "GameId" INTEGER NOT NULL, "Kind" TEXT NOT NULL, "SortOrder" INTEGER NOT NULL, "MimeType" TEXT NOT NULL, "Width" INTEGER NOT NULL, "Height" INTEGER NOT NULL, "Content" BLOB NOT NULL, "ThumbnailContent" BLOB NULL, CONSTRAINT "FK_GameMedia_Games_GameId" FOREIGN KEY ("GameId") REFERENCES "Games" ("Id") ON DELETE CASCADE);
                INSERT INTO "GameMedia_new" ("Id", "GameId", "Kind", "SortOrder", "MimeType", "Width", "Height", "Content", "ThumbnailContent") SELECT "Id", "GameId", CASE "Kind" WHEN 0 THEN 'Cover' WHEN 1 THEN 'Screenshot' WHEN 2 THEN 'Icon' ELSE "Kind" END, "SortOrder", "MimeType", "Width", "Height", "Content", "ThumbnailContent" FROM "GameMedia";
                DROP TABLE "GameMedia";
                ALTER TABLE "GameMedia_new" RENAME TO "GameMedia";
                CREATE UNIQUE INDEX "IX_GameMedia_GameId_Kind" ON "GameMedia" ("GameId", "Kind") WHERE "Kind" IN ('Cover', 'Icon');
                COMMIT;
                """;
            migrate.ExecuteNonQuery();
            using var reenable = connection.CreateCommand(); reenable.CommandText = "PRAGMA foreign_keys = ON;"; reenable.ExecuteNonQuery();
        }
        finally { connection.Close(); }
    }

    private static string? GetColumnType(System.Data.Common.DbConnection connection, string table, string column)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\");";
        using var reader = command.ExecuteReader();
        while (reader.Read()) if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return reader.GetString(2);
        return null;
    }
}
