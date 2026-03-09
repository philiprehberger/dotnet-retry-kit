namespace Philiprehberger.RetryKit;

public enum BackoffStrategy { Exponential, Linear, Fixed }

public class RetryOptions
{
    public int MaxAttempts { get; set; } = 3;
    public BackoffStrategy Backoff { get; set; } = BackoffStrategy.Exponential;
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
    public bool Jitter { get; set; } = true;
    public Func<Exception, bool>? RetryOn { get; set; }
    public Action<Exception, int>? OnRetry { get; set; }
    public Action<int>? OnSuccess { get; set; }
    public Action<Exception, int>? OnFailure { get; set; }
}

public class RetryError : Exception
{
    public int Attempts { get; }
    public Exception LastError { get; }

    public RetryError(int attempts, Exception lastError)
        : base($"All {attempts} attempts failed: {lastError.Message}", lastError)
    {
        Attempts = attempts;
        LastError = lastError;
    }
}

public static class Retry
{
    private static readonly Random Rng = new();

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
                    opts.OnRetry?.Invoke(ex, attempt);
                    var delay = CalculateDelay(attempt, opts);
                    await Task.Delay(delay, ct);
                }
            }
        }

        opts.OnFailure?.Invoke(lastError!, opts.MaxAttempts);
        throw new RetryError(opts.MaxAttempts, lastError!);
    }

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
                    opts.OnRetry?.Invoke(ex, attempt);
                    var delay = CalculateDelay(attempt, opts);
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

public static class Presets
{
    public static RetryOptions Aggressive => new()
    {
        MaxAttempts = 5,
        Backoff = BackoffStrategy.Exponential,
        InitialDelay = TimeSpan.FromMilliseconds(500),
        MaxDelay = TimeSpan.FromSeconds(5),
        Jitter = true,
    };

    public static RetryOptions Gentle => new()
    {
        MaxAttempts = 3,
        Backoff = BackoffStrategy.Exponential,
        InitialDelay = TimeSpan.FromSeconds(2),
        MaxDelay = TimeSpan.FromSeconds(30),
        Jitter = true,
    };

    public static RetryOptions NetworkRequest => new()
    {
        MaxAttempts = 3,
        Backoff = BackoffStrategy.Exponential,
        InitialDelay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(10),
        Jitter = true,
    };

    public static RetryOptions DatabaseQuery => new()
    {
        MaxAttempts = 3,
        Backoff = BackoffStrategy.Linear,
        InitialDelay = TimeSpan.FromMilliseconds(500),
        MaxDelay = TimeSpan.FromSeconds(5),
        Jitter = false,
    };
}
