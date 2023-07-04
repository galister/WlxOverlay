namespace WlxOverlay.GUI;

public abstract class Control
{
    public readonly int X;
    public readonly int Y;

    public readonly uint Width;
    public readonly uint Height;

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