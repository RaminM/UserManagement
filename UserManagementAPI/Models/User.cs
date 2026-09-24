namespace UserManagementAPI.Models;

public record User(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    string Department);
