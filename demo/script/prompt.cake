#reference "../../BuildArtifacts/temp/_PublishedLibraries/Cake.Prompt/net8.0/Cake.Prompt.dll"

// Note: running this script under cake.tool 6.1.0 emits 6× CS8632
// warnings ("nullable annotation outside #nullable context") from
// cake.tool's generated alias-wrapper file. The generator copies the
// addin's `string?` signatures but doesn't put its synthesized file in
// nullable context. The warnings are diagnostics-only — the code
// compiles, the alias calls work, the demo runs correctly.
//
// They cannot be silenced from this script: pragma directives here
// don't reach the generated file, and `#nullable enable` here would
// only put OUR script under nullable checking (surfacing unrelated
// warnings in the demo body). Upstream issue against cake-build/cake
// is queued in workspace TODO.

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
            Prompt("Enter:", "default", timeout);
        }
        catch (TimeoutException ex) when (ex.Message.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            threw = true;
            sw.Stop();
            AssertThat(sw.Elapsed >= timeout, "Timeout fired too early: " + sw.ElapsedMilliseconds + "ms");
            AssertThat(sw.Elapsed < TimeSpan.FromSeconds(5), "Timeout fired too late: " + sw.ElapsedMilliseconds + "ms");
        }

        AssertThat(threw, "Expected TimeoutException to fire on blocked stdin");

        elapsedMs = sw.ElapsedMilliseconds;
    }
    finally
    {
        Console.SetIn(origIn);
        Console.SetOut(origOut);
        blocking.Release();
    }

    // Log AFTER the finally so Console.Out is restored — otherwise these lines
    // go to the redirected StringWriter and are silently discarded.
    Information("TimeoutException fired after {0}ms (timeout was {1}ms)",
        elapsedMs,
        (int)timeout.TotalMilliseconds);
    Information("Timeout-Fires OK");
});

RunTarget("Default");
