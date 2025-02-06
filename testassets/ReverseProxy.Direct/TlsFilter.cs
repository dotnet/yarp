// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Utilities.Tls;

namespace Yarp.ReverseProxy.Sample;

public static class TlsFilter
{
    // This sniffs the TLS handshake and rejects requests that meat specific criteria.
    internal static async Task ProcessAsync(ConnectionContext connectionContext, Func<Task> next, ILogger logger)
    {
        var input = connectionContext.Transport.Input;
        // Count how many bytes we've examined so we never go backwards, Pipes don't allow that.
        var minBytesExamined = 0L;
        while (true)
        {
            var result = await input.ReadAsync();
            var buffer = result.Buffer;

            if (result.IsCompleted)
            {
                return;
            }

            if (buffer.Length == 0)
            {
                continue;
            }

            if (!TryReadHello(buffer, logger, out var abort))
            {
                minBytesExamined = buffer.Length;
                input.AdvanceTo(buffer.Start, buffer.End);
                continue;
            }

            var examined = buffer.Slice(buffer.Start, minBytesExamined).End;
            input.AdvanceTo(buffer.Start, examined);

            if (abort)
            {
                // Close the connection.
                return;
            }

            break;
        }

        await next();
    }

    private static bool TryReadHello(ReadOnlySequence<byte> buffer, ILogger logger, out bool abort)
    {
        abort = false;

        if (!buffer.IsSingleSegment)
        {
            throw new NotImplementedException("Multiple buffer segments");
        }
        var data = buffer.First.Span;

        TlsFrameHelper.TlsFrameInfo info = default;
        if (!TlsFrameHelper.TryGetFrameInfo(data, ref info))
        {
            if (info.ParsingStatus == TlsFrameHelper.ParsingStatus.InvalidFrame)
            {
                logger.LogInformation("Invalid TLS frame");
                abort = true;
            }
            return false;
        }

        if (!info.SupportedVersions.HasFlag(System.Security.Authentication.SslProtocols.Tls12))
        {
            logger.LogInformation("Unsupported versions: {versions}", info.SupportedVersions);
            abort = true;
        }
        else
        {
            logger.LogInformation("Protocol versions: {versions}", info.SupportedVersions);
        }

        if (!AllowHost(info.TargetName))
        {
            logger.LogInformation("Disallowed host: {host}", info.TargetName);
            abort = true;
        }
        else
        {
            logger.LogInformation("SNI: {host}", info.TargetName);
        }

        return true;
    }

    private static bool AllowHost(string targetName)
    {
        if (string.Equals("localhost", targetName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }
}
