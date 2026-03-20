namespace Philiprehberger.RetryKit;

/// <summary>
/// Specifies the backoff strategy used between retry attempts.
/// </summary>
public enum BackoffStrategy
{
    /// <summary>Delay doubles with each attempt.</summary>
    Exponential,
    /// <summary>Delay increases linearly with each attempt.</summary>
    Linear,
    /// <summary>Delay remains constant between attempts.</summary>
    Fixed
}

/// <summary>
/// Configuration options for retry behavior.
/// </summary>
public class RetryOptions
{
    /// <summary>Maximum number of attempts before giving up. Default is 3.</summary>
    public int MaxAttempts { get; set; } = 3;
    /// <summary>Backoff strategy between retries. Default is <see cref="BackoffStrategy.Exponential"/>.</summary>
    public BackoffStrategy Backoff { get; set; } = BackoffStrategy.Exponential;
    /// <summary>Initial delay before the first retry. Default is 1 second.</summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
    /// <summary>Maximum delay cap between retries. Default is 30 seconds.</summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
    /// <summary>Whether to apply random jitter to delays. Default is true.</summary>
    public bool Jitter { get; set; } = true;
    /// <summary>Optional predicate that determines whether an exception should be retried.</summary>
    public Func<Exception, bool>? RetryOn { get; set; }
    /// <summary>Callback invoked before each retry with the exception, attempt number, and delay.</summary>
    public Action<Exception, int, TimeSpan>? OnRetry { get; set; }
    /// <summary>Callback invoked on success with the attempt number.</summary>
    public Action<int>? OnSuccess { get; set; }
    /// <summary>Callback invoked after all attempts fail with the last exception and attempt count.</summary>
    public Action<Exception, int>? OnFailure { get; set; }
}

/// <summary>
/// Exception thrown when all retry attempts have been exhausted.
/// </summary>
public class RetryError : Exception
{
    /// <summary>Total number of attempts made before failure.</summary>
    public int Attempts { get; }
    /// <summary>The exception from the last failed attempt.</summary>
    public Exception LastError { get; }

    /// <summary>
    /// Initializes a new <see cref="RetryError"/> with the attempt count and last exception.
    /// </summary>
    /// <param name="attempts">Number of attempts made.</param>
    /// <param name="lastError">The exception from the final attempt.</param>
    public RetryError(int attempts, Exception lastError)
        : base($"All {attempts} attempts failed: {lastError.Message}", lastError)
    {
        Attempts = attempts;
        LastError = lastError;
    }
}

/// <summary>
/// Provides static methods for executing operations with automatic retry logic.
/// </summary>
public static class Retry
{
    private static readonly Random Rng = new();

