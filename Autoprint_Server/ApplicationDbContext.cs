using Microsoft.EntityFrameworkCore;
using Autoprint.Server.Models;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext()
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=Autoprint.db");
        }
    }
    public DbSet<Location> Locations { get; set; }
}