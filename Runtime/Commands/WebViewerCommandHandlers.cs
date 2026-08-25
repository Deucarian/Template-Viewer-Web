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
            IViewerAuthenticationEventPublisher authenticationEventPublisher = null,
            bool includeGenericVisibilityCommands = true,
            ICommandHandler<WebViewerApplication> initializationHandler = null)
        {
            var handlers = new List<ICommandHandler<WebViewerApplication>>
            {
                initializationHandler ?? new InitializeWebViewerCommandHandler(),
                new DisposeWebViewerCommandHandler(),
                new ViewerAuthenticationCommandHandler<WebViewerApplication>(
                    authenticationEventPublisher)
            };
            if (includeGenericVisibilityCommands)
            {
                handlers.Insert(1, new SelectWebViewerElementsCommandHandler());
                handlers.Insert(2, new ClearWebViewerSelectionCommandHandler());
            }

            return handlers;
        }
    }

    public sealed class InitializeWebViewerCommandHandler :
        ICommandHandler<WebViewerApplication>
    {
        public const string CommandName = "initialize_viewer";

        private static readonly IReadOnlyList<string> Names =
            new[] { CommandName };

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
