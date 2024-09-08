using HopInLine.Shared;
using Microsoft.AspNetCore.Components;

public class OverlayService
{
    public event Action OnShowOverlay;
    public event Action OnHideOverlay;

    public void ShowOverlay()
    {
        OnShowOverlay?.Invoke();
    }

    public void HideOverlay()
    {
        OnHideOverlay?.Invoke();
    }
}