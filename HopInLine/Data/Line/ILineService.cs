namespace HopInLine.Data.Line
{
	/// <summary>
	/// Service contract for managing lines and participants.
	///
	/// Every mutating method takes a <c>manageToken</c> parameter to prevent unauthorized modifications.
	/// Mutating methods throw <see cref="UnauthorizedAccessException"/> when the <c>manageToken</c> does not match the line's <see cref="LineDto.ManageToken"/>.
	///
	/// Note: <see cref="AddParticipantAsync"/> deliberately takes a <c>name</c> string, not a <see cref="Participant"/> entity.
	/// The current hub method accepts a client-supplied entity including Position, TurnCount, Removed, LineId, and the Line navigation property,
	/// which is a mass-assignment vulnerability. This contract closes that hole by accepting only the participant name.
	/// </summary>
	public interface ILineService
	{
		/// <summary>
		/// Gets a line by ID.
		/// </summary>
		/// <param name="lineId">The ID of the line to retrieve.</param>
		/// <returns>The <see cref="LineDto"/>, or null if the line does not exist.</returns>
		Task<LineDto?> GetLineAsync(string lineId);

		/// <summary>
		/// Creates a new line.
		/// </summary>
		/// <param name="name">The name of the line.</param>
		/// <param name="description">Optional description of the line.</param>
		/// <param name="autoReAdd">Whether removed participants are automatically re-added.</param>
		/// <param name="autoAdvanceLine">Whether the line automatically advances.</param>
		/// <param name="autoAdvanceInterval">The interval for automatic advancement.</param>
		/// <returns>The created <see cref="LineDto"/> including its <see cref="LineDto.ManageToken"/>, so the creator can be shown both the join link and the manage link.</returns>
		Task<LineDto> CreateLineAsync(string name, string? description, bool autoReAdd,
										bool autoAdvanceLine, TimeSpan autoAdvanceInterval);

		/// <summary>
		/// Adds a participant to a line by name. No manageToken required; anyone can join.
		/// </summary>
		/// <param name="lineId">The ID of the line.</param>
		/// <param name="participantName">The name of the participant to add.</param>
		Task AddParticipantAsync(string lineId, string participantName);

		/// <summary>
		/// Updates a line's settings. Requires valid manageToken.
		/// </summary>
		/// <param name="lineId">The ID of the line.</param>
		/// <param name="manageToken">The line's management token.</param>
		/// <param name="name">The new name.</param>
		/// <param name="description">The new description.</param>
		/// <param name="autoAdvanceLine">Whether the line automatically advances.</param>
		/// <param name="autoReAdd">Whether removed participants are automatically re-added.</param>
		/// <param name="autoRestartTimerOnAdvance">Whether the timer restarts on advance.</param>
		/// <param name="autoAdvanceInterval">The interval for automatic advancement.</param>
		/// <returns></returns>
		Task UpdateLineAsync(string lineId, string manageToken, string name, string? description,
							 bool autoAdvanceLine, bool autoReAdd, bool autoRestartTimerOnAdvance,
							 TimeSpan autoAdvanceInterval);

		/// <summary>
		/// Deletes a line. Requires valid manageToken.
		/// </summary>
		/// <param name="lineId">The ID of the line.</param>
		/// <param name="manageToken">The line's management token.</param>
		Task DeleteLineAsync(string lineId, string manageToken);

		/// <summary>
		/// Advances the line to the next participant. Requires valid manageToken.
		/// </summary>
		/// <param name="lineId">The ID of the line.</param>
		/// <param name="manageToken">The line's management token.</param>
		Task AdvanceLineAsync(string lineId, string manageToken);

		/// <summary>
		/// Moves a participant up in the line. Requires valid manageToken.
		/// </summary>
		/// <param name="lineId">The ID of the line.</param>
		/// <param name="manageToken">The line's management token.</param>
		/// <param name="participantId">The ID of the participant to move.</param>
		Task MoveParticipantUpAsync(string lineId, string manageToken, string participantId);

		/// <summary>
		/// Moves a participant down in the line. Requires valid manageToken.
		/// </summary>
		/// <param name="lineId">The ID of the line.</param>
		/// <param name="manageToken">The line's management token.</param>
		/// <param name="participantId">The ID of the participant to move.</param>
		Task MoveParticipantDownAsync(string lineId, string manageToken, string participantId);

		/// <summary>
		/// Removes a participant from the line. Requires valid manageToken.
		/// </summary>
		/// <param name="lineId">The ID of the line.</param>
		/// <param name="manageToken">The line's management token.</param>
		/// <param name="participantId">The ID of the participant to remove.</param>
		Task RemoveParticipantAsync(string lineId, string manageToken, string participantId);

		/// <summary>
		/// Re-adds a removed participant to the line. Requires valid manageToken.
		/// </summary>
		/// <param name="lineId">The ID of the line.</param>
		/// <param name="manageToken">The line's management token.</param>
		/// <param name="participantId">The ID of the participant to re-add.</param>
		Task ReAddRemovedParticipantAsync(string lineId, string manageToken, string participantId);

		/// <summary>
		/// Permanently deletes a removed participant. Requires valid manageToken.
		/// </summary>
		/// <param name="lineId">The ID of the line.</param>
		/// <param name="manageToken">The line's management token.</param>
		/// <param name="participantId">The ID of the participant to delete.</param>
		Task DeleteRemovedParticipantAsync(string lineId, string manageToken, string participantId);

		/// <summary>
		/// Pauses the line's timer. Requires valid manageToken.
		/// </summary>
		/// <param name="lineId">The ID of the line.</param>
		/// <param name="manageToken">The line's management token.</param>
		Task PauseTimerAsync(string lineId, string manageToken);

		/// <summary>
		/// Resumes the line's timer. Requires valid manageToken.
		/// </summary>
		/// <param name="lineId">The ID of the line.</param>
		/// <param name="manageToken">The line's management token.</param>
		Task ResumeTimerAsync(string lineId, string manageToken);

		/// <summary>
		/// Restarts the line's timer. Requires valid manageToken.
		/// </summary>
		/// <param name="lineId">The ID of the line.</param>
		/// <param name="manageToken">The line's management token.</param>
		Task RestartTimerAsync(string lineId, string manageToken);

		/// <summary>
		/// Validates that the provided manageToken is correct for the line.
		/// </summary>
		/// <param name="lineId">The ID of the line.</param>
		/// <param name="manageToken">The token to validate.</param>
		/// <returns>True if the token is valid; false otherwise.</returns>
		Task<bool> ValidateManageTokenAsync(string lineId, string manageToken);
	}
}
