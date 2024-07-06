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
    private Stream _stream2 = null!;
    private ILogger _utf8JsonLog = null!;

    [GlobalSetup]
    public void Setup()
    {
        _stream = new MemoryStream();
        _stream2 = new MemoryStream();
        _exception = new Exception("An Error");
        _jsonLog = new LoggerConfiguration()
            .WriteTo.Sink(new NullSink(new JsonFormatter(), new StreamWriter(_stream)))
            .CreateLogger();
        _utf8JsonLog = new LoggerConfiguration()
            .WriteTo.Sink(new NullSink(new Utf8JsonFormatter(skipValidation: true), new StreamWriter(_stream2)))
            .CreateLogger();
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _stream.Dispose();
        _stream2.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void EmitLogEvent()
    {
        _jsonLog.Information(_exception, "Hello, {Name}!", "World");
    }

    [Benchmark]
    public void EmitLogEventUtf8()
    {
        _jsonLog.Information(_exception, "Hello, {Name}!", "World");
    }

    [Benchmark]
    public void IntProperties()
    {
        _jsonLog.Information(_exception, "Hello, {A} {B} {C}!", 1, 2, 3);
    }

    [Benchmark]
    public void IntPropertiesUtf8()
    {
        _utf8JsonLog.Information(_exception, "Hello, {A} {B} {C}!", 1, 2, 3);
    }

    [Benchmark]
    public void ComplexProperties()
    {
        _jsonLog.Information(_exception, "Hello, {A} {@B} {C}!", new DateTime(1970, 1, 1),
            new { B = new DateTime(2000, 1, 1), C = new[] { 1, 2, 3 } }, new Dictionary<string, DateTime>
            {
                { "D", new DateTime(2000, 1, 1) },
                { "E", new DateTime(2000, 1, 1) },
                { "F", new DateTime(2000, 1, 1) },
            });
    }

    [Benchmark]
    public void ComplexPropertiesUtf8()
    {
        _utf8JsonLog.Information(_exception, "Hello, {A} {@B} {C}!", new DateTime(1970, 1, 1),
            new { B = new DateTime(2000, 1, 1), C = new[] { 1, 2, 3 } }, new Dictionary<string, DateTime>
            {
                { "D", new DateTime(2000, 1, 1) },
                { "E", new DateTime(2000, 1, 1) },
                { "F", new DateTime(2000, 1, 1) },
            });
    }
}
