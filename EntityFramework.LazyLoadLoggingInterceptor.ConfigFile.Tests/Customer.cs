using System.Collections.Generic;

namespace EntityFramework.LazyLoadLoggingInterceptor.Tests
{
    public class Customer
    {
        public int CustomerId { get; set; }
        public string Name { get; set; }
        public virtual ICollection<Invoice> Invoices { get; set; }
    }
}