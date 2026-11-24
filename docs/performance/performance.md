---
uid: performance
title: Performance
---

# Performance

This page describes the work the SDK does during a test run, and when it does it. It also lists the
settings that change that work, and how to find out whether the SDK slows a run down.

The page gives no timings. They depend on the machine, the framework and the suite. To measure the
SDK yourself, see [Measure the SDK](#measure-the-sdk).

---

## The work for each test

When a test ends, the adapter builds one record of it: the test's identity, its outcome and
duration, its error text and stack trace when it failed, its retry attempt, and how many tests ran
at the same time. The SDK adds the record to an in-memory queue.

This step does no file or network I/O, and it does not wait for other work.

The SDK does not sample. It records every test that runs.

---

## The work at other times

| When | `LocalOnly` mode | `Cloud` mode |
|---|---|---|
| The session starts | The SDK reads the configuration and detects the CI, the branch and the environment, once. | The same. |
| During the run | Nothing. | The SDK uploads a batch when [`BatchSize`](../configuration/configuration-reference.md#batchsize) records are waiting, and when the [`FlushInterval`](../configuration/configuration-reference.md#flushinterval) timer fires. |
| The session ends | The SDK writes the run to the [local store](../configuration/local-store.md), and removes old runs. | The SDK writes the run to the local store first. Then it uploads the records that are left, and a final upload that closes the session. |

`Auto`, the default mode, is `Cloud` with an API key and `LocalOnly` without one. See
[Mode](../configuration/configuration-reference.md#mode).

### Uploads during the run

In `Cloud` mode, a batch upload starts on the thread of the test that filled the batch. That thread
takes the batch from the queue, serializes it to JSON and compresses it, before the request goes over
the network. The test thread does not wait for the network. The timer upload runs on a thread-pool
thread.

Only one upload runs at a time. An upload that is due while another one runs waits for it.

### The end of the session

The session ends after the last test. The SDK writes the local store before any upload, so the run
is on disk even when the upload fails or the process is stopped.

Each upload, with its retries and the delays between them, stops after twice
[`UploadTimeout`](../configuration/configuration-reference.md#uploadtimeout): 60 seconds by default.
When a batch upload fails, the SDK sends no more batches, and it still sends the final upload. A
failing upload therefore adds a bounded wait to the end of the run. It does not fail the run, unless
[`StrictMode`](../configuration/configuration-reference.md#strictmode) is on.

---

## Memory

The SDK keeps every record of the run in memory for the whole session, in every mode except
`Disabled`. It needs them all to write the run to the local store at the end. An upload during the
run does not free them.

Memory therefore grows with the size of the run: the number of test executions, retries included,
and the length of their error messages and stack traces. A run with many failures and deep stacks
uses more. `BatchSize` does not limit it.

---

## Settings that change the work

| Setting | Effect |
|---|---|
| [`Mode`](../configuration/configuration-reference.md#mode) | `LocalOnly` makes no uploads. `Disabled` records nothing. |
| [`Enabled`](../configuration/configuration-reference.md#enabled) | `false` records nothing, the same as `Disabled`. |
| [`BatchSize`](../configuration/configuration-reference.md#batchsize) | `Cloud` only. A larger batch makes fewer uploads, and each one serializes more records on one test thread. |
| [`FlushInterval`](../configuration/configuration-reference.md#flushinterval) | `Cloud` only. The time between timer uploads. |
| [`EnableCompression`](../configuration/configuration-reference.md#enablecompression) | Compresses uploads larger than 1 KB. It costs CPU and saves bandwidth. |
| [`UploadStackTraces`](../configuration/configuration-reference.md#uploadstacktraces) | Off by default. On, it adds stack trace text to the uploads. The local store keeps the traces either way. |
| [`UploadTimeout`](../configuration/configuration-reference.md#uploadtimeout) | Sets the longest wait for one upload: twice this value. |
| [`MaxRetries`](../configuration/configuration-reference.md#maxretries), [`RetryDelay`](../configuration/configuration-reference.md#retrydelay) | How often, and after how long, a failed upload is retried, within the limit above. |

---

## Find out whether the SDK slows a run

1. **Measure the run without the SDK.** `XPING_ENABLED=false` turns the SDK off for one run:

   ```bash
   XPING_ENABLED=false dotnet test -c Release
   dotnet test -c Release
   ```

   Compare the two durations over several runs, because test durations vary from run to run. On
   Windows PowerShell, set the variable with `$env:XPING_ENABLED = "false"` first.

2. **Check the end of the run.** A long pause after the last test is usually an upload. The SDK logs
   each upload and its result. Set
   [`LogLevel`](../configuration/configuration-reference.md#loglevel) to `Debug` for more detail. For
   upload errors, see [Common issues](../troubleshooting/common-issues.md).

3. **Profile the test process.** `dotnet-counters` shows memory and GC activity while the tests run.
   `dotnet-trace` records where the time goes.

   ```bash
   dotnet tool install -g dotnet-counters
   dotnet tool install -g dotnet-trace

   dotnet-counters monitor --name testhost
   dotnet-trace collect --name testhost
   ```

   Open the `.nettrace` file in Visual Studio or PerfView, or convert it with
   `dotnet-trace convert --format speedscope`. Look for `Xping` frames.

When you find SDK work that slows a run, open an issue at
[github.com/xping-dev/sdk-dotnet](https://github.com/xping-dev/sdk-dotnet/issues) with the trace and
the run's size.

---

## Measure the SDK

The repository has a BenchmarkDotNet project for the SDK's own code paths: recording, batching,
serialization, configuration, environment detection and the local store. It measures on your
machine. See
[the benchmarks README](https://github.com/xping-dev/sdk-dotnet/blob/main/tests/Xping.Sdk.Benchmarks/README.md).

---

## See also

- [Configuration reference](../configuration/configuration-reference.md)
- [Local store](../configuration/local-store.md)
- [Common issues](../troubleshooting/common-issues.md)
