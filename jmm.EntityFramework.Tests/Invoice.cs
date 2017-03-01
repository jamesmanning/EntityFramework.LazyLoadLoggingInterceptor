namespace jmm.EntityFramework.Tests
{
    public class Invoice
    {
        public int InvoiceId { get; set; }
        public string Number { get; set; }

        public int CustomerId { get; set; }
        public virtual Customer Customer { get; set; }
    }
}