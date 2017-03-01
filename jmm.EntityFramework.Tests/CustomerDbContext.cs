using System.Data.Entity;

namespace jmm.EntityFramework.Tests
{
    public class CustomerDbContext : DbContext
    {
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Customer> Customers { get; set; }
    }
}