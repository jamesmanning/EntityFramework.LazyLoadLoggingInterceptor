using System.Collections.Generic;

namespace jmm.EntityFramework.Tests
{
    public class Customer
    {
        public int CustomerId { get; set; }
        public string Name { get; set; }
        public virtual ICollection<Invoice> Invoices { get; set; }
    }
}