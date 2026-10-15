using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Users;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(refreshToken => refreshToken.Id);

        builder.Property(refreshToken => refreshToken.TokenHash).HasMaxLength(64);

        builder.HasIndex(refreshToken => refreshToken.TokenHash).IsUnique();

        builder.HasIndex(refreshToken => refreshToken.FamilyId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(refreshToken => refreshToken.UserId);

        // Maps to PostgreSQL's xmin system column so concurrent rotations of the same token conflict.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();
    }
}
