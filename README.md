# Philiprehberger.RetryKit

Configurable retry logic and circuit breaker for .NET — exponential backoff, jitter, and built-in presets.

## Install

```bash
dotnet add package Philiprehberger.RetryKit
```

## Usage

### Retry

```csharp
using Philiprehberger.RetryKit;

// Async retry with defaults (3 attempts, exponential backoff)
var result = await Retry.ExecuteAsync(async ct =>
{
    var response = await httpClient.GetAsync("/api/data", ct);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadAsStringAsync(ct);
});

// Synchronous retry
var value = Retry.Execute(() => SomeOperation());

// Custom options
var result = await Retry.ExecuteAsync(async ct =>
{
    return await FetchData(ct);
}, new RetryOptions
{
    MaxAttempts = 5,
    Backoff = BackoffStrategy.Linear,
    InitialDelay = TimeSpan.FromMilliseconds(500),
    MaxDelay = TimeSpan.FromSeconds(10),
    Jitter = true,
    OnRetry = (ex, attempt) => Console.WriteLine($"Retry {attempt}: {ex.Message}"),
});

// Use presets
var data = await Retry.ExecuteAsync(ct => FetchData(ct), Presets.Aggressive);
```

### Circuit Breaker

```csharp
var breaker = new CircuitBreaker(
    failureThreshold: 5,
    resetTimeoutSeconds: 30,
    onStateChange: (from, to) => Console.WriteLine($"{from} -> {to}")
);

// Synchronous
var result = breaker.Call(() => SomeOperation());

// Async
var data = await breaker.CallAsync(async () => await FetchData());
```

## Presets

| Preset | Attempts | Backoff | Initial Delay | Max Delay |
|--------|----------|---------|---------------|-----------|
| `Aggressive` | 5 | Exponential | 500ms | 5s |
| `Gentle` | 3 | Exponential | 2s | 30s |
| `NetworkRequest` | 3 | Exponential | 1s | 10s |
| `DatabaseQuery` | 3 | Linear | 500ms | 5s |

## License

MIT
