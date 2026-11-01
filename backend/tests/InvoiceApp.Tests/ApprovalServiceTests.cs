
using InvoiceApp.Domain;
using Xunit;

namespace InvoiceApp.Tests;

public class ApprovalServiceTests
{
    private sealed record ApprovalSnapshot(string Approver, string Role, DateTime ApprovedAt);

    private sealed record InvoiceSnapshot(
        string Id,
        string CustomerName,
        decimal Amount,
        InvoiceStatus Status,
        DateTime CreatedAt,
        string? DecidedBy,
        DateTime? DecidedAt,
        string? DecisionNote,
        IReadOnlyList<ApprovalSnapshot> ApprovalHistory);

    private static Invoice InvoiceWith(
        string id,
        InvoiceStatus status,
        decimal amount = 100m) =>
        new()
        {
            Id = id,
            CustomerName = "Test School",
            Amount = amount,
            Status = status
        };

    private static ApprovalService ServiceFor(
        FakeInvoiceRepository repository) =>
        new(repository);

    private static ApprovalService ServiceFor(params Invoice[] invoices) =>
        ServiceFor(new FakeInvoiceRepository(invoices));

    private static InvoiceSnapshot CaptureState(Invoice invoice) =>
        new(
            invoice.Id,
            invoice.CustomerName,
            invoice.Amount,
            invoice.Status,
            invoice.CreatedAt,
            invoice.DecidedBy,
            invoice.DecidedAt,
            invoice.DecisionNote,
            invoice.ApprovalHistory
                .Select(approval => new ApprovalSnapshot(
                    approval.Approver,
                    approval.Role,
                    approval.ApprovedAt))
                .ToArray());

    private static void AssertUnchanged(InvoiceSnapshot expected, Invoice invoice)
    {
        Assert.Equal(expected.Id, invoice.Id);
        Assert.Equal(expected.CustomerName, invoice.CustomerName);
        Assert.Equal(expected.Amount, invoice.Amount);
        Assert.Equal(expected.Status, invoice.Status);
        Assert.Equal(expected.CreatedAt, invoice.CreatedAt);
        Assert.Equal(expected.DecidedBy, invoice.DecidedBy);
        Assert.Equal(expected.DecidedAt, invoice.DecidedAt);
        Assert.Equal(expected.DecisionNote, invoice.DecisionNote);
        Assert.Equal(
            expected.ApprovalHistory,
            invoice.ApprovalHistory
                .Select(approval => new ApprovalSnapshot(
                    approval.Approver,
                    approval.Role,
                    approval.ApprovedAt))
                .ToArray());
    }

