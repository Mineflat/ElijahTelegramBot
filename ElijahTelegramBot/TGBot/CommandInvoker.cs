using ElijahTelegramBot.Core;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bots;
using Telegram.Bots.Http;
using Telegram.Bots.Types;



namespace ElijahTelegramBot.TGBot
{
    internal static class CommandInvoker
    {
        private delegate Task<(bool success, string errorMessage, Logger.LogLevel logLevel)> InvokeScriptDelegate(BotAction targetCommand, bool useRandom,
            (long chatID, long messageID) IDs);
        private static InvokeScriptDelegate RunScriptNormally = InvokeScript;
        #region Global commands
        public static (bool success, string errorMessage, Logger.LogLevel logLevel) Setup()
        {
            if (TGBot.WorkingConfiguration.ParallelScriptQueueLimit != null
                && TGBot.WorkingConfiguration.ParallelScriptQueueLimit > 0)
            {
                RunScriptNormally = InvokeScriptWithQueue;
                var setupResult = Storage.SetupResetTimer();
                Logger.Log(setupResult.logLevel, setupResult.errorMessage);
                return (true, $"На выполнение скриптов установлено ограничение:" +
                    $"\n\tНе более {TGBot.WorkingConfiguration.ParallelScriptQueueLimit} параллельных запусков любых команд типа Script",
                    Logger.LogLevel.Info);
            }
            else
            {
                return (true, "Внимание! На выполнение скриптов не установлено ограничение." +
                    "\n\tКоличество параллельных запусков любых команд типа Script НЕ ОГРАНИЧЕНО" +
                    "\n\tЧтобы это сообщение не появлялось, задайте параметр ParallelScriptQueueLimit в основном конфигурационном файле приложения",
                    Logger.LogLevel.Warn);
            }
        }
        public static async Task<(bool success, string errorMessage, Logger.LogLevel logLevel)> InvokeComand(BotAction targetCommand,
            (long chatID, long messageID) IDs)
        {
            if (TGBot._botClient == null) return (false, "Не удалось запустить команду, т.к. переменная не инициализирована (TGBot._botClient = null)", Logger.LogLevel.Critical);
            (bool success, string errorMessage, Logger.LogLevel logLevel) invocationResult;
            switch (targetCommand.Type.Trim().ToLower())
            {
                case "image":
                    invocationResult = await InvokeImage(targetCommand, false, IDs);
                    break;
                case "file":
                    invocationResult = await InvokeFile(targetCommand, false, IDs);
                    break;
                case "full_text":
                    invocationResult = await InvokeText(targetCommand, false, IDs);
                    break;
                case "random_text":
                    invocationResult = await InvokeText(targetCommand, true, IDs);
                    break;
                case "random_image":
                    invocationResult = await InvokeImage(targetCommand, true, IDs);
                    break;
                case "random_file":
                    invocationResult = await InvokeFile(targetCommand, true, IDs);
                    break;
                case "script":
                    invocationResult = await RunScriptNormally.Invoke(targetCommand, false, IDs);
                    break;
                case "random_script":
                    invocationResult = await RunScriptNormally.Invoke(targetCommand, true, IDs);
                    break;
                default:
                    targetCommand.ComandEnabled = false;
                    invocationResult = (false,
                        $"Команда {targetCommand.InvokeCommand} деактивирована, т.к. имеет неизвестный тип выполнения (Type): \"{targetCommand.Type}\"",
                        Logger.LogLevel.Warn);
                    break;
            }
            if (targetCommand.PostAction != null) return await InvokeComand(targetCommand.PostAction, IDs);
            return invocationResult;
        }
        #endregion
        #region Subcommands
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
        #endregion

