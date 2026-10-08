using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace HopInLine.Data.Line
{
    public class LineService : ILineService
    {
        private readonly ILineRepository _lineRepository;
        private readonly ILineChangeNotifier _notifier;
        private readonly ILineOwnerNotifier _ownerNotifier;
        private readonly IEmailRateLimiter _rateLimiter;
        private readonly IActionRateLimiter? _actionRateLimiter;
        private readonly ClientContext _client;

        public LineService(ILineRepository lineRepository,
            ILineChangeNotifier notifier,
            ILineOwnerNotifier ownerNotifier,
            IEmailRateLimiter rateLimiter,
            IActionRateLimiter? actionRateLimiter = null,
            ClientContext? client = null)
        {
            _lineRepository = lineRepository;
            _notifier = notifier;
            _ownerNotifier = ownerNotifier;
            _rateLimiter = rateLimiter;
            _actionRateLimiter = actionRateLimiter;
            _client = client ?? new ClientContext();
        }

        /// <summary>
        /// Authorization guard called at the top of every mutating method.
        /// Normalizes the id, loads the line, throws KeyNotFoundException if the line does not exist,
        /// compares the token, throws UnauthorizedAccessException on mismatch, and returns the line.
        /// </summary>
        private async Task<Line> AuthorizeAsync(string lineId, string manageToken)
        {
            Throttle(RateLimitedAction.Write);
            lineId = IdGenerator.NormalizeId(lineId);

            var line = await _lineRepository.GetLineAsync(lineId);
            if (line == null)
            {
                throw new KeyNotFoundException();
            }

            // Compare tokens in constant time
            bool tokensMatch = FixedTimeCompare(line.ManageToken, manageToken);
            if (!tokensMatch)
            {
                throw new UnauthorizedAccessException();
            }

            return line;
        }

        /// <summary>
        /// Per-client rate limit, checked at the top of every public method that touches the database.
        /// Throws <see cref="RateLimitExceededException"/> when the caller is over its budget.
        /// </summary>
        private void Throttle(RateLimitedAction action)
        {
            if (_actionRateLimiter != null && !_actionRateLimiter.TryConsume(action, _client))
            {
                throw new RateLimitExceededException("You're doing that too fast — please wait a moment and try again.");
            }
        }

        /// <summary>
        /// Compares two tokens in constant time to prevent timing attacks.
        /// Both stored and provided tokens must be non-null and non-empty; otherwise returns false.
        /// </summary>
        private static bool FixedTimeCompare(string? stored, string? provided)
        {
            // Guard against null or empty stored token
            if (string.IsNullOrEmpty(stored))
            {
                return false;
            }

            // Guard against null or empty provided token
            if (string.IsNullOrEmpty(provided))
            {
                return false;
            }

            byte[] storedBytes = Encoding.UTF8.GetBytes(stored);
            byte[] providedBytes = Encoding.UTF8.GetBytes(provided);

            return CryptographicOperations.FixedTimeEquals(storedBytes, providedBytes);
        }

        /// <summary>
        /// Gets a line by ID. Returns a LineDto with ManageToken set to string.Empty
        /// (the read path must never leak the token to a page that did not supply it).
        /// </summary>
        public async Task<LineDto?> GetLineAsync(string lineId)
        {
            Throttle(RateLimitedAction.Read);
            lineId = IdGenerator.NormalizeId(lineId);
            var line = await _lineRepository.GetLineAsync(lineId);
            if (line == null)
            {
                return null;
            }

            var dto = LineDto.FromLine(line);
            dto.ManageToken = string.Empty; // Never leak the token
            return dto;
        }

        /// <summary>
        /// Creates a new line. Returns the real token to its creator.
        /// </summary>
        public async Task<LineDto> CreateLineAsync(string name, string? description, bool autoReAdd,
            bool autoAdvanceLine, TimeSpan autoAdvanceInterval, string? ownerEmail = null)
        {
            Throttle(RateLimitedAction.Create);

            // Server-side length enforcement
            if (string.IsNullOrEmpty(name) || name.Length > 100)
            {
                throw new ArgumentException("Line name must be between 1 and 100 characters.");
            }

            if (!string.IsNullOrEmpty(description) && description.Length > 500)
            {
                throw new ArgumentException("Description must not exceed 500 characters.");
            }

            if (!string.IsNullOrWhiteSpace(ownerEmail))
            {
                try
                {
                    var parsedAddress = new System.Net.Mail.MailAddress(ownerEmail);
                    if (parsedAddress.Address != ownerEmail)
                    {
                        throw new FormatException();
                    }
                }
                catch (FormatException)
                {
                    throw new ArgumentException("Owner email is not a valid email address.");
                }
            }

            // Interval bounds
            if (autoAdvanceInterval < TimeSpan.FromMinutes(1) || autoAdvanceInterval > TimeSpan.FromHours(24))
            {
                throw new ArgumentOutOfRangeException(nameof(autoAdvanceInterval), "Auto-advance interval must be between 1 minute and 24 hours.");
            }

            const int maxAttempts = 100;
            int attempt = 0;
            string newId = "";

            for (attempt = 0; attempt < maxAttempts; attempt++)
            {
                newId = IdGenerator.GenerateUniqueId();
                if (!_lineRepository.LineExists(newId))
                {
                    break;
                }
            }

            if (attempt >= maxAttempts)
            {
                throw new InvalidOperationException("Could not allocate a unique line id.");
            }

            var newLine = new Line
            {
                Id = newId,
                Name = name,
                Description = description,
                AutoAdvanceLine = autoAdvanceLine,
                AutoAdvanceInterval = autoAdvanceInterval,
                AutoReAdd = autoReAdd,
                CountDownStart = DateTime.UtcNow,
                ManageToken = IdGenerator.GenerateManageToken()
            };

            await _lineRepository.AddLineAsync(newLine, CancellationToken.None);
            await _notifier.NotifyAsync(newLine.Id);

            if (!string.IsNullOrWhiteSpace(ownerEmail) && _rateLimiter.TryConsume(ownerEmail))
            {
                var viewUrl = $"/view-line/{newLine.Id}";
                var manageUrl = $"/manage/{newLine.Id}/{newLine.ManageToken}";
                await _ownerNotifier.NotifyLineCreatedAsync(ownerEmail, newLine.Name, viewUrl, manageUrl);
            }

            return LineDto.FromLine(newLine); // Return the real token to the creator
        }

        /// <summary>
        /// Adds a participant to a line by name. No manageToken required; joining is public.
        /// Builds the Participant server-side via ParticipantFactory.Create(...).
        /// Never accepts a caller-supplied Participant.
        /// </summary>
        public async Task<ParticipantJoinResult> AddParticipantAsync(string lineId, string participantName, string? manageToken = null)
        {
            Throttle(RateLimitedAction.Write);
            lineId = IdGenerator.NormalizeId(lineId);

            // Validate name: reject null, whitespace-only, or over-60-character names
            if (string.IsNullOrWhiteSpace(participantName))
            {
                throw new ArgumentException("Participant name cannot be null or whitespace.");
            }

            if (participantName.Length > 60)
            {
                throw new ArgumentException("Participant name must not exceed 60 characters.");
            }

            var line = await _lineRepository.GetLineAsync(lineId);
            if (line == null)
            {
                throw new KeyNotFoundException();
            }

            // Abuse limit: throttle joins per line
            // Managers adding people (valid token) are exempt
            var isManager = !string.IsNullOrEmpty(manageToken) && FixedTimeCompare(line.ManageToken, manageToken);
            if (!isManager && _actionRateLimiter != null && !_actionRateLimiter.TryConsumeJoin(lineId))
            {
                throw new RateLimitExceededException("Lots of people are joining right now — please try again in a moment.");
            }

            // Abuse limit: participant cap at 200
            var activeParticipants = line.Participants.Where(p => !p.Removed).ToList();
            if (activeParticipants.Count >= 200)
            {
                throw new InvalidOperationException("This line is full.");
            }

            // Build the Participant server-side via ParticipantFactory
            var participant = ParticipantFactory.Create(participantName);

            await _lineRepository.AddParticipantAsync(lineId, participant);

            line = await _lineRepository.GetLineAsync(lineId);
            if (line != null && line.Participants.Count == 1)
            {
                line.CountDownStart = DateTime.UtcNow;
            }

            await _notifier.NotifyAsync(lineId);

            return new ParticipantJoinResult(participant.Id, participant.LeaveToken);
        }

        /// <summary>
        /// Updates a line's settings. Requires valid manageToken.
        /// </summary>
        public async Task UpdateLineAsync(string lineId, string manageToken, string name, string? description,
            bool autoAdvanceLine, bool autoReAdd, bool autoRestartTimerOnAdvance,
            TimeSpan autoAdvanceInterval)
        {
            var line = await AuthorizeAsync(lineId, manageToken);

            // Server-side length enforcement
            if (string.IsNullOrEmpty(name) || name.Length > 100)
            {
                throw new ArgumentException("Line name must be between 1 and 100 characters.");
            }

            if (!string.IsNullOrEmpty(description) && description.Length > 500)
            {
                throw new ArgumentException("Description must not exceed 500 characters.");
            }

            // Interval bounds
            if (autoAdvanceInterval < TimeSpan.FromMinutes(1) || autoAdvanceInterval > TimeSpan.FromHours(24))
            {
                throw new ArgumentOutOfRangeException(nameof(autoAdvanceInterval), "Auto-advance interval must be between 1 minute and 24 hours.");
            }

            line.Name = name;
            line.Description = description;
            line.AutoAdvanceLine = autoAdvanceLine;
            line.AutoReAdd = autoReAdd;
            line.AutoRestartTimerOnAdvance = autoRestartTimerOnAdvance;
            line.AutoAdvanceInterval = autoAdvanceInterval;

            await _lineRepository.UpdateLineAsync(line);
            await _notifier.NotifyAsync(line.Id);
        }

        /// <summary>
        /// Deletes a line. Requires valid manageToken.
        /// </summary>
        public async Task DeleteLineAsync(string lineId, string manageToken)
        {
            var line = await AuthorizeAsync(lineId, manageToken);

            await _lineRepository.RemoveLineAsync(line.Id, CancellationToken.None);
            await _notifier.NotifyAsync(line.Id);
        }

        /// <summary>
        /// Advances the line to the next participant. Requires valid manageToken.
        /// Abuse limit: reject a manual advance made less than 1 second after this line's CountDownStart.
        /// </summary>
        public async Task AdvanceLineAsync(string lineId, string manageToken)
        {
            var line = await AuthorizeAsync(lineId, manageToken);

            // Advance throttle: reject a manual advance made less than 1 second after CountDownStart
            var timeSinceCountDown = DateTime.UtcNow - line.CountDownStart;
            if (timeSinceCountDown < TimeSpan.FromSeconds(1))
            {
                // Return without advancing (blocks click-spam loop from racing the queue)
                return;
            }

            await _lineRepository.AdvanceLineAsync(line.Id);
            line = await _lineRepository.GetLineAsync(line.Id);
            if (line == null || line.Participants.IsNullOrEmpty())
            {
                // Notify anyway (as per the original SERVICE-001 logic)
                await _notifier.NotifyAsync(lineId);
                return;
            }

            await _notifier.NotifyAsync(line.Id);
        }

        /// <summary>
        /// Moves a participant up in the line. Requires valid manageToken.
        /// </summary>
        public async Task MoveParticipantUpAsync(string lineId, string manageToken, string participantId)
        {
            var line = await AuthorizeAsync(lineId, manageToken);

            await _lineRepository.MoveParticipantUpAsync(line.Id, participantId);
            await _notifier.NotifyAsync(line.Id);
        }

        /// <summary>
        /// Moves a participant down in the line. Requires valid manageToken.
        /// </summary>
        public async Task MoveParticipantDownAsync(string lineId, string manageToken, string participantId)
        {
            var line = await AuthorizeAsync(lineId, manageToken);

            await _lineRepository.MoveParticipantDownAsync(line.Id, participantId);
            await _notifier.NotifyAsync(line.Id);
        }

        /// <summary>
        /// Removes a participant from the line. Requires valid manageToken.
        /// </summary>
        public async Task RemoveParticipantAsync(string lineId, string manageToken, string participantId)
        {
            var line = await AuthorizeAsync(lineId, manageToken);

            await _lineRepository.RemovedParticipantAsync(line.Id, participantId);
            await _notifier.NotifyAsync(line.Id);
        }

        /// <summary>
        /// Removes the calling participant from the line using their personal leave token.
        /// Distinct authorization axis from the manage token: only the participant's own
        /// LeaveToken is checked, not Line.ManageToken.
        /// </summary>
        public async Task RemoveSelfAsync(string lineId, string participantId, string leaveToken)
        {
            Throttle(RateLimitedAction.Write);
            lineId = IdGenerator.NormalizeId(lineId);

            var line = await _lineRepository.GetLineAsync(lineId);
            if (line == null)
            {
                throw new KeyNotFoundException();
            }

            var participant = line.Participants.FirstOrDefault(p => p.Id == participantId);
            if (participant == null)
            {
                throw new KeyNotFoundException();
            }

            bool tokensMatch = FixedTimeCompare(participant.LeaveToken, leaveToken);
            if (!tokensMatch)
            {
                throw new UnauthorizedAccessException();
            }

            if (participant.Removed)
            {
                return;
            }

            await _lineRepository.RemovedParticipantAsync(line.Id, participantId);
            await _notifier.NotifyAsync(line.Id);
        }

        /// <summary>
        /// Re-adds a removed participant to the line. Requires valid manageToken.
        /// </summary>
        public async Task ReAddRemovedParticipantAsync(string lineId, string manageToken, string participantId)
        {
            var line = await AuthorizeAsync(lineId, manageToken);

            await _lineRepository.ReAddRemovedParticipantAsync(line.Id, participantId);
            await _notifier.NotifyAsync(line.Id);
        }

        /// <summary>
        /// Permanently deletes a removed participant. Requires valid manageToken.
        /// </summary>
        public async Task DeleteRemovedParticipantAsync(string lineId, string manageToken, string participantId)
        {
            var line = await AuthorizeAsync(lineId, manageToken);

            await _lineRepository.DeleteParticipantAsync(line.Id, participantId);
            await _notifier.NotifyAsync(line.Id);
        }

        /// <summary>
        /// Pauses the line's timer. Requires valid manageToken.
        /// </summary>
        public async Task PauseTimerAsync(string lineId, string manageToken)
        {
            var line = await AuthorizeAsync(lineId, manageToken);

            await _lineRepository.PauseTimerAsync(line.Id);
            await _notifier.NotifyAsync(line.Id);
        }

        /// <summary>
        /// Resumes the line's timer. Requires valid manageToken.
        /// </summary>
        public async Task ResumeTimerAsync(string lineId, string manageToken)
        {
            var line = await AuthorizeAsync(lineId, manageToken);

            await _lineRepository.ResumeTimerAsync(line.Id);
            await _notifier.NotifyAsync(line.Id);
        }

        /// <summary>
        /// Restarts the line's timer. Requires valid manageToken.
        /// </summary>
        public async Task RestartTimerAsync(string lineId, string manageToken)
        {
            var line = await AuthorizeAsync(lineId, manageToken);

            await _lineRepository.RestartTimerAsync(line.Id);
            await _notifier.NotifyAsync(line.Id);
        }

        /// <summary>
        /// Validates that the provided manageToken is correct for the line.
        /// Must never throw for a wrong token or a missing line — returns false instead.
        /// </summary>
        public async Task<bool> ValidateManageTokenAsync(string lineId, string manageToken)
        {
            // Outside the try: a throttled caller must get an error, not a silent "invalid token"
            Throttle(RateLimitedAction.Read);
            try
            {
                lineId = IdGenerator.NormalizeId(lineId);
                var line = await _lineRepository.GetLineAsync(lineId);
                if (line == null)
                {
                    return false;
                }

                return FixedTimeCompare(line.ManageToken, manageToken);
            }
            catch
            {
                return false;
            }
        }
    }
}