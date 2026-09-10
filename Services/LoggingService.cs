using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace AirGestureAI.Services
{
    /// <summary>
    /// Structured, thread-safe logging service utilizing an asynchronous Channel queue
    /// for high-performance buffered disk writing, rolling policies, compression, and UI subscription.
    /// </summary>
    public sealed class LoggingService : IAsyncDisposable
    {
        private readonly string _logsDirectory;
        private readonly Channel<LogEntry> _logChannel;
        private readonly ConcurrentBag<ILogSubscriber> _subscribers = new();
        private readonly Task _writerTask;
        private readonly CancellationTokenSource _cts = new();

        private string _currentDateString;
        private int _currentRotationIndex = 0;
        private StreamWriter? _currentWriter;
        private long _maxFileSizeBytes = 10 * 1024 * 1024; // Default: 10MB
        private int _retentionDays = 7; // Default: 7 days retention

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggingService"/> class.
        /// </summary>
        /// <param name="baseDirectory">The base directory where the Logs folder will be created.</param>
        public LoggingService(string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentException("Base directory cannot be null or empty.", nameof(baseDirectory));

            _logsDirectory = Path.Combine(baseDirectory, "Logs");
            Directory.CreateDirectory(_logsDirectory);

            _currentDateString = DateTime.UtcNow.ToString("yyyyMMdd");

            // Configure bounded or unbounded channel. Bounded helps prevent out-of-memory if disk is stuck.
            _logChannel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(10000)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false
            });

            _writerTask = Task.Run(ProcessLogQueueAsync);

            // Clean up old log files at startup
            Task.Run(CleanOldLogsQuietly);
        }

        /// <summary>
        /// Gets or sets the maximum size in bytes of a single log file before it rolls over.
        /// </summary>
        public long MaxFileSizeBytes
        {
            get => _maxFileSizeBytes;
            set => _maxFileSizeBytes = value > 0 ? value : throw new ArgumentException("Size must be positive.");
        }

        /// <summary>
        /// Gets or sets the log retention duration in days.
        /// </summary>
        public int RetentionDays
        {
            get => _retentionDays;
            set => _retentionDays = value > 0 ? value : throw new ArgumentException("Retention days must be positive.");
        }

        /// <summary>
        /// Adds a UI subscriber to receive live log messages.
        /// </summary>
        /// <param name="subscriber">The subscriber instance.</param>
        public void AddSubscriber(ILogSubscriber subscriber)
        {
            if (subscriber != null)
            {
                _subscribers.Add(subscriber);
            }
        }

        /// <summary>
        /// Logs a structured entry at the Trace level.
        /// </summary>
        public void Trace(string message, string subsystem = "", string correlationId = "") =>
            Log(LogLevel.Trace, message, subsystem, correlationId);

        /// <summary>
        /// Logs a structured entry at the Debug level.
        /// </summary>
        public void Debug(string message, string subsystem = "", string correlationId = "") =>
            Log(LogLevel.Debug, message, subsystem, correlationId);

        /// <summary>
        /// Logs a structured entry at the Information level.
        /// </summary>
        public void Information(string message, string subsystem = "", string correlationId = "") =>
            Log(LogLevel.Information, message, subsystem, correlationId);

        /// <summary>
        /// Logs a structured entry at the Warning level.
        /// </summary>
        public void Warning(string message, string subsystem = "", string correlationId = "") =>
            Log(LogLevel.Warning, message, subsystem, correlationId);

        /// <summary>
        /// Logs a structured entry at the Error level.
        /// </summary>
        public void Error(string message, Exception? exception = null, string subsystem = "", string correlationId = "") =>
            Log(LogLevel.Error, message, subsystem, correlationId, exception);

        /// <summary>
        /// Logs a structured entry at the Critical level.
        /// </summary>
        public void Critical(string message, Exception? exception = null, string subsystem = "", string correlationId = "") =>
            Log(LogLevel.Critical, message, subsystem, correlationId, exception);

        /// <summary>
        /// Enqueues a log entry to the asynchronous processing channel.
        /// </summary>
        /// <param name="level">The severity level.</param>
        /// <param name="message">The message body.</param>
        /// <param name="subsystem">The associated subsystem label.</param>
        /// <param name="correlationId">The correlation identifier.</param>
        /// <param name="exception">An optional exception to include.</param>
        public void Log(LogLevel level, string message, string subsystem = "", string correlationId = "", Exception? exception = null)
        {
            var entry = new LogEntry
            {
                Level = level,
                Message = message,
                Subsystem = subsystem,
                CorrelationId = correlationId,
                ExceptionInfo = exception != null ? $"{exception.GetType().FullName}: {exception.Message}\n{exception.StackTrace}" : null
            };

            // Post to channel
            _logChannel.Writer.TryWrite(entry);

            // Broadcast immediately to live UI subscribers
            foreach (var subscriber in _subscribers)
            {
                if (level >= subscriber.MinimumLevel)
                {
                    try
                    {
                        subscriber.OnLogEntry(entry);
                    }
                    catch
                    {
                        // Protect logging pipeline from subscriber failures
                    }
                }
            }
        }

        /// <summary>
        /// Searches all local log files and active log buffer entries for matches against specified filters.
        /// </summary>
        public async Task<IReadOnlyList<LogEntry>> SearchLogsAsync(
            string? keyword,
            LogLevel? minLevel,
            DateTime? from,
            DateTime? to,
            CancellationToken cancellationToken = default)
        {
            var results = new List<LogEntry>();

            await Task.Run(() =>
            {
                var files = Directory.GetFiles(_logsDirectory, "airgesture_*.log");
                Array.Sort(files); // Read in chronological order

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var reader = new StreamReader(fs, Encoding.UTF8);
                        string? line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            if (TryParseLogLine(line, out var entry) && entry != null)
                            {
                                if (MatchesFilter(entry, keyword, minLevel, from, to))
                                {
                                    results.Add(entry);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Silently ignore individual file reading errors to keep search running
                    }
                }
            }, cancellationToken).ConfigureAwait(false);

            return results.AsReadOnly();
        }

        /// <summary>
        /// Exports filtered logs to a structured CSV file.
        /// </summary>
        public async Task ExportToCsvAsync(string outputPath, string? keyword, LogLevel? minLevel, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
        {
            var entries = await SearchLogsAsync(keyword, minLevel, from, to, cancellationToken).ConfigureAwait(false);

            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(fs, Encoding.UTF8);

            await writer.WriteLineAsync("TimestampUtc,LogLevel,Subsystem,CorrelationId,Message,ExceptionInfo").ConfigureAwait(false);

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var csvLine = $"\"{entry.TimestampUtc:O}\",\"{entry.Level}\",\"{EscapeCsv(entry.Subsystem)}\",\"{EscapeCsv(entry.CorrelationId)}\",\"{EscapeCsv(entry.Message)}\",\"{EscapeCsv(entry.ExceptionInfo ?? string.Empty)}\"";
                await writer.WriteLineAsync(csvLine).ConfigureAwait(false);
            }

            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Exports filtered logs to a structured JSON file.
        /// </summary>
        public async Task ExportToJsonAsync(string outputPath, string? keyword, LogLevel? minLevel, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
        {
            var entries = await SearchLogsAsync(keyword, minLevel, from, to, cancellationToken).ConfigureAwait(false);

            var options = new JsonSerializerOptions { WriteIndented = true };
            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(fs, entries, options, cancellationToken).ConfigureAwait(false);
        }

        private async Task ProcessLogQueueAsync()
        {
            var reader = _logChannel.Reader;

            try
            {
                while (await reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
                {
                    while (reader.TryRead(out var entry))
                    {
                        EnsureWriterState();
                        if (_currentWriter != null)
                        {
                            var serialized = SerializeLogEntry(entry);
                            await _currentWriter.WriteLineAsync(serialized).ConfigureAwait(false);
                        }
                    }

                    if (_currentWriter != null)
                    {
                        await _currentWriter.FlushAsync().ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal exit on token cancel
            }
            finally
            {
                // Write any remaining entries
                while (reader.TryRead(out var entry))
                {
                    EnsureWriterState();
                    if (_currentWriter != null)
                    {
                        var serialized = SerializeLogEntry(entry);
                        _currentWriter.WriteLine(serialized);
                    }
                }

                CloseWriter();
            }
        }

        private void EnsureWriterState()
        {
            var dateStr = DateTime.UtcNow.ToString("yyyyMMdd");
            bool rolloverNeeded = false;

            if (_currentWriter != null && dateStr != _currentDateString)
            {
                rolloverNeeded = true;
            }
            else if (_currentWriter != null)
            {
                _currentWriter.Flush();
                var baseStream = _currentWriter.BaseStream;
                if (baseStream != null && baseStream.Length >= _maxFileSizeBytes)
                {
                    rolloverNeeded = true;
                }
            }

            if (_currentWriter == null || rolloverNeeded)
            {
                CloseWriter();

                if (rolloverNeeded && dateStr == _currentDateString)
                {
                    _currentRotationIndex++;
                }
                else if (dateStr != _currentDateString)
                {
                    // Trigger asynchronous compression of yesterday's logs
                    var prevDateStr = _currentDateString;
                    Task.Run(() => CompressLogFilesForDateQuietly(prevDateStr));

                    _currentDateString = dateStr;
                    _currentRotationIndex = 0;
                }

                string filePath;
                do
                {
                    var suffix = _currentRotationIndex == 0 ? "" : $"_{_currentRotationIndex}";
                    filePath = Path.Combine(_logsDirectory, $"airgesture_{_currentDateString}{suffix}.log");
                    if (_currentRotationIndex > 0 && File.Exists(filePath) && new FileInfo(filePath).Length >= _maxFileSizeBytes)
                    {
                        _currentRotationIndex++;
                    }
                    else
                    {
                        break;
                    }
                } while (true);

                var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                _currentWriter = new StreamWriter(fs, Encoding.UTF8);
            }
        }

        private void CloseWriter()
        {
            if (_currentWriter != null)
            {
                try
                {
                    _currentWriter.Flush();
                    _currentWriter.Dispose();
                }
                catch
                {
                    // Ignored
                }
                _currentWriter = null;
            }
        }

        private string SerializeLogEntry(LogEntry entry)
        {
            // Format log entries as structured JSON lines for easy structured parsing
            return JsonSerializer.Serialize(entry, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        }

        private bool TryParseLogLine(string line, out LogEntry? entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(line)) return false;

            try
            {
                if (line.StartsWith("{") && line.EndsWith("}"))
                {
                    entry = JsonSerializer.Deserialize<LogEntry>(line);
                    return entry != null;
                }
            }
            catch
            {
                // Fall back if line corrupted
            }

            return false;
        }

        private bool MatchesFilter(LogEntry entry, string? keyword, LogLevel? minLevel, DateTime? from, DateTime? to)
        {
            if (minLevel.HasValue && entry.Level < minLevel.Value) return false;
            if (from.HasValue && entry.TimestampUtc < from.Value.ToUniversalTime()) return false;
            if (to.HasValue && entry.TimestampUtc > to.Value.ToUniversalTime()) return false;

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                bool matchesMsg = entry.Message.Contains(keyword, StringComparison.OrdinalIgnoreCase);
                bool matchesSub = entry.Subsystem.Contains(keyword, StringComparison.OrdinalIgnoreCase);
                bool matchesExc = entry.ExceptionInfo?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false;
                bool matchesCorr = entry.CorrelationId?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false;
                return matchesMsg || matchesSub || matchesExc || matchesCorr;
            }

            return true;
        }

        private void CleanOldLogsQuietly()
        {
            try
            {
                var files = Directory.GetFiles(_logsDirectory, "airgesture_*.*");
                var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTimeUtc < cutoff)
                    {
                        fileInfo.Delete();
                    }
                }
            }
            catch
            {
                // Silence cleanup errors
            }
        }

        private void CompressLogFilesForDateQuietly(string dateString)
        {
            try
            {
                var files = Directory.GetFiles(_logsDirectory, $"airgesture_{dateString}*.log");
                foreach (var file in files)
                {
                    var compressedPath = file + ".gz";
                    if (File.Exists(compressedPath)) continue;

                    using (var originalFileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var compressedFileStream = File.Create(compressedPath))
                    using (var compressionStream = new GZipStream(compressedFileStream, CompressionMode.Compress))
                    {
                        originalFileStream.CopyTo(compressionStream);
                    }

                    // Delete original log file after successful compression
                    File.Delete(file);
                }
            }
            catch
            {
                // Silence compression errors
            }
        }

        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\"", "\"\"");
        }

        /// <summary>
        /// Disposes asynchronously by draining the channel and shutting down background tasks.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            _logChannel.Writer.Complete();

            _cts.Cancel();

            try
            {
                await _writerTask.ConfigureAwait(false);
            }
            catch
            {
                // Ignore background task termination exceptions
            }

            _cts.Dispose();
        }
    }
}
