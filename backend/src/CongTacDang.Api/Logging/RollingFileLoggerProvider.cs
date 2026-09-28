using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace CongTacDang.Api.Logging;

/// <summary>Cấu hình log ra file (section <c>Logging:File</c>).</summary>
public sealed class RollingFileLoggerOptions
{
    /// <summary>Bật/tắt log ra file.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Thư mục chứa file log. Rỗng → thư mục <c>logs</c> cạnh (không nằm trong) thư mục publish.</summary>
    public string? Path { get; set; }

    /// <summary>Số ngày giữ file log; file cũ hơn bị xóa khi sang ngày mới và khi khởi động.</summary>
    public int RetainedDays { get; set; } = 30;

    /// <summary>Tiền tố tên file: <c>{FilePrefix}-yyyyMMdd.log</c>.</summary>
    public string FilePrefix { get; set; } = "congtacdang";

    /// <summary>Số dòng log tối đa chờ ghi; vượt quá thì bỏ dòng cũ nhất để không chặn request.</summary>
    public int QueueCapacity { get; set; } = 10_000;

    public string ResolveDirectory()
    {
        if (!string.IsNullOrWhiteSpace(Path))
            return System.IO.Path.GetFullPath(Path);

        var baseDir = AppContext.BaseDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        var parent = Directory.GetParent(baseDir)?.FullName ?? baseDir;
        return System.IO.Path.Combine(parent, "logs");
    }
}

