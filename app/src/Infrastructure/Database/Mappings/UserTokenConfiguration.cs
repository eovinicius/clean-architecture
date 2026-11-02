using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Mappings;

internal sealed class UserTokenConfiguration : IEntityTypeConfiguration<UserToken>
{
    public void Configure(EntityTypeBuilder<UserToken> builder)
    {
        builder.HasKey(userToken => userToken.Id);

        builder.Property(userToken => userToken.TokenHash).HasMaxLength(64);

        builder.HasIndex(userToken => userToken.TokenHash).IsUnique();

        builder.HasIndex(userToken => new { userToken.UserId, userToken.Purpose });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(userToken => userToken.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Maps to PostgreSQL's xmin system column so two concurrent uses of the same token conflict.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();
    }
}
