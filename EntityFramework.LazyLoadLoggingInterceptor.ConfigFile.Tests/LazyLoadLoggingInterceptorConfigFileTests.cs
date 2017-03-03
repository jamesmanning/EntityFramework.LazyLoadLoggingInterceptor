using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EntityFramework.LazyLoadLoggingInterceptor.Tests;
using NUnit.Framework;

namespace EntityFramework.LazyLoadLoggingInterceptor.ConfigFile.Tests
{
    [TestFixture]
    public class LazyLoadLoggingInterceptorConfigFileTests
    {
        public void CreateAndPopulateDatabase()
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
                            new InvoiceLineItem() {Description = "SomeInvoiceLineItem1-SomeInvoiceNumber1"},
                            new InvoiceLineItem() {Description = "SomeInvoiceLineItem2-SomeInvoiceNumber1"},
                        }
                    },
                    new Invoice()
                    {
                        Number = "SomeInvoiceNumber2",
                        InvoiceLineItems = new List<InvoiceLineItem>()
                        {
                            new InvoiceLineItem() {Description = "SomeInvoiceLineItem1-SomeInvoiceNumber2"},
                            new InvoiceLineItem() {Description = "SomeInvoiceLineItem2-SomeInvoiceNumber2"},
                            new InvoiceLineItem() {Description = "SomeInvoiceLineItem3-SomeInvoiceNumber2"},
                        }
                    },
                    new Invoice()
                    {
                        Number = "SomeInvoiceNumber3",
                        InvoiceLineItems = new List<InvoiceLineItem>()
                        {
                            new InvoiceLineItem() {Description = "SomeInvoiceLineItem1-SomeInvoiceNumber3"},
                            new InvoiceLineItem() {Description = "SomeInvoiceLineItem2-SomeInvoiceNumber3"},
                            new InvoiceLineItem() {Description = "SomeInvoiceLineItem3-SomeInvoiceNumber3"},
                            new InvoiceLineItem() {Description = "SomeInvoiceLineItem4-SomeInvoiceNumber3"},
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
            LazyLoadLoggingInterceptor.RegisteredInstance.LazyLoadRuntimes.Clear();
        }

        [Test]
        public void InterceptorInConfigFileIsLoadedWhenContextOpened()
        {
            // before we've opened a context, there should be no registered instance
            Assert.IsNull(LazyLoadLoggingInterceptor.RegisteredInstance);
            using (var db = new CustomerDbContext())
            {
                // we've opened a context, so there should be an instance registered now
                Assert.NotNull(LazyLoadLoggingInterceptor.RegisteredInstance);
                CreateAndPopulateDatabase();
                var invoices = db.Invoices.ToList();
                Assert.NotNull(invoices);
                Assert.AreEqual(3, invoices.Count);

                // lazy load via navigation entity reference
                Assert.AreEqual(0, LazyLoadLoggingInterceptor.RegisteredInstance.LazyLoadRuntimes.Count);
                for (int i = 0; i < invoices.Count; i++)
                {
                    var queriedInvoice = invoices.ElementAt(i);
                    var lazyLoadedCustomer = queriedInvoice.Customer;
                    Assert.AreEqual(1, lazyLoadedCustomer.CustomerId);
                    Assert.AreEqual("SomeCustomerName", lazyLoadedCustomer.Name);
                }
                Assert.AreEqual(1, LazyLoadLoggingInterceptor.RegisteredInstance.LazyLoadRuntimes.Count);
                var lazyLoadEntryForCustomerProperty = LazyLoadLoggingInterceptor.RegisteredInstance.LazyLoadRuntimes.Single();
                StringAssert.Contains("lazy load detected accessing navigation property Customer from entity Invoice", lazyLoadEntryForCustomerProperty.Key);
                Assert.AreEqual(1, lazyLoadEntryForCustomerProperty.Value.Count);
                Assert.True(lazyLoadEntryForCustomerProperty.Value.Single() >= 0); // should be a valid runtime in milliseconds
            }
        }
    }
}