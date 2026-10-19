// =============================================================================
// SessionBoundWriter.cs
//
// Helper used by the session writers. It keeps one output (a file, a video
// writer, ...) open for the current experiment session:
//   - when a session starts, the output is opened on the next write,
//   - when the session stops, the output is closed immediately,
//   - when no session is running, writes are silently skipped.
// All access is serialised with a lock, so data never goes to a file that
// is being closed.
// =============================================================================
using System;

namespace Buridan.Bonsai
{
    sealed class SessionBoundWriter<TOutput> : IDisposable where TOutput : class, IDisposable
    {
        readonly object syncRoot = new object();
        readonly Func<ExperimentSession, TOutput> openOutput;
        ExperimentSession session;
        TOutput output;

        /// <summary>
        /// Creates the helper. 'openOutput' opens the output for a session.
        /// </summary>
        public SessionBoundWriter(Func<ExperimentSession, TOutput> openOutput)
        {
            this.openOutput = openOutput;
            SessionManager.SessionStopped += OnSessionStopped;
        }

        /// <summary>
        /// Runs 'write' with the output of the current session. Does nothing if
        /// no session is running.
        /// </summary>
        public void Write(Action<TOutput, ExperimentSession> write)
        {
            lock (syncRoot)
            {
                ExperimentSession current = SessionManager.Current;
                if (current != session)
                {
                    // The session changed since the last write.
                    CloseOutput();
                    session = current;
                    if (session != null)
                    {
                        output = openOutput(session);
                    }
                }
                if (session == null)
                {
                    return;
                }
                write(output, session);
            }
        }

        void OnSessionStopped(ExperimentSession stoppedSession)
        {
            lock (syncRoot)
            {
                if (stoppedSession == session)
                {
                    CloseOutput();
                    session = null;
                }
            }
        }

        void CloseOutput()
        {
            if (output != null)
            {
                output.Dispose();
                output = null;
            }
        }

        public void Dispose()
        {
            SessionManager.SessionStopped -= OnSessionStopped;
            lock (syncRoot)
            {
                CloseOutput();
                session = null;
            }
        }
    }
}
