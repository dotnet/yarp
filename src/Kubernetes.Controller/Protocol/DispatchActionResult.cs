// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Yarp.Kubernetes.Protocol;

namespace Yarp.Kubernetes.Controller.Dispatching;

/// <summary>
/// DispatchActionResult is an IActionResult which registers itself as
/// an IDispatchTarget with the provided IDispatcher. As long as the client
/// is connected this result will continue to write data to the response.
/// </summary>
public class DispatchActionResult : IActionResult, IDispatchTarget
{
    private static readonly byte[] _newline = Encoding.UTF8.GetBytes(Environment.NewLine);

    private readonly IDispatcher _dispatcher;
    private Task _task = Task.CompletedTask;
    private readonly object _taskSync = new();
    private HttpContext _httpContext;

    public DispatchActionResult(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public override string ToString()
    {
        return $"{_httpContext?.Connection.Id}:{_httpContext?.TraceIdentifier}";
    }

    public async Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var cancellationToken = context.HttpContext.RequestAborted;

        _httpContext = context.HttpContext;
        _httpContext.Response.ContentType = "text/plain";
        _httpContext.Response.Headers["Connection"] = "close";
        await _httpContext.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);

        await _dispatcher.AttachAsync(this, cancellationToken).ConfigureAwait(false);

        try
        {
            var utf8Bytes = JsonSerializer.SerializeToUtf8Bytes(new Message
            {
                MessageType = MessageType.Heartbeat
            });

            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(35), cancellationToken).ConfigureAwait(false);
                await SendAsync(utf8Bytes, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (TaskCanceledException)
        {
            // This is fine.
        }
        finally
        {
            _dispatcher.Detach(this);
        }
    }

    public async Task SendAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        var result = Task.CompletedTask;

        lock (_taskSync)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_task.IsCanceled || _task.IsFaulted)
            {
                result = _task;
            }
            else
            {
                _task = DoSendAsync(_task, bytes);
            }

            async Task DoSendAsync(Task task, byte[] bytes)
            {
                await task.ConfigureAwait(false);
                await _httpContext.Response.BodyWriter.WriteAsync(bytes, cancellationToken);
                await _httpContext.Response.BodyWriter.WriteAsync(_newline, cancellationToken);
                await _httpContext.Response.BodyWriter.FlushAsync(cancellationToken);
            }
        }

        await result.ConfigureAwait(false);
    }
}
