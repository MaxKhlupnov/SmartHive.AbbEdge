using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace abbRemoteMonitoringGateway.Services.Diagnostics
{
    public class ConsoleLogger : ILogger{

          
        public LogLevel LogLevel { 
            get {
                return LogLevel.Always;
            } 
        }

        public string FormatDate(long time){
            return DateTimeOffset.FromUnixTimeMilliseconds(time).ToString( System.Globalization.DateTimeFormatInfo.InvariantInfo);           
        }

        public bool DebugIsEnabled { get {return true;}}
        public bool InfoIsEnabled { get {return true;}}

        // The following 5 methods allow to log a message, capturing the location where the log is generated
        // Use "Write()" to write the message regardless of the log level, e.g. at startup

        public void Write(
            string message,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0){
                Console.WriteLine(message);
            }

        public void Debug(
            string message,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0){
                Console.WriteLine(message);
            }

        public void Info(
            string message,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0){
                Console.WriteLine(message);
            }

        public void Warn(
            string message,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0){
                Console.WriteLine(message);
            }

        public void Error(
            string message,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0){
                Console.WriteLine(message);
            }

   
        public void LogToFile(string filePath, string text){
            Console.WriteLine(text);
        }

        public void Write(
            string message,
            Func<object> data,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (!string.IsNullOrEmpty(message)) message += ", ";
            message += Serialization.Serialize(data.Invoke());

            Console.WriteLine(message);
        }

        public void Debug(
            string message,
            Func<object> data,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (!string.IsNullOrEmpty(message)) message += ", ";
            message += Serialization.Serialize(data.Invoke());

            Console.WriteLine(message);
        }

        public void Info(
            string message,
            Func<object> data,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
       {
            if (!string.IsNullOrEmpty(message)) message += ", ";
            message += Serialization.Serialize(data.Invoke());

            Console.WriteLine(message);
        }

        public void Warn(
            string message,
            Func<object> data,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
       {
            if (!string.IsNullOrEmpty(message)) message += ", ";
            message += Serialization.Serialize(data.Invoke());

            Console.WriteLine(message);
        }

        public void Error(
            string message,
            Func<object> data,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (!string.IsNullOrEmpty(message)) message += ", ";
            message += Serialization.Serialize(data.Invoke());

            Console.WriteLine(message);
        }
    public void Write(
            string message,
            Exception e,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (!string.IsNullOrEmpty(message)) message += ", ";
            message += Serialization.SerializeException(e);

            Console.WriteLine(message);
        }

        public void Debug(
            string message,
            Exception e,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {           
            if (!string.IsNullOrEmpty(message)) message += ", ";
            message += Serialization.SerializeException(e);

            Console.WriteLine(message);
        }

        public void Info(
            string message,
            Exception e,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (!string.IsNullOrEmpty(message)) message += ", ";
            message += Serialization.SerializeException(e);

            Console.WriteLine(message);
        }

        public void Warn(
            string message,
            Exception e,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
             if (!string.IsNullOrEmpty(message)) message += ", ";
            message += Serialization.SerializeException(e);

            Console.WriteLine(message);
        }

        public void Error(
            string message,
            Exception e,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
             if (!string.IsNullOrEmpty(message)) message += ", ";
            message += Serialization.SerializeException(e);

            Console.WriteLine(message);
        }
    }    
}