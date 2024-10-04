using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Xping.Sdk.Shared;

internal static class EnumExtensions
{
    /// <summary>
    /// Gets the enum display name.
    /// </summary>
    /// <param name="enumValue">The enum value.</param>
    /// <returns>Add DisplayAttribute if exists. Otherwise, use the standard string representation.</returns>
    public static string GetDisplayName(this Enum enumValue)
    {
        ArgumentNullException.ThrowIfNull(enumValue, nameof(enumValue));

        return enumValue.GetType()
                        .GetMember(enumValue.ToString())
                        .First()
                        .GetCustomAttribute<DisplayAttribute>()?
                        .GetName() ?? enumValue.ToString();
    }
}
