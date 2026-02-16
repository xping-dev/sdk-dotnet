/*
 * © 2024 Xping.io. All Rights Reserved.
 *
 * This file is part of the Xping Solution.
 */

using System.Runtime.CompilerServices;

namespace Xping.Sdk.Shared;

/// <summary>
/// This class provides extension methods to help verify parameters' validity.
/// </summary>
public static class ArgumentValidation
{
    /// <summary>
    /// Basic Validation helper to verify parameter validity.
    /// </summary>
    /// <typeparam name="T">The instance type</typeparam>
    /// <param name="obj">The parameter instance to verify</param>
    /// <param name="parameterName">The parameter name to verify</param>
    /// <param name="memberName">The name of the calling member (automatically populated)</param>
    /// <param name="sourceFilePath">The file path of the calling code (automatically populated)</param>
    /// <param name="sourceLineNumber">The line number of the calling code (automatically populated)</param>
    /// <returns>The instance that was passed to verify</returns>
    public static T RequireNotNull<T>(
        this T? obj,
        [CallerArgumentExpression(nameof(obj))] string parameterName = "",
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
        where T : notnull
    {
        if (obj == null)
        {
            throw new ArgumentNullException(
                parameterName,
                $"Argument is null. " +
                $"CallerMemberName={memberName}, " +
                $"CallerFilePath={sourceFilePath}, " +
                $"CallerSourceLineNumber={sourceLineNumber}");
        }

        return obj;
    }

    /// <summary>
    /// Basic Validation helper to verify nullable value type parameter validity.
    /// </summary>
    /// <typeparam name="T">The value type</typeparam>
    /// <param name="obj">The parameter instance to verify</param>
    /// <param name="parameterName">The parameter name to verify</param>
    /// <param name="memberName">The name of the calling member (automatically populated)</param>
    /// <param name="sourceFilePath">The file path of the calling code (automatically populated)</param>
    /// <param name="sourceLineNumber">The line number of the calling code (automatically populated)</param>
    /// <returns>The instance that was passed to verify</returns>
    public static T RequireNotNull<T>(
        this T? obj,
        [CallerArgumentExpression(nameof(obj))] string parameterName = "",
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
        where T : struct
    {
        if (!obj.HasValue)
        {
            throw new ArgumentNullException(
                parameterName,
                $"Argument is null. " +
                $"CallerMemberName={memberName}, " +
                $"CallerFilePath={sourceFilePath}, " +
                $"CallerSourceLineNumber={sourceLineNumber}");
        }

        return obj.Value;
    }

    /// <summary>
    /// Verify the validity of a parameter instance through a condition.
    /// </summary>
    /// <typeparam name="T">The instance type</typeparam>
    /// <param name="obj">The parameter instance to verify</param>
    /// <param name="condition">The condition to test for</param>
    /// <param name="parameterName">The parameter name to verify</param>
    /// <param name="message">The message to post when the parameter instance fails validation</param>
    /// <param name="memberName">The name of the calling member (automatically populated)</param>
    /// <param name="sourceFilePath">The file path of the calling code (automatically populated)</param>
    /// <param name="sourceLineNumber">The line number of the calling code (automatically populated)</param>
    /// <returns>The instance that was passed to verify</returns>
    public static T RequireCondition<T>(
        this T obj,
        Func<T, bool> condition,
        string message,
        [CallerArgumentExpression(nameof(obj))] string parameterName = "",
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        if (obj == null)
        {
            throw new ArgumentNullException(
                nameof(obj),
                $"Argument is null. " +
                $"CallerMemberName={memberName}, " +
                $"CallerFilePath={sourceFilePath}, " +
                $"CallerSourceLineNumber={sourceLineNumber}");
        }

        if (condition == null)
        {
            throw new ArgumentNullException(
                nameof(condition),
                $"Argument is null. " +
                $"CallerMemberName={memberName}, " +
                $"CallerFilePath={sourceFilePath}, " +
                $"CallerSourceLineNumber={sourceLineNumber}");
        }

        if (parameterName == null)
        {
            throw new ArgumentNullException(
                nameof(parameterName),
                $"Argument is null. " +
                $"CallerMemberName={memberName}, " +
                $"CallerFilePath={sourceFilePath}, " +
                $"CallerSourceLineNumber={sourceLineNumber}");
        }

        if (!condition(obj))
        {
            throw new ArgumentException(
                message + $", " +
                $"CallerMemberName={memberName}, " +
                $"CallerFilePath={sourceFilePath}, " +
                $"CallerSourceLineNumber={sourceLineNumber}",
                parameterName);
        }

        return obj;
    }

    /// <summary>
    /// Verify a string parameter to make sure it's not null or while space.
    /// </summary>
    /// <param name="obj">The parameter instance to verify</param>
    /// <param name="parameterName">The parameter name to verify</param>
    /// <param name="memberName">The name of the calling member (automatically populated)</param>
    /// <param name="sourceFilePath">The file path of the calling code (automatically populated)</param>
    /// <param name="sourceLineNumber">The line number of the calling code (automatically populated)</param>
    /// <returns>The instance that was passed to verify</returns>
    public static string RequireNotNullOrWhiteSpace(
        this string obj,
        [CallerArgumentExpression(nameof(obj))] string parameterName = "",
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        if (obj == null)
        {
            throw new ArgumentNullException(
                nameof(obj),
                $"Argument is null. " +
                $"CallerMemberName={memberName}, " +
                $"CallerFilePath={sourceFilePath}, " +
                $"CallerSourceLineNumber={sourceLineNumber}");
        }

        if (parameterName == null)
        {
            throw new ArgumentNullException(
                nameof(parameterName),
                $"Argument is null. " +
                $"CallerMemberName={memberName}, " +
                $"CallerFilePath={sourceFilePath}, " +
                $"CallerSourceLineNumber={sourceLineNumber}");
        }

        if (string.IsNullOrWhiteSpace(obj))
        {
            throw new ArgumentException(
                $"Argument exception. " +
                $"CallerMemberName={memberName}, " +
                $"CallerFilePath={sourceFilePath}, " +
                $"CallerSourceLineNumber={sourceLineNumber}",
                parameterName);
        }

        return obj;
    }
}
