using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace BusterWood.Restarting
{
    /// <summary>A service that may be restarted on failure</summary>
    public interface IRestartable
    {
        /// <summary>The task that is running</summary>
        /// <remarks>needed so we can monitor and re-start on error</remarks>
        Task MonitoredTask { get; }

        /// <summary>The implemeter should pause the requested amout of time</summary>
        Task PauseBeforeRestart(TimeSpan delay);

        /// <summary>Start the service, typically re-loads state from the database</summary>
        /// <returns>A <see cref="Task"/> which completes when the implementor has restarted</returns>
        Task Restart();

        /// <summary>Called by <see cref="RestartMonitoring"/> when the maximum number of re-starts has been reached and no more attempts will be made</summary>
        Task MaxRestartsReached(int attempts);
    }

    public class RestartMonitoring
    {
        readonly IEnumerator<TimeSpan> _delays;

        public RestartMonitoring(IEnumerable<TimeSpan> delays)
        {
            if (delays == null) throw new ArgumentNullException(nameof(delays));
            _delays = delays.GetEnumerator();
        }

        public void Monitor(IRestartable restartable, int attempt = 0)
        {
            if (restartable.MonitoredTask == null) throw new ArgumentException("restartable.Running is null");
            restartable.MonitoredTask.ContinueWith(t => { AttemptRestart(restartable, attempt, t.Exception); }, TaskContinuationOptions.OnlyOnFaulted);
        }

        async Task AttemptRestart(IRestartable restartable, int attempt, Exception error)
        {
            attempt += 1;

            // quite if there are no more delays in the sequence
            if (!_delays.MoveNext())
            {
                await restartable.MaxRestartsReached(attempt);
                return;
            }

            // attempt another restart
            TimeSpan delay = _delays.Current;
            Debug.Assert(delay > TimeSpan.Zero);
            await restartable.PauseBeforeRestart(delay); 
            try
            {
                await restartable.Restart();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Failed to restart: " + ex); //TODO: some other way of reporting this?
                return;
            }
            Monitor(restartable, attempt);
        }

    }

}
