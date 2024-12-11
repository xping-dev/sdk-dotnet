namespace Xping.Sdk.Validations.Content;

/// <summary>
/// Represents an HTML attribute with a name and value.
/// </summary>
/// <param name="Name">The qualified name of the attribute.</param>
/// <param name="Value">The value of the attribute.</param>
public record HtmlAttribute(string Name, string Value);
