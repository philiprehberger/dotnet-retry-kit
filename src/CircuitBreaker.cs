namespace Philiprehberger.RetryKit;

public enum CircuitState { Closed, Open, HalfOpen }

/// <summary>
/// Snapshot of circuit breaker metrics at a point in time.
/// </summary>
/// <param name="TotalAttempts">Total number of calls made through the circuit breaker.</param>
/// <param name="FailureCount">Number of failed calls.</param>
/// <param name="SuccessCount">Number of successful calls.</param>
/// <param name="State">Current state of the circuit breaker.</param>
public record CircuitBreakerMetrics(
    int TotalAttempts,
    int FailureCount,
    int SuccessCount,
    CircuitState State);

public class CircuitOpenException : Exception
{
    public CircuitOpenException() : base("Circuit breaker is open — request rejected") { }
}

public class CircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _resetTimeout;
    private readonly int _halfOpenMaxAttempts;
    private readonly Action<CircuitState, CircuitState>? _onStateChange;
    private readonly Action<int>? _onCircuitOpen;
    private readonly object _lock = new();

    private CircuitState _state = CircuitState.Closed;
    private int _failures;
    private int _successes;
    private int _totalAttempts;
    private DateTime _lastFailureTime;
    private int _halfOpenAttempts;

    public CircuitBreaker(
        int failureThreshold = 5,
        int resetTimeoutSeconds = 30,
        int halfOpenMaxAttempts = 1,
        Action<CircuitState, CircuitState>? onStateChange = null,
        Action<int>? onCircuitOpen = null)
    {
        _failureThreshold = failureThreshold;
        _resetTimeout = TimeSpan.FromSeconds(resetTimeoutSeconds);
        _halfOpenMaxAttempts = halfOpenMaxAttempts;
        _onStateChange = onStateChange;
        _onCircuitOpen = onCircuitOpen;
    }

    public CircuitState State
    {
        get { lock (_lock) return _state; }
    }

    /// <summary>
    /// Returns current circuit breaker metrics including total attempts, failure count,
    /// success count, and current state.
    /// </summary>
    /// <returns>A snapshot of the current circuit breaker metrics.</returns>
    public CircuitBreakerMetrics GetMetrics()
    {
        lock (_lock)
        {
            return new CircuitBreakerMetrics(
                TotalAttempts: _totalAttempts,
                FailureCount: _failures,
                SuccessCount: _successes,
                State: _state);
        }
    }

    private void Transition(CircuitState to)
    {
        if (_state != to)
        {
            var from = _state;
            _state = to;
            _onStateChange?.Invoke(from, to);
        }
    }

    public T Call<T>(Func<T> fn)
    {
        lock (_lock)
        {
            _totalAttempts++;

            if (_state == CircuitState.Open)
            {
                if (DateTime.UtcNow - _lastFailureTime >= _resetTimeout)
                {
                    Transition(CircuitState.HalfOpen);
                    _halfOpenAttempts = 0;
                }
                else throw new CircuitOpenException();
            }

            if (_state == CircuitState.HalfOpen && _halfOpenAttempts >= _halfOpenMaxAttempts)
                throw new CircuitOpenException();

            if (_state == CircuitState.HalfOpen)
                _halfOpenAttempts++;
        }

        try
        {
            var result = fn();
            lock (_lock)
            {
                _successes++;
                if (_state == CircuitState.HalfOpen)
                    Transition(CircuitState.Closed);
                _failures = 0;
            }
            return result;
        }
        catch
        {
            lock (_lock)
            {
                _failures++;
                _lastFailureTime = DateTime.UtcNow;

                if (_state == CircuitState.HalfOpen || _failures >= _failureThreshold)
                {
                    Transition(CircuitState.Open);
                    _onCircuitOpen?.Invoke(_failures);
                }
            }
            throw;
        }
    }

    public async Task<T> CallAsync<T>(Func<Task<T>> fn)
    {
        lock (_lock)
        {
            _totalAttempts++;

            if (_state == CircuitState.Open)
            {
                if (DateTime.UtcNow - _lastFailureTime >= _resetTimeout)
                {
                    Transition(CircuitState.HalfOpen);
                    _halfOpenAttempts = 0;
                }
                else throw new CircuitOpenException();
            }

            if (_state == CircuitState.HalfOpen && _halfOpenAttempts >= _halfOpenMaxAttempts)
                throw new CircuitOpenException();

            if (_state == CircuitState.HalfOpen)
                _halfOpenAttempts++;
        }

        try
        {
            var result = await fn();
            lock (_lock)
            {
                _successes++;
                if (_state == CircuitState.HalfOpen)
                    Transition(CircuitState.Closed);
                _failures = 0;
            }
            return result;
        }
        catch
        {
            lock (_lock)
            {
                _failures++;
                _lastFailureTime = DateTime.UtcNow;

                if (_state == CircuitState.HalfOpen || _failures >= _failureThreshold)
                {
                    Transition(CircuitState.Open);
                    _onCircuitOpen?.Invoke(_failures);
                }
            }
            throw;
        }
    }
}
