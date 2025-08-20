// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 
using System.Diagnostics;

namespace Norr.Diagnostics.Logging.Activity;

public static class Operation
{
    public static System.Diagnostics.Activity? Start(string name) =>
        new System.Diagnostics.Activity(name).Start();
}
