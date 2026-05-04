#!/usr/bin/env dotnet
#:sdk Cake.Sdk@6.1.0
#:project ../../src/Cake.Prompt/Cake.Prompt.csproj

// Cake SDK consumer demo for Cake.Prompt. Runs as a file-based
// .NET program (introduced in .NET 10) using the Cake.Sdk
// directives. The #:project directive above lets the SDK build
// the addin from source rather than referencing a published nupkg.
//
// To run locally:
//   cd demo/sdk
//   dotnet cake.cs
//
// Runs the same three checks the script and frosting demos run.

using System.Diagnostics;
using System.IO;
using System.Threading;

Task("Default")
    .IsDependentOn("Argument-Validation")
    .IsDependentOn("Default-Result-Fallback")
    .IsDependentOn("Timeout-Fires");

Task("Argument-Validation")
    .Does(() =>
{
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

// ----- Helpers (must come AFTER top-level statements per CS8803) -----

static void AssertThat(bool condition, string message)
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

    public override string? ReadLine()
    {
        _gate.Wait();
        return null;
    }
}
