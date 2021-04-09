// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System
{
    public static partial class Environment
    {
        public static partial class NUMA
        {
            public static bool IsAvailable { get; } = IsNumaAvailable();
        }
    }
}
