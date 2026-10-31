// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Yarp.Application.Configuration;

/// <summary>
/// Source-generated metadata used to dump the resolved config in debug mode (trim/AOT friendly).
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(YarpAppConfig))]
internal sealed partial class YarpAppConfigJsonContext : JsonSerializerContext
{
}
