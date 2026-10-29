using Microsoft.EntityFrameworkCore;
using ToDoList.Core;
using ToDoList.Data.Entities;

namespace ToDoList.Data;

public sealed class TodoDbContext(DbContextOptions<TodoDbContext> options) : DbContext(options)
{
  public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();
  public DbSet<TaskEntity> Tasks => Set<TaskEntity>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    var categories = modelBuilder.Entity<CategoryEntity>();
    categories.ToTable("Categories");
    categories.HasKey(c => c.Id);
    categories.Property(c => c.Id).ValueGeneratedNever();
    categories.Property(c => c.Name).IsRequired().HasMaxLength(TodoRules.MaxNameLength);
    categories.HasIndex(c => c.Name).IsUnique();

    var tasks = modelBuilder.Entity<TaskEntity>();
    tasks.ToTable("Tasks");
    tasks.HasKey(t => t.Id);
    tasks.Property(t => t.Id).ValueGeneratedNever();
    tasks.Property(t => t.Title).IsRequired().HasMaxLength(TodoRules.MaxNameLength);
    tasks.HasOne<CategoryEntity>().WithMany().HasForeignKey(t => t.CategoryId).OnDelete(DeleteBehavior.Restrict);
    tasks.HasOne<TaskEntity>().WithMany().HasForeignKey(t => t.ParentId).OnDelete(DeleteBehavior.Restrict);
    tasks.HasIndex(t => new { t.ParentId, t.Position });
  }
}
