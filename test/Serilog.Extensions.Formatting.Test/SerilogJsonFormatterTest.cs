using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Newtonsoft.Json;
using Serilog.Events;
using Serilog.Formatting.Json;
using Serilog.Parsing;
using Xunit.Abstractions;
using static Serilog.Events.LogEventLevel;

namespace Serilog.Extensions.Formatting.Test;

public class SerilogJsonFormatterTest
{
    public SerilogJsonFormatterTest(ITestOutputHelper output)
    {
        _output = output;
    }

    private readonly ITestOutputHelper _output;
    private string FormatToJson(LogEvent @event)
    {
        var formatter = new Utf8JsonFormatter();
        var output = new StringWriter();
        formatter.Format(@event, output);
        string result = output.ToString();
        Helpers.AssertValidJson(result, _output);
        return result;
    }

    // static dynamic FormatJson(LogEvent @event)
    // {
    //     string output = FormatToJson(@event);
    //     return JsonSerializer.Deserialize<dynamic>(output)!;
    // }
    private dynamic FormatJson(LogEvent @event)
    {
        string output = FormatToJson(@event);
        var serializer = new JsonSerializer { DateParseHandling = DateParseHandling.None };
        return serializer.Deserialize(new JsonTextReader(new StringReader(output)))!;
    }

    private class MyDictionary : Dictionary<string, object>;

    // static dynamic FormatEvent(LogEvent e)
    // {
    //     var j = new JsonFormatter();
    //
    //     var f = new StringWriter();
    //     j.Format(e, f);
    //
    //     return JsonSerializer.Deserialize<dynamic>(f.ToString())!;
    // }
    private static dynamic FormatEvent(LogEvent e)
    {
        var j = new JsonFormatter();

        var f = new StringWriter();
        j.Format(e, f);

        return JsonConvert.DeserializeObject<dynamic>(f.ToString())!;
    }

