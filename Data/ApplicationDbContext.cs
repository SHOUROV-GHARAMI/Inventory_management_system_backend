using Microsoft.EntityFrameworkCore;
using InventoryManagement.API.Models;

namespace InventoryManagement.API.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Inventory> Inventories { get; set; }
    public DbSet<CustomField> CustomFields { get; set; }
    public DbSet<Item> Items { get; set; }
    public DbSet<ItemFieldValue> ItemFieldValues { get; set; }
    public DbSet<UserInventoryAccess> UserInventoryAccesses { get; set; }
    public DbSet<Comment> Comments { get; set; }
    public DbSet<ItemLike> ItemLikes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User entity configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => new { e.Provider, e.ProviderId });
        });

        // Inventory entity configuration
        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.HasOne(i => i.Owner)
                .WithMany(u => u.OwnedInventories)
                .HasForeignKey(i => i.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.CreatedAt);
        });

        // CustomField entity configuration
        modelBuilder.Entity<CustomField>(entity =>
        {
            entity.HasOne(cf => cf.Inventory)
                .WithMany(i => i.CustomFields)
                .HasForeignKey(cf => cf.InventoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Item entity configuration
        modelBuilder.Entity<Item>(entity =>
        {
            entity.HasOne(i => i.Inventory)
                .WithMany(inv => inv.Items)
                .HasForeignKey(i => i.InventoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(i => i.CreatedByUser)
                .WithMany()
                .HasForeignKey(i => i.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Composite unique index for inventory_id + custom_id
            entity.HasIndex(e => new { e.InventoryId, e.CustomId }).IsUnique();
        });

        // ItemFieldValue entity configuration
        modelBuilder.Entity<ItemFieldValue>(entity =>
        {
            entity.HasOne(ifv => ifv.Item)
                .WithMany(i => i.FieldValues)
                .HasForeignKey(ifv => ifv.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ifv => ifv.CustomField)
                .WithMany(cf => cf.ItemFieldValues)
                .HasForeignKey(ifv => ifv.CustomFieldId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.NumberValue).HasPrecision(18, 4);
        });

        // UserInventoryAccess entity configuration
        modelBuilder.Entity<UserInventoryAccess>(entity =>
        {
            entity.HasOne(uia => uia.User)
                .WithMany(u => u.InventoryAccesses)
                .HasForeignKey(uia => uia.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(uia => uia.Inventory)
                .WithMany(i => i.UserAccesses)
                .HasForeignKey(uia => uia.InventoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique constraint: one user can only have one access entry per inventory
            entity.HasIndex(e => new { e.UserId, e.InventoryId }).IsUnique();
        });

        // Comment entity configuration
        modelBuilder.Entity<Comment>(entity =>
        {
            entity.HasOne(c => c.Inventory)
                .WithMany(i => i.Comments)
                .HasForeignKey(c => c.InventoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.User)
                .WithMany(u => u.Comments)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.CreatedAt);
        });

        // ItemLike entity configuration
        modelBuilder.Entity<ItemLike>(entity =>
        {
            entity.HasOne(il => il.Item)
                .WithMany(i => i.Likes)
                .HasForeignKey(il => il.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(il => il.User)
                .WithMany(u => u.ItemLikes)
                .HasForeignKey(il => il.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Unique constraint: one user can only like an item once
            entity.HasIndex(e => new { e.ItemId, e.UserId }).IsUnique();
        });
    }
}
