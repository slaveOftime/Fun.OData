using Microsoft.EntityFrameworkCore;

namespace Db
{
  public class DemoDbContext : DbContext
  {
    public DbSet<Person> Persons { get; set; }
    public DbSet<Role> Roles { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
      base.OnConfiguring(optionsBuilder);
      optionsBuilder.UseSqlite("Data Source=demo.db");
    }
  }
}
