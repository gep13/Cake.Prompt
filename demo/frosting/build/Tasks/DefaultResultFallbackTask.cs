using System;
using System.IO;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Frosting;

namespace Build.Tasks
{
    [TaskName("Default-Result-Fallback")]
    public sealed class DefaultResultFallbackTask : FrostingTask<BuildContext>
    {
        public override void Run(BuildContext context)
        {
            // Empty stdin (EOF) → ReadLine returns null → alias returns defaultResult.
            var origIn = Console.In;
            var origOut = Console.Out;
            try
            {
                Console.SetIn(new StringReader(string.Empty));
                Console.SetOut(new StringWriter());

                var result = context.Prompt("Enter:", "default-value", TimeSpan.FromSeconds(5));
                if (result != "default-value")
                {
                    throw new Exception("Expected defaultResult on empty stdin, got: " + (result ?? "<null>"));
                }
            }
            finally
            {
                Console.SetIn(origIn);
                Console.SetOut(origOut);
            }

            context.Information("Default-Result-Fallback OK");
        }
    }
}
