using System;
using System.Diagnostics;
using System.IO;
using Cake.Common.IO;
using Cake.Core;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Cake.Prompt.Tests
{
    // Console.SetIn/Out are static, so tests in this collection run
    // sequentially to avoid stdin/stdout cross-contamination between
    // concurrent test methods.
    [Collection("ConsoleSerial")]
    public class PromptAliasesTests
    {
        [Fact]
        public void Prompt_throws_when_context_is_null()
        {
            Action act = () => PromptAliases.Prompt(null, "any message", "any default", TimeSpan.FromSeconds(1));

            act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("context");
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-30)]
        public void Prompt_throws_when_timeout_is_negative(int seconds)
        {
            var context = Substitute.For<ICakeContext>();

            Action act = () => context.Prompt("any", "default", TimeSpan.FromSeconds(seconds));

            act.Should().Throw<ArgumentOutOfRangeException>().And.ParamName.Should().Be("timeout");
        }

        [Fact]
        public void Prompt_treats_TimeSpan_Zero_as_default_30s_not_invalid()
        {
            // TimeSpan.Zero == default(TimeSpan), and the alias coerces a
            // default-valued timeout to 30s. This is a quirk preserved from
            // the original 1.x behaviour; assert it explicitly so the
            // negative-only ArgumentOutOfRangeException test above doesn't
            // give the impression that zero is also rejected.
            var context = Substitute.For<ICakeContext>();

            // Use stdin EOF to short-circuit ReadLine immediately so we don't
            // actually wait 30s.
            var result = WithRedirectedConsole(string.Empty, () =>
                context.Prompt("Enter:", "fallback", TimeSpan.Zero));

            result.Should().Be("fallback");
        }

        [Fact]
        public void Prompt_returns_user_input_when_input_is_not_empty()
        {
            var context = Substitute.For<ICakeContext>();

            var result = WithRedirectedConsole("hello\n", () =>
                context.Prompt("Enter:", "default", TimeSpan.FromSeconds(5)));

            result.Should().Be("hello");
        }

        [Fact]
        public void Prompt_returns_default_when_user_just_presses_enter()
        {
            var context = Substitute.For<ICakeContext>();

            var result = WithRedirectedConsole("\n", () =>
                context.Prompt("Enter:", "default", TimeSpan.FromSeconds(5)));

            result.Should().Be("default");
        }

        [Fact]
        public void Prompt_throws_TimeoutException_when_no_input_arrives_within_timeout()
        {
            var context = Substitute.For<ICakeContext>();
            var timeout = TimeSpan.FromMilliseconds(500);

            // Hold stdin open with a blocking reader that produces nothing,
            // so Console.ReadLine genuinely blocks instead of EOF-ing.
            var blockingReader = new BlockingTextReader();
            var origIn = Console.In;
            var origOut = Console.Out;
            Console.SetIn(blockingReader);
            Console.SetOut(new StringWriter());

            try
            {
                var sw = Stopwatch.StartNew();
                Action act = () => context.Prompt("Enter:", "default", timeout);

                act.Should().Throw<TimeoutException>()
                    .Which.Message.Should().Contain("timed out");

                sw.Stop();
                sw.Elapsed.Should().BeGreaterThanOrEqualTo(timeout);
                sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5), "the timeout should fire promptly, not hang");
            }
            finally
            {
                Console.SetIn(origIn);
                Console.SetOut(origOut);
                blockingReader.Release();
            }
        }

        private static T WithRedirectedConsole<T>(string stdinContent, Func<T> action)
        {
            var origIn = Console.In;
            var origOut = Console.Out;
            try
            {
                Console.SetIn(new StringReader(stdinContent));
                Console.SetOut(new StringWriter());
                return action();
            }
            finally
            {
                Console.SetIn(origIn);
                Console.SetOut(origOut);
            }
        }

        // Minimal TextReader that blocks on Read/ReadLine until Release() is
        // called, so we can exercise the timeout path without an EOF short-circuit.
        private sealed class BlockingTextReader : TextReader
        {
            private readonly System.Threading.ManualResetEventSlim _gate = new(false);

            public void Release() => _gate.Set();

            public override int Read()
            {
                _gate.Wait();
                return -1;
            }

            public override string ReadLine()
            {
                _gate.Wait();
                return null;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _gate.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}
