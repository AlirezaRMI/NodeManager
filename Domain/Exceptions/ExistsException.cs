using System.Net;
using Domain.Common;

namespace Domain.Exceptions;

public class ExistsException : AppException
{
    public ExistsException()
        : base(ApiResultStatusCode.Duplicate)
    {
    }

    public ExistsException(string message)
        : base(ApiResultStatusCode.Duplicate, message)
    {
    }

    public ExistsException(object additionalData)
        : base(ApiResultStatusCode.Duplicate, additionalData)
    {
    }

    public ExistsException(string message, object additionalData)
        : base(ApiResultStatusCode.Duplicate, message, additionalData)
    {
    }

    public ExistsException(string message, HttpStatusCode httpStatusCode)
        : base(ApiResultStatusCode.Duplicate, message, httpStatusCode)
    {
    }

    public ExistsException(string message, HttpStatusCode httpStatusCode, object additionalData)
        : base(ApiResultStatusCode.Duplicate, message, httpStatusCode, additionalData)
    {
    }

    public ExistsException(string message, Exception innerException)
        : base(ApiResultStatusCode.Duplicate, message, innerException)
    {
    }

    public ExistsException(string message, Exception innerException, object additionalData)
        : base(ApiResultStatusCode.Duplicate, message, innerException, additionalData)
    {
    }

    public ExistsException(string message, HttpStatusCode httpStatusCode, Exception innerException)
        : base(ApiResultStatusCode.Duplicate, message, httpStatusCode, innerException)
    {
    }

    public ExistsException(string message, HttpStatusCode httpStatusCode, Exception innerException,
        object additionalData)
        : base(ApiResultStatusCode.Duplicate, message, httpStatusCode, innerException, additionalData)
    {
    }
}