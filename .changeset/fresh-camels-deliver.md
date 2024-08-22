---
"alexaka1.serilog.extensions.formatting": minor
---

Add `Renderings` property to the output JSON, bringing it to feature parity with `JsonFormatter`

Add `Encoder` parameter to `Utf8JsonFormatter` constructor. If you are sure that the consumer is going to interpret the JSON as UTF-8, you can set this to `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` to not escape non-ASCII characters.
