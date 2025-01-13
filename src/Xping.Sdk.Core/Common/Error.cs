/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Diagnostics;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Core.Common;

/// <summary>
/// The Error class encapsulates the details of an error that occurs within the SDK. It has attributes: Code and 
/// Message. The Code represents a value that indicates the type of error, while the Message is a string that 
/// provides a human-readable description of the error. 
/// <note>
/// The Error class is intended to be used internally within the SDK.
/// </note>
/// </summary>
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public sealed class Error(string code, string message) : IEquatable<Error>
{
    /// <summary>
    /// A static field that represents an empty error with no code or message.
    /// </summary>
    public static readonly Error None = new(string.Empty, string.Empty);

    /// <summary>
    /// Gets the string representation of the code that indicates the type of error.
    /// </summary>
    public string Code { get; } = code.RequireNotNull(nameof(code));

    /// <summary>
    /// Gets the string representation of the error that provides human-readable description.
    /// </summary>
    public string Message { get; } = message.RequireNotNull(nameof(message));

    /// <summary>
    /// Determines whether the current Error object is equal to another Error object.
    /// </summary>
    /// <param name="other">The Error object to compare with the current object.</param>
    /// <returns><c>true</c> if the current object and other have the same value; otherwise, <c>false</c>.</returns>
    public bool Equals(Error? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (Code == null || other.Code == null)
        {
            return false;
        }

        return Code == other.Code;
    }

    /// <summary>
    /// Determines whether the current Error object is equal to a specified object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>
    /// <c>true</c> if the current object and obj are both Error objects and have the same value; otherwise, 
    /// <c>false</c>.
    /// </returns>
    public override bool Equals(object? obj)
    {
        return Equals(obj as Error);
    }

    /// <summary>
    /// Returns the hash code for the current Error object.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public override int GetHashCode()
    {
        return string.GetHashCode(Code, StringComparison.InvariantCulture);
    }

    /// <summary>
    /// Determines whether two Error objects have the same value.
    /// </summary>
    /// <param name="lhs">The first Error object to compare.</param>
    /// <param name="rhs">The second Error object to compare.</param>
    /// <returns><c>true</c> if lhs and rhs have the same value; otherwise, <c>false</c>.</returns>
    public static bool operator ==(Error? lhs, Error? rhs)
    {
        if (lhs is null || rhs is null)
        {
            return Equals(lhs, rhs);
        }

        return lhs.Equals(rhs);
    }

    /// <summary>
    /// Determines whether two Error objects have different values.
    /// </summary>
    /// <param name="lhs">The first Error object to compare.</param>
    /// <param name="rhs">The second Error object to compare.</param>
    /// <returns><c>true</c> if lhs and rhs have different values; otherwise, <c>false</c>.</returns>
    public static bool operator !=(Error? lhs, Error? rhs)
    {
        if (lhs is null || rhs is null)
        {
            return !Equals(lhs, rhs);
        }

        return !lhs.Equals(rhs);
    }

    /// <summary>
    /// Converts an Error object to a string representation.
    /// </summary>
    /// <param name="error">The Error object to convert.</param>
    /// <returns>A string that represents the error.</returns>
    public static implicit operator string(Error error) => error.RequireNotNull(nameof(error)).ToString();

    /// <summary>
    /// Returns a string that represents the current Error object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        return $"Error {Code}: {Message}";
    }

    private string GetDebuggerDisplay()
    {
        return ToString();
    }
}
