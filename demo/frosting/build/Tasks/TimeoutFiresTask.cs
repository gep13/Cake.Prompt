using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Frosting;

namespace Build.Tasks
{
    [TaskName("Timeout-Fires")]
    public sealed class TimeoutFiresTask : FrostingTask<BuildContext>
    {
        public override void Run(BuildContext context)
        {
            // Block stdin so ReadLine genuinely waits → Task.Delay(timeout)
            // wins → TimeoutException. Regression-test for the bug fixed
            // by 1.2.2 (#15).
            var origIn = Console.In;
            var origOut = Console.Out;
            var blocking = new BlockingTextReader();
            var timeout = TimeSpan.FromMilliseconds(500);
            long elapsedMs;
            try
            {
                Console.SetIn(blocking);
                Console.SetOut(new StringWriter());

                var sw = Stopwatch.StartNew();
                var threw = false;

                try
                {
                    context.Prompt("Enter:", "default", timeout);
                }
                catch (TimeoutException ex) when (ex.Message.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    threw = true;
                    sw.Stop();
                    if (sw.Elapsed < timeout)
                    {
                        throw new Exception("Timeout fired too early: " + sw.ElapsedMilliseconds + "ms");
                    }

                    if (sw.Elapsed >= TimeSpan.FromSeconds(5))
                    {
                        throw new Exception("Timeout fired too late: " + sw.ElapsedMilliseconds + "ms");
                    }
                }

                if (!threw)
                {
                    throw new Exception("Expected TimeoutException to fire on blocked stdin");
                }

                elapsedMs = sw.ElapsedMilliseconds;
            }
            finally
            {
                Console.SetIn(origIn);
                Console.SetOut(origOut);
                blocking.Release();
            }

            context.Information(
                "TimeoutException fired after {0}ms (timeout was {1}ms)",
                elapsedMs,
                (int)timeout.TotalMilliseconds);
            context.Information("Timeout-Fires OK");
        }

        private sealed class BlockingTextReader : TextReader
        {
            private readonly ManualResetEventSlim gate = new ManualResetEventSlim(false);

            public void Release() => this.gate.Set();

            public override int Read()
            {
                this.gate.Wait();
                return -1;
            }

            public override string ReadLine()
            {
                this.gate.Wait();
                return null;
            }
        }
    }
}
