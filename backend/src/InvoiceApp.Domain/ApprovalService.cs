
namespace InvoiceApp.Domain;

/// <summary>
/// Moves invoices through their lifecycle and enforces role-based approval limits.
/// </summary>
public class ApprovalService
{
    private const decimal ManagerApprovalLimit = 50000m;
    private const decimal DirectorApprovalLimit = 500000m;

    private const string ManagerRole = "Manager";
    private const string DirectorRole = "Director";

    private readonly IInvoiceRepository _repository;

    public ApprovalService(IInvoiceRepository repository)
    {
        _repository = repository;
    }

    public Invoice Submit(string invoiceId)
    {
        return _repository.ExecuteAtomically(invoiceId, invoice =>
        {
            if (invoice is null)
            {
                throw new KeyNotFoundException($"Invoice {invoiceId} was not found.");
            }

            if (invoice.Status != InvoiceStatus.Draft)
            {
                throw new InvalidOperationException(
                    $"Invoice {invoiceId} is {invoice.Status}; only drafts can be submitted.");
            }

            invoice.Status = InvoiceStatus.PendingApproval;
            return invoice.CreateSnapshot();
        });
    }

    public Invoice Approve(string invoiceId, string approver, string approverRole)
    {
        return _repository.ExecuteAtomically(invoiceId, invoice =>
        {
            if (invoice is null)
            {
                throw new KeyNotFoundException($"Invoice {invoiceId} was not found.");
            }

            EnsurePending(invoice);
            EnsureValidApprover(approver);
            EnsureSupportedRole(approverRole);

            if (invoice.Amount < 0)
            {
                throw new InvalidOperationException(
                    $"Invoice {invoice.Id} has a negative amount and cannot be approved.");
            }

            if (invoice.Amount > DirectorApprovalLimit)
            {
                if (approverRole != DirectorRole)
                {
                    throw new InvalidOperationException(
                        "Only Directors can approve invoices above 500,000.");
                }

                EnsureDistinctDirector(invoice, approver);

                invoice.ApprovalHistory.Add(new InvoiceApproval
                {
                    Approver = approver,
                    Role = approverRole,
                    ApprovedAt = DateTime.UtcNow
                });

                if (invoice.ApprovalHistory.Count < 2)
                {
                    return invoice.CreateSnapshot();
                }

                invoice.Status = InvoiceStatus.Approved;
                invoice.DecidedBy = approver;
                invoice.DecidedAt = DateTime.UtcNow;
                return invoice.CreateSnapshot();
            }

            EnsureApprovalLimit(invoice, approverRole);

            invoice.ApprovalHistory.Add(new InvoiceApproval
            {
                Approver = approver,
                Role = approverRole,
                ApprovedAt = DateTime.UtcNow
            });
            invoice.Status = InvoiceStatus.Approved;
            invoice.DecidedBy = approver;
            invoice.DecidedAt = DateTime.UtcNow;
            return invoice.CreateSnapshot();
        });
    }

    public Invoice Reject(string invoiceId, string approver, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "A reason is required to reject an invoice.", nameof(reason));
        }

        return _repository.ExecuteAtomically(invoiceId, invoice =>
        {
            if (invoice is null)
            {
                throw new KeyNotFoundException($"Invoice {invoiceId} was not found.");
            }

            EnsurePending(invoice);
            EnsureValidApprover(approver);

            invoice.Status = InvoiceStatus.Rejected;
            invoice.DecidedBy = approver;
            invoice.DecidedAt = DateTime.UtcNow;
            invoice.DecisionNote = reason;
            return invoice.CreateSnapshot();
        });
    }

    private static void EnsureApprovalLimit(Invoice invoice, string approverRole)
    {
        switch (approverRole)
        {
            case ManagerRole when invoice.Amount <= ManagerApprovalLimit:
                return;

            case ManagerRole:
                throw new InvalidOperationException(
                    $"Manager cannot approve invoice {invoice.Id}. " +
                    $"The maximum approval amount is {ManagerApprovalLimit:N0}.");

            case DirectorRole when invoice.Amount <= DirectorApprovalLimit:
                return;

            case DirectorRole:
                throw new InvalidOperationException(
                    $"Director cannot approve invoice {invoice.Id}. " +
                    $"The maximum approval amount is {DirectorApprovalLimit:N0}.");

            default:
                throw new InvalidOperationException(
                    $"Role '{approverRole}' is not authorized to approve invoices.");
        }
    }

    private static void EnsurePending(Invoice invoice)
    {
        if (invoice.Status != InvoiceStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                $"Invoice {invoice.Id} is {invoice.Status}; only pending invoices can be decided.");
        }
    }

    private static void EnsureValidApprover(string approver)
    {
        if (string.IsNullOrWhiteSpace(approver))
        {
            throw new InvalidOperationException(
                "An approver identity is required.");
        }
    }

    private static void EnsureSupportedRole(string approverRole)
    {
        if (approverRole is not ManagerRole and not DirectorRole)
        {
            throw new InvalidOperationException(
                $"Role '{approverRole}' is not authorized to approve invoices.");
        }
    }

    private static void EnsureDistinctDirector(Invoice invoice, string approver)
    {
        if (invoice.ApprovalHistory.Any(
            approval => string.Equals(
                approval.Approver,
                approver,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "A different Director must provide the second approval.");
        }

        if (invoice.ApprovalHistory.Count >= 2)
        {
            throw new InvalidOperationException(
                "The invoice already has the required Director approvals.");
        }
    }
}
