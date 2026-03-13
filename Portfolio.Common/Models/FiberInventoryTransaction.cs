namespace Portfolio.Common.Models;

public class FiberInventoryTransaction
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public int MaterialId { get; set; }
    public FiberMaterial Material { get; set; } = null!;
    public string TransactionType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal QtyBeforeTransaction { get; set; }
    public decimal QtyAfterTransaction { get; set; }
    public string? Notes { get; set; }
    public DateTime TransactionDate { get; set; }
}
