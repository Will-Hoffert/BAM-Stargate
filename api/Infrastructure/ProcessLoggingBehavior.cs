using MediatR;
using StargateAPI.Business.Data;
using StargateAPI.Controllers;
using System.Diagnostics;
using System.Text.Json;

namespace StargateAPI.Infrastructure
{
    // Writes one ProcessLog row for every MediatR request, saying how it ended. Program.cs adds it before the
    // pre-processor behavior, so it also records requests that validation rejects.
    public class ProcessLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
    {
        // The name that identifies a person has no length limit, so without a cap a request could fill the log.
        public const int MaxRequestJsonLength = 4000;

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ProcessLoggingBehavior<TRequest, TResponse>> _logger;

        public ProcessLoggingBehavior(IServiceScopeFactory scopeFactory, ILogger<ProcessLoggingBehavior<TRequest, TResponse>> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            // Taken before the pre-processors write cleaned values back into the request, so it shows what the caller sent.
            var requestJson = SerializeRequest(request);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var response = await next();
                await WriteAsync(ProcessLogLevel.Information, (response as BaseResponse)?.Message ?? "Successful", requestJson, null, stopwatch.ElapsedMilliseconds);
                return response;
            }
            catch (BadHttpRequestException ex)
            {
                await WriteAsync(ProcessLogLevel.Warning, ex.Message, requestJson, ex, stopwatch.ElapsedMilliseconds);
                throw;
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                await WriteAsync(ProcessLogLevel.Warning, "The request was cancelled.", requestJson, ex, stopwatch.ElapsedMilliseconds);
                throw;
            }
            catch (Exception ex)
            {
                await WriteAsync(ProcessLogLevel.Error, GlobalExceptionHandler.UnexpectedErrorMessage, requestJson, ex, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        private static string SerializeRequest(TRequest request)
        {
            string json;
            try
            {
                json = JsonSerializer.Serialize(request, JsonOptions);
            }
            catch (Exception)
            {
                // Logging must never stop a request from running.
                return "(the request could not be serialized)";
            }

            return json.Length <= MaxRequestJsonLength ? json : json[..MaxRequestJsonLength] + "... (truncated)";
        }

        private async Task WriteAsync(ProcessLogLevel level, string message, string requestJson, Exception? exception, long durationMs)
        {
            try
            {
                // A context of its own: after a failure the request's context still holds the changes that failed,
                // and saving the log row through it would try to save those too.
                await using var scope = _scopeFactory.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<StargateContext>();

                context.ProcessLogs.Add(new ProcessLog
                {
                    TimestampUtc = DateTime.UtcNow,
                    Level = level,
                    Operation = typeof(TRequest).Name,
                    Message = message,
                    RequestJson = requestJson,
                    Exception = exception?.ToString(),
                    DurationMs = durationMs
                });

                // Not the request's token, so a cancelled request still gets its entry.
                await context.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                // The caller gets the response their request earned, even if it can't be logged.
                _logger.LogError(ex, "Could not write the ProcessLog entry for {Operation}.", typeof(TRequest).Name);
            }
        }
    }
}
