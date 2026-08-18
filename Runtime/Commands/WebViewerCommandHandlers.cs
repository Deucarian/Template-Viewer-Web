using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.CommandRouting;
using Deucarian.ViewerAuthentication;

namespace Deucarian.TemplateViewerWeb.Commands
{
    public static class WebViewerCommandHandlers
    {
        public static IReadOnlyList<ICommandHandler<WebViewerApplication>> Create(
            IViewerAuthenticationEventPublisher authenticationEventPublisher = null)
        {
            return new ICommandHandler<WebViewerApplication>[]
            {
                new InitializeWebViewerCommandHandler(),
                new SelectWebViewerElementsCommandHandler(),
                new ClearWebViewerSelectionCommandHandler(),
                new DisposeWebViewerCommandHandler(),
                new ViewerAuthenticationCommandHandler<WebViewerApplication>(
                    authenticationEventPublisher)
            };
        }
    }

    public sealed class InitializeWebViewerCommandHandler :
        ICommandHandler<WebViewerApplication>
    {
        private static readonly IReadOnlyList<string> Names =
            new[] { "initialize_viewer" };

        public IReadOnlyList<string> CommandNames => Names;

        public async Task<CommandResult> HandleAsync(
            CommandExecutionContext<WebViewerApplication> context,
            CancellationToken cancellationToken)
        {
            if (!context.Command.TryReadPayload(
                    out WebViewerInitializeRequest request,
                    out string error))
            {
                return CommandResult.Failure("invalid_payload", error);
            }

            CommandOperationResult result =
                await context.Application.InitializeAsync(
                    request,
                    context.Command.Metadata.RemoteEndpoint,
                    cancellationToken);
            return WebViewerCommandResultMapper.Map(result);
        }
    }

    public sealed class SelectWebViewerElementsCommandHandler :
        ICommandHandler<WebViewerApplication>
    {
        private static readonly IReadOnlyList<string> Names =
            new[] { "select_elements" };

        public IReadOnlyList<string> CommandNames => Names;

        public async Task<CommandResult> HandleAsync(
            CommandExecutionContext<WebViewerApplication> context,
            CancellationToken cancellationToken)
        {
            if (!context.Command.TryReadPayload(
                    out WebViewerSelectionRequest request,
                    out string error))
            {
                return CommandResult.Failure("invalid_payload", error);
            }

            CommandOperationResult result = await context.Application.SelectAsync(
                request,
                context.Command.Metadata.RemoteEndpoint,
                cancellationToken);
            return WebViewerCommandResultMapper.Map(result);
        }
    }

    public sealed class ClearWebViewerSelectionCommandHandler :
        ICommandHandler<WebViewerApplication>
    {
        private static readonly IReadOnlyList<string> Names =
            new[] { "clear_selection" };

        public IReadOnlyList<string> CommandNames => Names;

        public async Task<CommandResult> HandleAsync(
            CommandExecutionContext<WebViewerApplication> context,
            CancellationToken cancellationToken)
        {
            if (!context.Command.TryReadPayload(
                    out WebViewerRevisionRequest request,
                    out string error))
            {
                return CommandResult.Failure("invalid_payload", error);
            }

            CommandOperationResult result = await context.Application.ClearAsync(
                request,
                context.Command.Metadata.RemoteEndpoint,
                cancellationToken);
            return WebViewerCommandResultMapper.Map(result);
        }
    }

    public sealed class DisposeWebViewerCommandHandler :
        ICommandHandler<WebViewerApplication>
    {
        private static readonly IReadOnlyList<string> Names =
            new[] { "dispose_viewer" };

        public IReadOnlyList<string> CommandNames => Names;

        public async Task<CommandResult> HandleAsync(
            CommandExecutionContext<WebViewerApplication> context,
            CancellationToken cancellationToken)
        {
            if (!context.Command.TryReadPayload(
                    out WebViewerRevisionRequest request,
                    out string error))
            {
                return CommandResult.Failure("invalid_payload", error);
            }

            CommandOperationResult result =
                await context.Application.DisposeViewerAsync(
                    request,
                    context.Command.Metadata.RemoteEndpoint,
                    cancellationToken);
            return WebViewerCommandResultMapper.Map(result);
        }
    }

    internal static class WebViewerCommandResultMapper
    {
        public static CommandResult Map(CommandOperationResult result)
        {
            return result.Succeeded
                ? CommandResult.Success(result.Payload)
                : CommandResult.Failure(result.ErrorCode, result.Message, result.Payload);
        }
    }
}