    [Fact]
    public void ABooleanPropertySerializesAsBooleanValue()
    {
        string name = Some.String();
        const bool Value = true;
        var @event = Some.InformationEvent();
        @event.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(Value)));

        dynamic formatted = FormatJson(@event);

        Assert.Equal(Value, (bool)formatted.properties[name]);
    }

    [Fact]
    public void ACharPropertySerializesAsStringValue()
    {
        string name = Some.String();
        const char Value = 'c';
        var @event = Some.InformationEvent();
        @event.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(Value)));

        dynamic formatted = FormatJson(@event);

        Assert.Equal(Value.ToString(), (string)formatted.properties[name]);
    }

    [Fact]
    public void ADecimalSerializesAsNumericValue()
    {
        string name = Some.String();
        const decimal Value = 123.45m;
        var @event = Some.InformationEvent();
        @event.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(Value)));

        dynamic formatted = FormatJson(@event);

        Assert.Equal(Value, (decimal)formatted.properties[name]);
    }

    [Fact]
    public void ADictionaryWithScalarKeySerializesAsAnObject()
    {
        int dictKey = Some.Int();
        int dictValue = Some.Int();
        var dict = new DictionaryValue(new Dictionary<ScalarValue, LogEventPropertyValue>
        {
            { new ScalarValue(dictKey), new ScalarValue(dictValue) },
        });
        var dictProp = new LogEventProperty(Some.String(), dict);
        var @event = Some.InformationEvent();
        @event.AddOrUpdateProperty(dictProp);

        string formatted = FormatToJson(@event);
        string expected = $$"""{"{{dictKey}}":{{dictValue}}}""";
        Assert.Contains(expected, formatted);
    }

    [Fact]
    public void AnIntegerPropertySerializesAsIntegerValue()
    {
        string name = Some.String();
        int value = Some.Int();
        var @event = Some.InformationEvent();
        @event.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(value)));

        dynamic formatted = FormatJson(@event);

        Assert.Equal(value, (int)formatted.properties[name]);
    }

    [Fact]
    public void ASequencePropertySerializesAsArrayValue()
    {
        string name = Some.String();
        int[] ints = [Some.Int(), Some.Int()];
        var value = new SequenceValue(ints.Select(i => new ScalarValue(i)));
        var @event = Some.InformationEvent();
        @event.AddOrUpdateProperty(new LogEventProperty(name, value));

        dynamic formatted = FormatJson(@event);
        var result = new List<int>();
        foreach (dynamic? el in formatted.properties[name])
        {
            result.Add((int)el);
        }

        Assert.Equal(ints, result);
    }

    [Fact]
    public void AStructureSerializesAsAnObject()
    {
        int value = Some.Int();
        var memberProp = new LogEventProperty(Some.String(), new ScalarValue(value));
        var structure = new StructureValue(new[] { memberProp });
        var structureProp = new LogEventProperty(Some.String(), structure);
        var @event = Some.InformationEvent();
        @event.AddOrUpdateProperty(structureProp);

        dynamic formatted = FormatJson(@event);
        int result = (int)formatted.properties[structureProp.Name][memberProp.Name];
        Assert.Equal(value, result);
    }

    [Fact]
    public void CustomDictionariesAreDestructuredViaDictionaryValue_When_AsDictionary_Applied()
    {
        var dict = new MyDictionary
        {
            { "hello", "world" },
            { "nums", new[] { 1.2 } },
        };

        var e = DelegatingSink.GetLogEvent(l => l.Information("Value is {ADictionary}", dict),
            cfg => cfg.Destructure.AsDictionary<MyDictionary>());
        dynamic f = FormatJson(e);
        Assert.Equal("world", (string)f.properties.ADictionary["hello"]);
        Assert.Equal(1.2, (double)f.properties.ADictionary.nums[0]);
    }

    [Fact]
    public void CustomDictionariesAreDestructuredViaDictionaryValue_When_AsDictionary_Applied_IsValid()
    {
        var dict = new MyDictionary
        {
            { "hello", "world" },
            { "nums", new[] { 1.2 } },
        };

        var e = DelegatingSink.GetLogEvent(l => l.Information("Value is {ADictionary}", dict),
            cfg => cfg.Destructure.AsDictionary<MyDictionary>());
        string json = FormatToJson(e);
        Helpers.AssertValidJson(json);
    }

    [Fact]
    public void DictionariesAreDestructuredViaDictionaryValue()
    {
        var dict = new Dictionary<string, object>
        {
            { "hello", "world" },
            { "nums", new[] { 1.2 } },
        };

        var e = DelegatingSink.GetLogEvent(l => l.Information("Value is {ADictionary}", dict));
        dynamic f = FormatJson(e);

        Assert.Equal("world", (string)f.properties.ADictionary["hello"]);
        Assert.Equal(1.2, (double)f.properties.ADictionary.nums[0]);
    }

    [Fact]
    public void JsonFormattedDateOnly()
    {
        var @event = new LogEvent(
            DateTimeOffset.MaxValue,
            Information,
            null,
            Some.MessageTemplate(),
            new[] { new LogEventProperty("name", new ScalarValue(DateOnly.MaxValue)) });

        dynamic formatted = FormatJson(@event);
        Assert.Equal(
            "9999-12-31",
            (string)formatted.properties.name);
    }

    [Fact]
    public void JsonFormattedEventsIncludeTimestamp()
    {
        var @event = new LogEvent(
            new DateTimeOffset(2013, 3, 11, 15, 59, 0, 123, TimeSpan.FromHours(10)),
            Information,
            null,
            Some.MessageTemplate(),
            Array.Empty<LogEventProperty>());

        dynamic formatted = FormatJson(@event);

        Assert.Equal(
            "2013-03-11T15:59:00.1230000+10:00",
            (string)formatted.timestamp);
    }

    [Fact]
    public void JsonFormattedTimeOnly()
    {
        var @event = new LogEvent(
            DateTimeOffset.MaxValue,
            Information,
            null,
            Some.MessageTemplate(),
            new[] { new LogEventProperty("name", new ScalarValue(TimeOnly.MaxValue)) });

        dynamic formatted = FormatJson(@event);
        Assert.Equal(
            "23:59:59.9999999",
            (string)formatted.properties.name);
    }

    [Fact]
    public void PropertyTokensWithFormatStringsAreIncludedAsRenderings()
    {
        var p = new MessageTemplateParser();
        var e = new LogEvent(Some.OffsetInstant(), Information, null,
            p.Parse("{AProperty:000}"), new[] { new LogEventProperty("AProperty", new ScalarValue(12)) });

        dynamic d = FormatEvent(e);

        dynamic[] rs = ((IEnumerable)d.Renderings).Cast<dynamic>().ToArray();
        Assert.Single(rs);
        dynamic? ap = d.Renderings.AProperty;
        dynamic[] fs = ((IEnumerable)ap).Cast<dynamic>().ToArray();
        Assert.Single(fs);
        Assert.Equal("000", (string)fs.Single().Format);
        Assert.Equal("012", (string)fs.Single().Rendering);
    }

    [Fact]
    public void PropertyTokensWithoutFormatStringsAreNotIncludedAsRenderings()
    {
        var p = new MessageTemplateParser();
        var e = new LogEvent(Some.OffsetInstant(), Information, null,
            p.Parse("{AProperty}"), new[] { new LogEventProperty("AProperty", new ScalarValue(12)) });

        dynamic d = FormatEvent(e);

        var rs = (IEnumerable)d.Renderings;
        Assert.Null(rs);
    }

    [Fact]
    public void ReadonlyDictionariesAreDestructuredViaDictionaryValue()
    {
        var dict = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>
        {
            { "hello", "world" },
            { "nums", new[] { 1.2 } },
        });

        var e = DelegatingSink.GetLogEvent(l => l.Information("Value is {ADictionary}", dict));
        dynamic f = FormatJson(e);

        Assert.Equal("world", (string)f.properties.ADictionary["hello"]);
        Assert.Equal(1.2, (double)f.properties.ADictionary.nums[0]);
    }

    [Fact] // See https://github.com/serilog/serilog/issues/1924
    public void RenderedMessageIsIncludedCorrectlyWhenRequired()
    {
        var p = new MessageTemplateParser();
        var e = new LogEvent(Some.OffsetInstant(), Information, null,
            p.Parse("value: {AProperty}"), new[] { new LogEventProperty("AProperty", new ScalarValue(12)) });

        var formatter = new Utf8JsonFormatter(renderMessage: true);

        var buffer = new StringWriter();
        formatter.Format(e, buffer);
        string json = buffer.ToString();

        Assert.Contains(""","messageTemplate":"value: {AProperty}","renderedMessage":"value: 12",""", json);
    }

    [Fact]
    public void SequencesOfSequencesAreSerialized()
    {
        var p = new MessageTemplateParser();
        var e = new LogEvent(Some.OffsetInstant(), Information, null,
            p.Parse("{@AProperty}"),
            new[]
            {
                new LogEventProperty("AProperty", new SequenceValue([new SequenceValue([new ScalarValue("Hello")])])),
            });

        dynamic d = FormatEvent(e);

        string? h = (string)d.Properties.AProperty[0][0];
        Assert.Equal("Hello", h);
    }

    [Fact]
    public void TraceAndSpanAreIgnoredWhenAbsent()
    {
        var evt = Some.LogEvent(traceId: default, spanId: default);
        var sw = new StringWriter();
        var formatter = new Utf8JsonFormatter();
        formatter.Format(evt, sw);
        string formatted = sw.ToString();
        Assert.DoesNotContain("traceId", formatted);
        Assert.DoesNotContain("spanId", formatted);
    }

    [Fact]
    public void TraceAndSpanAreIncludedWhenPresent()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var evt = Some.LogEvent(traceId: traceId, spanId: spanId);
        var sw = new StringWriter();
        var formatter = new Utf8JsonFormatter();
        formatter.Format(evt, sw);
        string formatted = sw.ToString();
        Assert.Contains($"""
            "traceId":"{traceId}"
            """, formatted);
        Assert.Contains($"""
            "spanId":"{spanId}"
            """, formatted);
    }
}
