using Microsoft.EntityFrameworkCore;

public class BotDbContext : DbContext
{
    public DbSet<UserInfo> Users => Set<UserInfo>();
    public DbSet<BotChat> BotChats => Set<BotChat>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=bot.db");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
