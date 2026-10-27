namespace Suzume.Core;

/// <summary>信号が off → on に変わった瞬間だけ true を返す(手招きの 1 回分を拾う)。</summary>
public sealed class RisingEdge
{
    private bool previous;

    public bool Update(bool signal)
    {
        var fired = signal && !previous;
        previous = signal;
        return fired;
    }

    public void Reset() => previous = false;
}
