using Microsoft.AspNetCore.Diagnostics;
using StargateAPI.Controllers;
using System.Net;

namespace StargateAPI.Infrastructure
{
    // Turns any exception that escapes a controller into the BaseResponse envelope.
    // UseExceptionHandler has already logged the exception with its stack trace,
    // so the response only tells the caller what they need to know.
    public class GlobalExceptionHandler : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var response = exception switch
            {
                // Thrown for requests the API rejects; its message is written for the caller.
                BadHttpRequestException badRequest => new BaseResponse
                {
                    Success = false,
                    Message = badRequest.Message,
                    ResponseCode = badRequest.StatusCode
                },
                _ => new BaseResponse
                {
                    Success = false,
                    Message = "An unexpected error occurred.",
                    ResponseCode = (int)HttpStatusCode.InternalServerError
                }
            };

            httpContext.Response.StatusCode = response.ResponseCode;
            await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
            return true;
        }
    }
}
