# Changelog

## 0.1.1 (2026-03-10)

- Add README to NuGet package so it displays on nuget.org

## 0.1.0 (2026-03-09)

- Initial release
- `Retry.ExecuteAsync<T>` and `Retry.Execute<T>` with configurable options
- Exponential, linear, and fixed backoff strategies with optional jitter
- `CircuitBreaker` with Closed/Open/HalfOpen states
- Built-in presets: Aggressive, Gentle, NetworkRequest, DatabaseQuery
