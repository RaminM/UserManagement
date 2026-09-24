namespace UserManagementAPI.Middleware;

internal static class LogSanitizer
{
    /// <summary>
    /// Request paths are client-controlled (e.g. "%0A" decodes to a newline). Replace control
    /// characters so a crafted path can't forge extra log lines.
    /// </summary>
    public static string Clean(string value) =>
        string.Create(value.Length, value, static (span, source) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = char.IsControl(source[i]) ? '?' : source[i];
            }
        });
}
