/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Globalization;
using System.Runtime.CompilerServices;

namespace Xping.Sdk.Shared;

/// <summary>
/// This class provide extension methods to help verify parameters validity.
/// </summary>
internal static class ArgumentValidation
{
    /// <summary>
    /// Basic Validation helper to verify parameter validity.
    /// </summary>
    /// <typeparam name="T">The instance type</typeparam>
    /// <param name="obj">The parameter instance to verify</param>
    /// <param name="parameterName">The parameter name to verify (automatically captured)</param>
    /// <param name="memberName">The name of the calling member (automatically captured)</param>
    /// <param name="sourceFilePath">The source file path of the caller (automatically captured)</param>
    /// <param name="sourceLineNumber">The line number in the source file of the caller (automatically captured)</param>
    /// <returns>The instance that was passed to verify</returns>
    public static T RequireNotNull<T>(
        this T? obj,
        [CallerArgumentExpression(nameof(obj))] string parameterName = "",
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(parameterName, nameof(parameterName));

        if (obj == null)
        {
            throw new ArgumentNullException(parameterName,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Argument value is null. CallerMemberName={0}, CallerFilePath={1}, CallerSourceLineNumber={2}",
                    memberName,
                    sourceFilePath,
                    sourceLineNumber));
        }

        return obj!;
    }

    /// <summary>
    /// Verify validity of parameter instance through a condition.
    /// </summary>
    /// <typeparam name="T">The instance type</typeparam>
    /// <param name="obj">The parameter instance to verify</param>
    /// <param name="condition">The condition to test for</param>
    /// <param name="message">The message to post when the parameter instance fails validation</param>
    /// <param name="parameterName">The parameter name to verify (automatically captured)</param>
    /// <param name="memberName">The name of the calling member (automatically captured)</param>
    /// <param name="sourceFilePath">The source file path of the caller (automatically captured)</param>
    /// <param name="sourceLineNumber">The line number in the source file of the caller (automatically captured)</param>
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
        ArgumentException.ThrowIfNullOrEmpty(parameterName, nameof(parameterName));
        ArgumentException.ThrowIfNullOrEmpty(message, nameof(message));
        ArgumentNullException.ThrowIfNull(obj, parameterName);
        ArgumentNullException.ThrowIfNull(condition, nameof(condition));

        if (!condition(obj))
        {
            throw new ArgumentException(
                message + string.Format(
                    CultureInfo.InvariantCulture,
                    ", CallerMemberName={0}, CallerFilePath={1}, CallerSourceLineNumber={2}",
                    memberName,
                    sourceFilePath,
                    sourceLineNumber), parameterName);
        }

        return obj;
    }

    /// <summary>
    /// Vefiy string parameter to make sure it's not null or while space.
    /// </summary>
    /// <param name="obj">The parameter instance to verify</param>
    /// <param name="parameterName">The parameter name to verify (automatically captured)</param>
    /// <param name="memberName">The name of the calling member (automatically captured)</param>
    /// <param name="sourceFilePath">The source file path of the caller (automatically captured)</param>
    /// <param name="sourceLineNumber">The line number in the source file of the caller (automatically captured)</param>
    /// <returns>The instance that was passed to verify</returns>
    public static string RequireNotNullOrWhiteSpace(
        this string obj,
        [CallerArgumentExpression(nameof(obj))] string parameterName = "",
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        ArgumentException.ThrowIfNullOrEmpty(parameterName, nameof(parameterName));
        ArgumentNullException.ThrowIfNull(obj, parameterName);

        if (string.IsNullOrWhiteSpace(obj))
        {
            var msg = string.Format(
                CultureInfo.InvariantCulture,
                "Argument is white space. CallerMemberName={0}, CallerFilePath={1}, CallerSourceLineNumber={2}",
                memberName,
                sourceFilePath,
                sourceLineNumber);

            throw new ArgumentException(msg, parameterName);
        }

        return obj;
    }

    /// <summary>
    /// Verify string parameter to make sure it's not null or empty.
    /// </summary>
    /// <param name="obj">The parameter instance to verify</param>
    /// <param name="parameterName">The parameter name to verify (automatically captured)</param>
    /// <param name="memberName">The name of the calling member (automatically captured)</param>
    /// <param name="sourceFilePath">The source file path of the caller (automatically captured)</param>
    /// <param name="sourceLineNumber">The line number in the source file of the caller (automatically captured)</param>
    /// <returns>The instance that was passed to verify</returns>
    public static string RequireNotNullOrEmpty(
        this string obj,
        [CallerArgumentExpression(nameof(obj))] string parameterName = "",
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        ArgumentException.ThrowIfNullOrEmpty(parameterName, nameof(parameterName));
        ArgumentNullException.ThrowIfNull(obj, parameterName);

        if (string.IsNullOrEmpty(obj))
        {
            var msg = string.Format(
                CultureInfo.InvariantCulture,
                "Argument is empty. CallerMemberName={0}, CallerFilePath={1}, CallerSourceLineNumber={2}",
                memberName,
                sourceFilePath,
                sourceLineNumber);

            throw new ArgumentException(msg, parameterName);
        }

        return obj;
    }
}