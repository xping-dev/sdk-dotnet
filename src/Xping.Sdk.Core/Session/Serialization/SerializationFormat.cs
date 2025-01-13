/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Session.Serialization;

/// <summary>
/// Specifies the format of the serialization for the property bag values.
/// </summary>
public enum SerializationFormat
{
    /// <summary>
    /// Indicates that the property bag values are serialized using binary encoding.
    /// </summary>
    Binary,
    /// <summary>
    /// Indicates that the property bag values are serialized using XML encoding.
    /// </summary>
    XML
}
