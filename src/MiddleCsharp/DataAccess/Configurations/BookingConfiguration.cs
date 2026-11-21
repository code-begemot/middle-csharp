using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiddleCsharp.Models;

namespace MiddleCsharp.DataAccess.Configurations;

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings");

        builder.HasKey(booking => booking.Id);
        builder.Property(booking => booking.Id).ValueGeneratedNever();

        builder.Property(booking => booking.EventId).IsRequired();

        builder.Property(booking => booking.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(booking => booking.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(booking => booking.ProcessedAt)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(booking => new { booking.Status, booking.CreatedAt });

        builder.HasOne(booking => booking.Event)
            .WithMany(evt => evt.Bookings)
            .HasForeignKey(booking => booking.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
