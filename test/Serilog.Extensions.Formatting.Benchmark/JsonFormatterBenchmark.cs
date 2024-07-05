using BenchmarkDotNet.Attributes;
using Serilog.Formatting.Json;

namespace Serilog.Extensions.Formatting.Benchmark;

[SimpleJob]
[MemoryDiagnoser]
public class JsonFormatterBenchmark
{
    private Exception _exception = null!;
    private ILogger _jsonLog = null!;
    private Stream _stream = null!;
    private ILogger _utf8JsonLog = null!;

    [ParamsAllValues]
    public Loggers Logger { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _stream = new MemoryStream();
        _exception = new Exception("An Error");
        _jsonLog = new LoggerConfiguration()
            .WriteTo.Sink(new NullSink(new JsonFormatter(), new StreamWriter(_stream)))
            .CreateLogger();
        _utf8JsonLog = new LoggerConfiguration()
            .WriteTo.Sink(new NullSink(new Utf8JsonFormatter(), new StreamWriter(_stream)))
            .CreateLogger();
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _stream.Dispose();
    }

    [Benchmark]
    public void EmitLogEvent()
    {
        switch (Logger)
        {
            case Loggers.Json:
                _jsonLog.Information(_exception, "Hello, {Name}!", "World");
                break;
            case Loggers.Utf8Json:
                _utf8JsonLog.Information(_exception, "Hello, {Name}!", "World");
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    [Benchmark]
    public void IntProperties()
    {
        switch (Logger)
        {
            case Loggers.Json:
                _jsonLog.Information(_exception, "Hello, {A} {B} {C}!", 1, 2, 3);
                break;
            case Loggers.Utf8Json:
                _utf8JsonLog.Information(_exception, "Hello, {A} {B} {C}!", 1, 2, 3);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    [Benchmark]
    public void ComplexProperties()
    {
        switch (Logger)
        {
            case Loggers.Json:
                _jsonLog.Information(_exception, "Hello, {A} {@B} {C}!", new DateTime(1970, 1, 1),
                    new { B = new DateTime(2000, 1, 1), C = new[] { 1, 2, 3 } }, new Dictionary<string, DateTime>
                    {
                        { "D", new DateTime(2000, 1, 1) },
                        { "E", new DateTime(2000, 1, 1) },
                        { "F", new DateTime(2000, 1, 1) },
                    });
                break;
            case Loggers.Utf8Json:
                _utf8JsonLog.Information(_exception, "Hello, {A} {@B} {C}!", new DateTime(1970, 1, 1),
                    new { B = new DateTime(2000, 1, 1), C = new[] { 1, 2, 3 } }, new Dictionary<string, DateTime>
                    {
                        { "D", new DateTime(2000, 1, 1) },
                        { "E", new DateTime(2000, 1, 1) },
                        { "F", new DateTime(2000, 1, 1) },
                    });
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public enum Loggers
    {
        Json,
        Utf8Json,
    }
}
