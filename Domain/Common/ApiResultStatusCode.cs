using System.ComponentModel.DataAnnotations;

namespace Domain.Common;

public enum ApiResultStatusCode
{
    [Display(Name = "Operation completed successfully")]
    Success = 0,

    [Display(Name = "Internal server error")]
    ServerError = 1,

    [Display(Name = "Bad request")]
    BadRequest = 2,

    [Display(Name = "Unauthorized")]
    Unauthorized = 3,

    [Display(Name = "Invalid username or password")]
    InvalidCredentials = 4,

    [Display(Name = "Access denied")]
    NoPermission = 5,

    [Display(Name = "Resource not found")]
    NotFound = 6,

    [Display(Name = "No items found")]
    ListEmpty = 7,

    [Display(Name = "Processing error")]
    LogicError = 8,

    [Display(Name = "Duplicate data")]
    Duplicate = 9,

    [Display(Name = "Unable to delete due to dependencies")]
    CouldNotDelete = 10,

    [Display(Name = "Resource is not editable")]
    UnEditable = 11,

    [Display(Name = "Resource is blocked")]
    Blocked = 12,

    [Display(Name = "Not implemented")]
    NotImplemented = 13,

    [Display(Name = "Border service error")]
    MarzbanError = 14,

    [Display(Name = "Telegram bot error")]
    TelegramError = 15
}
