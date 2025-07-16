using System.Net.Mime;
using Domain.Common;
using Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Api.Filters;

public class ExceptionHandler(ILogger<ExceptionHandler> logger) : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        logger.LogError(context.Exception, "Unhandled exception");

        ApiResult result = context.Exception switch
        {
            null => throw new NotImplementedException(),
            BadRequestException => new BadRequestResult(),
            NotFoundException nfx => nfx,
            AppException appException => appException,
            _ => new ApiResult(false, ApiResultStatusCode.ServerError, Message: context.Exception.Message)
        };

        context.HttpContext.Response.ContentType = MediaTypeNames.Application.ProblemJson;  
        context.Result = new ObjectResult(result);

        await Task.CompletedTask;
    }
}