using ElijahTelegramBot.Core;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bots;
using Telegram.Bots.Http;



namespace ElijahTelegramBot.TGBot
{
    internal static class CommandInvoker
    {
        private static int GetRandom(int minValue, int maxValue)
        {
            if (minValue > maxValue)
                throw new ArgumentOutOfRangeException(nameof(minValue), "minValue must be less than or equal to maxValue");

            if (minValue == maxValue)
                return minValue; // Диапазон содержит одно значение

            // Разница между maxValue и minValue
            long range = (long)maxValue - minValue + 1;

            // Байтовый массив для хранения случайных данных
            byte[] randomBytes = new byte[4]; // Int32 занимает 4 байта
            using (var rng = RandomNumberGenerator.Create())
            {
                int randomValue;
                do
                {
                    rng.GetBytes(randomBytes); // Заполняем массив случайными байтами
                    randomValue = BitConverter.ToInt32(randomBytes, 0) & int.MaxValue; // Преобразуем в положительное число
                }
                while (randomValue >= range * (int.MaxValue / range)); // Исключаем сдвиги диапазона

                return (int)(randomValue % range + minValue); // Преобразуем в диапазон
            }
        }
        private static (bool success, string errorMessage, Logger.LogLevel logLevel) GetRandomPath(string dirPath, string[] extentions)
        {
            if (extentions.Length == 0) return (false, "Не удалось выбрать произвольный файл: пустой массив расширений файлов", Logger.LogLevel.Error);
            if (!Directory.Exists(dirPath)) return (false, "Не удалось выбрать произвольный файл: директория не сущесутвует", Logger.LogLevel.Error);
            try
            {
                var selectedFiles = extentions
                    .SelectMany(ext => Directory.GetFiles(dirPath, ext, SearchOption.AllDirectories))
                    .ToArray();
                if (selectedFiles.Length == 0) return (false, $"Не удалось выбрать произвольный файл: в директории {dirPath} нет ни одного файла с указанными расширениями {extentions.Length}", Logger.LogLevel.Error);
                return (true, selectedFiles[GetRandom(0, selectedFiles.Length - 1)], Logger.LogLevel.Success);
            }
            catch (Exception directoryLookupException)
            {
                return (false, $"Не удалось выбрать произвольный файл: {directoryLookupException.Message}", Logger.LogLevel.Error);
            }
        }
        public static (bool success, string errorMessage, Logger.LogLevel logLevel) GetRandomText(string fullPath)
        {
            if (!System.IO.File.Exists(fullPath)) return (false, $"Не удалось выбрать произвольную строку в файле {fullPath}: файл не сущесутвует", Logger.LogLevel.Error);
            try
            {
                string fullText = System.IO.File.ReadAllText(fullPath);
                List<string>? lines = JsonSerializer.Deserialize<List<string>>(fullText);
                if (lines == null || lines.Count == 0) return (false, $"Не удалось выбрать произвольную строку в файле {fullPath}: файл пуст", Logger.LogLevel.Error);
                return (true, lines[GetRandom(0, lines.Count - 1)], Logger.LogLevel.Success);
            }
            catch (Exception directoryLookupException)
            {
                return (false, $"Не удалось выбрать произвольную строку в файле {fullPath}: {directoryLookupException.Message}", Logger.LogLevel.Error);
            }
        }
        public static async Task<(bool success, string errorMessage, Logger.LogLevel logLevel)> InvokeComand(BotAction targetCommand,
            ITelegramBotClient _botClient, (long chatID, long messageID) IDs)
        {
            (bool success, string errorMessage, Logger.LogLevel logLevel) invocationResult;
            switch (targetCommand.Type.Trim().ToLower())
            {
                case "script":
                    invocationResult = await InvokeScript(targetCommand, false, _botClient, IDs);
                    break;
                case "image":
                    invocationResult = await InvokeImage(targetCommand, false, _botClient, IDs);
                    break;
                case "file":
                    invocationResult = await InvokeFile(targetCommand, false, _botClient, IDs);
                    break;
                case "full_text":
                    invocationResult = await InvokeText(targetCommand, false, _botClient, IDs);
                    break;
                case "random_text":
                    invocationResult = await InvokeText(targetCommand, true, _botClient, IDs);
                    break;
                case "random_image":
                    invocationResult = await InvokeImage(targetCommand, true, _botClient, IDs);
                    break;
                case "random_file":
                    invocationResult = await InvokeFile(targetCommand, true, _botClient, IDs);
                    break;
                case "random_script":
                    invocationResult = await InvokeScript(targetCommand, true, _botClient, IDs);
                    break;
                default:
                    targetCommand.ComandEnabled = false;
                    invocationResult = (false,
                        $"Команда {targetCommand.InvokeCommand} деактивирована, т.к. имеет неизвестный тип выполнения (Type): \"{targetCommand.Type}\"",
                        Logger.LogLevel.Warn);
                    break;
            }
            if (targetCommand.PostAction != null) return await InvokeComand(targetCommand.PostAction, _botClient, IDs);
            return invocationResult;
        }
        public static async Task<(bool success, string errorMessage, Logger.LogLevel logLevel)> InvokeFile(BotAction targetCommand, bool useRandom,
            ITelegramBotClient _botClient, (long chatID, long messageID) IDs)
        {
            string path = targetCommand.FilePath;
            string replyText = targetCommand.ReplyText;
            try
            {
                if (useRandom)
                {
                    var getRandomPathResult = GetRandomPath(path, new string[] { "*" });
                    if (getRandomPathResult.success) return getRandomPathResult;
                    path = getRandomPathResult.errorMessage;
                }
                Logger.Log(Logger.LogLevel.Info, $"Выбранный файл для команды {targetCommand.InvokeCommand}: {path}");
                if (File.Exists(targetCommand.RandomReplyPath))
                {
                    var getRandomTextResult = GetRandomText(targetCommand.RandomReplyPath);
                    if (getRandomTextResult.success) replyText = getRandomTextResult.errorMessage;
                }
                // Отправка файла
                string? filename = Path.GetFileName(path);
                if (string.IsNullOrEmpty(filename)) return (false, "", Logger.LogLevel.Error);
                await using Stream stream = System.IO.File.OpenRead(path);
                await _botClient.SendDocument(IDs.chatID,
                    document: Telegram.Bot.Types.InputFile.FromStream(stream, filename),
                    caption: replyText);
                return (true, $"Успешно отправлен файл в чат {IDs.chatID}: {path}", Logger.LogLevel.Success);
            }
            catch (Exception CommandInvocationException)
            {
                targetCommand.ComandEnabled = false;
                Logger.Log(Logger.LogLevel.Info, $"Команда {targetCommand.InvokeCommand} отключена из-за ошибки выполнения");
                return (false, $"Ошибка при выполнении команды {targetCommand.InvokeCommand}:\n{CommandInvocationException.Message}", Logger.LogLevel.Error);
            }
        }
        public static async Task<(bool success, string errorMessage, Logger.LogLevel logLevel)> InvokeImage(BotAction targetCommand, bool useRandom,
            ITelegramBotClient _botClient, (long chatID, long messageID) IDs)
        {
            string path = targetCommand.FilePath;
            string replyText = targetCommand.ReplyText;
            try
            {
                if (useRandom)
                {
                    var getRandomPathResult = GetRandomPath(path, ["*.png", "*.jpeg", "*.jpg"]);
                    if (!getRandomPathResult.success) return getRandomPathResult;
                    path = getRandomPathResult.errorMessage;
                }
                Logger.Log(Logger.LogLevel.Info, $"Выбранный файл для команды {targetCommand.InvokeCommand}: {path}");
                if (File.Exists(targetCommand.RandomReplyPath))
                {
                    var getRandomTextResult = GetRandomText(targetCommand.RandomReplyPath);
                    if (getRandomTextResult.success) replyText = getRandomTextResult.errorMessage;
                }
                string? filename = Path.GetFileName(path);
                using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var fts = new InputFileStream(fileStream, filename);
                    await _botClient.SendPhoto(IDs.chatID, fts,
                        replyParameters:
                        new ReplyParameters()
                        {
                            AllowSendingWithoutReply = false
                        }, caption: replyText
                    );
                }
                return (true, $"Успешно отправлена картинка в чат {IDs.chatID}: {path}", Logger.LogLevel.Comand);
            }
            catch (Exception CommandInvocationException)
            {
                targetCommand.ComandEnabled = false;
                Logger.Log(Logger.LogLevel.Info, $"Команда {targetCommand.InvokeCommand} отключена из-за ошибки выполнения");
                return (false, $"Ошибка при выполнении команды {targetCommand.InvokeCommand}:\n{CommandInvocationException.Message}", Logger.LogLevel.Error);
            }
        }
        public static async Task<(bool success, string errorMessage, Logger.LogLevel logLevel)> InvokeText(BotAction targetCommand, bool useRandom,
            ITelegramBotClient _botClient, (long chatID, long messageID) IDs)
        {
            try
            {
                string FullText = System.IO.File.ReadAllText(targetCommand.FilePath);
                string replyText = targetCommand.ReplyText;
                if (useRandom)
                {
                    var getRandomTextResult = GetRandomText(targetCommand.FilePath);
                    if (!getRandomTextResult.success) return getRandomTextResult;
                    FullText = getRandomTextResult.errorMessage;
                }

                if (File.Exists(targetCommand.RandomReplyPath))
                {
                    var getRandomTextResult = GetRandomText(targetCommand.RandomReplyPath);
                    if (getRandomTextResult.success) replyText = getRandomTextResult.errorMessage;
                }
                Logger.Log(Logger.LogLevel.Debug, $"Выбранный текст для команды {targetCommand.InvokeCommand}: {FullText}");
                // Отправка сообщения, я мать того ебаната который ввел ReplyParameters шатал
                // Ебаный сын шлюхи ебанул параметр MessageID по НАЗВАНИЮ СУКА КЛАССА 
                // IDE в ахуе с этого хуесоса...
                // Документация на момент 10.11 - параша ебаная 
                // Что? Тут указано, что можно указывать в replyParameters ID сообщения?
                // https://telegrambots.github.io/book/2/send-msg/text-msg.html
                // У библиотеки ответ прост: а отсосать не хотите? Это конкретный тип ReplyParameters. 
                // Он такой же спидозный, как и мамаша того овоща, который ее писал

                await _botClient.SendMessage(IDs.chatID, replyText + Environment.NewLine + FullText,
                Telegram.Bot.Types.Enums.ParseMode.Markdown,
                protectContent: false,
                replyParameters:
                    new ReplyParameters()
                    {
                        AllowSendingWithoutReply = false
                    }
                );
                return (true, $"Успешно отправлено сообщениев чат {IDs.chatID}:\n{FullText}", Logger.LogLevel.Comand);
            }
            catch (Exception CommandInvocationException)
            {
                targetCommand.ComandEnabled = false;
                Logger.Log(Logger.LogLevel.Info, $"Команда {targetCommand.InvokeCommand} отключена из-за ошибки выполнения");
                return (false, $"Ошибка при выполнении команды {targetCommand.InvokeCommand}:\n{CommandInvocationException.Message}", Logger.LogLevel.Error);
            }
        }
        public static async Task<(bool success, string errorMessage, Logger.LogLevel logLevel)> InvokeScript(BotAction targetCommand, bool useRandom,
            ITelegramBotClient _botClient, (long chatID, long messageID) IDs)
        {
            string path = targetCommand.FilePath;
            try
            {
                if (useRandom)
                {
                    var getRandomPathResult = GetRandomPath(path, new string[] { "*" });
                    if (getRandomPathResult.success) return getRandomPathResult;
                    path = getRandomPathResult.errorMessage;
                }
                Logger.Log(Logger.LogLevel.Info, $"Выбранный файл для команды {targetCommand.InvokeCommand}: {path}");
                // Постановка в отдельном потоке задачи

                await _botClient.SendMessage(IDs.chatID,
                    $"Задача не может быть поставлена на выполнение, т.к. этот функционал недоступен в данной версии приложения:\n{path}",
                    Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    protectContent: false,
                    replyParameters:
                        new ReplyParameters()
                        {
                            AllowSendingWithoutReply = false
                        }
                    );

                //await _botClient.SendMessage(IDs.chatID,
                //    $"Задача не может быть поставлена на выполнение, т.к. этот функционал недоступен в данной версии приложения:\n{path}",
                //    Telegram.Bot.Types.Enums.ParseMode.Markdown,
                //    protectContent: false,
                //    replyParameters:
                //        new ReplyParameters()
                //        {
                //            AllowSendingWithoutReply = false
                //        }
                //    );
                return (true, $"Задача не может быть поставлена на выполнение, т.к. этот функционал недоступен в данной версии приложения:\n{path}", Logger.LogLevel.Warn);
                //return (true, $"Успешно поставлена на выполнение задача: {path}", Logger.LogLevel.Comand);    
            }
            catch (Exception CommandInvocationException)
            {
                targetCommand.ComandEnabled = false;
                Logger.Log(Logger.LogLevel.Info, $"Команда {targetCommand.InvokeCommand} отключена из-за ошибки выполнения");
                return (false, $"Ошибка при выполнении команды {targetCommand.InvokeCommand}:\n{CommandInvocationException.Message}", Logger.LogLevel.Error);
            }
        }
    }
}
