using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SongStash.Domain.ExternalReferences;
using SongStash.Domain.Finds;
using SongStash.Domain.Songs;
using SongStash.Domain.Tags;

namespace SongStash.Infrastructure.Persistence;

public sealed class SongStashDbContext(DbContextOptions<SongStashDbContext> options) : DbContext(options)
{
    public DbSet<Song> Songs => Set<Song>();
    public DbSet<FindEvent> FindEvents => Set<FindEvent>();
    public DbSet<ExternalReference> ExternalReferences => Set<ExternalReference>();
    public DbSet<Tag> Tags => Set<Tag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var uriConverter = new ValueConverter<Uri?, string?>(
            uri => uri == null ? null : uri.ToString(),
            value => value == null ? null : new Uri(value, UriKind.RelativeOrAbsolute));

        modelBuilder.Entity<Song>(entity =>
        {
            entity.ToTable("Songs");
            entity.HasKey(song => song.Id);
            entity.Property(song => song.Id).ValueGeneratedNever();
            entity.Property(song => song.Title).IsRequired();
            entity.Property(song => song.Artist).IsRequired();
            entity.Property(song => song.Album);
            entity.Property(song => song.ArtworkUrl).HasConversion(uriConverter);
            entity.Property(song => song.Genre);
            entity.Property(song => song.Notes);
            entity.Property(song => song.IsFavorite).IsRequired();
            entity.Property(song => song.CreatedAt).IsRequired();
            entity.Property(song => song.UpdatedAt).IsRequired();
            entity.HasIndex(song => song.Title);
            entity.HasIndex(song => song.Artist);
            entity.HasMany<FindEvent>().WithOne().HasForeignKey(find => find.SongId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany<ExternalReference>().WithOne().HasForeignKey(reference => reference.SongId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FindEvent>(entity =>
        {
            entity.ToTable("FindEvents");
            entity.HasKey(find => find.Id);
            entity.Property(find => find.Id).ValueGeneratedNever();
            entity.Property(find => find.SongId).IsRequired();
            entity.Property(find => find.FoundAt);
            entity.Property(find => find.Source).HasConversion<string>().IsRequired();
            entity.Property(find => find.ImportedAt).IsRequired();
            entity.Property(find => find.SourceItemId);
            entity.HasIndex(find => find.SongId);
            entity.HasIndex(find => find.SourceItemId);
        });

        modelBuilder.Entity<ExternalReference>(entity =>
        {
            entity.ToTable("ExternalReferences");
            entity.HasKey(reference => reference.Id);
            entity.Property(reference => reference.Id).ValueGeneratedNever();
            entity.Property(reference => reference.SongId).IsRequired();
            entity.Property(reference => reference.Provider).HasConversion<string>().IsRequired();
            entity.Property(reference => reference.ExternalId);
            entity.Property(reference => reference.Url).HasConversion(uriConverter);
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.ToTable("Tags");
            entity.HasKey(tag => tag.Id);
            entity.Property(tag => tag.Id).ValueGeneratedNever();
            entity.Property(tag => tag.Name).IsRequired();
            entity.Property(tag => tag.NormalizedName).IsRequired();
            entity.HasIndex(tag => tag.NormalizedName);
        });
    }
}
