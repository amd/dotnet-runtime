// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Threading
{
    internal sealed partial class PortableThreadPool
    {
        private class Contention
        {
            public static readonly Contention ThreadPoolContention = new Contention();

            public static readonly bool IsDisabled = AppContextConfigHelper.GetBooleanConfig("System.Threading.ThreadPool.Contention.Disable", false);

            private long _totalWaits;
            private readonly long _waitThreshold;
            private readonly short _stepDown;

            public bool ContentionDetected
            {
                get => _totalWaits <= _waitThreshold;
            }

            public short StepDown
            {
                get => _stepDown;
            }

            private Contention()
            {
                _waitThreshold = AppContextConfigHelper.GetInt32Config("System.Threading.ThreadPool.Contention.Threshold", 30, false);
                _stepDown = AppContextConfigHelper.GetInt16Config("System.Threading.ThreadPool.Contention.StepDown", 4, false);
                _totalWaits = 0;
            }

            public void ReportWait() => Interlocked.Increment(ref _totalWaits);

            public void ReportWork() => ResetWaits();

            public void ReportThreadCountChange() => ResetWaits();

            private void ResetWaits() => Interlocked.Exchange(ref _totalWaits, 0);
        }
    }
}
