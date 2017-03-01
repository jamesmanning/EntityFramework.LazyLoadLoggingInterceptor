using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using Xunit;

namespace jmm.EntityFramework.Tests
{
    public class LazyLoadLoggingInterceptorTests
    {
        [Fact]
        public void TestMethod1()
        {
            // Act
            var customer = new Customer()
            {
                Name = "SomeCustomerName",
                Invoices = new List<Invoice>()
                {
                    new Invoice()
                    {
                        Number = "SomeInvoiceNumber",
                    }
                },
            };
            using (var db = new CustomerDbContext())
            {
                db.Customers.Add(customer);
                db.SaveChanges();

                var customers = db.Customers.ToList();
                Assert.NotNull(customers);
                Assert.Equal(1, customers.Count);
                Assert.Equal("SomeCustomerName", customers[0].Name);

                // lazy load via navigation entity collection
                Assert.NotNull(customers[0].Invoices);
                Assert.Equal(1, customers[0].Invoices.Count);
                Assert.Equal(1, customers[0].Invoices.Single().CustomerId);
                Assert.Equal(1, customers[0].Invoices.Single().InvoiceId);
                Assert.Equal("SomeInvoiceNumber", customers[0].Invoices.Single().Number);

                var invoices = db.Invoices.ToList();
                Assert.NotNull(invoices);
                Assert.Equal(1, invoices.Count);
                Assert.Equal("SomeInvoiceNumber", invoices[0].Number);

                // lazy load via navigation entity reference
                Assert.Equal(1, invoices[0].InvoiceId);
                Assert.Equal(1, invoices[0].CustomerId);
                Assert.NotNull(invoices[0].Customer);
                Assert.Equal(1, invoices[0].Customer.CustomerId);
                Assert.Equal("SomeCustomerName", invoices[0].Customer.Name);
            }
        }
    }
    public class CustomerDbContext : DbContext
    {
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Customer> Customers { get; set; }
    }
    public class Customer
    {
        public int CustomerId { get; set; }
        public string Name { get; set; }
        public virtual ICollection<Invoice> Invoices { get; set; }
    }
    public class Invoice
    {
        public int InvoiceId { get; set; }
        public string Number { get; set; }

        public int CustomerId { get; set; }
        public virtual Customer Customer { get; set; }
    }
}
