// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace WlxOverlay.GUI;

/// <summary>
/// An UI element that contains multiple possible layouts
/// </summary>
public class ContentPager
{
    private Canvas _canvas;

    private List<List<Control>> _pages = new();
    private int _curPage = -1;

    public ContentPager(Canvas canvas)
    {
        _canvas = canvas;
    }

    public int NewPage()
    {
        var page = new List<Control>();
        var idx = _pages.Count;
        _pages.Add(page);
        return idx;
    }

    public void AddControl(int page, Control c)
    {
        _pages[page].Add(c);
    }

    public void SetActivePage(int newPage)
    {
        if (_curPage == newPage)
            return;

        if (_curPage >= 0)
            foreach (var control in _pages[_curPage])
                _canvas.RemoveControl(control);

        foreach (var control in _pages[newPage])
            _canvas.AddControl(control);

        _canvas.BuildInteractiveLayer();
        _canvas.MarkDirty();

        ActivePageChanged?.Invoke(this, newPage);
        _curPage = newPage;
    }

    public int ActivePage => _curPage;

    public event EventHandler<int>? ActivePageChanged;
}
