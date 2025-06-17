namespace Swoq.ReplayViewer.Util;

/// <summary>
/// A Stream that reads from a file and blocks when at EOF until more data is written.
/// A bit like the Unix `tail -f` command, but it does start reading from the beginning of the file.
/// </summary>
public class TailStream : Stream
{
    private readonly FileStream fileStream;
    private readonly FileSystemWatcher watcher;
    private readonly AutoResetEvent dataAvailableEvent = new(false);
    private readonly TimeSpan pollingInterval;
    private bool disposed;

    /// <summary>
    /// Creates a new TailStream for the specified file path.
    /// </summary>
    /// <param name="filePath">The file to tail.</param>
    /// <param name="pollingInterval">Polling interval in case of missed watcher events. Defaults to 100ms.</param>
    public TailStream(string filePath, TimeSpan? pollingInterval = null)
    {
        this.pollingInterval = pollingInterval ?? TimeSpan.FromMilliseconds(100);

        // Open the file for reading, allowing others to write
        fileStream = new FileStream(
           filePath,
           FileMode.Open,
           FileAccess.Read,
           FileShare.ReadWrite,
           4096,
           FileOptions.SequentialScan
        );

        // Setup a watcher on the file's directory
        var directory = Path.GetDirectoryName(filePath) ?? throw new ArgumentException("File path must have a valid directory.", nameof(filePath));
        var fileName = Path.GetFileName(filePath);

        watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.Size | NotifyFilters.LastWrite
        };

        watcher.Changed += OnFileChanged;
        watcher.EnableRaisingEvents = true;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Signal that more data may be available
        dataAvailableEvent.Set();
    }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => true;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => fileStream.Position;
        set => fileStream.Position = value;
    }

    /// <inheritdoc />
    public override void Flush()
    {
        // No-op, as this is a read-only stream
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(disposed, nameof(TailStream));

        while (true)
        {
            int bytesRead;
            try
            {
                bytesRead = fileStream.Read(buffer, offset, count);
            }
            catch (ObjectDisposedException)
            {
                throw;
            }

            if (bytesRead > 0)
            {
                return bytesRead;
            }

            // No data available currently, wait until watcher signals
            // or the stream is disposed
            ObjectDisposedException.ThrowIf(disposed, nameof(TailStream));

            dataAvailableEvent.WaitOne(pollingInterval);
            ObjectDisposedException.ThrowIf(disposed, nameof(TailStream));

            // After being signaled, attempt to read again
            continue;
        }
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException("Seek is not supported in TailStream.");
    }

    /// <inheritdoc />
    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("Write is not supported in TailStream.");
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposed)
            return;

        if (disposing)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Changed -= OnFileChanged;
            watcher.Dispose();
            dataAvailableEvent.Set(); // Unblock any waiting Read calls
            dataAvailableEvent.Dispose();
            fileStream.Dispose();
        }

        disposed = true;
        base.Dispose(disposing);
    }
}
