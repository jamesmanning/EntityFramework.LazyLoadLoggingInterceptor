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
                        Number = "SomeInvoiceNumber1",
                        InvoiceLineItems = new List<InvoiceLineItem>()
                        {
                            new InvoiceLineItem() { Description = "SomeInvoiceLineItem1-SomeInvoiceNumber1" },
                            new InvoiceLineItem() { Description = "SomeInvoiceLineItem2-SomeInvoiceNumber1" },
                        }
                    },
                    new Invoice()
                    {
                        Number = "SomeInvoiceNumber2",
                        InvoiceLineItems = new List<InvoiceLineItem>()
                        {
                            new InvoiceLineItem() { Description = "SomeInvoiceLineItem1-SomeInvoiceNumber2" },
                            new InvoiceLineItem() { Description = "SomeInvoiceLineItem2-SomeInvoiceNumber2" },
                            new InvoiceLineItem() { Description = "SomeInvoiceLineItem3-SomeInvoiceNumber2" },
                        }
                    },
                    new Invoice()
                    {
                        Number = "SomeInvoiceNumber3",
                        InvoiceLineItems = new List<InvoiceLineItem>()
                        {
                            new InvoiceLineItem() { Description = "SomeInvoiceLineItem1-SomeInvoiceNumber3" },
                            new InvoiceLineItem() { Description = "SomeInvoiceLineItem2-SomeInvoiceNumber3" },
                            new InvoiceLineItem() { Description = "SomeInvoiceLineItem3-SomeInvoiceNumber3" },
                            new InvoiceLineItem() { Description = "SomeInvoiceLineItem4-SomeInvoiceNumber3" },
                        }
                    },
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

        [TearDown]
        public void AfterEachTest()
        {
            this.LazyLoadLoggingInterceptor.Dispose();
        }

        private LazyLoadLoggingInterceptor LazyLoadLoggingInterceptor => LazyLoadLoggingInterceptor.RegisteredInstance ?? new LazyLoadLoggingInterceptor(0, false);

        [Test]
        public void LazyLoadOfNavigationPropertyReferenceIsTracked()
        {
            using (var db = new CustomerDbContext())
            {
                var invoices = db.Invoices.ToList();
                Assert.NotNull(invoices);
                Assert.AreEqual(3, invoices.Count);
                for (int i = 0; i < invoices.Count; i++)
                {
                    var queriedInvoice = invoices.ElementAt(i);
                    Assert.AreEqual(1, queriedInvoice.CustomerId);
                    Assert.AreEqual(i + 1, queriedInvoice.InvoiceId);
                    Assert.AreEqual($"SomeInvoiceNumber{i + 1}", queriedInvoice.Number);
                }

                // lazy load via navigation entity reference
                Assert.AreEqual(0, this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Count);
                for (int i = 0; i < invoices.Count; i++)
                {
                    var queriedInvoice = invoices.ElementAt(i);
                    var lazyLoadedCustomer = queriedInvoice.Customer;
                    Assert.AreEqual(1, lazyLoadedCustomer.CustomerId);
                    Assert.AreEqual("SomeCustomerName", lazyLoadedCustomer.Name);
                }
                Assert.AreEqual(1, this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Count);
                var lazyLoadEntryForCustomerProperty = this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Single();
                StringAssert.Contains("lazy load detected accessing navigation property Customer from entity Invoice", lazyLoadEntryForCustomerProperty.Key);
                Assert.AreEqual(1, lazyLoadEntryForCustomerProperty.Value.Count);
                Assert.True(lazyLoadEntryForCustomerProperty.Value.Single() >= 0); // should be a valid runtime in milliseconds
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

                Assert.AreEqual(3, queriedCustomer.Invoices.Count);
                for (int i = 0; i < queriedCustomer.Invoices.Count; i++)
                {
                    var lazyLoadedInvoice = queriedCustomer.Invoices.ElementAt(i);
                    Assert.AreEqual(1, lazyLoadedInvoice.CustomerId);
                    Assert.AreEqual(i + 1, lazyLoadedInvoice.InvoiceId);
                    Assert.AreEqual($"SomeInvoiceNumber{i + 1}", lazyLoadedInvoice.Number);
                }
            }
        }

        [Test]
        public void LazyLoadsOfNPlus1SelectsAreTracked()
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
                // get rid of this lazy-load entry to make the N+1 asserts below simpler
                this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Clear();

                for (int invoiceIndex = 0; invoiceIndex < queriedCustomer.Invoices.Count; invoiceIndex++)
                {
                    var invoice = queriedCustomer.Invoices.ElementAt(invoiceIndex);

                    // no lazy load before accessing the InvoiceLineItems under this invoice
                    for (int lineItemIndex = 0; lineItemIndex < invoice.InvoiceLineItems.Count; lineItemIndex++)
                    {
                        var invoiceLineItem = invoice.InvoiceLineItems.ElementAt(lineItemIndex);

                        Assert.AreEqual($"SomeInvoiceLineItem{lineItemIndex + 1}-{invoice.Number}", invoiceLineItem.Description);
                    }
                    // The one lazy load entry should have incremented with the lazy-load of the line items
                    Assert.AreEqual(1, this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Count);
                    var lazyLoadEntryForLineItemsProperty = this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Single();
                    Assert.AreEqual(invoiceIndex+1, lazyLoadEntryForLineItemsProperty.Value.Count);
                }
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
                Assert.AreEqual(3, invoices.Count);
                for (int i = 0; i < invoices.Count; i++)
                {
                    var queriedInvoice = invoices.ElementAt(i);
                    Assert.AreEqual(1, queriedInvoice.CustomerId);
                    Assert.AreEqual(i + 1, queriedInvoice.InvoiceId);
                    Assert.AreEqual($"SomeInvoiceNumber{i + 1}", queriedInvoice.Number);
                }

                // navigation entity reference was eager loaded at query time
                Assert.AreEqual(0, this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Count);
                foreach (var queriedInvoice in invoices)
                {
                    Assert.NotNull(queriedInvoice.Customer);
                    var eagerLoadedCustomer = queriedInvoice.Customer;
                    Assert.AreEqual(1, eagerLoadedCustomer.CustomerId);
                    Assert.AreEqual("SomeCustomerName", eagerLoadedCustomer.Name);
                }
                Assert.AreEqual(0, this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Count);
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

                Assert.AreEqual(3, queriedCustomer.Invoices.Count);
                for (int i = 0; i < queriedCustomer.Invoices.Count; i++)
                {
                    var eagerLoadedInvoice = queriedCustomer.Invoices.ElementAt(i);
                    Assert.AreEqual(1, eagerLoadedInvoice.CustomerId);
                    Assert.AreEqual(i+1, eagerLoadedInvoice.InvoiceId);
                    Assert.AreEqual($"SomeInvoiceNumber{i+1}", eagerLoadedInvoice.Number);
                }
            }
        }

        [Test]
        public void ExplicitLoadOfNavigationPropertyReferenceIsTracked()
        {
            using (var db = new CustomerDbContext())
            {
                var invoices = db.Invoices.ToList();
                Assert.NotNull(invoices);
                Assert.AreEqual(3, invoices.Count);
                for (int i = 0; i < invoices.Count; i++)
                {
                    var queriedInvoice = invoices.ElementAt(i);
                    Assert.AreEqual(1, queriedInvoice.CustomerId);
                    Assert.AreEqual(i + 1, queriedInvoice.InvoiceId);
                    Assert.AreEqual($"SomeInvoiceNumber{i + 1}", queriedInvoice.Number);
                }

                // explicit load before referencing navigation entity reference
                Assert.AreEqual(0, this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Count);
                foreach (var queriedInvoice in invoices)
                {
                    db.Entry(queriedInvoice).Reference(x => x.Customer).Load();
                    var explicitLoadedCustomer = queriedInvoice.Customer;
                    Assert.NotNull(explicitLoadedCustomer);
                    Assert.AreEqual(1, explicitLoadedCustomer.CustomerId);
                    Assert.AreEqual("SomeCustomerName", explicitLoadedCustomer.Name);
                }
                Assert.AreEqual(0, this.LazyLoadLoggingInterceptor.LazyLoadRuntimes.Count);
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

                Assert.AreEqual(3, queriedCustomer.Invoices.Count);
                for (int i = 0; i < queriedCustomer.Invoices.Count; i++)
                {
                    var explicitLoadedInvoice = queriedCustomer.Invoices.ElementAt(i);
                    Assert.AreEqual(1, explicitLoadedInvoice.CustomerId);
                    Assert.AreEqual(i + 1, explicitLoadedInvoice.InvoiceId);
                    Assert.AreEqual($"SomeInvoiceNumber{i + 1}", explicitLoadedInvoice.Number);
                }
            }
        }
    }
}
