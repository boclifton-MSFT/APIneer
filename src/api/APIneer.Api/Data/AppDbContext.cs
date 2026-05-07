using APIneer.Api.Models;
using Microsoft.EntityFrameworkCore;
using Environment = APIneer.Api.Models.Environment;

namespace APIneer.Api.Data;

/// <summary>
/// EF Core database context for the APIneer API.
/// Backed by SQLite by default; the connection string is configured in <c>Program.cs</c>.
/// </summary>
public class AppDbContext : DbContext
{
  public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
  {
  }

  public DbSet<Workspace> Workspaces => Set<Workspace>();
  public DbSet<Collection> Collections => Set<Collection>();
  public DbSet<CollectionFolder> CollectionFolders => Set<CollectionFolder>();
  public DbSet<ApiRequest> ApiRequests => Set<ApiRequest>();
  public DbSet<RequestHistory> RequestHistory => Set<RequestHistory>();
  public DbSet<Assertion> Assertions => Set<Assertion>();
  public DbSet<Environment> Environments => Set<Environment>();
  public DbSet<EnvironmentVariable> EnvironmentVariables => Set<EnvironmentVariable>();
  public DbSet<McpServerConfig> McpServerConfigs => Set<McpServerConfig>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    // ─── Workspace ──────────────────────────────────────────
    modelBuilder.Entity<Workspace>(b =>
    {
      b.HasKey(w => w.Id);
      b.Property(w => w.Name).IsRequired().HasMaxLength(200);
      b.HasIndex(w => w.Name);

      b.HasMany(w => w.Collections)
              .WithOne(c => c.Workspace)
              .HasForeignKey(c => c.WorkspaceId)
              .OnDelete(DeleteBehavior.Cascade);

      b.HasMany(w => w.Environments)
              .WithOne(e => e.Workspace)
              .HasForeignKey(e => e.WorkspaceId)
              .OnDelete(DeleteBehavior.Cascade);
    });

    // ─── Collection ─────────────────────────────────────────
    modelBuilder.Entity<Collection>(b =>
    {
      b.HasKey(c => c.Id);
      b.Property(c => c.Name).IsRequired().HasMaxLength(200);
      b.HasIndex(c => c.WorkspaceId);
      b.HasIndex(c => c.UpdatedAt);

      b.HasMany(c => c.Folders)
              .WithOne(f => f.Collection)
              .HasForeignKey(f => f.CollectionId)
              .OnDelete(DeleteBehavior.Cascade);

      b.HasMany(c => c.Requests)
              .WithOne(r => r.Collection)
              .HasForeignKey(r => r.CollectionId)
              .OnDelete(DeleteBehavior.Cascade);
    });

    // ─── CollectionFolder (self-referencing tree) ───────────
    modelBuilder.Entity<CollectionFolder>(b =>
    {
      b.HasKey(f => f.Id);
      b.Property(f => f.Name).IsRequired().HasMaxLength(200);
      b.HasIndex(f => f.CollectionId);
      b.HasIndex(f => f.ParentFolderId);

      b.HasOne(f => f.ParentFolder)
              .WithMany(f => f.SubFolders)
              .HasForeignKey(f => f.ParentFolderId)
              .OnDelete(DeleteBehavior.Restrict);

      b.HasMany(f => f.Requests)
              .WithOne(r => r.Folder)
              .HasForeignKey(r => r.FolderId)
              .OnDelete(DeleteBehavior.SetNull);
    });

    // ─── ApiRequest ─────────────────────────────────────────
    modelBuilder.Entity<ApiRequest>(b =>
    {
      b.HasKey(r => r.Id);
      b.Property(r => r.Name).IsRequired().HasMaxLength(300);
      b.Property(r => r.Method).IsRequired().HasMaxLength(16);
      b.Property(r => r.Url).IsRequired();
      b.HasIndex(r => r.CollectionId);
      b.HasIndex(r => r.FolderId);
      b.HasIndex(r => r.UpdatedAt);

      b.HasMany(r => r.History)
              .WithOne(h => h.Request)
              .HasForeignKey(h => h.RequestId)
              .OnDelete(DeleteBehavior.Cascade);

      b.HasMany(r => r.Assertions)
              .WithOne(a => a.Request)
              .HasForeignKey(a => a.RequestId)
              .OnDelete(DeleteBehavior.Cascade);
    });

    // ─── RequestHistory ─────────────────────────────────────
    modelBuilder.Entity<RequestHistory>(b =>
    {
      b.HasKey(h => h.Id);
      b.Property(h => h.Method).IsRequired().HasMaxLength(16);
      b.Property(h => h.Url).IsRequired();
      b.HasIndex(h => h.RequestId);
      b.HasIndex(h => h.ExecutedAt);
    });

    // ─── Assertion ──────────────────────────────────────────
    modelBuilder.Entity<Assertion>(b =>
    {
      b.HasKey(a => a.Id);
      b.Property(a => a.Type).IsRequired().HasMaxLength(64);
      b.Property(a => a.Expected).IsRequired();
      b.HasIndex(a => a.RequestId);
    });

    // ─── Environment ────────────────────────────────────────
    modelBuilder.Entity<Environment>(b =>
    {
      b.HasKey(e => e.Id);
      b.Property(e => e.Name).IsRequired().HasMaxLength(200);
      b.HasIndex(e => e.WorkspaceId);

      b.HasMany(e => e.Variables)
              .WithOne(v => v.Environment)
              .HasForeignKey(v => v.EnvironmentId)
              .OnDelete(DeleteBehavior.Cascade);
    });

    // ─── EnvironmentVariable ────────────────────────────────
    modelBuilder.Entity<EnvironmentVariable>(b =>
    {
      b.HasKey(v => v.Id);
      b.Property(v => v.Key).IsRequired().HasMaxLength(200);
      b.Property(v => v.Value).IsRequired();
      b.HasIndex(v => new { v.EnvironmentId, v.Key }).IsUnique();
    });

    // ─── McpServerConfig ────────────────────────────────────
    modelBuilder.Entity<McpServerConfig>(b =>
    {
      b.HasKey(c => c.Id);
      b.Property(c => c.Name).IsRequired().HasMaxLength(200);
      b.Property(c => c.TransportType).IsRequired().HasMaxLength(32);
      b.HasIndex(c => c.Name).IsUnique();
    });
  }
}
