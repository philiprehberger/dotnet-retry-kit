# Changelog

## 0.3.2 (2026-03-22)

- Add dates to changelog entries

## 0.3.1 (2026-03-20)

- Add API section to README
- Add LangVersion and TreatWarningsAsErrors to csproj

## 0.3.0 (2026-03-16)

- Add `ExecuteIfAsync` and `ExecuteIf` for conditional retry based on exception type
- Add `CircuitBreaker.GetMetrics()` for observability

## 0.2.3 (2026-03-16)

- Add Development section to README
- Add GenerateDocumentationFile and RepositoryType to .csproj

## 0.2.0 (2026-03-13)

- Add `ExecuteWithFallback` and `ExecuteWithFallbackAsync` for returning default values after retry exhaustion
- Add `ExecuteWithTimeoutAsync` for per-attempt timeout with automatic retry
- Add enhanced `OnRetry` callback now receives attempt number and delay

## 0.1.1 (2026-03-10)

- Add README to NuGet package so it displays on nuget.org

## 0.1.0 (2026-03-09)

- Initial release
- `Retry.ExecuteAsync<T>` and `Retry.Execute<T>` with configurable options
- Exponential, linear, and fixed backoff strategies with optional jitter
- `CircuitBreaker` with Closed/Open/HalfOpen states
- Built-in presets: Aggressive, Gentle, NetworkRequest, DatabaseQuery
