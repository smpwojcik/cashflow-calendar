using Microsoft.EntityFrameworkCore;

namespace CashFlowCalendar.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Txn> Txns => Set<Txn>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Account>()
            .Property(a => a.CurrentBalance)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Txn>()
            .Property(t => t.Amount)
            .HasPrecision(18, 2);
    }
}

public class Account
{
    public int Id { get; set; }
    public string Name { get; set; } = "Primary";

    public decimal CurrentBalance { get; set; }
    public DateOnly BalanceAsOfDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
}

public class Txn
{
    public int Id { get; set; }

    public int AccountId { get; set; }
    public Account? Account { get; set; }

    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string? Payee { get; set; }
    public string? Notes { get; set; }
}
