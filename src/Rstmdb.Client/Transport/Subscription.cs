using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Rstmdb.Client.Transport;

/// <summary>
/// A channel-based event stream for WATCH_INSTANCE and WATCH_ALL subscriptions.
/// </summary>
public sealed class Subscription : IAsyncDisposable
{
    private readonly Channel<SubscriptionEvent> _events;
    private readonly Channel<Exception> _errors;
    private readonly Connection _connection;
    private int _disposed;

    /// <summary>Subscription ID assigned by the server.</summary>
    public string Id { get; internal set; } = "";

    /// <summary>Read-side channel for streaming events.</summary>
    public ChannelReader<SubscriptionEvent> Events => _events.Reader;

    /// <summary>Read-side channel for errors (e.g. connection closed).</summary>
    public ChannelReader<Exception> Errors => _errors.Reader;

    internal Subscription(Connection connection)
    {
        _connection = connection;
        _events = Channel.CreateBounded<SubscriptionEvent>(new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = true,
        });
        _errors = Channel.CreateBounded<Exception>(new BoundedChannelOptions(16)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = true,
        });
    }

    /// <summary>
    /// Convenience method to read all events as an async enumerable.
    /// </summary>
    public async IAsyncEnumerable<SubscriptionEvent> ReadAllAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var evt in _events.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return evt;
        }
    }

    /// <summary>
    /// Sends an event to the channel. Non-blocking; drops oldest if full.
    /// </summary>
    internal void TryWriteEvent(SubscriptionEvent evt)
    {
        _events.Writer.TryWrite(evt);
    }

    /// <summary>
    /// Called by Connection when the connection drops.
    /// </summary>
    internal void CloseFromConnection()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        _errors.Writer.TryWrite(new IOException("rstmdb: connection closed"));
        _events.Writer.TryComplete();
        _errors.Writer.TryComplete();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        // Best-effort UNWATCH
        try
        {
            _connection.RemoveSubscription(Id);
            var parms = new Dictionary<string, object> { ["subscription_id"] = Id };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await _connection.SendRequestAsync(Protocol.Operations.Unwatch, parms, cts.Token).ConfigureAwait(false);
        }
        catch { }

        _events.Writer.TryComplete();
        _errors.Writer.TryComplete();
    }
}
