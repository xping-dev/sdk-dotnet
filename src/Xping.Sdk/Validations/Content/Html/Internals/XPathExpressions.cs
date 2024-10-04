using System.Xml.XPath;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Validations.Content.Html.Internals;

internal class XPath(string name, XPathExpression expression, string? metaData = null)
{
    public string Name { get; } = name.RequireNotNullOrEmpty(nameof(name));
    public string? MetaData { get; } = metaData;
    public XPathExpression Expression { get; } = expression.RequireNotNull(nameof(expression));
}

internal static class XPaths
{
    // Fixed XPaths
    private static readonly Lazy<XPath> AltXPath = new(() =>
        new XPath("[@alt]", XPathExpression.Compile($"//*[@alt]"), metaData: "alt"));
    private static readonly Lazy<XPath> TitleXPath = new(() =>
        new XPath("<title>", XPathExpression.Compile($"//head/title")));
    private static readonly Lazy<XPath> TitleAttrXPath = new(() =>
        new XPath("[@title]", XPathExpression.Compile($"//*[@title]"), metaData: "title"));
    private static readonly Lazy<XPath> LabelXPath = new(() =>
        new XPath("<label>", XPathExpression.Compile($"//label")));
    private static readonly Lazy<XPath> PlaceholderXPath = new(() =>
        new XPath("[@placeholder]", XPathExpression.Compile($"//*[@placeholder]"), metaData: "placeholder"));
    private static readonly Lazy<XPath> ImageXPath = new(() =>
        new XPath("<img>", XPathExpression.Compile($"//img")));
    private static readonly Lazy<XPath> ScriptXPath = new(() =>
        new XPath("<script>", XPathExpression.Compile($"//script")));
    private static readonly Lazy<XPath> StyleSheetXPath = new(() =>
        new XPath("<link type=\"css\">", XPathExpression.Compile("//link[contains(@type, \"css\")]")));

    // Properties for fixed XPaths
    public static XPath Alt => AltXPath.Value;
    public static XPath Title => TitleXPath.Value;
    public static XPath TitleAttribute => TitleAttrXPath.Value;
    public static XPath Label => LabelXPath.Value;
    public static XPath Placeholder => PlaceholderXPath.Value;
    public static XPath Image => ImageXPath.Value;
    public static XPath Script => ScriptXPath.Value;
    public static XPath StyleSheet => StyleSheetXPath.Value;

    // Methods for parameterized XPaths
    public static XPath GetMetaTag(HtmlAttribute attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);
    
        return new XPath(
            $"<meta[{attribute.Name}=\"{attribute.Value}\"]>", 
            XPathExpression.Compile($"//meta[@{attribute.Name}='{attribute.Value}']"),
            metaData: attribute.Name);
    }

    public static XPath TestIdAttribute(string testIdAttribute)
    {
        ArgumentException.ThrowIfNullOrEmpty(testIdAttribute);

        return new XPath(
            $"//[@{testIdAttribute}]", 
            XPathExpression.Compile($"//*[@{testIdAttribute}]"),
            metaData: testIdAttribute);
    }
}
