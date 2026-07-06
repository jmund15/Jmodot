namespace Jmodot.Implementation.UI.Motion;

using System;
using Godot;

/// <summary>
/// Fire-and-forget UI juice tweens. Each helper kills the target's previous
/// UiMotion tween (tracked via node metadata) before starting, so re-entrant
/// calls never stack. Scale pops centre the pivot; PulseModulate restores the
/// modulate captured before the first pulse (value-safe under re-entry).
/// </summary>
public static class UiMotion
{
    private static readonly StringName TweenMeta = new("_jmo_ui_motion_tween");
    private static readonly StringName BaseModulateMeta = new("_jmo_ui_motion_base_modulate");

    public static Tween HoverPop(Control target, float scale = 1.06f, float duration = 0.08f)
    {
        target.PivotOffset = target.Size / 2f;
        var tween = Fresh(target);
        tween.TweenProperty(target, "scale", new Vector2(scale, scale), duration)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        return tween;
    }

    public static Tween PressDepress(Control target, float scale = 0.94f, float duration = 0.06f)
    {
        target.PivotOffset = target.Size / 2f;
        var tween = Fresh(target);
        tween.TweenProperty(target, "scale", new Vector2(scale, scale), duration)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        return tween;
    }

    public static Tween ScaleReset(Control target, float duration = 0.08f)
    {
        var tween = Fresh(target);
        tween.TweenProperty(target, "scale", Vector2.One, duration)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        return tween;
    }

    public static Tween PanelSlideIn(Control panel, Vector2 fromOffset, float duration = 0.25f)
    {
        var restingPosition = panel.Position;
        panel.Position = restingPosition + fromOffset;
        var tween = Fresh(panel);
        tween.TweenProperty(panel, "position", restingPosition, duration)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        return tween;
    }

    /// <summary>Counts a label from one value to another. The format callback is
    /// a pure value→string projection (defaults to plain digits).</summary>
    public static Tween NumberTicker(
        Label label, long from, long to, float duration = 0.4f,
        Func<long, string>? format = null)
    {
        var tween = Fresh(label);
        tween.TweenMethod(Callable.From((float t) =>
        {
            var value = (long)Math.Round(from + ((to - from) * (double)t));
            label.Text = format is null ? value.ToString() : format(value);
        }), 0f, 1f, duration);
        return tween;
    }

    public static Tween PulseModulate(
        CanvasItem target, Color peak, float attack = 0.15f, float release = 0.35f)
    {
        var baseModulate = target.HasMeta(BaseModulateMeta)
            ? target.GetMeta(BaseModulateMeta).AsColor()
            : target.Modulate;
        target.SetMeta(BaseModulateMeta, baseModulate);
        var tween = Fresh(target);
        tween.TweenProperty(target, "modulate", peak, attack);
        tween.TweenProperty(target, "modulate", baseModulate, release);
        return tween;
    }

    public static Tween FadeOutAfter(CanvasItem target, float delay, float fadeDuration)
    {
        var tween = Fresh(target);
        tween.TweenInterval(delay);
        tween.TweenProperty(target, "modulate:a", 0f, fadeDuration);
        return tween;
    }

    /// <summary>Opt-in hover/press juice for a button — call from the owning
    /// scene's _Ready (explicit wiring; no global scanning).</summary>
    public static void AttachButtonJuice(
        BaseButton button, float hoverScale = 1.06f, float pressScale = 0.94f)
    {
        button.MouseEntered += () => HoverPop(button, hoverScale);
        button.MouseExited += () => ScaleReset(button);
        button.ButtonDown += () => PressDepress(button, pressScale);
        button.ButtonUp += () => HoverPop(button, hoverScale);
    }

    /// <summary>Kills the target's previous UiMotion tween and registers a new
    /// one — the re-entry guard every helper routes through.</summary>
    private static Tween Fresh(CanvasItem target)
    {
        if (target.HasMeta(TweenMeta)
            && target.GetMeta(TweenMeta).As<Tween>() is { } previous
            && previous.IsValid())
        {
            previous.Kill();
        }
        var tween = target.CreateTween();
        target.SetMeta(TweenMeta, tween);
        return tween;
    }
}