        #region Bot action realization by type
        public static async Task<(bool success, string errorMessage, Logger.LogLevel logLevel)> InvokeFile(BotAction targetCommand, bool useRandom,
            (long chatID, long messageID) IDs)
        {
            string path = targetCommand.FilePath;
            string replyText = targetCommand.ReplyText;
            try
            {
                if (useRandom)
                {
                    var getRandomPathResult = GetRandomPath(path, new string[] { "*" });
                    if (!getRandomPathResult.success) return getRandomPathResult;
                    path = getRandomPathResult.errorMessage;
                }
                Logger.Log(Logger.LogLevel.Info, $"Выбранный файл для команды {targetCommand.InvokeCommand}: {path}");
                if (System.IO.File.Exists(targetCommand.RandomReplyPath))
                {
                    var getRandomTextResult = GetRandomText(targetCommand.RandomReplyPath);
                    if (getRandomTextResult.success) replyText = getRandomTextResult.errorMessage;
                }
                // Отправка файла
                string? filename = Path.GetFileName(path);
                if (string.IsNullOrEmpty(filename)) return (false, "", Logger.LogLevel.Error);
                await using Stream stream = System.IO.File.OpenRead(path);
                if (filename.ToLower().EndsWith(".mp4"))
                {
#pragma warning disable CS8604 // Возможно, аргумент-ссылка, допускающий значение NULL.
                    await TGBot._botClient.SendVideo(IDs.chatID,
                    video: Telegram.Bot.Types.InputFile.FromStream(stream, filename),
                    caption: replyText);
#pragma warning restore CS8604 // Возможно, аргумент-ссылка, допускающий значение NULL.
                }
                else
                {
#pragma warning disable CS8604 // Возможно, аргумент-ссылка, допускающий значение NULL.
                    await TGBot._botClient.SendDocument(IDs.chatID,
                        document: Telegram.Bot.Types.InputFile.FromStream(stream, filename),
                        caption: replyText);
#pragma warning restore CS8604 // Возможно, аргумент-ссылка, допускающий значение NULL.
                }
                stream.Dispose();
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
            (long chatID, long messageID) IDs)
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
                if (System.IO.File.Exists(targetCommand.RandomReplyPath))
                {
                    var getRandomTextResult = GetRandomText(targetCommand.RandomReplyPath);
                    if (getRandomTextResult.success) replyText = getRandomTextResult.errorMessage;
                }
                string? filename = Path.GetFileName(path);
                using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var fts = new InputFileStream(fileStream, filename);
#pragma warning disable CS8604 // Возможно, аргумент-ссылка, допускающий значение NULL.
                    await TGBot._botClient.SendPhoto(IDs.chatID, fts,
                        replyParameters:
                        new ReplyParameters()
                        {
                            AllowSendingWithoutReply = false
                        }, caption: replyText
                    );
#pragma warning restore CS8604 // Возможно, аргумент-ссылка, допускающий значение NULL.
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
            (long chatID, long messageID) IDs)
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

                if (System.IO.File.Exists(targetCommand.RandomReplyPath))
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

#pragma warning disable CS8604 // Возможно, аргумент-ссылка, допускающий значение NULL.
                await TGBot._botClient.SendMessage(IDs.chatID, replyText + Environment.NewLine + FullText,
                Telegram.Bot.Types.Enums.ParseMode.Markdown,
                protectContent: false,
                replyParameters:
                    new ReplyParameters()
                    {
                        AllowSendingWithoutReply = false
                    }
                );
#pragma warning restore CS8604 // Возможно, аргумент-ссылка, допускающий значение NULL.
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
            (long chatID, long messageID) IDs)
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
#pragma warning disable CS8604 // Возможно, аргумент-ссылка, допускающий значение NULL.
                await TGBot._botClient.SendMessage(IDs.chatID, $"Задача {targetCommand.InvokeCommand} устанавливается для выполнения:" +
                    "```" +
                    $"bash -c {targetCommand.FilePath} {targetCommand.CommandArgs}" +
                    "```" +
                    $"Я напишу, когда ее выполнение закончится",
                    Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    protectContent: false,
                    replyParameters:
                    new ReplyParameters()
                    {
                        AllowSendingWithoutReply = false
                    }
                );
#pragma warning restore CS8604 // Возможно, аргумент-ссылка, допускающий значение NULL.
                return (true, $"Задача не может быть поставлена на выполнение, т.к. этот функционал недоступен в данной версии приложения:\n{path}", Logger.LogLevel.Warn);
            }
            catch (Exception CommandInvocationException)
            {
                targetCommand.ComandEnabled = false;
                Logger.Log(Logger.LogLevel.Info, $"Команда {targetCommand.InvokeCommand} отключена из-за ошибки выполнения");
                return (false, $"Ошибка при выполнении команды {targetCommand.InvokeCommand}:\n{CommandInvocationException.Message}", Logger.LogLevel.Error);
            }
        }
        public static async Task<(bool success, string errorMessage, Logger.LogLevel logLevel)> InvokeScriptWithQueue(BotAction targetCommand, bool useRandom,
            (long chatID, long messageID) IDs)
        {
            //var queueAddResult = Storage.AddScriptInQueue();
            //if (!queueAddResult.success) return queueAddResult;
            //Logger.Log(queueAddResult.logLevel, queueAddResult.errorMessage);
            return await InvokeScript(targetCommand, useRandom, IDs);
        }
        #endregion

    }
}
