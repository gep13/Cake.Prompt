using System;
using System.Threading.Tasks;
using Cake.Core;
using Cake.Core.Annotations;

namespace Cake.Common.IO
{
    /// <summary>
    /// Contains functionality related to interactive console prompts.
    /// </summary>
    [CakeAliasCategory("Prompt")]
    public static class PromptAliases
    {
        /// <summary>
        /// Prompts the user for input.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="message">The message which is shown to the user.</param>
        /// <returns>The user input.</returns>
        [CakeMethodAlias]
        public static string Prompt(this ICakeContext context, string message)
        {
            return Prompt(context, message, defaultResult: null);
        }

        /// <summary>
        /// Prompts the user for input.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="message">The message which is shown to the user.</param>
        /// <param name="timeout">Timeout time, defaults to 30 seconds</param>
        /// <returns>The user input.</returns>
        [CakeMethodAlias]
        public static string Prompt(this ICakeContext context, string message, TimeSpan timeout)
        {
            return Prompt(context, message, null, timeout);
        }

        /// <summary>
        /// Prompts the user for input.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="message">The message which is shown to the user.</param>
        /// <param name="defaultResult">Value supplied if the user simply returns with no input</param>
        /// <returns>The user input.</returns>
        [CakeMethodAlias]
        public static string Prompt(this ICakeContext context, string message, string defaultResult)
        {
            return Prompt(context, message, defaultResult, TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// Prompts the user for input.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="message">The message which is shown to the user.</param>
        /// <param name="defaultResult">Value supplied if the user simply returns with no input</param>
        /// <param name="timeout">Timeout time, defaults to 30 seconds</param>
        /// <returns>The user input.</returns>
        [CakeMethodAlias]
        public static string Prompt(this ICakeContext context, string message, string defaultResult, TimeSpan timeout)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            timeout = timeout == default ? TimeSpan.FromSeconds(30) : timeout;

            if (timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout), "timeout must be greater than zero");

            // Console.ReadLine() is a blocking synchronous call that ignores
            // CancellationToken — it returns only when the user presses Enter
            // or stdin is closed. To honour the timeout we race the read
            // against Task.Delay(timeout) via Task.WhenAny. If the delay wins
            // the read task is orphaned and continues blocking in the
            // background until the user presses Enter or the process exits;
            // that is unavoidable without P/Invoke or a Console.KeyAvailable
            // polling loop, and is acceptable for a CI/build-script context.
            var readTask = Task.Run(() =>
            {
                Console.Write(string.IsNullOrEmpty(defaultResult) ? message : $"{message} [{defaultResult}]");
                var readLine = Console.ReadLine();
                return string.IsNullOrEmpty(readLine) ? defaultResult : readLine;
            });

            var winner = Task.WhenAny(readTask, Task.Delay(timeout)).GetAwaiter().GetResult();
            if (winner != readTask)
            {
                throw new TimeoutException($"Prompt timed out after {timeout:g}.");
            }

            return readTask.GetAwaiter().GetResult();
        }
    }
}