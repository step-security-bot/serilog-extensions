# Alexaka1.Serilog.Extensions.Formatting

## Utf8JsonFormatter

A simple JSON formatter for Serilog that uses the `System.Text.Json.Utf8JsonWriter` to write the log events to the output stream.

> [!IMPORTANT]
> This formatter currently does not support the `Renderings` property of Serilog.

### Usage

```csharp
var logger = new LoggerConfiguration()
    .WriteTo.File(new Utf8JsonFormatter(), "log.json")
    .CreateLogger();
```

```json5
{
  "Name": "Console",
  "Args": {
    "formatter": {
      "type": "Serilog.Extensions.Formatting.Utf8JsonFormatter, Serilog.Extensions.Formatting",
      // if you want to use a custom naming policy, you can specify it here
      "namingPolicy": "System.Text.Json.JsonNamingPolicy::CamelCase"
    }
  }
}
```

### Options

The `Utf8JsonFormatter` constructor accepts the following options:

- `closingDelimiter`: Closing delimiter of the log event. Defaults to `Environment.NewLine`.
- `renderMessage`: A boolean that determines whether the message template will be rendered. Defaults to `false`.
- `formatProvider`: An `IFormatProvider` that will be used to format the message template. Defaults to `CultureInfo.InvariantCulture`.
- `spanBufferSize`: The size of the buffer used to format the `ISpanFormattable` values. Defaults to `64`.
- `skipValidation`: A boolean that determines whether the JSON writer will skip validation. Defaults to `true`.
- `namingPolicy`: A `JsonNamingPolicy` that will be used to convert the property names. Default is leaving the property names as they are.
