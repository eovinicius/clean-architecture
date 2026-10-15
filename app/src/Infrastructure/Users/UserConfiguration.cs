using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Users;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        // 320 is the maximum length of an email address (RFC 5321).
        builder.Property(u => u.Email).HasMaxLength(320);

        builder.Property(u => u.FirstName).HasMaxLength(100);

        builder.Property(u => u.LastName).HasMaxLength(100);

        builder.HasIndex(u => u.Email).IsUnique();
    }
}