    [Fact]
    public void Submit_MovesDraftToPendingApproval()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Draft);

        var result = ServiceFor(invoice).Submit("INV-1");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
    }

    [Fact]
    public void Submit_RejectsInvoiceThatIsNotADraft()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.Approved));

        Assert.Throws<InvalidOperationException>(() => service.Submit("INV-1"));
    }

    [Fact]
    public void Approve_SetsStatusApproverAndTimestamp()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));
        var timestampBeforeApproval = DateTime.UtcNow;

        var result = service.Approve("INV-1", "demo.manager", "Manager");

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.manager", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        var approval = Assert.Single(result.ApprovalHistory);
        Assert.Equal("demo.manager", approval.Approver);
        Assert.Equal("Manager", approval.Role);
        Assert.InRange(approval.ApprovedAt, timestampBeforeApproval, DateTime.UtcNow);
    }

    [Fact]
    public void Approve_ManagerCanApproveInvoiceAtExactLimit()
    {
        var service = ServiceFor(
            InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 50000m));

        var result = service.Approve("INV-1", "demo.manager", "Manager");

        Assert.Equal(InvoiceStatus.Approved, result.Status);
    }

    [Fact]
    public void Approve_ManagerCannotApproveAboveLimit_LeavesInvoiceUnchanged()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 50000.01m);
        invoice.DecidedBy = "previous.user";
        invoice.DecidedAt = DateTime.UtcNow.AddDays(-1);
        invoice.DecisionNote = "Previous note";

        var service = ServiceFor(invoice);
        var previousState = CaptureState(invoice);

        Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-1", "demo.manager", "Manager"));

        AssertUnchanged(previousState, invoice);
    }

    [Fact]
    public void Approve_DirectorCanApproveInvoiceAtExactLimit()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 500000m);
        var service = ServiceFor(invoice);

        var result = service.Approve("INV-1", "demo.director1", "Director");

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        var approval = Assert.Single(result.ApprovalHistory);
        Assert.Equal("demo.director1", approval.Approver);
        Assert.Equal("Director", approval.Role);
        Assert.NotEqual(default, approval.ApprovedAt);
    }

    [Fact]
    public void Approve_ManagerCannotApproveHighValueInvoiceAfterFirstDirectorApproval()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 500000.01m);
        invoice.ApprovalHistory.Add(new InvoiceApproval
        {
            Approver = "demo.director1",
            Role = "Director",
            ApprovedAt = DateTime.UtcNow.AddMinutes(-1)
        });
        var service = ServiceFor(invoice);
        var previousState = CaptureState(invoice);

        Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-1", "demo.manager", "Manager"));

        AssertUnchanged(previousState, invoice);
    }

    [Fact]
    public void Approve_DirectorCanApproveInvoiceBelowManagerLimit()
    {
        var service = ServiceFor(
            InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 10000m));

        var result = service.Approve("INV-1", "demo.director1", "Director");

        Assert.Equal(InvoiceStatus.Approved, result.Status);
    }

    [Fact]
    public void Approve_RejectsUnauthorizedRole_LeavesInvoiceUnchanged()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);

        var service = ServiceFor(invoice);
        var previousState = CaptureState(invoice);

        Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-1", "demo.admin", "Admin"));

        AssertUnchanged(previousState, invoice);
    }

    [Fact]
    public void Approve_RejectsNegativeAmount_LeavesInvoiceUnchanged()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, -0.01m);
        invoice.DecidedBy = "previous.user";
        invoice.DecidedAt = DateTime.UtcNow.AddDays(-1);
        invoice.DecisionNote = "Previous note";
        invoice.ApprovalHistory.Add(new InvoiceApproval
        {
            Approver = "previous.approver",
            Role = "Director",
            ApprovedAt = DateTime.UtcNow.AddHours(-1)
        });

        var service = ServiceFor(invoice);
        var previousState = CaptureState(invoice);

        Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-1", "demo.manager", "Manager"));

        AssertUnchanged(previousState, invoice);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Approve_RejectsBlankApprover_LeavesInvoiceUnchanged(string? approver)
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        var service = ServiceFor(invoice);
        var previousState = CaptureState(invoice);

        Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-1", approver!, "Manager"));

        AssertUnchanged(previousState, invoice);
    }

    [Theory]
    [InlineData(InvoiceStatus.Draft)]
    [InlineData(InvoiceStatus.Approved)]
    [InlineData(InvoiceStatus.Rejected)]
    public void Approve_RejectsInvoiceThatIsNotPending_LeavesInvoiceUnchanged(
        InvoiceStatus status)
    {
        var invoice = InvoiceWith("INV-1", status);
        var service = ServiceFor(invoice);
        var previousState = CaptureState(invoice);

        Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-1", "demo.manager", "Manager"));

        AssertUnchanged(previousState, invoice);
    }

    [Fact]
    public void Approve_ThrowsWhenInvoiceDoesNotExist()
    {
        var service = ServiceFor();

        Assert.Throws<KeyNotFoundException>(
            () => service.Approve("INV-404", "demo.manager", "Manager"));
    }

    [Fact]
    public void Reject_RecordsReasonAndApprover()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        var result = service.Reject("INV-1", "demo.manager", "Duplicate invoice");

        Assert.Equal(InvoiceStatus.Rejected, result.Status);
        Assert.Equal("Duplicate invoice", result.DecisionNote);
    }

    [Fact]
    public void Reject_RequiresAReason()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        Assert.Throws<ArgumentException>(
            () => service.Reject("INV-1", "demo.manager", "  "));
    }

    [Fact]
    public void Approve_FirstDirectorApproval_LeavesHighValueInvoicePending()
    {
        var invoice = InvoiceWith(
            "INV-600", InvoiceStatus.PendingApproval, 600000m);

        var service = ServiceFor(invoice);
        var timestampBeforeApproval = DateTime.UtcNow;

        var result = service.Approve(
            "INV-600", "demo.director1", "Director");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        var approval = Assert.Single(result.ApprovalHistory);
        Assert.Equal("demo.director1", approval.Approver);
        Assert.Equal("Director", approval.Role);
        Assert.InRange(approval.ApprovedAt, timestampBeforeApproval, DateTime.UtcNow);
        Assert.Null(result.DecidedBy);
        Assert.Null(result.DecidedAt);
    }

    [Fact]
    public void Approve_SecondDistinctDirectorApproval_CompletesHighValueInvoice()
    {
        var invoice = InvoiceWith(
            "INV-600", InvoiceStatus.PendingApproval, 600000m);

        var service = ServiceFor(invoice);

        service.Approve("INV-600", "demo.director1", "Director");

        var result = service.Approve(
            "INV-600", "demo.director2", "Director");

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal(2, result.ApprovalHistory.Count);
        Assert.Equal("demo.director1", result.ApprovalHistory[0].Approver);
        Assert.Equal("demo.director2", result.ApprovalHistory[1].Approver);
        Assert.Equal("Director", result.ApprovalHistory[0].Role);
        Assert.Equal("Director", result.ApprovalHistory[1].Role);
        Assert.NotEqual(default, result.ApprovalHistory[0].ApprovedAt);
        Assert.NotEqual(default, result.ApprovalHistory[1].ApprovedAt);
        Assert.Equal("demo.director2", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
    }

    [Fact]
    public void Approve_SameDirectorCannotApproveTwice_LeavesInvoiceUnchanged()
    {
        var invoice = InvoiceWith(
            "INV-600", InvoiceStatus.PendingApproval, 600000m);

        var service = ServiceFor(invoice);

        service.Approve("INV-600", "demo.director1", "Director");

        var previousState = CaptureState(invoice);

        Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-600", "demo.director1", "Director"));

        AssertUnchanged(previousState, invoice);
    }

    [Fact]
    public void Approve_ManagerCannotApproveHighValueInvoice_LeavesInvoiceUnchanged()
    {
        var invoice = InvoiceWith(
            "INV-600", InvoiceStatus.PendingApproval, 600000m);

        var service = ServiceFor(invoice);
        var previousState = CaptureState(invoice);

        Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-600", "demo.manager", "Manager"));

        AssertUnchanged(previousState, invoice);
    }

    [Fact]
    public void Approve_SameDirectorWithDifferentCasingCannotApproveTwice()
    {
        var invoice = InvoiceWith(
            "INV-600", InvoiceStatus.PendingApproval, 600000m);

        var service = ServiceFor(invoice);

        service.Approve("INV-600", "demo.director1", "Director");
        var previousState = CaptureState(invoice);

        Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-600", "DEMO.DIRECTOR1", "Director"));

        AssertUnchanged(previousState, invoice);
    }

    [Fact]
    public void Approve_ThirdDirectorCannotApproveAlreadyApprovedInvoice()
    {
        var invoice = InvoiceWith(
            "INV-600", InvoiceStatus.PendingApproval, 600000m);

        var repository = new FakeInvoiceRepository(invoice);
        var service = ServiceFor(repository);

        service.Approve("INV-600", "demo.director1", "Director");
        service.Approve("INV-600", "demo.director2", "Director");

        var previousUpdateCount = repository.UpdateCallCount;
        var previousState = CaptureState(invoice);

        Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-600", "demo.director3", "Director"));

        AssertUnchanged(previousState, invoice);
        Assert.Equal(previousUpdateCount, repository.UpdateCallCount);
    }
}
