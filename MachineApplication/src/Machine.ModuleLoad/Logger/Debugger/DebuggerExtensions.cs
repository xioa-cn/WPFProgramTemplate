using System.Runtime.CompilerServices;

namespace Machine.ModuleLoad.Logger.Debugger;

public static class DebuggerExtensions {
    public static LoggerInfo Log(this string msg, Exception? exception = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "") {
        return new LoggerInfo(msg, exception, filePath, lineNumber, memberName);
    }

    public class LoggerInfo {
        internal LoggerInfo(string? msg, Exception? exception = null,
            string filePath = "",
            int lineNumber = 0,
            string memberName = "") {
            Msg = msg ?? "Msg Is Null...";
            Exception = exception;
            this._filePath = filePath;
            this._lineNumber = lineNumber;
            this._memberName = memberName;
        }

        private string _filePath;
        private int _lineNumber;
        private string _memberName;

        public LoggerInfo Caller() {
            Msg += $" [F: {_filePath} | {_memberName} | L:{_lineNumber}]";
            return this;
        }

        public string Msg { get; private set; }
        public Exception? Exception { get; }

        public void Fatal() {
            GlobalLogger.DebuggerLogger?.Fatal(Msg, Exception);
        }

        public void Warn() {
            GlobalLogger.DebuggerLogger?.Warn(Msg);
        }

        public void Success() {
            GlobalLogger.DebuggerLogger?.Success(Msg);
        }

        public void Info() {
            GlobalLogger.DebuggerLogger?.Info(Msg);
        }

        public void Debug() {
            GlobalLogger.DebuggerLogger?.Debug(Msg);
        }

        public void Trace() {
            GlobalLogger.DebuggerLogger?.Trace(Msg);
        }
    }
}