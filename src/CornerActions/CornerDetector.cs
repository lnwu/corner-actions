namespace CornerActions;

internal enum HotCorner
{
    None = 0,
    TopLeft = 1,
    TopRight = 2,
}

/// <summary>
/// Decides when a hot corner turns into an action. This type is pure: it owns no
/// timers, touches no Win32 API, and takes the current time as an argument, so
/// every rule below is covered by <see cref="SelfTest"/>.
///
/// State machine:
///   idle    --cursor enters a corner-->  dwelling (start timestamp)
///   dwelling --cursor leaves-->          idle
///   dwelling --stayed >= dwellMs-->      fired (action runs, cooldown starts)
///   fired   --cursor leaves the corner (with hysteresis)--> idle
///
/// The "fired" state is why a single hover can never turn the display off twice:
/// after the action runs the cursor has to leave the corner before the same
/// corner can fire again. The cooldown adds a second guard for the case where
/// the pointer is moved out and immediately back in.
/// </summary>
internal sealed class CornerDetector
{
    /// <summary>
    /// Extra pixels the cursor must travel past the corner zone before the corner
    /// re-arms. Without it, a pointer parked on the zone edge would oscillate
    /// between "inside" and "outside" and fire repeatedly.
    /// </summary>
    private const int HysteresisPx = 4;

    private readonly int _cornerSizeDips;
    private readonly int _dwellMs;
    private readonly int _cooldownMs;

    private HotCorner _dwellCorner = HotCorner.None;
    private long _dwellStartMs;
    private HotCorner _firedCorner = HotCorner.None;
    private long _cooldownUntilMs;

    internal CornerDetector(int cornerSizeDips, int dwellMs, int cooldownMs)
    {
        _cornerSizeDips = Math.Max(1, cornerSizeDips);
        _dwellMs = Math.Max(0, dwellMs);
        _cooldownMs = Math.Max(0, cooldownMs);
    }

    /// <summary>Forgets all state, e.g. after the monitor layout changed.</summary>
    internal void Reset()
    {
        _dwellCorner = HotCorner.None;
        _dwellStartMs = 0;
        _firedCorner = HotCorner.None;
        _cooldownUntilMs = 0;
    }

    /// <summary>
    /// Feeds one sample of the cursor position. Returns the corner whose action
    /// should run on this sample, or <see cref="HotCorner.None"/>.
    /// </summary>
    internal HotCorner Evaluate(
        IReadOnlyList<MonitorInfo> monitors,
        bool topLeftEnabled,
        bool topRightEnabled,
        int cursorX,
        int cursorY,
        long nowMs)
    {
        var hit = HitTest(monitors, topLeftEnabled, topRightEnabled, cursorX, cursorY, 0);

        // A corner that already fired stays latched until the pointer leaves it by
        // HysteresisPx, so a parked pointer can never fire twice.
        if (_firedCorner != HotCorner.None)
        {
            if (HitTest(monitors, topLeftEnabled, topRightEnabled, cursorX, cursorY, HysteresisPx) == _firedCorner)
            {
                _dwellCorner = HotCorner.None;
                return HotCorner.None;
            }

            _firedCorner = HotCorner.None;
        }

        if (hit == HotCorner.None)
        {
            _dwellCorner = HotCorner.None;
            return HotCorner.None;
        }

        if (_dwellCorner != hit)
        {
            _dwellCorner = hit;
            _dwellStartMs = nowMs;
            return HotCorner.None;
        }

        if (nowMs - _dwellStartMs < _dwellMs || nowMs < _cooldownUntilMs)
        {
            return HotCorner.None;
        }

        _firedCorner = hit;
        _dwellCorner = HotCorner.None;
        _cooldownUntilMs = nowMs + _cooldownMs;
        return hit;
    }

    /// <summary>
    /// Returns the corner that contains the point. A point outside every monitor
    /// rectangle never hits, which also re-arms a corner when the pointer leaves
    /// the screen edge in a negative-coordinate multi-monitor layout.
    /// </summary>
    private HotCorner HitTest(
        IReadOnlyList<MonitorInfo> monitors,
        bool topLeftEnabled,
        bool topRightEnabled,
        int x,
        int y,
        int marginPx)
    {
        foreach (var monitor in monitors)
        {
            if (x < monitor.Left || x >= monitor.Right || y < monitor.Top || y >= monitor.Bottom)
            {
                continue;
            }

            var size = MonitorLayout.CornerSizePx(monitor, _cornerSizeDips) + marginPx;

            if (topLeftEnabled && x < monitor.Left + size && y < monitor.Top + size)
            {
                return HotCorner.TopLeft;
            }

            if (topRightEnabled && x >= monitor.Right - size && y < monitor.Top + size)
            {
                return HotCorner.TopRight;
            }
        }

        return HotCorner.None;
    }
}
