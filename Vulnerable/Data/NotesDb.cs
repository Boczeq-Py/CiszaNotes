using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PrivateNotes;

public sealed class NotesDb(DbContextOptions<NotesDb> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Note> Notes => Set<Note>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Note>(n =>
        {
            n.Property(x => x.Title).HasMaxLength(160).IsRequired();
            n.Property(x => x.Content).HasMaxLength(20000).IsRequired();
            n.HasIndex(x => new { x.OwnerId, x.UpdatedAt });
            n.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
