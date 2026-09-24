using System.ComponentModel.DataAnnotations;

namespace UserManagementAPI.Models;

/// <summary>Payload used to create or update a user.</summary>
public record UserRequest(
    [Required, StringLength(50), RegularExpression(UserRequest.TextPattern, ErrorMessage = UserRequest.TextError)] string FirstName,
    [Required, StringLength(50), RegularExpression(UserRequest.TextPattern, ErrorMessage = UserRequest.TextError)] string LastName,
    // [EmailAddress] accepts "a@b"; require a dotted domain and no whitespace.
    [Required, StringLength(254), RegularExpression(UserRequest.EmailPattern, ErrorMessage = "The Email field is not a valid e-mail address.")] string Email,
    [Required, StringLength(50), RegularExpression(UserRequest.TextPattern, ErrorMessage = UserRequest.TextError)] string Department)
{
    public const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

    // Rejects control characters (newlines, tabs, ...), which enable log injection and break display.
    public const string TextPattern = @"^[^\p{Cc}]+$";
    public const string TextError = "The {0} field must not contain control characters.";

    /// <summary>Returns a copy with surrounding whitespace removed from every field.</summary>
    public UserRequest Normalized() => new(FirstName.Trim(), LastName.Trim(), Email.Trim(), Department.Trim());
}
