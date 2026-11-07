using System.Threading.Channels;

namespace ProdFalcon.Application.Scanning;

public sealed class ScanWorkItem
{
    public Guid TenantId { get; init; }

    public int? UserId { get; init; }

    public Guid ProjectId { get; init; }

    public int ScanResultId { get; init; }

    /// <summary>Kept in memory for the queued job only. Never persisted.</summary>
    public string? GitHubAccessToken { get; init; }
}

public interface IScanQueue
{
    ValueTask QueueAsync(ScanWorkItem item, CancellationToken cancellationToken = default);

    bool TryCancel(int scanResultId);

    CancellationToken CancellationFor(int scanResultId);

    IAsyncEnumerable<ScanWorkItem> ReadAllAsync(CancellationToken cancellationToken);

    void Forget(int scanResultId);
}

public sealed class ScanQueue : IScanQueue
{
    private readonly Channel<ScanWorkItem> _channel = Channel.CreateBounded<ScanWorkItem>(
        new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.Wait });

    private readonly Dictionary<int, CancellationTokenSource> _tokens = new();
    private readonly object _gate = new();

    public async ValueTask QueueAsync(ScanWorkItem item, CancellationToken cancellationToken = default)
    {
        var cts = new CancellationTokenSource();
        lock (_gate)
        {
            _tokens[item.ScanResultId] = cts;
        }

        await _channel.Writer.WriteAsync(item, cancellationToken);
    }

    public bool TryCancel(int scanResultId)
    {
        CancellationTokenSource? cts;
        lock (_gate)
        {
            _tokens.TryGetValue(scanResultId, out cts);
        }

        if (cts == null)
            return false;

        cts.Cancel();
        return true;
    }

    public CancellationToken CancellationFor(int scanResultId)
    {
        lock (_gate)
        {
            return _tokens.TryGetValue(scanResultId, out var cts) ? cts.Token : CancellationToken.None;
        }
    }

    public void Forget(int scanResultId)
    {
        lock (_gate)
        {
            if (_tokens.Remove(scanResultId, out var cts))
                cts.Dispose();
        }
    }

    public IAsyncEnumerable<ScanWorkItem> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
