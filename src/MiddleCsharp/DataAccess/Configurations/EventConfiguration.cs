using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiddleCsharp.Models;

namespace MiddleCsharp.DataAccess.Configurations;

internal sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("events");

        builder.HasKey(evt => evt.Id);
        builder.Property(evt => evt.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(evt => evt.Title)
            .HasColumnName("title")
            .IsRequired()
            .HasMaxLength(EventConstraints.TitleMaxLength);

        builder.Property(evt => evt.Description)
            .HasColumnName("description")
            .HasMaxLength(EventConstraints.DescriptionMaxLength);

        builder.Property(evt => evt.StartAt)
            .HasColumnName("start_at")
            .IsRequired()
            .HasColumnType("timestamp without time zone");

        builder.Property(evt => evt.EndAt)
            .HasColumnName("end_at")
            .IsRequired()
            .HasColumnType("timestamp without time zone");

        builder.Property(evt => evt.TotalSeats)
            .HasColumnName("total_seats")
            .IsRequired();

        builder.Property(evt => evt.AvailableSeats)
            .HasColumnName("available_seats")
            .IsRequired();

        builder.HasIndex(evt => evt.StartAt);

        builder.HasMany(evt => evt.Bookings)
            .WithOne(booking => booking.Event);
    }
}
