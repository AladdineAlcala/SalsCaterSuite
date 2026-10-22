namespace SalsCaterSuite.WebApi.Logging;

/// <summary>
/// A <see cref="Stream"/> decorator that lets the request logger see the beginning of a response
/// body without changing a single byte the client receives (BR-22).
/// <para>
/// Every write is passed straight through to the wrapped stream, so the bytes the client
/// receives are <b>byte-for-byte identical</b> to the bytes the application produced (BR-22.2 /
/// AC-OPS-07). In parallel, the first <c>maxBytes</c> are retained for the log record. Once the
/// cap is reached the retention stops: a multi-megabyte XLSX export or PDF is streamed through
/// and is never fully buffered (BR-22.1 / risk R2).
/// </para>
/// </summary>
public sealed class ResponseCaptureStream : Stream
{
    private readonly Stream _inner;
    private readonly int _maxBytes;
    private readonly MemoryStream _buffer = new();

    private bool _captureComplete;

    /// <summary>Creates the decorator over <paramref name="inner"/> with the given capture cap.</summary>
    public ResponseCaptureStream(Stream inner, int maxBytes)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        _inner = inner;
        _maxBytes = maxBytes;
    }

    /// <summary>Whether the response was longer than the capture cap (BR-22.1).</summary>
    public bool Truncated { get; private set; }

    /// <summary>The total number of bytes written to the response (specification §4.2).</summary>
    public long TotalBytesWritten { get; private set; }

    /// <summary>The retained prefix of the response — never longer than the capture cap.</summary>
    public byte[] GetCapturedBytes() => _buffer.ToArray();

    public override bool CanRead => true;

    /// <summary>
    /// BR-22.6 — the capture stream is <b>seekable over its own retained buffer</b>, never over
    /// the inner stream. The error path calls <c>HttpResponse.Clear()</c>, which resets a seekable
    /// body and cannot reset a non-seekable one: with a non-seekable capture the stale bytes of a
    /// partially written response would survive into the recorded body. The inner (Kestrel) stream
    /// is not seekable, so the seek contract is honoured over the retained buffer instead.
    /// </summary>
    public override bool CanSeek => true;

    public override bool CanWrite => true;

    /// <summary>The length of the <b>retained</b> buffer — not the bytes written to the response.</summary>
    public override long Length => _buffer.Length;

    /// <summary>The read/write position within the retained buffer.</summary>
    public override long Position
    {
        get => _buffer.Position;
        set => _buffer.Position = value;
    }

    public override void Flush() => _inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        _inner.FlushAsync(cancellationToken);

    /// <summary>Reads from the retained buffer — the part of the response that was captured.</summary>
    public override int Read(byte[] buffer, int offset, int count) => _buffer.Read(buffer, offset, count);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        _buffer.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        _buffer.ReadAsync(buffer, cancellationToken);

    /// <summary>Seeks within the retained buffer.</summary>
    public override long Seek(long offset, SeekOrigin origin) => _buffer.Seek(offset, origin);

    /// <summary>
    /// BR-22.6 — resets the retained capture. <c>SetLength(0)</c> is what
    /// <c>HttpResponse.Clear()</c> calls on a seekable body: it discards the partially written
    /// response so the recorded body is the body <b>as it stands after the reset</b>. The inner
    /// stream is deliberately <b>not</b> touched — it is not seekable and delegating would throw.
    /// </summary>
    public override void SetLength(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        _buffer.SetLength(value);
        _buffer.Position = 0;

        if (value == 0)
        {
            // The capture starts again from scratch after a reset.
            _captureComplete = false;
            Truncated = false;
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        TotalBytesWritten += count;
        Capture(buffer.AsSpan(offset, count));
        _inner.Write(buffer, offset, count);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        TotalBytesWritten += count;
        Capture(buffer.AsSpan(offset, count));
        await _inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        TotalBytesWritten += buffer.Length;
        Capture(buffer.Span);
        await _inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public override void WriteByte(byte value)
    {
        TotalBytesWritten += 1;
        Capture(new ReadOnlySpan<byte>([value]));
        _inner.WriteByte(value);
    }

    /// <summary>
    /// Releases the retained buffer. The <b>wrapped</b> stream is deliberately left open — the
    /// host owns the response body and disposes it itself.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _buffer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Capture(ReadOnlySpan<byte> bytes)
    {
        if (_captureComplete)
        {
            return;
        }

        var remaining = _maxBytes - (int)_buffer.Length;

        if (bytes.Length <= remaining)
        {
            _buffer.Write(bytes);
            return;
        }

        if (remaining > 0)
        {
            _buffer.Write(bytes[..remaining]);
        }

        // The cap is reached: stop retaining and stream the remainder straight through.
        _captureComplete = true;
        Truncated = true;
    }
}
