namespace HopInLine.Data.Line
{
	/// <summary>
	/// In-process publish/subscribe contract for line change notifications.
	///
	/// The returned <see cref="IDisposable"/> MUST remove the handler from the subscription list.
	/// <see cref="NotifyAsync"/> loads the current line and invokes every registered handler for the given line ID.
	/// Handlers may be invoked on a background thread, so Blazor components must marshal with <see cref="Microsoft.AspNetCore.Components.ComponentBase.InvokeAsync(Func{Task})"/>.
	/// </summary>
	public interface ILineChangeNotifier
	{
		/// <summary>
		/// Subscribes a handler to notifications for a specific line.
		/// </summary>
		/// <param name="lineId">The ID of the line to subscribe to.</param>
		/// <param name="handler">Async callback that receives the updated LineDto. Must not throw.</param>
		/// <returns>An <see cref="IDisposable"/> that unsubscribes the handler when disposed.</returns>
		IDisposable Subscribe(string lineId, Func<LineDto, Task> handler);

		/// <summary>
		/// Loads the current line and notifies all subscribers for that line ID.
		/// </summary>
		/// <param name="lineId">The ID of the line that changed.</param>
		Task NotifyAsync(string lineId);
	}
}
