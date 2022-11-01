namespace X11Overlay.UI;

public abstract class Control
{
    public int X;
    public int Y;
    public uint Width;
    public uint Height;

    public Control(int x, int y, uint w, uint h)
    {
        X = x;
        Y = y;
        Width = w;
        Height = h;
    }

    public abstract void Render(ITexture canvasTex);
}