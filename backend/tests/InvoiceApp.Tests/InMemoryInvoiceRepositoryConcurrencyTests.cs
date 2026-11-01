using InvoiceApp.Api;
using InvoiceApp.Domain;
using Xunit;

namespace InvoiceApp.Tests;

public class InMemoryInvoiceRepositoryConcurrencyTests
{
    private static async Task<bool[]> RunConcurrently(params Action[] operations)
    {
        using var ready = new CountdownEvent(operations.Length);
        using var start = new ManualResetEventSlim(false);

        var tasks = operations.Select(operation => Task.Factory.StartNew(() =>
        {
            ready.Signal();
            start.Wait();

            try
            {
                operation();
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default));

        var attempts = Task.WhenAll(tasks);
        var allReady = ready.Wait(TimeSpan.FromSeconds(10));
        start.Set();
        Assert.True(allReady, "All approval requests should reach the synchronization gate.");

        return await attempts.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static (InMemoryInvoiceRepository Repository, ApprovalService Service, Invoice Invoice)
        CreateHighValueInvoice()
    {
        var repository = new InMemoryInvoiceRepository();
        var invoice = repository.Add(new Invoice
        {
            Id = "INV-CONCURRENT",
            CustomerName = "Concurrency Test",
            Amount = 600000m,
            Status = InvoiceStatus.PendingApproval
        });

        return (repository, new ApprovalService(repository), invoice);
    }

    [Fact]
    public async Task Approve_SameDirectorConcurrently_RecordsExactlyOneApproval()
    {
        var (_, service, invoice) = CreateHighValueInvoice();

        var results = await RunConcurrently(
            () => service.Approve(invoice.Id, "director.one", "Director"),
            () => service.Approve(invoice.Id, "director.one", "Director"));

        Assert.Single(results, succeeded => succeeded);
        Assert.Single(results, succeeded => !succeeded);
        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        var approval = Assert.Single(invoice.ApprovalHistory);
        Assert.Equal("director.one", approval.Approver);
        Assert.Equal("Director", approval.Role);
        Assert.Null(invoice.DecidedBy);
        Assert.Null(invoice.DecidedAt);
    }

    [Fact]
    public async Task Approve_DistinctDirectorsConcurrently_RecordsBothAndApprovesInvoice()
    {
        var (_, service, invoice) = CreateHighValueInvoice();

        var results = await RunConcurrently(
            () => service.Approve(invoice.Id, "director.one", "Director"),
            () => service.Approve(invoice.Id, "DIRECTOR.TWO", "Director"));

        Assert.All(results, Assert.True);
        Assert.Equal(InvoiceStatus.Approved, invoice.Status);
        Assert.Equal(2, invoice.ApprovalHistory.Count);
        Assert.Equal(
            2,
            invoice.ApprovalHistory
                .Select(approval => approval.Approver)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());
        Assert.Contains(
            invoice.ApprovalHistory,
            approval => approval.Approver == "director.one");
        Assert.Contains(
            invoice.ApprovalHistory,
            approval => approval.Approver == "DIRECTOR.TWO");
        Assert.All(invoice.ApprovalHistory, approval => Assert.Equal("Director", approval.Role));
        Assert.NotNull(invoice.DecidedBy);
        Assert.NotNull(invoice.DecidedAt);
    }
}
