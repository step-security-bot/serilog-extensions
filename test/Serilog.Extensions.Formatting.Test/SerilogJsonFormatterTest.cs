using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json.Nodes;
using Serilog.Events;
using Serilog.Parsing;
using Xunit.Abstractions;
using static Serilog.Events.LogEventLevel;

namespace Serilog.Extensions.Formatting.Test;

public class SerilogJsonFormatterTest(ITestOutputHelper output)
{
    private string FormatToJson(LogEvent @event)
    {
        var formatter = new Utf8JsonFormatter();
        var stringWriter = new StringWriter();
        formatter.Format(@event, stringWriter);
        string result = stringWriter.ToString();
        Helpers.AssertValidJson(result, output);
        return result;
    }

    private JsonObject FormatJson(LogEvent @event)
    {
        string json = FormatToJson(@event);
        return JsonNode.Parse(json)?.AsObject() ?? new JsonObject();
    }

    private class MyDictionary : Dictionary<string, object>;

    private static JsonObject FormatEvent(LogEvent e)
    {
        var j = new Utf8JsonFormatter();

        var f = new StringWriter();
        j.Format(e, f);
        return JsonNode.Parse(f.ToString())?.AsObject() ?? new JsonObject();
    }

    [Fact]
    public void ABooleanPropertySerializesAsBooleanValue()
    {
        string name = Some.String();
        const bool Value = true;
        var @event = Some.InformationEvent();
        @event.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(Value)));

        var formatted = FormatJson(@event);

        Assert.Equal(Value, (bool?)formatted["properties"]?[name]);
    }

    [Fact]
    public void ACharPropertySerializesAsStringValue()
    {
        string name = Some.String();
        const char Value = 'c';
        var @event = Some.InformationEvent();
        @event.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(Value)));

        var formatted = FormatJson(@event);

        Assert.Equal(Value.ToString(), (string?)formatted["properties"]?[name]);
    }

    [Fact]
    public void ADecimalSerializesAsNumericValue()
    {
        string name = Some.String();
        const decimal Value = 123.45m;
        var @event = Some.InformationEvent();
        @event.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(Value)));

        var formatted = FormatJson(@event);

        Assert.Equal(Value, (decimal?)formatted["properties"]?[name]);
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
    public void ADoubleSerializesAsNumericValue()
    {
        string name = Some.String();
        const double Value = 123.45;
        var @event = Some.InformationEvent();
        @event.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(Value)));

        var formatted = FormatJson(@event);

        Assert.Equal(Value, (double?)formatted["properties"]?[name]);
    }

    [Fact]
    public void AEnumPropertySerializesAsStringValue()
    {
        string name = Some.String();
        var value = TestEnum.Value1;
        var @event = Some.InformationEvent();
        @event.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(value)));

        var formatted = FormatJson(@event);

        Assert.Equal(value.ToString(), (string?)formatted["properties"]?[name]);

        value = TestEnum.asdf;
        @event.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(value)));

        formatted = FormatJson(@event);

        Assert.Equal(value.ToString(), (string?)formatted["properties"]?[name]);
    }

    [Fact]
    public void AFloatSerializesAsNumericValue()
    {
        string name = Some.String();
        const float Value = 123.45f;
        var @event = Some.InformationEvent();
        @event.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(Value)));

        var formatted = FormatJson(@event);

        Assert.Equal(Value, (float?)formatted["properties"]?[name]);
    }

    [Fact]
    public void AnIntegerPropertySerializesAsIntegerValue()
    {
        string name = Some.String();
        int value = Some.Int();
        var @event = Some.InformationEvent();
        @event.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(value)));

        var formatted = FormatJson(@event);

        Assert.Equal(value, (int?)formatted["properties"]?[name]);
    }

    [Fact]
    public void ASbyteSerializesAsNumericValue()
    {
        string name = Some.String();
        const sbyte Value = 123;
        var @event = Some.InformationEvent();
        @event.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(Value)));

        var formatted = FormatJson(@event);

        Assert.Equal(Value, (sbyte?)formatted["properties"]?[name]);
    }

    [Fact]
    public void ASequencePropertySerializesAsArrayValue()
    {
        string name = Some.String();
        int?[] ints = [Some.Int(), Some.Int()];
        var value = new SequenceValue(ints.Select(i => new ScalarValue(i)));
        var @event = Some.InformationEvent();
        @event.AddOrUpdateProperty(new LogEventProperty(name, value));

        var formatted = FormatJson(@event);
        var result = new List<int?>();
        foreach (var el in formatted["properties"]?[name]?.AsArray() ?? [])
        {
            result.Add((int?)el);
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

        var formatted = FormatJson(@event);
        int? result = (int?)formatted["properties"]?[structureProp.Name]?[memberProp.Name];
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
        var f = FormatJson(e);
        Assert.Equal("world", (string?)f["properties"]?["aDictionary"]?["hello"]);
        Assert.Equal(1.2, (double?)f["properties"]?["aDictionary"]?["nums"]?[0]);
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
        var f = FormatJson(e);

        Assert.Equal("world", (string?)f["properties"]?["aDictionary"]?["hello"]);
        Assert.Equal(1.2, (double?)f["properties"]?["aDictionary"]?["nums"]?[0]);
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

        var formatted = FormatJson(@event);
        Assert.Equal(
            "9999-12-31",
            (string?)formatted["properties"]!["name"]);
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

        var formatted = FormatJson(@event);

        Assert.Equal(
            "2013-03-11T15:59:00.1230000+10:00",
            (string?)formatted["timestamp"]);
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

        var formatted = FormatJson(@event);
        Assert.Equal(
            "23:59:59.9999999",
            (string?)formatted["properties"]?["name"]);
    }

    [Fact]
    public void PropertyTokensWithFormatStringsAreIncludedAsRenderings()
    {
        var p = new MessageTemplateParser();
        var e = new LogEvent(Some.OffsetInstant(), Information, null,
            p.Parse("{AProperty:000}"), new[] { new LogEventProperty("AProperty", new ScalarValue(12)) });

        var d = FormatEvent(e);

        var rs = d["renderings"]?.AsObject() ?? new JsonObject();
        Assert.Single(rs);
        var ap = d["renderings"]?["aProperty"];
        var fs = ap?.AsObject() ?? new JsonObject();
        Assert.Single(fs);
        Assert.Equal("000", (string?)fs["format"]);
        Assert.Equal("012", (string?)fs["rendering"]);
    }

    [Fact]
    public void PropertyTokensWithoutFormatStringsAreNotIncludedAsRenderings()
    {
        var p = new MessageTemplateParser();
        var e = new LogEvent(Some.OffsetInstant(), Information, null,
            p.Parse("{AProperty}"), new[] { new LogEventProperty("AProperty", new ScalarValue(12)) });

        var d = FormatEvent(e);

        var rs = (IEnumerable)d["renderings"]!;
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
        var f = FormatJson(e);

        Assert.Equal("world", (string?)f["properties"]?["aDictionary"]?["hello"]);
        Assert.Equal(1.2, (double?)f["properties"]?["aDictionary"]?["nums"]?[0]);
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

        var d = FormatEvent(e);

        string? h = (string?)d["properties"]?["aProperty"]?[0]?[0];
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
        Assert.DoesNotContain("traceId", formatted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("spanId", formatted, StringComparison.OrdinalIgnoreCase);
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

internal enum TestEnum
{
    Value1,

    // ReSharper disable once InconsistentNaming
    asdf,
}
