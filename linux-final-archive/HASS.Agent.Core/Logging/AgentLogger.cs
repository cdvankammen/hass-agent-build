using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace HASS.Agent.Core.Logging;

/// <summary>
/// Enhanced logging framework with detailed context information for easy debugging.
/// All log entries include class, method, line number, and correlation ID.
/// </summary>
public static class AgentLogger
{
    private static bool _initialized;
    private static string _logPath = "";
    
    /// <summary>
    /// Initialize the logging system with enhanced formatting
    /// </summary>
    public static void Initialize(string logDirectory, string applicationName = "HASS.Agent")
    {
        if (_initialized) return;
        
        try
        {
            // Ensure log directory exists
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }
            
            _logPath = Path.Combine(logDirectory, $"{applicationName.ToLower()}.log");
            
            // Configure Serilog with enhanced output
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithProperty("Application", applicationName)
                .Enrich.WithProperty("MachineName", Environment.MachineName)
                
                // Console output with colors and context
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}",
                    restrictedToMinimumLevel: LogEventLevel.Information)
                
                // File output with full details
                .WriteTo.File(
                    path: _logPath,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}::{MemberName}:{LineNumber}] [Thread:{ThreadId}] {Message:lj}{NewLine}{Exception}",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    fileSizeLimitBytes: 10 * 1024 * 1024, // 10MB
                    rollOnFileSizeLimit: true,
                    shared: true)
                
                // JSON file for structured logging analysis
                .WriteTo.File(
                    formatter: new CompactJsonFormatter(),
                    path: Path.Combine(logDirectory, $"{applicationName.ToLower()}-structured-.json"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 3)
                
                .CreateLogger();
            
            _initialized = true;
            
            Log.Information("Logging initialized. Log path: {LogPath}", _logPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to initialize logging: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Get the current log file path
    /// </summary>
    public static string LogPath => _logPath;
    
    /// <summary>
    /// Log a debug message with caller context
    /// </summary>
    public static void Debug(
        string message,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        using (LogContext.PushProperty("SourceContext", GetClassName(filePath)))
        using (LogContext.PushProperty("MemberName", memberName))
        using (LogContext.PushProperty("LineNumber", lineNumber))
        {
            Log.Debug(message);
        }
    }
    
    /// <summary>
    /// Log a debug message with parameters and caller context
    /// </summary>
    public static void Debug<T>(
        string message,
        T propertyValue,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        using (LogContext.PushProperty("SourceContext", GetClassName(filePath)))
        using (LogContext.PushProperty("MemberName", memberName))
        using (LogContext.PushProperty("LineNumber", lineNumber))
        {
            Log.Debug(message, propertyValue);
        }
    }
    
    /// <summary>
    /// Log an information message with caller context
    /// </summary>
    public static void Info(
        string message,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        using (LogContext.PushProperty("SourceContext", GetClassName(filePath)))
        using (LogContext.PushProperty("MemberName", memberName))
        using (LogContext.PushProperty("LineNumber", lineNumber))
        {
            Log.Information(message);
        }
    }
    
    /// <summary>
    /// Log an information message with parameters and caller context
    /// </summary>
    public static void Info<T>(
        string message,
        T propertyValue,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        using (LogContext.PushProperty("SourceContext", GetClassName(filePath)))
        using (LogContext.PushProperty("MemberName", memberName))
        using (LogContext.PushProperty("LineNumber", lineNumber))
        {
            Log.Information(message, propertyValue);
        }
    }
    
    /// <summary>
    /// Log a warning message with caller context
    /// </summary>
    public static void Warning(
        string message,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        using (LogContext.PushProperty("SourceContext", GetClassName(filePath)))
        using (LogContext.PushProperty("MemberName", memberName))
        using (LogContext.PushProperty("LineNumber", lineNumber))
        {
            Log.Warning(message);
        }
    }
    
    /// <summary>
    /// Log a warning with exception and caller context
    /// </summary>
    public static void Warning(
        Exception exception,
        string message,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        using (LogContext.PushProperty("SourceContext", GetClassName(filePath)))
        using (LogContext.PushProperty("MemberName", memberName))
        using (LogContext.PushProperty("LineNumber", lineNumber))
        {
            Log.Warning(exception, message);
        }
    }
    
    /// <summary>
    /// Log an error message with caller context
    /// </summary>
    public static void Error(
        string message,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        using (LogContext.PushProperty("SourceContext", GetClassName(filePath)))
        using (LogContext.PushProperty("MemberName", memberName))
        using (LogContext.PushProperty("LineNumber", lineNumber))
        {
            Log.Error(message);
        }
    }
    
    /// <summary>
    /// Log an error with exception and caller context - MOST USEFUL FOR DEBUGGING
    /// </summary>
    public static void Error(
        Exception exception,
        string message,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        using (LogContext.PushProperty("SourceContext", GetClassName(filePath)))
        using (LogContext.PushProperty("MemberName", memberName))
        using (LogContext.PushProperty("LineNumber", lineNumber))
        using (LogContext.PushProperty("ExceptionType", exception.GetType().Name))
        using (LogContext.PushProperty("StackTrace", exception.StackTrace))
        {
            Log.Error(exception, message);
        }
    }
    
    /// <summary>
    /// Log a fatal error with caller context
    /// </summary>
    public static void Fatal(
        Exception exception,
        string message,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        using (LogContext.PushProperty("SourceContext", GetClassName(filePath)))
        using (LogContext.PushProperty("MemberName", memberName))
        using (LogContext.PushProperty("LineNumber", lineNumber))
        using (LogContext.PushProperty("ExceptionType", exception.GetType().Name))
        {
            Log.Fatal(exception, message);
        }
    }
    
    /// <summary>
    /// Create a scoped operation for tracking long-running processes
    /// </summary>
    public static IDisposable BeginOperation(
        string operationName,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "")
    {
        var operationId = Guid.NewGuid().ToString("N")[..8];
        var context = GetClassName(filePath);
        
        return new ScopedOperation(operationName, operationId, context, memberName);
    }
    
    /// <summary>
    /// Log method entry for tracing
    /// </summary>
    public static void TraceEnter(
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        using (LogContext.PushProperty("SourceContext", GetClassName(filePath)))
        using (LogContext.PushProperty("MemberName", memberName))
        using (LogContext.PushProperty("LineNumber", lineNumber))
        {
            Log.Verbose(">>> Entering {Method}", memberName);
        }
    }
    
    /// <summary>
    /// Log method exit for tracing
    /// </summary>
    public static void TraceExit(
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        using (LogContext.PushProperty("SourceContext", GetClassName(filePath)))
        using (LogContext.PushProperty("MemberName", memberName))
        using (LogContext.PushProperty("LineNumber", lineNumber))
        {
            Log.Verbose("<<< Exiting {Method}", memberName);
        }
    }
    
    /// <summary>
    /// Close and flush the logger
    /// </summary>
    public static void Shutdown()
    {
        Log.Information("Logging shutting down");
        Log.CloseAndFlush();
        _initialized = false;
    }
    
    private static string GetClassName(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return "Unknown";
        return Path.GetFileNameWithoutExtension(filePath);
    }
    
    /// <summary>
    /// Helper class for scoped operations
    /// </summary>
    private class ScopedOperation : IDisposable
    {
        private readonly string _operationName;
        private readonly string _operationId;
        private readonly Stopwatch _stopwatch;
        
        public ScopedOperation(string operationName, string operationId, string context, string memberName)
        {
            _operationName = operationName;
            _operationId = operationId;
            _stopwatch = Stopwatch.StartNew();
            
            using (LogContext.PushProperty("OperationId", operationId))
            using (LogContext.PushProperty("SourceContext", context))
            using (LogContext.PushProperty("MemberName", memberName))
            {
                Log.Information("▶ Starting operation: {Operation} [{OperationId}]", operationName, operationId);
            }
        }
        
        public void Dispose()
        {
            _stopwatch.Stop();
            using (LogContext.PushProperty("OperationId", _operationId))
            using (LogContext.PushProperty("Duration", _stopwatch.ElapsedMilliseconds))
            {
                Log.Information("✓ Completed operation: {Operation} [{OperationId}] in {Duration}ms", 
                    _operationName, _operationId, _stopwatch.ElapsedMilliseconds);
            }
        }
    }
}

/// <summary>
/// Extension methods for easier logging in classes
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Get a logger for the current class
    /// </summary>
    public static ILogger ForContext<T>(this ILogger logger)
    {
        return logger.ForContext(typeof(T));
    }
}
