using Microsoft.EntityFrameworkCore;
using P2PWallet.Models;
using P2PWallet.Services;
using System.Linq;

namespace P2PWallet.Services
{
    public class P2PWalletDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Account> Accounts { get; set; }

        public DbSet<Transaction> Transactions { get; set; }

        public P2PWalletDbContext(DbContextOptions<P2PWalletDbContext> options) : base(options) { }
    }
}
