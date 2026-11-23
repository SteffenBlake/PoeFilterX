using System.CommandLine;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using PoeFilterX.LanguageServer;

namespace PoeFilterX;

internal static partial class LanguageServerCmd
{
    public static Command Create()
    {
        var command = new Command("language-server", "Run the PoeFilterX language server (stdio by default).");
        var stdio = new Option<bool>("--stdio") { Description = "Exchange LSP messages over stdin and stdout." };
        var socket = new Option<int?>("--socket")
        {
            Description = "Connect to the editor's TCP listener on this localhost port.",
            Arity = ArgumentArity.ExactlyOne
        };
        var pipe = new Option<string?>("--pipe")
        {
            Description = "Connect to the editor's named pipe (Windows) or Unix socket path.",
            Arity = ArgumentArity.ExactlyOne
        };
        CommandConfiguration.AddOptions(command);
        command.Options.Add(stdio);
        command.Options.Add(socket);
        command.Options.Add(pipe);
        command.Validators.Add(result =>
        {
            var port = result.GetValue(socket);
            var transports = (result.GetValue(stdio) ? 1 : 0) + (port.HasValue ? 1 : 0)
                + (result.GetValue(pipe) is not null ? 1 : 0);
            if (transports > 1)
            {
                result.AddError("Select only one transport: --stdio, --socket, or --pipe.");
            }

            if (port is < 1 or > 65535)
            {
                result.AddError("Socket port must be between 1 and 65535.");
            }

            if (result.GetValue(pipe) is { Length: 0 })
            {
                result.AddError("Pipe name cannot be empty.");
            }
        });
        command.SetAction((result, cancellationToken) => CommandConfiguration.InvokeAsync(result, null, async settings =>
        {
            try
            {
                var port = settings.Get<int?>("socket");
                var pipeName = settings.Configuration["pipe"];
                if ((settings.Get("stdio", false) ? 1 : 0) + (port.HasValue ? 1 : 0) + (pipeName is not null ? 1 : 0) > 1)
                {
                    Console.Error.WriteLine("Select only one transport: --stdio, --socket, or --pipe.");
                    return 1;
                }

                return await LanguageServerHost.RunAsync(
                    port, pipeName, settings, cancellationToken
                );
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return 0;
            }
            catch (Exception exception) when (exception is IOException or SocketException
                or UnauthorizedAccessException or ArgumentException)
            {
                using var logging = CommandLogging.CreateFactory(LogLevel.Error);
                TransportFailed(logging.CreateLogger(nameof(LanguageServerCmd)), exception.Message);
                return 1;
            }
        }));

        return command;
    }

    [LoggerMessage(
        3000,
        LogLevel.Error,
        "Language server transport failed: {Message}"
    )]
    private static partial void TransportFailed(ILogger logger, string message);
}