/// <summary>
/// Ghi log có cấu trúc (JSON mỗi dòng) ra file xoay vòng theo ngày, giữ tối đa N ngày.
/// Dùng provider tự viết để không phải thêm thư viện ngoài; ghi bất đồng bộ qua hàng đợi nền.
/// Scope (ví dụ <c>CorrelationId</c>) được gắn vào từng dòng log.
/// </summary>
[ProviderAlias("File")]
public sealed class RollingFileLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private static readonly JsonWriterOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = false
    };

    private readonly RollingFileLoggerOptions _options;
    private readonly string _directory;
    private readonly Channel<(DateTimeOffset Timestamp, string Line)> _queue;
    private readonly Task _writerTask;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new(StringComparer.Ordinal);
    private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

    public RollingFileLoggerProvider(RollingFileLoggerOptions options)
    {
        _options = options;
        _directory = options.ResolveDirectory();
        Directory.CreateDirectory(_directory);

        _queue = Channel.CreateBounded<(DateTimeOffset, string)>(new BoundedChannelOptions(Math.Max(100, options.QueueCapacity))
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });
        _writerTask = Task.Run(ProcessQueueAsync);
    }

    /// <summary>Thư mục log thực tế sau khi phân giải cấu hình.</summary>
    public string LogDirectory => _directory;

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new FileLogger(name, this));

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

    public void Dispose()
    {
        _queue.Writer.TryComplete();
        try
        {
            _writerTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Không để lỗi ghi log làm hỏng quá trình tắt ứng dụng.
        }
    }

    private void Enqueue(DateTimeOffset timestamp, string line) => _queue.Writer.TryWrite((timestamp, line));

    private async Task ProcessQueueAsync()
    {
        StreamWriter? writer = null;
        DateOnly? currentDay = null;
        try
        {
            DeleteExpiredFiles();
            await foreach (var (timestamp, line) in _queue.Reader.ReadAllAsync())
            {
                var day = DateOnly.FromDateTime(timestamp.LocalDateTime);
                if (writer == null || currentDay != day)
                {
                    if (writer != null)
                    {
                        await writer.DisposeAsync();
                        DeleteExpiredFiles();
                    }

                    try
                    {
                        writer = OpenWriter(day);
                        currentDay = day;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        // Không mở được file (đĩa đầy, thiếu quyền...) → bỏ dòng này, thử lại ở dòng sau.
                        writer = null;
                        continue;
                    }
                }

                try
                {
                    await writer.WriteLineAsync(line);
                    if (_queue.Reader.Count == 0)
                        await writer.FlushAsync();
                }
                catch (IOException)
                {
                    // Lỗi đĩa: mở lại file ở dòng tiếp theo.
                    await writer.DisposeAsync();
                    writer = null;
                }
            }
        }
        finally
        {
            if (writer != null)
                await writer.DisposeAsync();
        }
    }

    private StreamWriter OpenWriter(DateOnly day)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, $"{_options.FilePrefix}-{day:yyyyMMdd}.log");
        var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        return new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = false };
    }

    private void DeleteExpiredFiles()
    {
        if (_options.RetainedDays <= 0)
            return;

        try
        {
            var cutoff = DateOnly.FromDateTime(DateTime.Now).AddDays(-_options.RetainedDays);
            var prefix = _options.FilePrefix + "-";
            foreach (var file in Directory.EnumerateFiles(_directory, prefix + "*.log"))
            {
                var stamp = Path.GetFileNameWithoutExtension(file)[prefix.Length..];
                if (DateOnly.TryParseExact(stamp, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fileDay)
                    && fileDay < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _category;
        private readonly RollingFileLoggerProvider _provider;

        public FileLogger(string category, RollingFileLoggerProvider provider)
        {
            _category = category;
            _provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            _provider._scopeProvider.Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var timestamp = DateTimeOffset.Now;
            using var buffer = new MemoryStream();
            using (var json = new Utf8JsonWriter(buffer, JsonOptions))
            {
                json.WriteStartObject();
                json.WriteString("Timestamp", timestamp);
                json.WriteString("Level", logLevel.ToString());
                json.WriteString("Category", _category);
                if (eventId.Id != 0 || eventId.Name != null)
                {
                    json.WriteNumber("EventId", eventId.Id);
                    if (eventId.Name != null)
                        json.WriteString("EventName", eventId.Name);
                }

                json.WriteString("Message", formatter(state, exception));
                if (exception != null)
                    json.WriteString("Exception", exception.ToString());

                var written = new HashSet<string>(StringComparer.Ordinal) { "Timestamp", "Level", "Category", "EventId", "EventName", "Message", "Exception" };

                if (state is IEnumerable<KeyValuePair<string, object?>> properties)
                {
                    json.WriteStartObject("State");
                    foreach (var (key, value) in properties)
                    {
                        if (key == "{OriginalFormat}")
                            continue;
                        json.WriteString(key, Convert.ToString(value, CultureInfo.InvariantCulture));
                    }
                    json.WriteEndObject();
                }

                _provider._scopeProvider.ForEachScope((scope, writer) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object?>> pairs)
                    {
                        foreach (var (key, value) in pairs)
                        {
                            if (key == "{OriginalFormat}" || !written.Add(key))
                                continue;
                            writer.WriteString(key, Convert.ToString(value, CultureInfo.InvariantCulture));
                        }
                    }
                }, json);

                json.WriteEndObject();
            }

            _provider.Enqueue(timestamp, Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length));
        }
    }
}

public static class RollingFileLoggerExtensions
{
    /// <summary>Đăng ký log console có cấu trúc (JSON, kèm scope) và log file xoay vòng theo cấu hình <c>Logging:File</c>.</summary>
    public static ILoggingBuilder AddConfiguredLogging(this ILoggingBuilder logging, IConfiguration configuration, IHostEnvironment environment)
    {
        var consoleJson = configuration.GetValue<bool?>("Logging:Console:Json") ?? !environment.IsDevelopment();
        if (consoleJson)
        {
            logging.AddJsonConsole(options =>
            {
                options.IncludeScopes = true;
                options.UseUtcTimestamp = true;
                options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
            });
        }
        else
        {
            logging.AddSimpleConsole(options => options.IncludeScopes = true);
        }

        var fileOptions = new RollingFileLoggerOptions();
        configuration.GetSection("Logging:File").Bind(fileOptions);
        if (fileOptions.Enabled)
        {
            try
            {
                logging.AddProvider(new RollingFileLoggerProvider(fileOptions));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Không tạo được thư mục log (thiếu quyền...) → vẫn chạy với log console.
                Console.Error.WriteLine(
                    $"Không thể ghi log ra thư mục '{fileOptions.ResolveDirectory()}': {ex.Message}. Chỉ ghi log ra console.");
            }
        }

        return logging;
    }
}
