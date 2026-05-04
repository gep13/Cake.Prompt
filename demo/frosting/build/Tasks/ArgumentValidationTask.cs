using System;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Frosting;

namespace Build.Tasks
{
    [TaskName("Argument-Validation")]
    public sealed class ArgumentValidationTask : FrostingTask<BuildContext>
    {
        public override void Run(BuildContext context)
        {
            // TimeSpan.Zero == default(TimeSpan), which Prompt coerces to the
            // default 30s timeout (preserved 1.x behaviour). Only negative
            // timeouts are genuinely invalid.
            var threwForNegative = false;
            try
            {
                context.Prompt("any", "default", TimeSpan.FromSeconds(-1));
            }
            catch (ArgumentOutOfRangeException)
            {
                threwForNegative = true;
            }

            if (!threwForNegative)
            {
                throw new Exception("Expected ArgumentOutOfRangeException for negative timeout");
            }

            context.Information("Argument-Validation OK");
        }
    }
}
