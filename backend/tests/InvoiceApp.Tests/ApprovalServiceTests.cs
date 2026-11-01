
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Reject_RejectsBlankApprover_LeavesInvoiceUnchanged(string? approver)
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
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

        var exception = Assert.Throws<InvalidOperationException>(
            () => service.Reject("INV-1", approver!, "Duplicate invoice"));

        Assert.Equal("An approver identity is required.", exception.Message);
        AssertUnchanged(previousState, invoice);
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

    [Fact]
    public async Task Approve_ConcurrentRequestsFromSameDirector_CountsOnlyOneApproval()
    {
        var invoice = InvoiceWith(
            "INV-600", InvoiceStatus.PendingApproval, 600000m);
        var service = ServiceFor(invoice);
        using var ready = new CountdownEvent(2);
        using var start = new ManualResetEventSlim(false);

        Task<(bool Succeeded, InvoiceSnapshot State)> ApproveConcurrently() =>
            Task.Factory.StartNew(() =>
        {
            ready.Signal();
            start.Wait();

            try
            {
                service.Approve("INV-600", "demo.director1", "Director");
                return (true, CaptureState(invoice));
            }
            catch (InvalidOperationException)
            {
                return (false, CaptureState(invoice));
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        var attempts = Task.WhenAll(ApproveConcurrently(), ApproveConcurrently());
        var bothReady = ready.Wait(TimeSpan.FromSeconds(10));
        start.Set();
        Assert.True(bothReady, "Both approval requests should reach the synchronization gate.");

        var results = await attempts.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Single(results, attempt => attempt.Succeeded);
        var rejectedAttempt = Assert.Single(results, attempt => !attempt.Succeeded);
        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        var approval = Assert.Single(invoice.ApprovalHistory);
        Assert.Equal("demo.director1", approval.Approver);
        Assert.Equal("Director", approval.Role);
        Assert.Null(invoice.DecidedBy);
        Assert.Null(invoice.DecidedAt);
        Assert.Null(invoice.DecisionNote);
        AssertUnchanged(rejectedAttempt.State, invoice);
    }

    [Fact]
    public async Task Approve_ConcurrentRequestsFromDistinctDirectors_CompleteWithTwoApprovals()
    {
        var invoice = InvoiceWith(
            "INV-600", InvoiceStatus.PendingApproval, 600000m);
        var service = ServiceFor(invoice);
        using var ready = new CountdownEvent(2);
        using var start = new ManualResetEventSlim(false);

        Task<bool> ApproveConcurrently(string approver) => Task.Factory.StartNew(() =>
        {
            ready.Signal();
            start.Wait();

            try
            {
                service.Approve("INV-600", approver, "Director");
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        var attempts = Task.WhenAll(
            ApproveConcurrently("demo.director1"),
            ApproveConcurrently("demo.director2"));
        var bothReady = ready.Wait(TimeSpan.FromSeconds(10));
        start.Set();
        Assert.True(bothReady, "Both approval requests should reach the synchronization gate.");

        var results = await attempts.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.All(results, Assert.True);
        Assert.Equal(InvoiceStatus.Approved, invoice.Status);
        Assert.Equal(2, invoice.ApprovalHistory.Count);
        Assert.Equal(
            2,
            invoice.ApprovalHistory
                .Select(approval => approval.Approver)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());
        Assert.All(invoice.ApprovalHistory, approval => Assert.Equal("Director", approval.Role));
        Assert.Contains(
            invoice.DecidedBy,
            new[] { "demo.director1", "demo.director2" });
        Assert.NotNull(invoice.DecidedAt);
    }

    [Fact]
    public async Task Approve_ConcurrentApprovalAndRejection_OnlyOneDecisionSucceeds()
    {
        var invoice = InvoiceWith(
            "INV-600", InvoiceStatus.PendingApproval, 1000m);
        var service = ServiceFor(invoice);
        using var ready = new CountdownEvent(2);
        using var start = new ManualResetEventSlim(false);

        Task<(bool Succeeded, InvoiceSnapshot State)> ApproveConcurrently() =>
            Task.Factory.StartNew(() =>
            {
                ready.Signal();
                start.Wait();

                try
                {
                    service.Approve("INV-600", "demo.director1", "Director");
                    return (true, CaptureState(invoice));
                }
                catch (InvalidOperationException)
                {
                    return (false, CaptureState(invoice));
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        Task<(bool Succeeded, InvoiceSnapshot State)> RejectConcurrently() =>
            Task.Factory.StartNew(() =>
            {
                ready.Signal();
                start.Wait();

                try
                {
                    service.Reject("INV-600", "demo.manager", "Not approved");
                    return (true, CaptureState(invoice));
                }
                catch (InvalidOperationException)
                {
                    return (false, CaptureState(invoice));
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        var attempts = Task.WhenAll(ApproveConcurrently(), RejectConcurrently());
        var bothReady = ready.Wait(TimeSpan.FromSeconds(10));
        start.Set();
        Assert.True(bothReady, "Both lifecycle requests should reach the synchronization gate.");

        var results = await attempts.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Single(results, attempt => attempt.Succeeded);
        var rejectedAttempt = Assert.Single(results, attempt => !attempt.Succeeded);
        AssertUnchanged(rejectedAttempt.State, invoice);
        Assert.Contains(invoice.Status, new[] { InvoiceStatus.Approved, InvoiceStatus.Rejected });

        if (invoice.Status == InvoiceStatus.Approved)
        {
            Assert.Equal("demo.director1", invoice.DecidedBy);
            Assert.Null(invoice.DecisionNote);
            Assert.Single(invoice.ApprovalHistory);
        }
        else
        {
            Assert.Equal("demo.manager", invoice.DecidedBy);
            Assert.Equal("Not approved", invoice.DecisionNote);
            Assert.Empty(invoice.ApprovalHistory);
        }
    }

    [Fact]
    public void Submit_RejectsNonexistentInvoice()
    {
        var service = ServiceFor();

        var exception = Assert.Throws<KeyNotFoundException>(
            () => service.Submit("INV-MISSING"));

        Assert.Equal("Invoice INV-MISSING was not found.", exception.Message);
    }

    [Fact]
    public void Reject_RejectsNonexistentInvoice()
    {
        var service = ServiceFor();

        var exception = Assert.Throws<KeyNotFoundException>(
            () => service.Reject(
                "INV-MISSING", "demo.manager", "Duplicate invoice"));

        Assert.Equal("Invoice INV-MISSING was not found.", exception.Message);
    }

    [Fact]
    public void Approve_RejectsInvoiceThatAlreadyHasTwoDirectorApprovals_LeavesInvoiceUnchanged()
    {
        var invoice = InvoiceWith(
            "INV-600", InvoiceStatus.PendingApproval, 600000m);

        invoice.ApprovalHistory.AddRange(new[]
        {
            new InvoiceApproval
            {
                Approver = "demo.director1",
                Role = "Director"
            },
            new InvoiceApproval
            {
                Approver = "demo.director2",
                Role = "Director"
            }
        });

        var service = ServiceFor(invoice);
        var previousState = CaptureState(invoice);

        var exception = Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-600", "demo.director3", "Director"));

        Assert.Equal(
            "The invoice already has the required Director approvals.",
            exception.Message);

        AssertUnchanged(previousState, invoice);
    }

    [Fact]
    public async Task Submit_ConcurrentWithApproval_LeavesConsistentLifecycleState()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Draft);
        var service = ServiceFor(invoice);
        using var ready = new CountdownEvent(2);
        using var start = new ManualResetEventSlim(false);

        Task<bool> RunConcurrently(Func<Invoice> operation) => Task.Factory.StartNew(() =>
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
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        var attempts = Task.WhenAll(
            RunConcurrently(() => service.Submit("INV-1")),
            RunConcurrently(() => service.Approve("INV-1", "demo.manager", "Manager")));
        var bothReady = ready.Wait(TimeSpan.FromSeconds(10));
        start.Set();
        Assert.True(bothReady, "Both lifecycle requests should reach the synchronization gate.");

        var results = await attempts.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(results[0], "Submitting a draft should succeed.");
        if (results[1])
        {
            Assert.Equal(InvoiceStatus.Approved, invoice.Status);
            Assert.Single(invoice.ApprovalHistory);
            Assert.Equal("demo.manager", invoice.DecidedBy);
            Assert.NotNull(invoice.DecidedAt);
        }
        else
        {
            Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
            Assert.Empty(invoice.ApprovalHistory);
            Assert.Null(invoice.DecidedBy);
            Assert.Null(invoice.DecidedAt);
            Assert.Null(invoice.DecisionNote);
        }
    }
}
