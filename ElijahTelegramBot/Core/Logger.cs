using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElijahTelegramBot.Core
{
    internal static class Logger
    {
        public enum LogLevel
        {
            Error, Debug, Info, Message, Warn, Comand, Success, Critical
        }

        public static void Log (LogLevel logLevel, string targetMessage)
        {
            if (string.IsNullOrEmpty(targetMessage)) return;
            if (!TGBot.TGBot.WorkingConfiguration.DebugMode && logLevel == LogLevel.Debug) return;
            string logMessage = $"[{DateTime.Now}][{logLevel.ToString().ToUpper()}]";
            switch (logLevel)
            {
                case LogLevel.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case LogLevel.Debug:
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                case LogLevel.Info:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogLevel.Message:
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    break;
                case LogLevel.Warn:
                    Console.ForegroundColor = ConsoleColor.Black;
                    Console.BackgroundColor = ConsoleColor.DarkYellow;
                    break;
                case LogLevel.Comand:
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    break;
                case LogLevel.Success:
                    Console.ForegroundColor = ConsoleColor.Green;
                    break;
                case LogLevel.Critical:
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.BackgroundColor = ConsoleColor.DarkRed;
                    break;
            }
            Console.Write(logMessage);
            Console.ResetColor();
            Console.WriteLine(" " + targetMessage);
            if(!string.IsNullOrEmpty(TGBot.TGBot.WorkingConfiguration.LogPath))
            {
                try
                {
                    File.AppendAllText(TGBot.TGBot.WorkingConfiguration.LogPath, $"{logMessage} {targetMessage}{Environment.NewLine}");
                }
                catch (Exception logWriteException)
                {
                    Console.WriteLine($"Ошибка записи лога в файл {TGBot.TGBot.WorkingConfiguration.LogPath}: {logWriteException.Message}");
                    TGBot.TGBot.WorkingConfiguration.LogPath = string.Empty;
                    Log(LogLevel.Info, $"Логирование в файл отключено");
                }
            }
            if (logLevel == LogLevel.Critical) Program.OnPanic();
        }
    }
}
