// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yarp.Kubernetes.Controller.Configuration;
using Yarp.Kubernetes.Controller.Hosting;
using Yarp.Kubernetes.Controller.Rate;

namespace Yarp.Kubernetes.Protocol;

public class Receiver : BackgroundHostedService
{
    private readonly ReceiverOptions _options;
    private readonly Limiter _limiter;
    private readonly IUpdateConfig _proxyConfigProvider;

    public Receiver(
        IOptions<ReceiverOptions> options,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<Receiver> logger,
        IUpdateConfig proxyConfigProvider) : base(hostApplicationLifetime, logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;

        _options.Client ??= new HttpMessageInvoker(new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(15),
        });

        // two requests per second after third failure
        _limiter = new Limiter(new Limit(2), 3);
        _proxyConfigProvider = proxyConfigProvider;
    }

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await _limiter.WaitAsync(cancellationToken).ConfigureAwait(false);

            Logger.LogInformation("Connecting with {ControllerUrl}", _options.ControllerUrl.ToString());

            try
            {
                var requestMessage = new HttpRequestMessage(HttpMethod.Get, _options.ControllerUrl);
                var responseMessage = await _options.Client.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
                using var stream = await responseMessage.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                using var cancellation = cancellationToken.Register(stream.Close);
                while (true)
                {
                    var json = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrEmpty(json))
                    {
                        break;
                    }

                    var message = System.Text.Json.JsonSerializer.Deserialize<Message>(json);
                    Logger.LogInformation("Received {MessageType} for {MessageKey}", message.MessageType, message.Key);

                    Logger.LogInformation(json);
                    Logger.LogInformation(message.MessageType.ToString());

                    if (message.MessageType == MessageType.Update)
                    {
                        await _proxyConfigProvider.UpdateAsync(message.Routes, message.Cluster, cancellation.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogInformation(ex, "Stream ended");
            }
        }
    }
}
