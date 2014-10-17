using System;
using System.Diagnostics;

namespace Baker
{
    /// <summary>
    /// Represents a tracing helper
    /// </summary>
    public static class Tracing
    {
        /// <summary>
        /// Prints out a debug message
        /// </summary>
        /// <param name="message"></param>
        public static void Debug(string category, string message)
        {
            Console.WriteLine("{0}: {1}", category, message);
        }

        /// <summary>
        /// Prints out an info message.
        /// </summary>
        /// <param name="message"></param>
        public static void Info(string category, string message)
        {
            Console.WriteLine("{0}: {1}", category, message);
        }

        /// <summary>
        /// Prints out an error message.
        /// </summary>
        /// <param name="message"></param>
        public static void Error(string category, string message)
        {
            Console.Write(category + ": ");
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine(message);
            Console.ForegroundColor = color;
        }

        /// <summary>
        /// Prints out an error message.
        /// </summary>
        /// <param name="ex"></param>
        public static void Error(string category, Exception ex)
        {
            Error(category, ex.Message);
        }
    }
}
