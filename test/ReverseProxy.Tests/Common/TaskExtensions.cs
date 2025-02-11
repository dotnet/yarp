// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.Tests.Common;

/// <summary>
/// Extensions for the <see cref="Task"/> class.
/// </summary>
internal static class TaskExtensions
{
    public static TimeSpan DefaultTimeoutTimeSpan { get; } = TimeSpan.FromSeconds(5);

    public static Task<T> DefaultTimeout<T>(this ValueTask<T> task)
    {
        return task.AsTask().TimeoutAfter(DefaultTimeoutTimeSpan);
    }

    public static Task DefaultTimeout(this ValueTask task)
    {
        return task.AsTask().TimeoutAfter(DefaultTimeoutTimeSpan);
    }

    public static Task<T> DefaultTimeout<T>(this Task<T> task)
    {
        return task.TimeoutAfter(DefaultTimeoutTimeSpan);
    }

    public static Task DefaultTimeout(this Task task)
    {
        return task.TimeoutAfter(DefaultTimeoutTimeSpan);
    }

    private static async Task<T> TimeoutAfter<T>(this Task<T> task, TimeSpan timeout,
        [CallerFilePath] string filePath = null,
        [CallerLineNumber] int lineNumber = default)
    {
        // Don't create a timer if the task is already completed
        // or the debugger is attached
        if (task.IsCompleted || Debugger.IsAttached)
        {
            return await task;
        }

        try
        {
            return await task.WaitAsync(timeout);
        }
        catch (TimeoutException ex) when (ex.Source == typeof(TaskExtensions).Namespace)
        {
            throw new TimeoutException(CreateMessage(timeout, filePath, lineNumber));
        }
    }

    private static async Task TimeoutAfter(this Task task, TimeSpan timeout,
        [CallerFilePath] string filePath = null,
        [CallerLineNumber] int lineNumber = default)
    {
        // Don't create a timer if the task is already completed
        // or the debugger is attached
        if (task.IsCompleted || Debugger.IsAttached)
        {
            await task;
            return;
        }

        var cts = new CancellationTokenSource();
        if (task == await Task.WhenAny(task, Task.Delay(timeout, cts.Token)))
        {
            cts.Cancel();
            await task;
        }
        else
        {
            throw new TimeoutException(CreateMessage(timeout, filePath, lineNumber));
        }
    }

    private static string CreateMessage(TimeSpan timeout, string filePath, int lineNumber)
        => string.IsNullOrEmpty(filePath)
        ? $"The operation timed out after reaching the limit of {timeout.TotalMilliseconds}ms."
        : $"The operation at {filePath}:{lineNumber} timed out after reaching the limit of {timeout.TotalMilliseconds}ms.";
}
