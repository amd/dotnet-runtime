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

            private long _totalBottomOuts;
            private long _lastContentionAdjustment;
            private readonly long _countThreshold;
            private readonly short _stepDown;
            private readonly long _contentionAdjustmentThreshold;

            public bool ContentionDetected
            {
                get => Interlocked.Read(ref _totalBottomOuts) >= _countThreshold;
            }

            public bool AdjustingForContention
            {
                get => (Environment.TickCount - Interlocked.Read(ref _lastContentionAdjustment)) <= _contentionAdjustmentThreshold;
            }

            public short StepDown
            {
                get => _stepDown;
            }

            private Contention()
            {
                _countThreshold = AppContextConfigHelper.GetInt32Config("System.Threading.ThreadPool.Contention.Threshold", 10, false);
                _stepDown = AppContextConfigHelper.GetInt16Config("System.Threading.ThreadPool.Contention.StepDown", 4, false);
                _contentionAdjustmentThreshold = AppContextConfigHelper.GetInt32Config("System.Threading.ThreadPool.Contention.AdjustmentThreshold", 500);
                _totalBottomOuts = 0;
                _lastContentionAdjustment = Environment.TickCount;
            }

            public void ReportBottomOut()
            {
                Interlocked.Increment(ref _totalBottomOuts);
            }

            public void ReportThreadBoundsChange()
            {
                Interlocked.Exchange(ref _totalBottomOuts, 0);
                Interlocked.Exchange(ref _lastContentionAdjustment, Environment.TickCount);
            }
        }
    }
}
