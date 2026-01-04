using Microsoft.EntityFrameworkCore;

namespace Api.Db
{
    public class ApplicationContext(IConfiguration configuration) : DbContext
    {
        public DbSet<File> Files { get; set; }
        private readonly string _connexionString = configuration.GetConnectionString("database")!;
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(_connexionString);
        }

    }
}
