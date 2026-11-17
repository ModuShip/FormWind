namespace FormWind;

/// <summary>How <see cref="TwTransition.CrossFade(System.Windows.Forms.Control, System.Action, TwMotionOptions)"/> takes the picture of the old look (<see cref="TwMotionOptions.Capture"/>).</summary>
public enum TwTransitionCapture
{
    /// <summary>
    /// As shown on screen, after the pending paints and the next composition of the desktop window manager; the window's
    /// content from the window manager when it is covered. Always right, but waiting for the composition and reading the
    /// screen back take most of a transition's time on large parts.
    /// </summary>
    Auto,

    /// <summary>
    /// Redrawn into a picture with <see cref="System.Windows.Forms.Control.DrawToBitmap"/>, without waiting for the screen:
    /// several times faster on a double-buffered page, but overlapping controls and text in nested scrolled panels can come
    /// out wrong. For pages of side-by-side controls (cards, forms).
    /// </summary>
    Drawing,

    /// <summary>
    /// No picture: the part turns to its background color and the new look fades in over it. The cheapest; the old look is
    /// not seen fading.
    /// </summary>
    None,
}
