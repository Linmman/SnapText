using System.Drawing;

namespace SnapText.Overlay;

/// <summary>多屏覆盖层共享的框选状态（物理像素、虚拟桌面坐标）。</summary>
internal sealed class SelectionState
{
    public Rectangle Rect { get; private set; }
    public bool HasSelection { get; private set; }

    public event Action? Changed;

    public void Update(Rectangle rect)
    {
        Rect = rect;
        HasSelection = true;
        Changed?.Invoke();
    }
}
