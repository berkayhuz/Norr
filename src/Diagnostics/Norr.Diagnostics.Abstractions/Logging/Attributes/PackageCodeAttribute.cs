// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 

using System;

namespace Norr.Diagnostics.Abstractions.Logging.Attributes;

/// <summary>Bir Norr paketinin kısa kodunu tanımlar (örn: "PM", "PAN", "PAT").</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class PackageCodeAttribute : Attribute
{
    public PackageCodeAttribute(string code) => Code = code;
    public string Code
    {
        get;
    }
}
