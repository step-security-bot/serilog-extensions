using System.Globalization;
using System.Text;
using System.Text.Json;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Parsing;

namespace Serilog.Extensions.Formatting;

/// <summary>
///     Formats log events in a simple JSON structure using <see cref="System.Text.Json.Utf8JsonWriter" />.
///     Instances of this class are safe for concurrent access by multiple threads.
/// </summary>
/// <remarks>
///     This formatter formats using camelCase keys. For properties,
///     it simply converts the first character to lower, using the provided format provider
/// </remarks>
public class Utf8JsonFormatter(
    string? closingDelimiter = null,
    bool renderMessage = false,
    IFormatProvider? formatProvider = null,
    int bufferSize = 64) : ITextFormatter
{
    private readonly string _closingDelimiter = closingDelimiter ?? Environment.NewLine;
    private readonly CultureInfo _formatProvider = formatProvider as CultureInfo ?? CultureInfo.InvariantCulture;
    private const string NoQuotingOfStrings = "l";


    /// <inheritdoc />
    public void Format(LogEvent? logEvent, TextWriter? output)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(output);
        Stream str;
        if (output is StreamWriter streamWriter)
        {
            str = streamWriter.BaseStream;
        }
        else
        {
            str = new MemoryStream();
        }

        using var writer = CreateWriter(str, new JsonWriterOptions { Indented = false, SkipValidation = true });
        writer.WriteStartObject();
        writer.WriteString("timestamp"u8, logEvent.Timestamp.ToString("O", formatProvider));
        writer.WriteString("level"u8, Enum.GetName(logEvent.Level));
        writer.WriteString("messageTemplate"u8, logEvent.MessageTemplate.Text);
        if (renderMessage)
        {
            writer.WriteString("renderedMessage"u8, logEvent.MessageTemplate.Render(logEvent.Properties));
        }

        if (logEvent.TraceId != null)
        {
            writer.WriteString("traceId"u8, logEvent.TraceId.ToString());
        }

        if (logEvent.SpanId != null)
        {
            writer.WriteString("spanId"u8, logEvent.SpanId.ToString());
        }

        if (logEvent.Exception != null)
        {
            writer.WriteString("exception"u8, logEvent.Exception.ToString());
        }

        if (logEvent.Properties.Count != 0)
        {
            writer.WriteStartObject("properties"u8);
            foreach (var property in logEvent.Properties)
            {
                writer.WritePropertyName([char.ToLowerInvariant(property.Key[0]), ..property.Key[1..]]);
                Visit(property.Value, writer);
            }

            writer.WriteEndObject();
        }

        //
        // var tokensWithFormat = logEvent.MessageTemplate.Tokens
        //     .OfType<PropertyToken>()
        //     .Where(pt => pt.Format != null)
        //     .GroupBy(pt => pt.PropertyName)
        //     .ToArray().AsSpan();
        //
        // if (tokensWithFormat.Length != 0)
        // {
        //     writer.WriteStartObject("renderings"u8);
        //     WriteRenderingsValues(tokensWithFormat, logEvent.Properties, writer);
        //     writer.WriteEndObject();
        // }

        writer.WriteEndObject();
        writer.Flush();
        if (output is not StreamWriter && str is MemoryStream mem)
        {
            // if we used memory stream, we wrote to the memory stream, so we need to write to the output manually
            using (mem)
            {
                output.Write(Encoding.UTF8.GetString(mem.ToArray()).AsSpan());
            }
        }

        output.Write(_closingDelimiter);
    }

    public virtual Utf8JsonWriter CreateWriter(Stream stream, JsonWriterOptions options)
    {
        return new Utf8JsonWriter(stream, options);
    }

    private void Visit<TState>(TState? value, Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(value);
        switch (value)
        {
            case ScalarValue sv:
                VisitScalarValue(sv, writer);
                return;
            case SequenceValue seqv:
                VisitSequenceValue(seqv, writer);
                return;
            case StructureValue strv:
                VisitStructureValue(strv, writer);
                return;
            case DictionaryValue dictv:
                VisitDictionaryValue(dictv, writer);
                return;
            default:
                throw new NotSupportedException($"The value {value} is not of a type supported by this visitor.");
        }
    }

    private void VisitDictionaryValue(DictionaryValue? value, Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartObject();
        foreach (var element in value.Elements)
        {
            if (element.Key.Value?.ToString() is { } key)
            {
                writer.WritePropertyName([char.ToLower(key[0], _formatProvider), ..key[1..]]);
            }
            else
            {
                writer.WritePropertyName("null"u8);
            }

            Visit(element.Value, writer);
        }

        writer.WriteEndObject();
    }

    private void VisitStructureValue(StructureValue? value, Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartObject();
        foreach (var property in value.Properties)
        {
            writer.WritePropertyName([char.ToLower(property.Name[0], _formatProvider), ..property.Name[1..]]);
            Visit(property.Value, writer);
        }

        if (value.TypeTag is not null)
        {
            writer.WriteString("_typeTag"u8, value.TypeTag);
        }

        writer.WriteEndObject();
    }

    private void VisitSequenceValue(SequenceValue? value, Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartArray();
        foreach (var element in value.Elements)
        {
            Visit(element, writer);
        }

        writer.WriteEndArray();
    }

    private void VisitScalarValue(ScalarValue? value, Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(value);
        switch (value.Value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string str:
                writer.WriteStringValue(str);
                break;
            case ValueType vt:
                switch (vt)
                {
                    case bool b:
                        writer.WriteBooleanValue(b);
                        break;
                    case DateTime dt:
                        writer.WriteStringValue(dt);
                        break;
                    case DateTimeOffset dto:
                        writer.WriteStringValue(dto);
                        break;
                    case char c:
                    {
                        writer.WriteStringValue([c]);
                        break;
                    }
                    case TimeSpan c:
                    {
                        Span<char> buffer = stackalloc char[bufferSize];
                        if (c.TryFormat(buffer, out int written, formatProvider: _formatProvider,
                                format: default))
                        {
                            writer.WriteStringValue(buffer[..written]);
                        }

                        break;
                    }
                    case DateOnly c:
                    {
                        Span<char> buffer = stackalloc char[bufferSize];
                        if (c.TryFormat(buffer, out int written, provider: _formatProvider,
                                format: "yyyy-MM-dd"))
                        {
                            writer.WriteStringValue(buffer[..written]);
                        }

                        break;
                    }
                    case TimeOnly c:
                    {
                        Span<char> buffer = stackalloc char[bufferSize];
                        if (c.TryFormat(buffer, out int written, provider: _formatProvider,
                                format: "O"))
                        {
                            writer.WriteStringValue(buffer[..written]);
                        }

                        break;
                    }
                    case not null when vt.GetType().IsEnum:
                    {
                        writer.WriteStringValue(vt.ToString());
                        break;
                    }
                    case ISpanFormattable span:
                    {
                        Span<char> buffer = stackalloc char[bufferSize];
                        if (span.TryFormat(buffer, out int written, provider: _formatProvider,
                                format: default))
                        {
                            writer.WriteRawValue(buffer[..written]);
                        }

                        break;
                    }
                }

                break;
            default:
                writer.WriteStringValue(value.Value?.ToString());
                break;
        }
    }

    private void WriteRenderingsValues(Span<IGrouping<string, PropertyToken>> tokensWithFormat,
        IReadOnlyDictionary<string, LogEventPropertyValue> properties, Utf8JsonWriter writer)
    {
        foreach (var propertyFormats in tokensWithFormat)
        {
            writer.WriteStartArray(propertyFormats.Key);
            foreach (var format in propertyFormats)
            {
                writer.WriteStartObject();
                writer.WriteString("format"u8, format.Format);
                writer.WritePropertyName("rendering"u8);
                RenderPropertyToken(format, properties, writer, _formatProvider, true, false);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }
    }

    private void RenderPropertyToken(PropertyToken pt, IReadOnlyDictionary<string, LogEventPropertyValue> properties,
        Utf8JsonWriter output, IFormatProvider formatProvider, bool isLiteral, bool isJson)
    {
        if (!properties.TryGetValue(pt.PropertyName, out var propertyValue))
        {
            output.WriteStringValue(pt.ToString());
            return;
        }

        if (!pt.Alignment.HasValue)
        {
            RenderValue(propertyValue, isLiteral, isJson, output, pt.Format, formatProvider);
        }
    }

    private void RenderValue(LogEventPropertyValue propertyValue, bool literal, bool json, Utf8JsonWriter output,
        string? format, IFormatProvider formatProvider)
    {
        if (literal && propertyValue is ScalarValue { Value: string str })
        {
            output.WriteStringValue(str);
        }
        else if (json && format == null)
        {
            Visit(propertyValue, output);
        }
        else
        {
            Render(propertyValue, output, format, formatProvider);
        }
    }

    // these should no longer be json
    private void Render(LogEventPropertyValue? value, Utf8JsonWriter output, string? format = null,
        IFormatProvider? formatProvider = null)
    {
        ArgumentNullException.ThrowIfNull(value);

        switch (value)
        {
            case ScalarValue sv:
                RenderScalarValue(sv, output, format, formatProvider);
                return;
            case SequenceValue seqv:
                RenderSequenceValue(seqv, output, format, formatProvider);
                return;
            case StructureValue strv:
                RenderStructureValue(strv, output, format, formatProvider);
                return;
            case DictionaryValue dictv:
                RenderDictionaryValue(dictv, output, format, formatProvider);
                return;
        }
    }

    private void RenderDictionaryValue(DictionaryValue value, Utf8JsonWriter output, string? format,
        IFormatProvider? formatProvider)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(value);
        output.WriteStartObject();
        foreach (var element in value.Elements)
        {
            if (element.Key.Value?.ToString() is { } key)
            {
                output.WritePropertyName([char.ToLower(key[0], _formatProvider), ..key[1..]]);
            }
            else
            {
                output.WritePropertyName("null"u8);
            }

            Render(element.Value, output, format, formatProvider);
        }
    }

    private void RenderStructureValue(StructureValue? value, Utf8JsonWriter output, string? format,
        IFormatProvider? formatProvider)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(value);
        if (value.TypeTag is not null)
        {
            output.WriteRawValue(value.TypeTag);
            output.WriteRawValue([' ']);
        }

        output.WriteStartObject();
        foreach (var property in value.Properties)
        {
            output.WriteRawValue([char.ToLower(property.Name[0], _formatProvider), ..property.Name[1..]]);
            Render(property.Value, output, format, formatProvider);
        }
    }

    private void RenderSequenceValue(SequenceValue? value, Utf8JsonWriter output, string? format,
        IFormatProvider? formatProvider)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(value);
        output.WriteStartArray();
        foreach (var element in value.Elements)
        {
            Render(element, output, format, formatProvider);
        }

        output.WriteEndArray();
    }

    private void RenderScalarValue(ScalarValue v, Utf8JsonWriter output, string? format,
        IFormatProvider? formatProvider)
    {
        object? value = v.Value;
        ArgumentNullException.ThrowIfNull(output);
        switch (value)
        {
            case null:
                output.WriteRawValue("null"u8);
                return;
            case string s:
            {
                if (format != NoQuotingOfStrings)
                {
                    output.WriteRawValue(['"', ..s.Replace("\"", "\\\""), '"']);
                }
                else
                {
                    output.WriteRawValue(s);
                }

                return;
            }
        }

        var custom = (ICustomFormatter?)formatProvider?.GetFormat(typeof(ICustomFormatter));
        if (custom != null)
        {
            output.WriteRawValue(custom.Format(format, value, formatProvider));
            return;
        }

        if (value is IFormattable f)
        {
            output.WriteStringValue(f.ToString(format, formatProvider ?? _formatProvider));
        }
        else
        {
            output.WriteStringValue(value.ToString() ?? "null");
        }
    }
}
