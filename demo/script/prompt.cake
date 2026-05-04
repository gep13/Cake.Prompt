#reference "../../BuildArtifacts/temp/_PublishedLibraries/Cake.Prompt/net6.0/Cake.Prompt.dll"

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

// Cake script consumer demo for Cake.Prompt. Runs under the Cake-major-
// matching cake.tool pinned by demo/script/.config/dotnet-tools.json
// (independent of the recipe build's tool, which is constrained by
// Cake.Recipe's Cake-2 era runtime).
//
// Runs three checks against the just-built _PublishedLibraries DLL:
//
//   1. Argument validation: zero/negative timeouts throw
//      ArgumentOutOfRangeException.
//   2. Default-result fallback: when stdin yields an empty line,
//      Prompt returns the supplied defaultResult.
//   3. Timeout fires: when stdin blocks indefinitely, Prompt throws
//      TimeoutException at ~the configured timeout (the bug fixed
//      in 1.2.2 / #15).
//
// Wired into Cake.Recipe's Run-Integration-Tests task via recipe.cake;
// runs as part of every CI build per the workspace integration-test
// pattern.

void AssertThat(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception("Assertion failed: " + message);
    }
}

// Minimal TextReader that blocks until Release() is called. Used to
// drive the timeout-fires path without an EOF short-circuit.
public sealed class BlockingTextReader : TextReader
{
    private readonly ManualResetEventSlim _gate = new ManualResetEventSlim(false);

    public void Release() { _gate.Set(); }

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
}

Task("Default")
    .IsDependentOn("Argument-Validation")
    .IsDependentOn("Default-Result-Fallback")
    .IsDependentOn("Timeout-Fires");

Task("Argument-Validation")
    .Does(() =>
{
    // Note: TimeSpan.Zero == default(TimeSpan), which the alias coerces
    // to the default 30s timeout (preserved 1.x behaviour). Only negative
    // timeouts are genuinely invalid.
    var threwForNegative = false;
    try
    {
        Prompt("any", "default", TimeSpan.FromSeconds(-1));
    }
    catch (ArgumentOutOfRangeException)
    {
        threwForNegative = true;
    }

    AssertThat(threwForNegative, "Expected ArgumentOutOfRangeException for negative timeout");

    Information("Argument-Validation OK");
});

Task("Default-Result-Fallback")
    .Does(() =>
{
    // Empty stdin (EOF) → ReadLine returns null → alias returns defaultResult.
    var origIn = Console.In;
    var origOut = Console.Out;
    try
    {
        Console.SetIn(new StringReader(string.Empty));
        Console.SetOut(new StringWriter());

        var result = Prompt("Enter:", "default-value", TimeSpan.FromSeconds(5));
        AssertThat(result == "default-value", "Expected defaultResult on empty stdin, got: " + (result ?? "<null>"));
    }
    finally
    {
        Console.SetIn(origIn);
        Console.SetOut(origOut);
    }

    Information("Default-Result-Fallback OK");
});

Task("Timeout-Fires")
    .Does(() =>
{
    // Block stdin so ReadLine genuinely waits → Task.Delay(timeout) wins → TimeoutException.
    var origIn = Console.In;
    var origOut = Console.Out;
    var blocking = new BlockingTextReader();
    try
    {
        Console.SetIn(blocking);
        Console.SetOut(new StringWriter());

        var timeout = TimeSpan.FromMilliseconds(500);
        var sw = Stopwatch.StartNew();
        var threw = false;

        try
        {
            Prompt("Enter:", "default", timeout);
        }
        catch (TimeoutException ex) when (ex.Message.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            threw = true;
            sw.Stop();
            AssertThat(sw.Elapsed >= timeout, "Timeout fired too early: " + sw.ElapsedMilliseconds + "ms");
            AssertThat(sw.Elapsed < TimeSpan.FromSeconds(5), "Timeout fired too late: " + sw.ElapsedMilliseconds + "ms");
            Information("TimeoutException fired after {0}ms (timeout was {1}ms)",
                sw.ElapsedMilliseconds,
                (int)timeout.TotalMilliseconds);
        }

        AssertThat(threw, "Expected TimeoutException to fire on blocked stdin");
    }
    finally
    {
        Console.SetIn(origIn);
        Console.SetOut(origOut);
        blocking.Release();
    }

    Information("Timeout-Fires OK");
});

RunTarget("Default");
