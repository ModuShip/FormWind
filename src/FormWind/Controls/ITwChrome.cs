namespace FormWind;

/// <summary>Controls that paint their own corners, border and background gradient (owner-draw).</summary>
public interface ITwChrome
{
    void SetChrome(TwChrome chrome);
}
