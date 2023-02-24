namespace WlxOverlay.UI;

public class DateTimeLabel : Label
{
    private readonly string _format;
    private readonly TimeZoneInfo _timeZoneInfo;
    public DateTimeLabel(string format, TimeZoneInfo tzInfo, int x, int y, uint w, uint h) : base("", x, y, w, h)
    {
        _format = format;
        _timeZoneInfo = tzInfo;
    }

    public override void Update()
    {
        base.Update();

        var newText = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZoneInfo).ToString(_format);

        if (newText == Text) return;

        Text = newText;
    }
}