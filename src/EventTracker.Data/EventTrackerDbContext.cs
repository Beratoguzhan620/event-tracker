using EventTracker.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventTracker.Data;

public class EventTrackerDbContext(DbContextOptions<EventTrackerDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Event> Events => Set<Event>();

    public DbSet<Attendee> Attendees => Set<Attendee>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(category =>
        {
            category.Property(c => c.Name).HasMaxLength(Limits.CategoryName).IsRequired();
            category.Property(c => c.Slug).HasMaxLength(Limits.CategoryName).IsRequired();
            category.Property(c => c.Icon).HasMaxLength(40).IsRequired();
            category.HasIndex(c => c.Slug).IsUnique();
            category.HasIndex(c => c.Name).IsUnique();
            category.HasData(CategorySeed.All);
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.Property(e => e.Title).HasMaxLength(Limits.Title).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(Limits.Description).IsRequired();
            entity.Property(e => e.Location).HasMaxLength(Limits.Location).IsRequired();
            entity.Property(e => e.CoverImagePath).HasMaxLength(260);
            entity.Property(e => e.SearchText).HasMaxLength(Limits.Title + Limits.Location + Limits.Description + 2).IsRequired();
            entity.HasIndex(e => e.StartsAt);

            entity.HasOne(e => e.Category)
                .WithMany(c => c.Events)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Events_EndsAfterStart", "\"EndsAt\" > \"StartsAt\"");
                table.HasCheckConstraint("CK_Events_Capacity", "\"Capacity\" IS NULL OR \"Capacity\" > 0");
            });
        });

        modelBuilder.Entity<Attendee>(attendee =>
        {
            attendee.Property(a => a.FullName).HasMaxLength(Limits.FullName).IsRequired();
            attendee.Property(a => a.Email).HasMaxLength(Limits.Email).IsRequired();
            attendee.HasIndex(a => new { a.EventId, a.Email }).IsUnique();
            attendee.HasIndex(a => new { a.EventId, a.Status, a.RegisteredAt });

            attendee.HasOne(a => a.Event)
                .WithMany(e => e.Attendees)
                .HasForeignKey(a => a.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

/// <summary>Column sizes shared by the model, the services and the form validation.</summary>
public static class Limits
{
    public const int CategoryName = 40;
    public const int Title = 120;
    public const int Description = 4000;
    public const int Location = 160;
    public const int FullName = 100;
    public const int Email = 254;
}
