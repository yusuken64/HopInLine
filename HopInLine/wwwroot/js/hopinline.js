window.hopInLine = (function() {
    var intervals = {};

    /**
     * Format duration in seconds according to DurationFormat.Humanize rules
     * - Under one hour: m:ss (minutes not zero-padded, seconds always two digits)
     * - One hour or more: h:mm:ss (all zero-padded)
     * - Hours never wrap on modulo 24
     */
    function formatDuration(totalSeconds) {
        if (totalSeconds <= 0) {
            return "Auto-advance happening now...";
        }

        var hours = Math.floor(totalSeconds / 3600);
        var minutes = Math.floor((totalSeconds % 3600) / 60);
        var seconds = Math.floor(totalSeconds % 60);

        if (hours === 0) {
            // Under one hour: m:ss format
            return minutes + ":" + (seconds < 10 ? "0" : "") + seconds;
        } else {
            // One hour or more: h:mm:ss format
            return hours + ":" + (minutes < 10 ? "0" : "") + minutes + ":" + (seconds < 10 ? "0" : "") + seconds;
        }
    }

    return {
        startCountdown: function(elementId, targetUtcIso) {
            var element = document.getElementById(elementId);
            if (!element) {
                return;
            }

            // Clear any existing interval for this element
            if (intervals[elementId]) {
                clearInterval(intervals[elementId]);
                delete intervals[elementId];
            }

            // Parse target UTC timestamp
            var targetTime = new Date(targetUtcIso).getTime();

            // Update immediately
            var updateCountdown = function() {
                var now = new Date().getTime();
                var remainingMs = targetTime - now;
                var remainingSeconds = remainingMs / 1000;

                var formatted = formatDuration(remainingSeconds);
                element.textContent = formatted;

                // Stop the interval if time has passed
                if (remainingSeconds <= 0) {
                    clearInterval(intervals[elementId]);
                    delete intervals[elementId];
                }
            };

            updateCountdown();

            // Set up interval to update once per second
            intervals[elementId] = setInterval(updateCountdown, 1000);
        },

        stopCountdown: function(elementId) {
            if (intervals[elementId]) {
                clearInterval(intervals[elementId]);
                delete intervals[elementId];
            }
        },

        confirm: function(message) {
            return window.confirm(message);
        }
    };
})();
