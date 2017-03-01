using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Infrastructure.Interception;
using System.Linq;
using NUnit.Framework;

namespace jmm.EntityFramework.Tests
{
    [TestFixture]
    public class LazyLoadLoggingInterceptorTests
    {
        [SetUp]
        public void BeforeEachTest()
        {
            using (var dataContext = new CustomerDbContext())
            {
                if (dataContext.Database.Exists())
                    dataContext.Database.Delete();
                dataContext.Database.Create();
            }

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
            // have the database start out with some test data
            using (var db = new CustomerDbContext())
            {
                db.Customers.Add(customer);
                db.SaveChanges();
            }


            // start each test with no previous load runtimes
            this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Clear();
        }

        private LazyLoadLoggingInterceptor LazyLoadLoggingInterceptor => LazyLoadLoggingInterceptor.RegisteredInstance ?? new LazyLoadLoggingInterceptor(0, false);

        [Test]
        public void LazyLoadOfNavigationPropertyReferenceIsTracked()
        {
            using (var db = new CustomerDbContext())
            {
                var invoices = db.Invoices.ToList();
                Assert.NotNull(invoices);
                Assert.AreEqual(1, invoices.Count);
                var queriedInvoice = invoices[0];
                Assert.AreEqual("SomeInvoiceNumber", queriedInvoice.Number);
                Assert.AreEqual(1, queriedInvoice.InvoiceId);
                Assert.AreEqual(1, queriedInvoice.CustomerId);

                // lazy load via navigation entity reference
                Assert.AreEqual(0, this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Count);
                var lazyLoadedCustomer = queriedInvoice.Customer;
                Assert.AreEqual(1, this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Count);
                var lazyLoadEntryForCustomerProperty = this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Single();
                StringAssert.Contains("lazy load detected accessing navigation property Customer from entity Invoice", lazyLoadEntryForCustomerProperty.Key);
                Assert.AreEqual(1, lazyLoadEntryForCustomerProperty.Value.Count);
                Assert.True(lazyLoadEntryForCustomerProperty.Value.Single() >= 0); // should be a valid runtime in milliseconds

                Assert.NotNull(lazyLoadedCustomer);
                Assert.AreEqual(1, lazyLoadedCustomer.CustomerId);
                Assert.AreEqual("SomeCustomerName", lazyLoadedCustomer.Name);
            }
        }

        [Test]
        public void LazyLoadOfNavigationPropertyCollectionIsTracked()
        {
            using (var db = new CustomerDbContext())
            {
                var customers = db.Customers.ToList();
                Assert.NotNull(customers);
                Assert.AreEqual(1, customers.Count);
                var queriedCustomer = customers[0];
                Assert.AreEqual("SomeCustomerName", queriedCustomer.Name);

                // lazy load via navigation entity collection
                Assert.AreEqual(0, this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Count);
                Assert.NotNull(queriedCustomer.Invoices);
                Assert.AreEqual(1, this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Count);
                var lazyLoadEntryForInvoicesProperty = this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Single();
                StringAssert.Contains("lazy load detected accessing navigation property Invoices from entity Customer", lazyLoadEntryForInvoicesProperty.Key);
                Assert.AreEqual(1, lazyLoadEntryForInvoicesProperty.Value.Count);
                var lazyLoadRuntimeInMilliseconds = lazyLoadEntryForInvoicesProperty.Value.Single();
                Assert.True(lazyLoadRuntimeInMilliseconds >= 0);

                Assert.AreEqual(1, queriedCustomer.Invoices.Count);
                var lazyLoadedInvoice = queriedCustomer.Invoices.Single();
                Assert.AreEqual(1, lazyLoadedInvoice.CustomerId);
                Assert.AreEqual(1, lazyLoadedInvoice.InvoiceId);
                Assert.AreEqual("SomeInvoiceNumber", lazyLoadedInvoice.Number);
            }
        }

