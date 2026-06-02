using System.Collections.Generic;
using AutoClicker.Core;

namespace AutoClicker.Core.Tests.Fakes;

/// <summary>Returns queued values in order; repeats the last value once exhausted.</summary>
public sealed class FakeRandomSource : IRandomSource
{
    private readonly Queue<double> _values;
    private double _last;

    public FakeRandomSource(params double[] values)
    {
        _values = new Queue<double>(values);
        _last = values.Length > 0 ? values[^1] : 0.0;
    }

    public double NextDouble()
    {
        if (_values.Count > 0) _last = _values.Dequeue();
        return _last;
    }
}
