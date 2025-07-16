using Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Api.Filters;

public class ValidateModelStateAttribute : ActionFilterAttribute

{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // 1. Run FluentValidation for each action argument
        foreach (var kvp in context.ActionArguments)
        {
            var argument = kvp.Value;
            if (argument == null)
                continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            var validator = context.HttpContext.RequestServices.GetService(validatorType) as IValidator;
            if (validator == null)
                continue;

            var validationResult = await validator.ValidateAsync(new ValidationContext<object>(argument));
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray()
                    );
                context.Result = new BadRequestObjectResult(new ValidationProblemDetails(errors));
                return;
            }
        }

        // 2. Fallback to default ModelState validation
        if (!context.ModelState.IsValid)
        {
            context.Result = new BadRequestObjectResult(context.ModelState);
            return;
        }

        await next();
    }
}

public class ApiResultFilterAttribute : ActionFilterAttribute
{
    public override async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is OkObjectResult okObjectResult) //Ok (object)
        {
            var apiResult = new ApiResult<object>(true, ApiResultStatusCode.Success, okObjectResult.Value);
            context.Result = new JsonResult(apiResult) { StatusCode = okObjectResult.StatusCode };
        }
        else if (context.Result is OkResult okResult) //Ok()
        {
            var apiResult = new ApiResult(true, ApiResultStatusCode.Success);
            context.Result = new JsonResult(apiResult) { StatusCode = okResult.StatusCode };
        }
        else if (context.Result is CreatedAtActionResult createdAtResult)
        {
            var apiResult = new ApiResult<object>(
                true,
                ApiResultStatusCode.Success,
                createdAtResult.Value
            );

            context.Result = new CreatedAtActionResult(
                createdAtResult.ActionName,
                createdAtResult.ControllerName,
                createdAtResult.RouteValues,
                apiResult
            )
            {
                StatusCode = createdAtResult.StatusCode
            };
        }
        else if (context.Result is ObjectResult badRequestObjectResult && badRequestObjectResult.StatusCode == 400)
        {
            string? message = null;
            string jsonValidationMessage = null;
            switch (badRequestObjectResult.Value)
            {
                case ValidationProblemDetails validationProblemDetails:
                    var errorMessages = validationProblemDetails.Errors.SelectMany(p => p.Value).Distinct();
                    message = string.Join(" | ", errorMessages);
                    // save validation as json, for sending to FrontEnd
                    Dictionary<string, string> validationObj = new Dictionary<string, string>();
                    foreach (var validationErrorItem in validationProblemDetails.Errors)
                    {
                        string key = validationErrorItem.Key.Split('.').Count() > 1
                            ? validationErrorItem.Key.Split('.')[1]
                            : validationErrorItem.Key;
                        validationObj.Add(key, validationErrorItem.Value[0]);
                    }
                    
                    break;
                case SerializableError errors:
                    var errorMessages2 = errors.SelectMany(p => (string[])p.Value).Distinct();
                    message = string.Join(" | ", errorMessages2);
                    break;
                case var value when value != null && !(value is ProblemDetails):
                    message = badRequestObjectResult.Value?.ToString();
                    break;
            }

            var apiResult = new ApiResult(false, ApiResultStatusCode.BadRequest, message, jsonValidationMessage);
            context.Result = new JsonResult(apiResult) { StatusCode = badRequestObjectResult.StatusCode };
        }
        else if (context.Result is ObjectResult notFoundObjectResult && notFoundObjectResult.StatusCode == 404)
        {
            string? message = null;
            if (notFoundObjectResult.Value != null && !(notFoundObjectResult.Value is ProblemDetails))
                message = notFoundObjectResult.Value.ToString();

            //var apiResult = new ApiResult<object>(false, ApiResultStatusCode.NotFound, notFoundObjectResult.Value);
            var apiResult = new ApiResult(false, ApiResultStatusCode.NotFound, message);
            context.Result = new JsonResult(apiResult) { StatusCode = notFoundObjectResult.StatusCode };
        }
        else if (context.Result is ContentResult contentResult)
        {
            var apiResult = new ApiResult(true, ApiResultStatusCode.Success, contentResult.Content);
            context.Result = new JsonResult(apiResult) { StatusCode = contentResult.StatusCode };
        }
        else if (context.Result is ObjectResult objectResult && objectResult.StatusCode == null
                                                             && !(objectResult.Value is ApiResult))
        {
            var apiResult = new ApiResult<object>(true, ApiResultStatusCode.Success, objectResult.Value);
            context.Result = new JsonResult(apiResult) { StatusCode = objectResult.StatusCode };
        }

        await base.OnResultExecutionAsync(context, next);
    }
}