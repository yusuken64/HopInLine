public class ConfirmService
{
    public bool IsVisible { get; private set; }
    public string Message { get; private set; } = "";

    public event Action? OnChange;

    private TaskCompletionSource<bool>? _tcs;

    public Task<bool> ConfirmAsync(string message)
    {
        Message = message;
        IsVisible = true;
        _tcs = new TaskCompletionSource<bool>();
        OnChange?.Invoke();
        return _tcs.Task;
    }

    public void Confirm()
    {
        IsVisible = false;
        var tcs = _tcs;
        _tcs = null;
        OnChange?.Invoke();
        tcs?.TrySetResult(true);
    }

    public void Cancel()
    {
        IsVisible = false;
        var tcs = _tcs;
        _tcs = null;
        OnChange?.Invoke();
        tcs?.TrySetResult(false);
    }
}
