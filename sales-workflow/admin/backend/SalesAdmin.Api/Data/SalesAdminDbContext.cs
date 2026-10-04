using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using SalesAdmin.Api.Entities;

namespace SalesAdmin.Api.Data;

public class SalesAdminDbContext : DbContext
{
    public SalesAdminDbContext(DbContextOptions<SalesAdminDbContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Discount> Discounts => Set<Discount>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<PaymentCondition> PaymentConditions => Set<PaymentCondition>();
    public DbSet<FollowUpRecord> FollowUps => Set<FollowUpRecord>();
    public DbSet<AgentInstruction> AgentInstructions => Set<AgentInstruction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Habilita a extensão pgvector no PostgreSQL
        modelBuilder.HasPostgresExtension("vector");

        // Category
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("categories");
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.Slug).IsUnique();
            entity.Property(c => c.Name).HasMaxLength(100).IsRequired();
            entity.Property(c => c.Slug).HasMaxLength(100).IsRequired();
            entity.Property(c => c.Description).HasMaxLength(500);
        });

        // Product
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.Sku).IsUnique();
            entity.Property(p => p.Sku).HasMaxLength(50).IsRequired();
            entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Price).HasPrecision(18, 2);
            entity.Property(p => p.Currency).HasMaxLength(3);
            entity.Property(p => p.Color).HasMaxLength(50);
            entity.Property(p => p.Size).HasMaxLength(20);
            entity.Property(p => p.Brand).HasMaxLength(100);
            entity.Property(p => p.ImageUrl).HasMaxLength(500);
            entity.Property(p => p.Embedding).HasColumnType("vector(1536)");

            entity.HasOne(p => p.Category)
                  .WithMany(c => c.Products)
                  .HasForeignKey(p => p.CategoryId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(p => p.Discounts)
                  .WithMany(d => d.TargetProducts)
                  .UsingEntity(j => j.ToTable("discount_products"));
        });

        // ProductImage
        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.ToTable("product_images");
            entity.HasKey(pi => pi.Id);
            entity.Property(pi => pi.Url).HasMaxLength(500).IsRequired();
            entity.Property(pi => pi.AltText).HasMaxLength(200);

            entity.HasOne(pi => pi.Product)
                  .WithMany(p => p.Images)
                  .HasForeignKey(pi => pi.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Discount
        modelBuilder.Entity<Discount>(entity =>
        {
            entity.ToTable("discounts");
            entity.HasKey(d => d.Id);
            entity.HasIndex(d => d.Code).IsUnique();
            entity.Property(d => d.Code).HasMaxLength(50).IsRequired();
            entity.Property(d => d.Name).HasMaxLength(200).IsRequired();
            entity.Property(d => d.DiscountType).HasMaxLength(20).IsRequired();
            entity.Property(d => d.DiscountValue).HasPrecision(18, 2);
            entity.Property(d => d.MinOrderValue).HasPrecision(18, 2);
            entity.Property(d => d.MaxDiscountAmount).HasPrecision(18, 2);
            entity.Property(d => d.Embedding).HasColumnType("vector(1536)");
        });

        // Customer
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.Email).IsUnique();
            entity.Property(c => c.Name).HasMaxLength(200).IsRequired();
            entity.Property(c => c.Email).HasMaxLength(200).IsRequired();
            entity.Property(c => c.Phone).HasMaxLength(20);
            entity.Property(c => c.DocumentNumber).HasMaxLength(20);
            entity.Property(c => c.DocumentType).HasMaxLength(10);
            entity.Property(c => c.CustomerType).HasMaxLength(10);
            entity.Property(c => c.Embedding).HasColumnType("vector(1536)");
        });

        // CustomerAddress
        modelBuilder.Entity<CustomerAddress>(entity =>
        {
            entity.ToTable("customer_addresses");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Label).HasMaxLength(50).IsRequired();
            entity.Property(a => a.Street).HasMaxLength(200).IsRequired();
            entity.Property(a => a.Number).HasMaxLength(20).IsRequired();
            entity.Property(a => a.Complement).HasMaxLength(100);
            entity.Property(a => a.Neighborhood).HasMaxLength(100).IsRequired();
            entity.Property(a => a.City).HasMaxLength(100).IsRequired();
            entity.Property(a => a.State).HasMaxLength(2).IsRequired();
            entity.Property(a => a.ZipCode).HasMaxLength(10).IsRequired();
            entity.Property(a => a.Country).HasMaxLength(2).IsRequired();

            entity.HasOne(a => a.Customer)
                  .WithMany(c => c.Addresses)
                  .HasForeignKey(a => a.CustomerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // PaymentCondition
        modelBuilder.Entity<PaymentCondition>(entity =>
        {
            entity.ToTable("payment_conditions");
            entity.HasKey(pc => pc.Id);
            entity.Property(pc => pc.Name).HasMaxLength(100).IsRequired();
            entity.Property(pc => pc.PaymentMethod).HasMaxLength(20).IsRequired();
            entity.Property(pc => pc.InterestRate).HasPrecision(5, 2);
            entity.Property(pc => pc.AdditionalDiscount).HasPrecision(5, 2);
        });

        // FollowUpRecord
        modelBuilder.Entity<FollowUpRecord>(entity =>
        {
            entity.ToTable("follow_ups");
            entity.HasKey(f => f.Id);
            entity.Property(f => f.Module).HasMaxLength(50).IsRequired();
            entity.Property(f => f.FollowUpType).HasMaxLength(50).IsRequired();
            entity.Property(f => f.ReferenceId).HasMaxLength(100);
            entity.Property(f => f.ReferenceTitle).HasMaxLength(200);
            entity.Property(f => f.Channel).HasMaxLength(30).IsRequired();
            entity.Property(f => f.MessageText).IsRequired();
            entity.Property(f => f.Status).HasMaxLength(30).IsRequired();

            entity.HasOne(f => f.Customer)
                  .WithMany(c => c.FollowUps)
                  .HasForeignKey(f => f.CustomerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // AgentInstruction
        modelBuilder.Entity<AgentInstruction>(entity =>
        {
            entity.ToTable("agent_instructions");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.WorkflowType).HasMaxLength(50).IsRequired();
            entity.Property(a => a.AgentRole).HasMaxLength(100).IsRequired();
            entity.Property(a => a.Instructions).IsRequired();
            entity.HasIndex(a => new { a.WorkflowType, a.AgentRole }).IsUnique();
        });
    }
}
