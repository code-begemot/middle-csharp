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
        builder.Property(evt => evt.Id).ValueGeneratedNever();

        builder.Property(evt => evt.Title)
            .IsRequired()
            .HasMaxLength(EventConstraints.TitleMaxLength);

        builder.Property(evt => evt.Description)
            .HasMaxLength(EventConstraints.DescriptionMaxLength);

        builder.Property(evt => evt.StartAt)
            .IsRequired()
            .HasColumnType("timestamp without time zone");

        builder.Property(evt => evt.EndAt)
            .IsRequired()
            .HasColumnType("timestamp without time zone");

        builder.Property(evt => evt.TotalSeats).IsRequired();
        builder.Property(evt => evt.AvailableSeats).IsRequired();

        builder.HasIndex(evt => evt.StartAt);

        builder.HasMany(evt => evt.Bookings)
            .WithOne(booking => booking.Event);
    }
}
