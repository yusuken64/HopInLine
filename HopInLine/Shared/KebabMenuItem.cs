using Microsoft.AspNetCore.Components;

public class KebabMenuItem
{
    public string Label { get; set; } = "";
    public string? AriaLabel { get; set; }
    public EventCallback OnClick { get; set; }
    public bool IsDangerous { get; set; }
}
