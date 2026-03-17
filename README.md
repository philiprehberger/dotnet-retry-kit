# Philiprehberger.RetryKit

[![CI](https://github.com/philiprehberger/dotnet-retry-kit/actions/workflows/ci.yml/badge.svg)](https://github.com/philiprehberger/dotnet-retry-kit/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Philiprehberger.RetryKit.svg)](https://www.nuget.org/packages/Philiprehberger.RetryKit)
[![License](https://img.shields.io/github/license/philiprehberger/dotnet-retry-kit)](LICENSE)

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
    OnRetry = (ex, attempt, delay) => Console.WriteLine($"Retry {attempt}: {ex.Message} (next in {delay.TotalMilliseconds}ms)"),
});

// Use presets
var data = await Retry.ExecuteAsync(ct => FetchData(ct), Presets.Aggressive);
```

### Fallback

Return a default value instead of throwing when all retries are exhausted:

```csharp
// Async fallback — returns "default" if all attempts fail
var result = await Retry.ExecuteWithFallbackAsync(
    async ct => await FetchData(ct),
    fallbackValue: "default",
    new RetryOptions { MaxAttempts = 3 }
);

// Synchronous fallback
var value = Retry.ExecuteWithFallback(
    () => LoadConfig(),
    fallbackValue: new Config { UseDefaults = true }
);
```

### Conditional Retry

Only retry specific exception types — non-matching exceptions are thrown immediately:

```csharp
// Only retry transient HTTP errors, fail fast on 4xx client errors
var result = await Retry.ExecuteIfAsync(
    async ct =>
    {
        var response = await httpClient.GetAsync("/api/data", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    },
    shouldRetry: ex => ex is HttpRequestException { StatusCode: >= System.Net.HttpStatusCode.InternalServerError },
    new RetryOptions { MaxAttempts = 3 }
);

// Synchronous version
var value = Retry.ExecuteIf(
    () => LoadFromDatabase(),
    shouldRetry: ex => ex is TimeoutException or IOException
);
```

### Per-Attempt Timeout

Each attempt gets its own timeout. If an attempt times out, it counts as a failure and triggers retry:

```csharp
var result = await Retry.ExecuteWithTimeoutAsync(
    async ct =>
    {
        var response = await httpClient.GetAsync("/api/data", ct);
        return await response.Content.ReadAsStringAsync(ct);
    },
    timeout: TimeSpan.FromSeconds(5),
    new RetryOptions { MaxAttempts = 3 }
);
```

### Enhanced OnRetry Callback

The `OnRetry` callback receives the exception, attempt number, and the computed delay before the next attempt:

```csharp
var options = new RetryOptions
{
    MaxAttempts = 5,
    OnRetry = (ex, attempt, delay) =>
        Console.WriteLine($"Attempt {attempt} failed: {ex.Message}. Retrying in {delay.TotalMilliseconds}ms..."),
};
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

// Check metrics
var metrics = breaker.GetMetrics();
Console.WriteLine($"State: {metrics.State}, Successes: {metrics.SuccessCount}, Failures: {metrics.FailureCount}");
```

## Presets

| Preset | Attempts | Backoff | Initial Delay | Max Delay |
|--------|----------|---------|---------------|-----------|
| `Aggressive` | 5 | Exponential | 500ms | 5s |
| `Gentle` | 3 | Exponential | 2s | 30s |
| `NetworkRequest` | 3 | Exponential | 1s | 10s |
| `DatabaseQuery` | 3 | Linear | 500ms | 5s |

## Development

```bash
dotnet build src/Philiprehberger.RetryKit.csproj --configuration Release
```

## License

MIT
