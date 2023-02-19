namespace WlxOverlay.UI;

public abstract class Control
{
    public int X;
    public int Y;

    public uint Width;
    public uint Height;

    protected bool Dirty = true;

    protected Canvas? Canvas;

    protected Control(int x, int y, uint w, uint h)
    {
        X = x;
        Y = y;
        Width = w;
        Height = h;
    }

    public virtual void SetCanvas(Canvas canvas)
    {
        Canvas = canvas;
    }

    public virtual void Update() { }

    public abstract void Render();
}