using Domain.Common;
using Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Filters;

/// <summary>
/// Represents the result of an API operation without strongly-typed data.
/// </summary>
public record ApiResult(
    bool IsSuccess,
    ApiResultStatusCode StatusCode,
    string? JsonValidationMessage = null,
    string Message = "")
{
    /// <summary>
    /// Implicit conversion from OkResult to a successful ApiResult.
    /// </summary>
    public static implicit operator ApiResult(OkResult result)
        => new(true, ApiResultStatusCode.Success);

    /// <summary>
    /// Implicit conversion from JsonResult to a successful ApiResult.
    /// </summary>
    public static implicit operator ApiResult(JsonResult result)
        => new(true, ApiResultStatusCode.Success);

    /// <summary>
    /// Implicit conversion from BadRequestResult to a failed ApiResult.
    /// </summary>
    public static implicit operator ApiResult(BadRequestResult result)
        => new(false, ApiResultStatusCode.BadRequest);

    /// <summary>
    /// Implicit conversion from ContentResult to a successful ApiResult, using the result's content as JsonValidationMessage.
    /// </summary>
    public static implicit operator ApiResult(ContentResult result)
        => new(true, ApiResultStatusCode.Success, result.Content);

    /// <summary>
    /// Implicit conversion from NotFoundResult to a failed ApiResult.
    /// </summary>
    public static implicit operator ApiResult(NotFoundResult result)
        => new(false, ApiResultStatusCode.NotFound);

    /// <summary>
    /// Implicit conversion from AppException to a failed ApiResult, using the exception's ApiStatusCode and Message.
    /// </summary>
    public static implicit operator ApiResult(AppException result)
        => new(false, result.ApiStatusCode, Message: result.Message);

    /// <summary>
    /// Implicit conversion from BadRequestObjectResult to a failed ApiResult, parsing error messages if present.
    /// </summary>
    public static implicit operator ApiResult(BadRequestObjectResult result)
    {
        var message = result.Value?.ToString();
        if (result.Value is SerializableError errors)
        {
            var errorMessages = errors.SelectMany(p => (string[])p.Value).Distinct();
            message = string.Join(" | ", errorMessages);
        }

        return new(false, ApiResultStatusCode.BadRequest, message);
    }
    

}

public record ApiResult<TData>(
    bool IsSuccess,
    ApiResultStatusCode StatusCode,
    TData? Data,
    string? JsonValidationMessage = null,
    string? Message = "") : ApiResult(IsSuccess, StatusCode, JsonValidationMessage, Message) where TData : class
{
    /// <summary>
    /// Implicit conversion from data to a successful ApiResult.
    /// </summary>
    public static implicit operator ApiResult<TData>(TData data)
        => new(true, ApiResultStatusCode.Success, data);

    /// <summary>
    /// Implicit conversion from OkResult to a successful ApiResult.
    /// </summary>
    public static implicit operator ApiResult<TData>(OkResult result)
        => new(true, ApiResultStatusCode.Success, null);

    /// <summary>
    /// Implicit conversion from JsonResult to a successful ApiResult.
    /// </summary>
    public static implicit operator ApiResult<TData>(JsonResult result)
        => new(true, ApiResultStatusCode.Success, null);

    /// <summary>
    /// Implicit conversion from OkObjectResult to a successful ApiResult, using the result's value as Data.
    /// </summary>
    public static implicit operator ApiResult<TData>(OkObjectResult result)
        => new(true, ApiResultStatusCode.Success, (TData)result.Value!);

    /// <summary>
    /// Implicit conversion from BadRequestResult to a failed ApiResult.
    /// </summary>
    public static implicit operator ApiResult<TData>(BadRequestResult result)
        => new(false, ApiResultStatusCode.BadRequest, null);

    /// <summary>
    /// Implicit conversion from AppException to a failed ApiResult, using the exception's AdditionalData and Message.
    /// </summary>
    public static implicit operator ApiResult<TData>(AppException result)
        => new(false,
            result.ApiStatusCode,
            (TData)result.AdditionalData,
            result.AdditionalData.ToString(),
            result.Message);

    /// <summary>
    /// Implicit conversion from UnauthorizedResult to a failed ApiResult.
    /// </summary>
    public static implicit operator ApiResult<TData>(UnauthorizedResult result)
        => new(false, ApiResultStatusCode.BadRequest, null);

    /// <summary>
    /// Implicit conversion from ContentResult to a successful ApiResult, using the result's content as JsonValidationMessage.
    /// </summary>
    public static implicit operator ApiResult<TData>(ContentResult result)
        => new(true, ApiResultStatusCode.Success, null, result.Content);

    /// <summary>
    /// Implicit conversion from NotFoundResult to a failed ApiResult.
    /// </summary>
    public static implicit operator ApiResult<TData>(NotFoundResult result)
        => new(false, ApiResultStatusCode.NotFound, null);

    /// <summary>
    /// Implicit conversion from NotFoundObjectResult to a failed ApiResult, using the result's value as Data.
    /// </summary>
    public static implicit operator ApiResult<TData>(NotFoundObjectResult result)
        => new(false, ApiResultStatusCode.NotFound, (TData)result.Value!);

    /// <summary>
    /// Implicit conversion from BadRequestObjectResult to a failed ApiResult, parsing error messages if present.
    /// </summary>
    public static implicit operator ApiResult<TData>(BadRequestObjectResult result)
    {
        var message = result.Value?.ToString();
        if (result.Value is SerializableError errors)
        {
            var errorMessages = errors.SelectMany(p => (string[])p.Value).Distinct();
            message = string.Join(" | ", errorMessages);
        }

        return new(false, ApiResultStatusCode.BadRequest, null, message);
    }
    
}