using System.Net;
using PoeFilterX.Engine.Configuration;
using System.Net.Sockets;
using System.IO.Pipes;
using Microsoft.VisualStudio.Threading;
using StreamJsonRpc;

namespace PoeFilterX.LanguageServer;

public static class LanguageServerHost
{
    public static async Task<int> RunAsync(
        int? socketPort,
        string? pipeName,
        ProjectSettings? settings,
        CancellationToken cancellationToken
    )
    {
        if (socketPort is { } port)
        {
            using var client = new TcpClient(AddressFamily.InterNetwork);
            await client.ConnectAsync(IPAddress.Loopback, port, cancellationToken).ConfigureAwait(false);
            await using var stream = client.GetStream();

            return await ServeAsync(stream, stream, settings, cancellationToken).ConfigureAwait(false);
        }

        if (pipeName is not null)
        {
            return await ConnectPipeAsync(pipeName, settings, cancellationToken).ConfigureAwait(false);
        }

        await using var input = Console.OpenStandardInput();
        await using var output = Console.OpenStandardOutput();

        return await ServeAsync(input, output, settings, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> ConnectPipeAsync(
        string name,
        ProjectSettings? settings,
        CancellationToken cancellationToken
    )
    {
        if (OperatingSystem.IsWindows())
        {
            const string prefix = @"\\.\pipe\";
            var pipeName = name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? name[prefix.Length..] : name;
            await using var pipe = new NamedPipeClientStream(
                ".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous
            );
            await pipe.ConnectAsync(cancellationToken).ConfigureAwait(false);

            return await ServeAsync(pipe, pipe, settings, cancellationToken).ConfigureAwait(false);
        }

        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(name), cancellationToken).ConfigureAwait(false);
        await using var stream = new NetworkStream(socket, ownsSocket: false);

        return await ServeAsync(stream, stream, settings, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> ServeAsync(
        Stream input,
        Stream output,
        ProjectSettings? settings,
        CancellationToken cancellationToken
    )
    {
        var formatter = new SystemTextJsonFormatter();
        formatter.JsonSerializerOptions.TypeInfoResolver = ProtocolJsonContext.Default;
        using var handler = new HeaderDelimitedMessageHandler(output, input, formatter);
        using var rpc = new JsonRpc(handler)
        {
            SynchronizationContext = new NonConcurrentSynchronizationContext(sticky: true),
            CancelLocallyInvokedMethodsWhenConnectionIsClosed = true,
            ExceptionStrategy = ExceptionProcessing.CommonErrorData
        };
        using var session = new LanguageServerSession(rpc, settings);
        session.Register();
        rpc.StartListening();
        var completed = await Task.WhenAny(rpc.Completion, session.Exit)
            .WaitAsync(cancellationToken).ConfigureAwait(false);
        if (completed == session.Exit)
        {
            return await session.Exit.ConfigureAwait(false);
        }

        await rpc.Completion.ConfigureAwait(false);
        return 1;
    }
}
