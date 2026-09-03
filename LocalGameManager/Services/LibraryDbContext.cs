using LocalGameManager.Models;
using Microsoft.EntityFrameworkCore;

namespace LocalGameManager.Services;

public sealed class LibraryDbContext(AppPaths paths) : DbContext
{
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<GameTag> GameTags => Set<GameTag>();
    public DbSet<Dlc> Dlcs => Set<Dlc>();
    public DbSet<GameMedia> GameMedia => Set<GameMedia>();
    public DbSet<StoreGame> StoreGames => Set<StoreGame>();
    public DbSet<StoreGameMedia> StoreGameMedia => Set<StoreGameMedia>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseSqlite($"Data Source={paths.DatabasePath}");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasIndex(game => game.NormalizedGamePath).IsUnique();
            entity.Property(game => game.Rating).HasDefaultValue(0);
            entity.Property(game => game.FileSizeBytes).HasDefaultValue(0L);
            entity.Property(game => game.TotalPlaySeconds).HasDefaultValue(0L);
            entity.Property(game => game.LaunchCount).HasDefaultValue(0);
        });
        modelBuilder.Entity<Tag>(entity => { entity.Property(tag => tag.Name).UseCollation("NOCASE"); entity.Property(tag => tag.Type).HasConversion<string>(); entity.HasIndex(tag => new { tag.Type, tag.Name }).IsUnique(); });
        modelBuilder.Entity<GameTag>(entity =>
        {
            entity.HasKey(link => new { link.GameId, link.TagId });
            entity.HasOne(link => link.Game).WithMany(game => game.GameTags).HasForeignKey(link => link.GameId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(link => link.Tag).WithMany(tag => tag.GameTags).HasForeignKey(link => link.TagId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Dlc>(entity =>
        {
            entity.Property(dlc => dlc.Name).UseCollation("NOCASE");
            entity.HasIndex(dlc => new { dlc.GameId, dlc.Name }).IsUnique();
            entity.HasOne(dlc => dlc.Game).WithMany(game => game.Dlcs).HasForeignKey(dlc => dlc.GameId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<GameMedia>(entity =>
        {
            entity.Property(media => media.Kind).HasConversion<string>();
            entity.HasIndex(media => new { media.GameId, media.Kind }).IsUnique().HasFilter("\"Kind\" IN ('Cover', 'Icon')");
            entity.HasOne(media => media.Game).WithMany(game => game.Media).HasForeignKey(media => media.GameId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<StoreGame>(entity => { entity.HasIndex(game => game.StoreId).IsUnique(); entity.Property(game => game.StoreId).UseCollation("NOCASE"); });
        modelBuilder.Entity<StoreGameMedia>(entity =>
        {
            entity.HasOne(media => media.StoreGame).WithMany(game => game.Screenshots).HasForeignKey(media => media.StoreGameId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(media => new { media.StoreGameId, media.SortOrder }).IsUnique();
        });
    }
}
