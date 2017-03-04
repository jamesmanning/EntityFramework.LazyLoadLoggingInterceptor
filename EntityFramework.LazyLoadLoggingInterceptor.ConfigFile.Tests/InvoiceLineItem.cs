namespace EntityFramework.LazyLoadLoggingInterceptor.Tests
{
    public class InvoiceLineItem
    {
        public int InvoiceLineItemId { get; set; }
        public string Description { get; set; }

        public int InvoiceId { get; set; }
        public virtual Invoice Invoice { get; set; }
    }
}