window.hopInLine = (function() {
    var intervals = {};

    var RECENT_LINES_KEY = "hopinline.recentLines";
    var MAX_RECENT_LINES = 20;

    function loadRecentLines() {
        try {
            var raw = window.localStorage.getItem(RECENT_LINES_KEY);
            return raw ? JSON.parse(raw) : [];
        } catch (e) {
            return [];
        }
    }

    function saveRecentLinesList(list) {
        try {
            window.localStorage.setItem(RECENT_LINES_KEY, JSON.stringify(list));
        } catch (e) {
            // localStorage unavailable (private mode, quota) — silently ignore
        }
    }

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
                element.classList.remove("countdown-tick");
                void element.offsetWidth; // force reflow so the animation restarts every tick
                element.classList.add("countdown-tick");

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
        },

        saveRecentLine: function(entry) {
            var list = loadRecentLines().filter(function(x) {
                return !(x.lineId === entry.lineId && x.role === entry.role);
            });
            entry.savedAt = new Date().toISOString();
            list.unshift(entry);
            if (list.length > MAX_RECENT_LINES) {
                list = list.slice(0, MAX_RECENT_LINES);
            }
            saveRecentLinesList(list);
        },

        getRecentLines: function() {
            return loadRecentLines();
        },

        removeRecentLine: function(lineId) {
            var list = loadRecentLines().filter(function(x) { return x.lineId !== lineId; });
            saveRecentLinesList(list);
        },

        saveMyParticipation: function(lineId, participantId, leaveToken) {
            try {
                window.localStorage.setItem("hopinline.myParticipation." + lineId, JSON.stringify({ participantId: participantId, leaveToken: leaveToken }));
            } catch (e) {}
        },

        getMyParticipation: function(lineId) {
            try {
                var raw = window.localStorage.getItem("hopinline.myParticipation." + lineId);
                return raw ? JSON.parse(raw) : null;
            } catch (e) {
                return null;
            }
        },

        copyToClipboard: function(text) {
            if (navigator.clipboard && navigator.clipboard.writeText) {
                return navigator.clipboard.writeText(text).then(function() {
                    return true;
                }).catch(function() {
                    return false;
                });
            }

            // Fallback for non-secure contexts / older browsers
            try {
                var textarea = document.createElement("textarea");
                textarea.value = text;
                textarea.style.position = "fixed";
                textarea.style.opacity = "0";
                document.body.appendChild(textarea);
                textarea.focus();
                textarea.select();
                var succeeded = document.execCommand("copy");
                document.body.removeChild(textarea);
                return Promise.resolve(succeeded);
            } catch (e) {
                return Promise.resolve(false);
            }
        }
    };
})();