    /// <summary>
    /// Executes an async operation with retry logic.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="fn">The async operation to execute.</param>
    /// <param name="options">Optional retry configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> fn,
        RetryOptions? options = null,
        CancellationToken ct = default)
    {
        var opts = options ?? new RetryOptions();
        Exception? lastError = null;

        for (int attempt = 1; attempt <= opts.MaxAttempts; attempt++)
        {
            try
            {
                var result = await fn(ct);
                opts.OnSuccess?.Invoke(attempt);
                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex;
                if (opts.RetryOn != null && !opts.RetryOn(ex)) throw;

                if (attempt < opts.MaxAttempts)
                {
                    var delay = CalculateDelay(attempt, opts);
                    opts.OnRetry?.Invoke(ex, attempt, delay);
                    await Task.Delay(delay, ct);
                }
            }
        }

        opts.OnFailure?.Invoke(lastError!, opts.MaxAttempts);
        throw new RetryError(opts.MaxAttempts, lastError!);
    }

    /// <summary>
    /// Executes a synchronous operation with retry logic.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="fn">The operation to execute.</param>
    /// <param name="options">Optional retry configuration.</param>
    /// <returns>The result of the operation.</returns>
    public static T Execute<T>(Func<T> fn, RetryOptions? options = null)
    {
        var opts = options ?? new RetryOptions();
        Exception? lastError = null;

        for (int attempt = 1; attempt <= opts.MaxAttempts; attempt++)
        {
            try
            {
                var result = fn();
                opts.OnSuccess?.Invoke(attempt);
                return result;
            }
            catch (Exception ex)
            {
                lastError = ex;
                if (opts.RetryOn != null && !opts.RetryOn(ex)) throw;

                if (attempt < opts.MaxAttempts)
                {
                    var delay = CalculateDelay(attempt, opts);
                    opts.OnRetry?.Invoke(ex, attempt, delay);
                    Thread.Sleep(delay);
                }
            }
        }

        opts.OnFailure?.Invoke(lastError!, opts.MaxAttempts);
        throw new RetryError(opts.MaxAttempts, lastError!);
    }

    /// <summary>
    /// Executes an async operation with retry logic, returning a fallback value if all attempts fail.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="fn">The async operation to execute.</param>
    /// <param name="fallbackValue">Value to return if all attempts fail.</param>
    /// <param name="options">Optional retry configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the operation or the fallback value.</returns>
    public static async Task<T> ExecuteWithFallbackAsync<T>(
        Func<CancellationToken, Task<T>> fn,
        T fallbackValue,
        RetryOptions? options = null,
        CancellationToken ct = default)
    {
        var opts = options ?? new RetryOptions();
        Exception? lastError = null;

        for (int attempt = 1; attempt <= opts.MaxAttempts; attempt++)
        {
            try
            {
                var result = await fn(ct);
                opts.OnSuccess?.Invoke(attempt);
                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex;
                if (opts.RetryOn != null && !opts.RetryOn(ex)) throw;

                if (attempt < opts.MaxAttempts)
                {
                    var delay = CalculateDelay(attempt, opts);
                    opts.OnRetry?.Invoke(ex, attempt, delay);
                    await Task.Delay(delay, ct);
                }
            }
        }

        opts.OnFailure?.Invoke(lastError!, opts.MaxAttempts);
        return fallbackValue;
    }

    /// <summary>
    /// Executes a synchronous operation with retry logic, returning a fallback value if all attempts fail.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="fn">The operation to execute.</param>
    /// <param name="fallbackValue">Value to return if all attempts fail.</param>
    /// <param name="options">Optional retry configuration.</param>
    /// <returns>The result of the operation or the fallback value.</returns>
    public static T ExecuteWithFallback<T>(
        Func<T> fn,
        T fallbackValue,
        RetryOptions? options = null)
    {
        var opts = options ?? new RetryOptions();
        Exception? lastError = null;

        for (int attempt = 1; attempt <= opts.MaxAttempts; attempt++)
        {
            try
            {
                var result = fn();
                opts.OnSuccess?.Invoke(attempt);
                return result;
            }
            catch (Exception ex)
            {
                lastError = ex;
                if (opts.RetryOn != null && !opts.RetryOn(ex)) throw;

                if (attempt < opts.MaxAttempts)
                {
                    var delay = CalculateDelay(attempt, opts);
                    opts.OnRetry?.Invoke(ex, attempt, delay);
                    Thread.Sleep(delay);
                }
            }
        }

        opts.OnFailure?.Invoke(lastError!, opts.MaxAttempts);
        return fallbackValue;
    }

    /// <summary>
    /// Executes an async operation with retry logic and a per-attempt timeout.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="fn">The async operation to execute.</param>
    /// <param name="timeout">Maximum duration for each individual attempt.</param>
    /// <param name="options">Optional retry configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public static async Task<T> ExecuteWithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> fn,
        TimeSpan timeout,
        RetryOptions? options = null,
        CancellationToken ct = default)
    {
        var opts = options ?? new RetryOptions();
        Exception? lastError = null;

        for (int attempt = 1; attempt <= opts.MaxAttempts; attempt++)
        {
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            try
            {
                var result = await fn(linkedCts.Token);
                opts.OnSuccess?.Invoke(attempt);
                return result;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested)
            {
                lastError = new TimeoutException($"Attempt {attempt} timed out after {timeout.TotalMilliseconds}ms", ex);
                if (opts.RetryOn != null && !opts.RetryOn(lastError)) throw lastError;

                if (attempt < opts.MaxAttempts)
                {
                    var delay = CalculateDelay(attempt, opts);
                    opts.OnRetry?.Invoke(lastError, attempt, delay);
                    await Task.Delay(delay, ct);
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
                if (opts.RetryOn != null && !opts.RetryOn(ex)) throw;

                if (attempt < opts.MaxAttempts)
                {
                    var delay = CalculateDelay(attempt, opts);
                    opts.OnRetry?.Invoke(ex, attempt, delay);
                    await Task.Delay(delay, ct);
                }
            }
        }

        opts.OnFailure?.Invoke(lastError!, opts.MaxAttempts);
        if (lastError is TimeoutException)
            throw lastError;
        throw new RetryError(opts.MaxAttempts, lastError!);
    }

    /// <summary>
    /// Executes with retry, but only retries when the exception matches the predicate.
    /// Non-matching exceptions are thrown immediately.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="shouldRetry">Predicate that determines whether the exception is retryable.</param>
    /// <param name="options">Optional retry configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public static async Task<T> ExecuteIfAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        Func<Exception, bool> shouldRetry,
        RetryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var opts = options ?? new RetryOptions();
        Exception? lastError = null;

        for (int attempt = 1; attempt <= opts.MaxAttempts; attempt++)
        {
            try
            {
                var result = await operation(cancellationToken);
                opts.OnSuccess?.Invoke(attempt);
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;

                if (!shouldRetry(ex))
                    throw;

                if (attempt < opts.MaxAttempts)
                {
                    var delay = CalculateDelay(attempt, opts);
                    opts.OnRetry?.Invoke(ex, attempt, delay);
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        opts.OnFailure?.Invoke(lastError!, opts.MaxAttempts);
        throw new RetryError(opts.MaxAttempts, lastError!);
    }

    /// <summary>
    /// Executes with retry, but only retries when the exception matches the predicate.
    /// Non-matching exceptions are thrown immediately.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The synchronous operation to execute.</param>
    /// <param name="shouldRetry">Predicate that determines whether the exception is retryable.</param>
    /// <param name="options">Optional retry configuration.</param>
    /// <returns>The result of the operation.</returns>
    public static T ExecuteIf<T>(
        Func<T> operation,
        Func<Exception, bool> shouldRetry,
        RetryOptions? options = null)
    {
        var opts = options ?? new RetryOptions();
        Exception? lastError = null;

        for (int attempt = 1; attempt <= opts.MaxAttempts; attempt++)
        {
            try
            {
                var result = operation();
                opts.OnSuccess?.Invoke(attempt);
                return result;
            }
            catch (Exception ex)
            {
                lastError = ex;

                if (!shouldRetry(ex))
                    throw;

                if (attempt < opts.MaxAttempts)
                {
                    var delay = CalculateDelay(attempt, opts);
                    opts.OnRetry?.Invoke(ex, attempt, delay);
                    Thread.Sleep(delay);
                }
            }
        }

        opts.OnFailure?.Invoke(lastError!, opts.MaxAttempts);
        throw new RetryError(opts.MaxAttempts, lastError!);
    }

    private static TimeSpan CalculateDelay(int attempt, RetryOptions opts)
    {
        double delayMs = opts.Backoff switch
        {
            BackoffStrategy.Exponential => opts.InitialDelay.TotalMilliseconds * Math.Pow(2, attempt - 1),
            BackoffStrategy.Linear => opts.InitialDelay.TotalMilliseconds * attempt,
            _ => opts.InitialDelay.TotalMilliseconds,
        };

        delayMs = Math.Min(delayMs, opts.MaxDelay.TotalMilliseconds);

        if (opts.Jitter)
            delayMs *= 0.5 + Rng.NextDouble() * 0.5;

        return TimeSpan.FromMilliseconds(delayMs);
    }
}

/// <summary>
/// Provides pre-configured <see cref="RetryOptions"/> for common use cases.
/// </summary>
public static class Presets
{
    /// <summary>Aggressive retry: 5 attempts, 500ms initial delay, 5s max, exponential with jitter.</summary>
    public static RetryOptions Aggressive => new()
    {
        MaxAttempts = 5,
        Backoff = BackoffStrategy.Exponential,
        InitialDelay = TimeSpan.FromMilliseconds(500),
        MaxDelay = TimeSpan.FromSeconds(5),
        Jitter = true,
    };

    /// <summary>Gentle retry: 3 attempts, 2s initial delay, 30s max, exponential with jitter.</summary>
    public static RetryOptions Gentle => new()
    {
        MaxAttempts = 3,
        Backoff = BackoffStrategy.Exponential,
        InitialDelay = TimeSpan.FromSeconds(2),
        MaxDelay = TimeSpan.FromSeconds(30),
        Jitter = true,
    };

    /// <summary>Network request preset: 3 attempts, 1s initial delay, 10s max, exponential with jitter.</summary>
    public static RetryOptions NetworkRequest => new()
    {
        MaxAttempts = 3,
        Backoff = BackoffStrategy.Exponential,
        InitialDelay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(10),
        Jitter = true,
    };

    /// <summary>Database query preset: 3 attempts, 500ms initial delay, 5s max, linear without jitter.</summary>
    public static RetryOptions DatabaseQuery => new()
    {
        MaxAttempts = 3,
        Backoff = BackoffStrategy.Linear,
        InitialDelay = TimeSpan.FromMilliseconds(500),
        MaxDelay = TimeSpan.FromSeconds(5),
        Jitter = false,
    };
}
