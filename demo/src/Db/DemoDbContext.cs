using Microsoft.EntityFrameworkCore;

namespace Db
{
  public class DemoDbContext : DbContext
  {
    public DemoDbContext(DbContextOptions<DemoDbContext> options) : base(options) { }

    public DbSet<Person> Persons { get; set; }
    public DbSet<Role> Roles { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
      base.OnConfiguring(optionsBuilder);
      optionsBuilder.UseSqlite("Data Source=demo.db");
    }
  }
}
