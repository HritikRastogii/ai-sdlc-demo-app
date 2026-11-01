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

    private static (InMemoryInvoiceRepository Repository, ApprovalService Service, string InvoiceId)
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

        return (repository, new ApprovalService(repository), invoice.Id);
    }

    [Fact]
    public async Task Approve_SameDirectorConcurrently_RecordsExactlyOneApproval()
    {
        var (repository, service, invoiceId) = CreateHighValueInvoice();

        var results = await RunConcurrently(
            () => service.Approve(invoiceId, "director.one", "Director"),
            () => service.Approve(invoiceId, "director.one", "Director"));

        Assert.Single(results, succeeded => succeeded);
        Assert.Single(results, succeeded => !succeeded);
        var invoice = repository.GetById(invoiceId)!;
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
        var (repository, service, invoiceId) = CreateHighValueInvoice();

        var results = await RunConcurrently(
            () => service.Approve(invoiceId, "director.one", "Director"),
            () => service.Approve(invoiceId, "DIRECTOR.TWO", "Director"));

        Assert.All(results, Assert.True);
        var invoice = repository.GetById(invoiceId)!;
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

    [Fact]
    public async Task ReadDuringAtomicUpdate_ObservesOnlyCompletedInvoiceSnapshot()
    {
        var (repository, _, invoiceId) = CreateHighValueInvoice();
        using var updateStarted = new ManualResetEventSlim(false);
        using var finishUpdate = new ManualResetEventSlim(false);

        var update = Task.Factory.StartNew(
            () => repository.ExecuteAtomically(invoiceId, invoice =>
            {
                Assert.NotNull(invoice);
                invoice.Status = InvoiceStatus.Approved;
                updateStarted.Set();
                Assert.True(finishUpdate.Wait(TimeSpan.FromSeconds(10)));
                invoice.ApprovalHistory.Add(new InvoiceApproval
                {
                    Approver = "director.one",
                    Role = "Director"
                });
                return invoice.CreateSnapshot();
            }),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        Assert.True(updateStarted.Wait(TimeSpan.FromSeconds(10)));
        using var getByIdStarted = new ManualResetEventSlim(false);
        using var getAllStarted = new ManualResetEventSlim(false);
        var getById = Task.Factory.StartNew(
            () =>
            {
                getByIdStarted.Set();
                return repository.GetById(invoiceId);
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        var getAll = Task.Factory.StartNew(
            () =>
            {
                getAllStarted.Set();
                return repository.GetAll();
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        try
        {
            Assert.True(getByIdStarted.Wait(TimeSpan.FromSeconds(10)));
            Assert.True(getAllStarted.Wait(TimeSpan.FromSeconds(10)));
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            Assert.False(getById.IsCompleted);
            Assert.False(getAll.IsCompleted);
        }
        finally
        {
            finishUpdate.Set();
        }

        var updatedInvoice = await update.WaitAsync(TimeSpan.FromSeconds(10));
        var byIdSnapshot = await getById.WaitAsync(TimeSpan.FromSeconds(10));
        var allSnapshot = Assert.Single(
            await getAll.WaitAsync(TimeSpan.FromSeconds(10)),
            invoice => invoice.Id == invoiceId);

        Assert.Equal(InvoiceStatus.Approved, updatedInvoice.Status);
        Assert.Single(updatedInvoice.ApprovalHistory);
        Assert.Equal(InvoiceStatus.Approved, byIdSnapshot!.Status);
        Assert.Single(byIdSnapshot.ApprovalHistory);
        Assert.Equal(InvoiceStatus.Approved, allSnapshot.Status);
        Assert.Single(allSnapshot.ApprovalHistory);
    }

    [Fact]
    public void ReadMethods_ReturnDetachedSnapshots()
    {
        var (repository, _, invoiceId) = CreateHighValueInvoice();

        var byIdSnapshot = repository.GetById(invoiceId)!;
        var byIdsSnapshot = Assert.Single(repository.GetByIds(new[] { invoiceId }));
        var allSnapshot = Assert.Single(
            repository.GetAll(),
            invoice => invoice.Id == invoiceId);
        byIdSnapshot.Status = InvoiceStatus.Rejected;
        byIdSnapshot.ApprovalHistory.Add(new InvoiceApproval { Approver = "external" });
        byIdsSnapshot.Amount = 1m;
        allSnapshot.CustomerName = "Changed outside repository";

        var persistedInvoice = repository.GetById(invoiceId)!;
        Assert.Equal(InvoiceStatus.PendingApproval, persistedInvoice.Status);
        Assert.Equal(600000m, persistedInvoice.Amount);
        Assert.Equal("Concurrency Test", persistedInvoice.CustomerName);
        Assert.Empty(persistedInvoice.ApprovalHistory);
    }

    [Fact]
    public void GetByIds_ReturnsDetachedSnapshots()
    {
        var repository = new InMemoryInvoiceRepository();
        var invoice = repository.Add(new Invoice
        {
            Id = "INV-GET-BY-IDS",
            CustomerName = "Original Customer",
            Amount = 250m,
            Status = InvoiceStatus.PendingApproval,
            ApprovalHistory =
            {
                new InvoiceApproval
                {
                    Approver = "director.one",
                    Role = "Director",
                    ApprovedAt = DateTime.UtcNow.AddDays(-2)
                }
            }
        });

        var snapshot = Assert.Single(repository.GetByIds(new[] { invoice.Id }));
        snapshot.CustomerName = "Mutated Customer";
        snapshot.Amount = 500m;
        snapshot.Status = InvoiceStatus.Rejected;
        snapshot.ApprovalHistory.Clear();
        snapshot.ApprovalHistory.Add(new InvoiceApproval
        {
            Approver = "tampered.approver",
            Role = "Manager",
            ApprovedAt = DateTime.UtcNow
        });

        var persistedInvoice = repository.GetById(invoice.Id)!;
        Assert.Equal("Original Customer", persistedInvoice.CustomerName);
        Assert.Equal(250m, persistedInvoice.Amount);
        Assert.Equal(InvoiceStatus.PendingApproval, persistedInvoice.Status);
        var approval = Assert.Single(persistedInvoice.ApprovalHistory);
        Assert.Equal("director.one", approval.Approver);
        Assert.Equal("Director", approval.Role);
    }

    [Fact]
    public void Add_ReturnsDetachedInvoice()
    {
        var repository = new InMemoryInvoiceRepository();
        var invoice = new Invoice
        {
            CustomerName = "Original Customer",
            Amount = 425m,
            Status = InvoiceStatus.Draft,
            ApprovalHistory =
            {
                new InvoiceApproval
                {
                    Approver = "director.one",
                    Role = "Director",
                    ApprovedAt = DateTime.UtcNow.AddDays(-1)
                }
            }
        };

        var returnedInvoice = repository.Add(invoice);
        returnedInvoice.CustomerName = "Mutated Customer";
        returnedInvoice.Amount = 999m;
        returnedInvoice.Status = InvoiceStatus.Rejected;
        returnedInvoice.ApprovalHistory[0].Approver = "tampered.approver";
        returnedInvoice.ApprovalHistory.Add(new InvoiceApproval
        {
            Approver = "manager.one",
            Role = "Manager",
            ApprovedAt = DateTime.UtcNow
        });

        invoice.CustomerName = "Caller Mutated";
        invoice.Amount = 321m;
        invoice.Status = InvoiceStatus.Approved;
        invoice.ApprovalHistory.Clear();
        invoice.ApprovalHistory.Add(new InvoiceApproval
        {
            Approver = "caller.approver",
            Role = "Manager",
            ApprovedAt = DateTime.UtcNow
        });

        var persistedInvoice = repository.GetById(returnedInvoice.Id)!;
        Assert.Equal("Original Customer", persistedInvoice.CustomerName);
        Assert.Equal(425m, persistedInvoice.Amount);
        Assert.Equal(InvoiceStatus.Draft, persistedInvoice.Status);
        var approval = Assert.Single(persistedInvoice.ApprovalHistory);
        Assert.Equal("director.one", approval.Approver);
        Assert.Equal("Director", approval.Role);
    }

    [Fact]
    public void Update_DoesNotRetainCallerMutableInvoiceObject()
    {
        var repository = new InMemoryInvoiceRepository();
        var invoice = repository.Add(new Invoice
        {
            Id = "INV-UPDATE-ALIASED",
            CustomerName = "Original Customer",
            Amount = 100m,
            Status = InvoiceStatus.PendingApproval
        });

        var toUpdate = repository.GetById(invoice.Id)!;
        toUpdate.CustomerName = "Updated Customer";
        toUpdate.Amount = 200m;
        toUpdate.Status = InvoiceStatus.Rejected;
        toUpdate.ApprovalHistory.Add(new InvoiceApproval
        {
            Approver = "director.one",
            Role = "Director",
            ApprovedAt = DateTime.UtcNow.AddMinutes(-5)
        });

        repository.Update(toUpdate);

        toUpdate.CustomerName = "Mutated After Update";
        toUpdate.Amount = 999m;
        toUpdate.Status = InvoiceStatus.Approved;
        toUpdate.ApprovalHistory[0].Approver = "tampered.approver";

        var persistedInvoice = repository.GetById(invoice.Id)!;
        Assert.Equal("Updated Customer", persistedInvoice.CustomerName);
        Assert.Equal(200m, persistedInvoice.Amount);
        Assert.Equal(InvoiceStatus.Rejected, persistedInvoice.Status);
        var approval = Assert.Single(persistedInvoice.ApprovalHistory);
        Assert.Equal("director.one", approval.Approver);
        Assert.Equal("Director", approval.Role);
    }

    [Fact]
    public void GetById_DeepCopiesApprovalHistory()
    {
        var repository = new InMemoryInvoiceRepository();
        var invoice = repository.Add(new Invoice
        {
            Id = "INV-APPROVAL-SNAPSHOT",
            CustomerName = "Approval Snapshot Customer",
            Amount = 750m,
            Status = InvoiceStatus.PendingApproval,
            ApprovalHistory =
            {
                new InvoiceApproval
                {
                    Approver = "director.one",
                    Role = "Director",
                    ApprovedAt = DateTime.UtcNow.AddDays(-3)
                },
                new InvoiceApproval
                {
                    Approver = "director.two",
                    Role = "Director",
                    ApprovedAt = DateTime.UtcNow.AddDays(-2)
                }
            }
        });

        var snapshot = repository.GetById(invoice.Id)!;
        snapshot.ApprovalHistory[0].Approver = "tampered.approver";
        snapshot.ApprovalHistory.Add(new InvoiceApproval
        {
            Approver = "manager.one",
            Role = "Manager",
            ApprovedAt = DateTime.UtcNow
        });

        var persistedInvoice = repository.GetById(invoice.Id)!;
        Assert.Equal(2, persistedInvoice.ApprovalHistory.Count);
        Assert.Equal("director.one", persistedInvoice.ApprovalHistory[0].Approver);
        Assert.Equal("director.two", persistedInvoice.ApprovalHistory[1].Approver);
    }
}
