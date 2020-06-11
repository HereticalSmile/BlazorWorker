using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorWorker.Demo.Shared
{
    /// <summary>
    /// This service runs insinde the worker.
    /// </summary>
    public class MathsService
    {
        public event EventHandler<int> Pi;
        public event EventHandler<Tuple<int, double?>> PiCancellable;

        private IEnumerable<int> AlternatingSequence(int start = 0)
        {
            int i;
            bool flip;
            if (start == 0)
            {
                yield return 1;
                i = 1;
                flip = false;
            }
            else
            {
                i = (start * 2) - 1;
                flip = start % 2 == 0;
            }

            while (true) yield return ((flip = !flip) ? -1 : 1) * (i += 2);
        }

        public double EstimatePI(int sumLength)
        {
            var lastReport = 0;
            return (4 * AlternatingSequence().Take(sumLength)
                .Select((x, i) => {
                    // Keep reporting events down a bit, serialization is expensive!
                    var progressDelta = (Math.Abs(i - lastReport) / (double)sumLength) * 100;
                    if (progressDelta > 3 || i >= sumLength - 1)
                    {
                        lastReport = i;
                        Pi?.Invoke(this, i);
                    }
                    return x; })
                .Sum(x => 1.0 / x));
        }

        private int idSource;
        private Dictionary<int, CancellationTokenSource> cancellationTokens = new Dictionary<int, CancellationTokenSource>();

        public int GetCancellationToken()
        {
            var nextId = ++idSource;
            cancellationTokens.Add(nextId, new CancellationTokenSource());

            return nextId;
        }

        public bool CancelCancellationToken(int id)
        {
            Console.WriteLine($"entering {nameof(CancelCancellationToken)} ");
            if (cancellationTokens.TryGetValue(id, out var cancellationTokenSource))
            {
                cancellationTokenSource.Cancel();
                var cancellationToken = cancellationTokenSource.Token;
                Console.WriteLine($"{nameof(CancelCancellationToken)}: {nameof(cancellationToken.IsCancellationRequested)}={cancellationToken.IsCancellationRequested} {cancellationToken.GetHashCode()}");
                return true;
            }

            return false;
        }


        public void EstimatePICancellable(int sumLength, int cancellationSourceId)
        {
            if (!cancellationTokens.TryGetValue(cancellationSourceId, out var cancellationTokenSource))
            {
                Console.WriteLine("Unknown cancellation source id", nameof(cancellationSourceId));
                throw new ArgumentException("Unknown cancellation source id", nameof(cancellationSourceId));
            }

            Task.Run(async () => await BeginEstimatePI(sumLength, cancellationTokenSource.Token));
        }

        

        private async Task BeginEstimatePI(int sumLength, CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine($"entering {nameof(BeginEstimatePI)} ");

                var lastReport = 0;
                var i = 0;
                var sum = 0d;
                foreach (var item in AlternatingSequence().Take(sumLength))
                {
                    // Keep reporting events down a bit, serialization is expensive!
                    var progressDelta = (Math.Abs(i - lastReport) / (double)sumLength) * 100;
                    if (progressDelta > 3 || i >= sumLength - 1)
                    {
                        await Task.Delay(10);
                        Console.WriteLine($"{nameof(BeginEstimatePI)}: {nameof(cancellationToken.IsCancellationRequested)}={cancellationToken.IsCancellationRequested} {cancellationToken.GetHashCode()}");
                        cancellationToken.ThrowIfCancellationRequested();
                        lastReport = i;
                        Console.WriteLine($"invoking Pi event with arg {i}");
                        PiCancellable?.Invoke(this, new Tuple<int, double?>(i, null));
                    }

                    sum += 1.0 / item;
                    i++;
                }

                var result = 4 * sum;
                PiCancellable?.Invoke(this, new Tuple<int, double?>(i, result));
            }
            catch (Exception e)
            {
                Console.WriteLine($"{nameof(BeginEstimatePI)} {e}");
                throw;
            }
        }

        public double EstimatePISlice(int sumStart, int sumLength)
        {
            Console.WriteLine($"EstimatePISlice({sumStart},{sumLength})");
            var lastReport = 0;
            return AlternatingSequence(sumStart)
                .Take(sumLength)
                .Select((x, i) => {

                    // Keep reporting events down a bit, serialization is expensive!
                    var progressDelta = (Math.Abs(i - lastReport) / (double)sumLength) * 100;
                    if (progressDelta > 3 || i >= sumLength - 1)
                    {
                        lastReport = i;
                        Pi?.Invoke(this, i);
                    }
                    return x;
                })
                .Sum(x => 1.0 / x);

        }
    }
}
