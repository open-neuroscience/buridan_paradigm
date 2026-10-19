// =============================================================================
// SessionManager.cs
//
// Keeps track of the experiment session that is currently recording.
//
// StartExperiment / StopExperiment change the current session; the session
// writers (video, tracking, Pi messages) look at it to know where to write.
// At most one session is active at a time.
// =============================================================================
using System;

namespace Buridan.Bonsai
{
    /// <summary>
    /// Provides access to the experiment session that is currently recording.
    /// </summary>
    public static class SessionManager
    {
        static readonly object SyncRoot = new object();
        static ExperimentSession current;

        /// <summary>
        /// Occurs after a session has stopped. Writers use it to close their
        /// files immediately, even if no more data arrives.
        /// </summary>
        public static event Action<ExperimentSession> SessionStopped;

        /// <summary>
        /// Gets the session currently recording, or null.
        /// </summary>
        public static ExperimentSession Current
        {
            get
            {
                lock (SyncRoot)
                {
                    return current;
                }
            }
        }

        /// <summary>
        /// Makes a session current. Throws if another session is still active.
        /// </summary>
        internal static void Start(ExperimentSession session)
        {
            lock (SyncRoot)
            {
                if (current != null)
                {
                    throw new InvalidOperationException("An experiment is already running: " + current.Folder);
                }
                current = session;
            }
        }

        /// <summary>
        /// Stops the current session (if any) and returns it, or null if none
        /// was running.
        /// </summary>
        internal static ExperimentSession Stop(string reason)
        {
            ExperimentSession stopped;
            lock (SyncRoot)
            {
                stopped = current;
                current = null;
            }
            if (stopped == null)
            {
                return null;
            }

            stopped.MarkStopped(reason);
            Action<ExperimentSession> handler = SessionStopped;
            if (handler != null)
            {
                handler(stopped);
            }
            return stopped;
        }
    }
}
