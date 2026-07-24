using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TanaHub.Infrastructure.Library;

public sealed class LibraryDbContext(DbContextOptions<LibraryDbContext> options) : DbContext(options)
{
    public DbSet<UserMediaEntryEntity> Entries => Set<UserMediaEntryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var stringListConverter = new ValueConverter<List<string>, string>(
            list => JsonSerializer.Serialize(list, (JsonSerializerOptions?)null),
            json => JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null) ?? new List<string>());

        var stringListComparer = new ValueComparer<List<string>>(
            (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
            list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
            list => list.ToList());

        modelBuilder.Entity<UserMediaEntryEntity>(entity =>
        {
            entity.ToTable("LibraryEntries");
            entity.HasKey(e => e.MediaId);
            entity.Property(e => e.Tags).HasConversion(stringListConverter, stringListComparer);
            entity.Property(e => e.CustomLists).HasConversion(stringListConverter, stringListComparer);
        });
    }
}
