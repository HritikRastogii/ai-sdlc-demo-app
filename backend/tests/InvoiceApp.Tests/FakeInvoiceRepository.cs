using InvoiceApp.Domain;

namespace InvoiceApp.Tests;

internal class FakeInvoiceRepository : IInvoiceRepository
{
    private readonly object _lock = new();
    private readonly List<Invoice> _invoices = new();

    public FakeInvoiceRepository(params Invoice[] seed) => _invoices.AddRange(seed);

    public IReadOnlyList<Invoice> GetAll()
    {
        lock (_lock) return _invoices.ToList();
    }

    public Invoice? GetById(string id)
    {
        lock (_lock) return _invoices.FirstOrDefault(i => i.Id == id);
    }

    public IReadOnlyList<Invoice> GetByIds(IEnumerable<string> ids)
    {
        var wanted = ids.ToHashSet();
        lock (_lock) return _invoices.Where(i => wanted.Contains(i.Id)).ToList();
    }

    public T ExecuteAtomically<T>(string invoiceId, Func<Invoice?, T> operation)
    {
        lock (_lock)
        {
            var invoice = _invoices.FirstOrDefault(i => i.Id == invoiceId);
            return operation(invoice);
        }
    }

    public Invoice Add(Invoice invoice)
    {
        lock (_lock)
        {
            _invoices.Add(invoice);
            return invoice;
        }
    }

    public bool Remove(string id)
    {
        lock (_lock) return _invoices.RemoveAll(i => i.Id == id) > 0;
    }

    public int UpdateCallCount { get; private set; }

    public void Update(Invoice invoice)
    {
        lock (_lock) UpdateCallCount++;
    }
}
