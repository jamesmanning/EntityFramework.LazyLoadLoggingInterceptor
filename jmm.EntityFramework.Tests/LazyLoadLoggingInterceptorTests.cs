using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Infrastructure.Interception;
using System.Linq;
using Xunit;

namespace jmm.EntityFramework.Tests
{
    public class LazyLoadLoggingInterceptorTests
    {
        public LazyLoadLoggingInterceptorTests()
        {
            // start each test with a clean database
            using (var dataContext = new CustomerDbContext())
            {
                if (dataContext.Database.Exists())
                    dataContext.Database.Delete();
                dataContext.Database.Create();
            }

            // start each test with no previous load runtimes
            this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Clear();
        }

        private LazyLoadLoggingInterceptor LazyLoadLoggingInterceptor => LazyLoadLoggingInterceptor.RegisteredInstance ?? new LazyLoadLoggingInterceptor(0, false);

        [Fact]
        public void LazyLoadOfNavigationPropertyReferenceIsTracked()
        {
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
            }

            using (var db = new CustomerDbContext())
            {
                var invoices = db.Invoices.ToList();
                Assert.NotNull(invoices);
                Assert.Equal(1, invoices.Count);
                var queriedInvoice = invoices[0];
                Assert.Equal("SomeInvoiceNumber", queriedInvoice.Number);
                Assert.Equal(1, queriedInvoice.InvoiceId);
                Assert.Equal(1, queriedInvoice.CustomerId);

                // lazy load via navigation entity reference
                Assert.Equal(0, this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Count);
                var lazyLoadedCustomer = queriedInvoice.Customer;
                Assert.Equal(1, this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Count);
                var lazyLoadEntryForCustomerProperty = this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Single();
                Assert.Contains("lazy load detected accessing navigation property Customer from entity Invoice", lazyLoadEntryForCustomerProperty.Key);
                Assert.Equal(1, lazyLoadEntryForCustomerProperty.Value.Count);
                Assert.True(lazyLoadEntryForCustomerProperty.Value.Single() >= 0); // should be a valid runtime in milliseconds

                Assert.NotNull(lazyLoadedCustomer);
                Assert.Equal(1, lazyLoadedCustomer.CustomerId);
                Assert.Equal("SomeCustomerName", lazyLoadedCustomer.Name);
            }
        }

        [Fact]
        public void LazyLoadOfNavigationPropertyCollectionIsTracked()
        {
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
            }

            using (var db = new CustomerDbContext())
            {
                var customers = db.Customers.ToList();
                Assert.NotNull(customers);
                Assert.Equal(1, customers.Count);
                var queriedCustomer = customers[0];
                Assert.Equal("SomeCustomerName", queriedCustomer.Name);

                // lazy load via navigation entity collection
                Assert.Equal(0, this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Count);
                Assert.NotNull(queriedCustomer.Invoices);
                Assert.Equal(1, this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Count);
                var lazyLoadEntryForInvoicesProperty = this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Single();
                Assert.Contains("lazy load detected accessing navigation property Invoices from entity Customer", lazyLoadEntryForInvoicesProperty.Key);
                Assert.Equal(1, lazyLoadEntryForInvoicesProperty.Value.Count);
                var lazyLoadRuntimeInMilliseconds = lazyLoadEntryForInvoicesProperty.Value.Single();
                Assert.True(lazyLoadRuntimeInMilliseconds >= 0);

                Assert.Equal(1, queriedCustomer.Invoices.Count);
                var lazyLoadedInvoice = queriedCustomer.Invoices.Single();
                Assert.Equal(1, lazyLoadedInvoice.CustomerId);
                Assert.Equal(1, lazyLoadedInvoice.InvoiceId);
                Assert.Equal("SomeInvoiceNumber", lazyLoadedInvoice.Number);
            }
        }
    }
}