        [Test]
        public void EagerLoadOfNavigationPropertyReferenceIsTracked()
        {
            using (var db = new CustomerDbContext())
            {
                var invoices = db.Invoices
                    .Include(x => x.Customer)
                    .ToList();
                Assert.NotNull(invoices);
                Assert.AreEqual(1, invoices.Count);
                var queriedInvoice = invoices[0];
                Assert.AreEqual("SomeInvoiceNumber", queriedInvoice.Number);
                Assert.AreEqual(1, queriedInvoice.InvoiceId);
                Assert.AreEqual(1, queriedInvoice.CustomerId);

                // navigation entity reference was eager loaded at query time
                Assert.AreEqual(0, this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Count);
                var eagerLoadedCustomer = queriedInvoice.Customer;
                Assert.AreEqual(0, this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Count);

                Assert.NotNull(eagerLoadedCustomer);
                Assert.AreEqual(1, eagerLoadedCustomer.CustomerId);
                Assert.AreEqual("SomeCustomerName", eagerLoadedCustomer.Name);
            }
        }
        [Test]
        public void EagerLoadOfNavigationPropertyCollectionIsNotTracked()
        {
            using (var db = new CustomerDbContext())
            {
                var customers = db.Customers
                    .Include(x => x.Invoices)
                    .ToList();
                Assert.NotNull(customers);
                Assert.AreEqual(1, customers.Count);
                var queriedCustomer = customers[0];
                Assert.AreEqual("SomeCustomerName", queriedCustomer.Name);

                // navigation entity collection was eager loaded at query time
                Assert.AreEqual(0, this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Count);
                Assert.NotNull(queriedCustomer.Invoices);
                Assert.AreEqual(0, this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Count);

                Assert.AreEqual(1, queriedCustomer.Invoices.Count);
                var eagerLoadedInvoice = queriedCustomer.Invoices.Single();
                Assert.AreEqual(1, eagerLoadedInvoice.CustomerId);
                Assert.AreEqual(1, eagerLoadedInvoice.InvoiceId);
                Assert.AreEqual("SomeInvoiceNumber", eagerLoadedInvoice.Number);
            }
        }

        [Test]
        public void ExplicitLoadOfNavigationPropertyReferenceIsTracked()
        {
            using (var db = new CustomerDbContext())
            {
                var invoices = db.Invoices.ToList();
                Assert.NotNull(invoices);
                Assert.AreEqual(1, invoices.Count);
                var queriedInvoice = invoices[0];
                Assert.AreEqual("SomeInvoiceNumber", queriedInvoice.Number);
                Assert.AreEqual(1, queriedInvoice.InvoiceId);
                Assert.AreEqual(1, queriedInvoice.CustomerId);

                // explicit load before referencing navigation entity reference
                Assert.AreEqual(0, this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Count);
                db.Entry(queriedInvoice).Reference(x => x.Customer).Load();
                var explicitLoadedCustomer = queriedInvoice.Customer;
                Assert.AreEqual(0, this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Count);

                Assert.NotNull(explicitLoadedCustomer);
                Assert.AreEqual(1, explicitLoadedCustomer.CustomerId);
                Assert.AreEqual("SomeCustomerName", explicitLoadedCustomer.Name);
            }
        }
        [Test]
        public void ExplicitLoadOfNavigationPropertyCollectionIsNotTracked()
        {
            using (var db = new CustomerDbContext())
            {
                var customers = db.Customers.ToList();
                Assert.NotNull(customers);
                Assert.AreEqual(1, customers.Count);
                var queriedCustomer = customers[0];
                Assert.AreEqual("SomeCustomerName", queriedCustomer.Name);

                // explicit load before referencing navigation entity collection
                Assert.AreEqual(0, this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Count);
                db.Entry(queriedCustomer).Collection(x => x.Invoices).Load();
                Assert.NotNull(queriedCustomer.Invoices);
                Assert.AreEqual(0, this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Count);

                Assert.AreEqual(1, queriedCustomer.Invoices.Count);
                var eagerLoadedInvoice = queriedCustomer.Invoices.Single();
                Assert.AreEqual(1, eagerLoadedInvoice.CustomerId);
                Assert.AreEqual(1, eagerLoadedInvoice.InvoiceId);
                Assert.AreEqual("SomeInvoiceNumber", eagerLoadedInvoice.Number);
            }
        }
    }
}
