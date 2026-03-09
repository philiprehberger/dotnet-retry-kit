# Changelog

## 0.1.0 (2026-03-09)

- Initial release
- `Retry.ExecuteAsync<T>` and `Retry.Execute<T>` with configurable options
- Exponential, linear, and fixed backoff strategies with optional jitter
- `CircuitBreaker` with Closed/Open/HalfOpen states
- Built-in presets: Aggressive, Gentle, NetworkRequest, DatabaseQuery
