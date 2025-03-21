namespace Saturn.Telegram.Db.Entities;

public class InvoiceEntity
{
    public Guid Id { get; set; }
    
    public Guid FromAccountId { get; set; }
    
    public Guid ToAccountId { get; set; }
    
    public decimal Amount { get; set; }
}