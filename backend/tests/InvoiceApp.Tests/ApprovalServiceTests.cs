
using InvoiceApp.Domain;
using Xunit;

namespace InvoiceApp.Tests;

public class ApprovalServiceTests
{
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

        var result = service.Approve("INV-1", "demo.manager", "Manager");

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.manager", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
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

        Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-1", "demo.manager", "Manager"));

        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        Assert.Equal("previous.user", invoice.DecidedBy);
        Assert.NotNull(invoice.DecidedAt);
        Assert.Equal("Previous note", invoice.DecisionNote);
    }

    [Fact]
    public void Approve_DirectorCanApproveInvoiceAtExactLimit()
    {
        var service = ServiceFor(
            InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 500000m));

        var result = service.Approve("INV-1", "demo.director1", "Director");

        Assert.Equal(InvoiceStatus.Approved, result.Status);
    }

    [Fact]
    public void Approve_DirectorCannotApproveAboveLimit_LeavesInvoiceUnchanged()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 500000.01m);
        invoice.DecidedBy = "previous.user";
        invoice.DecidedAt = DateTime.UtcNow.AddDays(-1);
        invoice.DecisionNote = "Previous note";

        var service = ServiceFor(invoice);

        Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-1", "demo.director1", "Director"));

        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        Assert.Equal("previous.user", invoice.DecidedBy);
        Assert.NotNull(invoice.DecidedAt);
        Assert.Equal("Previous note", invoice.DecisionNote);
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

        Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-1", "demo.admin", "Admin"));

        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        Assert.Null(invoice.DecidedBy);
        Assert.Null(invoice.DecidedAt);
    }

    [Fact]
    public void Approve_RejectsInvoiceThatIsStillADraft()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.Draft));

        Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-1", "demo.manager", "Manager"));
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

        var result = service.Approve(
            "INV-600", "demo.director1", "Director");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        Assert.Single(result.ApprovalHistory);
        Assert.Equal("demo.director1", result.ApprovalHistory[0].Approver);
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

        var previousApprovalCount = invoice.ApprovalHistory.Count;
        var previousApprover = invoice.ApprovalHistory[0].Approver;
        var previousApprovalTime = invoice.ApprovalHistory[0].ApprovedAt;

        Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-600", "demo.director1", "Director"));

        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        Assert.Equal(previousApprovalCount, invoice.ApprovalHistory.Count);
        Assert.Equal(previousApprover, invoice.ApprovalHistory[0].Approver);
        Assert.Equal(previousApprovalTime, invoice.ApprovalHistory[0].ApprovedAt);
    }

    [Fact]
    public void Approve_ManagerCannotApproveHighValueInvoice_LeavesInvoiceUnchanged()
    {
        var invoice = InvoiceWith(
            "INV-600", InvoiceStatus.PendingApproval, 600000m);

        var service = ServiceFor(invoice);

        Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-600", "demo.manager", "Manager"));

        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        Assert.Empty(invoice.ApprovalHistory);
        Assert.Null(invoice.DecidedBy);
        Assert.Null(invoice.DecidedAt);
    }

    [Fact]
    public void Approve_SameDirectorWithDifferentCasingCannotApproveTwice()
    {
        var invoice = InvoiceWith(
            "INV-600", InvoiceStatus.PendingApproval, 600000m);

        var service = ServiceFor(invoice);

        service.Approve("INV-600", "demo.director1", "Director");

        Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-600", "DEMO.DIRECTOR1", "Director"));

        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        Assert.Single(invoice.ApprovalHistory);
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

        Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-600", "demo.director3", "Director"));

        Assert.Equal(InvoiceStatus.Approved, invoice.Status);
        Assert.Equal(2, invoice.ApprovalHistory.Count);
        Assert.Equal("demo.director2", invoice.DecidedBy);
        Assert.Equal(previousUpdateCount, repository.UpdateCallCount);
    }
}
