namespace InvoiceApp.Domain;

public class InvoiceApproval
{
    public string Approver { get; set; } = string.Empty;
    public DateTime ApprovedAt { get; set; } = DateTime.UtcNow;
}
