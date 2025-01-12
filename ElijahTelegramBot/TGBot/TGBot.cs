﻿using ElijahTelegramBot.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using System.Threading;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using System.Data;
using Polly;
using System.Runtime.CompilerServices;


namespace ElijahTelegramBot.TGBot
{
    internal class TGBot
    {
        public static List<BotAction> AvailebleActions { get; protected set; } = new List<BotAction>();
        public static List<BotRole> AvailebleRoles { get; protected set; } = new List<BotRole>();
        public static Configuration WorkingConfiguration { get; protected set; } = new Configuration();
        public static ITelegramBotClient? _botClient { get; protected set; }
        public static ReceiverOptions? _receiverOptions { get; protected set; }
        public static CancellationTokenSource? _cts { get; protected set; }
        public static ushort BotErrorsLeft { get; protected set; } = 5;

        private static string _configurationPath = string.Empty;

        private static List<(string roleName, string comandName, BotAction command)> _roleCommandRatio
            = new List<(string roleName, string comandName, BotAction command)>();

        private static System.Threading.Timer? timer;
        public TGBot(string configurationPath)
        {
            _configurationPath = configurationPath;
            var initResult = InitConfiguration();
            Logger.Log(initResult.logLevel, initResult.errorMessage);
        }
        protected (bool success, string errorMessage, Logger.LogLevel logLevel) InitConfiguration()
        {
            Logger.Log(Logger.LogLevel.Info, "Чтение конфигурации сервера...");
            try
            {
                string fileContent = System.IO.File.ReadAllText(_configurationPath);
#pragma warning disable CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.
                WorkingConfiguration = JsonConvert.DeserializeObject<Configuration>(fileContent);
#pragma warning restore CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.
                if (WorkingConfiguration != null)
                {
                    Logger.Log(Logger.LogLevel.Info, "Инициализация...");
                    return InitBot();
                }
                return (false, $"Не удалось прочитать файл конфигурации \"{_configurationPath}\": пустой файл", Logger.LogLevel.Critical);
            }
            catch (Exception configurationReadException)
            {
                return (false, $"Не удалость прочитать конфигурационный файл сервера: {configurationReadException.Message}", Logger.LogLevel.Critical);
            }
        }
        protected (bool success, string errorMessage, Logger.LogLevel logLevel) InitBot()
        {
            // Примечение: конфигурация WorkingConfiguration должна быть считана и применена заранее в другом метода
            Logger.Log(Logger.LogLevel.Info, "Запуск дополнительных проверок перед стартом...");
            var initFunctions = new List<Func<(bool success, string errorMessage, Logger.LogLevel logLevel)>>()
            {
                VerifyConfigiration,
                InitRoles,
                InitCommands,
                InitTelegramBot
            };
            foreach (var func in initFunctions)
            {
                (bool success, string errorMessage, Logger.LogLevel logLevel) execResult = func();
                Logger.Log(execResult.logLevel, execResult.errorMessage);
                if (!execResult.success) return execResult;
            }
            return (true, "Инициализация бота успешно пройдена!", Logger.LogLevel.Success);
        }
        protected (bool success, string errorMessage, Logger.LogLevel logLevel) VerifyConfigiration()
        {
            Logger.Log(Logger.LogLevel.Info, "Проверка параметров конфигурации...");

            if (!System.IO.File.Exists(WorkingConfiguration.RolesPath)) return (false, "Не задан путь к файлу с конфигурацией РОЛЕЙ", Logger.LogLevel.Critical);
            if (!System.IO.File.Exists(WorkingConfiguration.ActionsPath)) return (false, "Не задан путь к файлу с конфигурацией ДЕЙСТВИЙ", Logger.LogLevel.Critical);
            if (string.IsNullOrEmpty(WorkingConfiguration.BotToken)) return (false, "Не задан токен для запуска Телеграмм-бота", Logger.LogLevel.Critical);
            if (WorkingConfiguration.AdminID <= 0) return (false, "Не задан ID администратора Телеграмм-бота", Logger.LogLevel.Critical);
            return (true, "Переданная конфигурация прошла первичную проверку", Logger.LogLevel.Info);
        }
        protected (bool success, string errorMessage, Logger.LogLevel logLevel) InitRoles()
        {
            Logger.Log(Logger.LogLevel.Info, "Выгрузка списока ролей пользователей...");
            try
            {
                var json = System.IO.File.ReadAllText(WorkingConfiguration.RolesPath);
#pragma warning disable CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.
                AvailebleRoles = JsonConvert.DeserializeObject<List<BotRole>>(json);
#pragma warning restore CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.
                if (AvailebleRoles == null)
                {
                    return (false, $"Произошла ошибка при применении РОЛЕЙ пользователей: неверный формат JSON", Logger.LogLevel.Critical);
                }
                var nonUniqRoleNames = AvailebleRoles.GroupBy(p => p.Name) // Группируем по имени
                    .Where(g => g.Count() > 1) // Оставляем только те группы, где больше 1 элемента
                    .SelectMany(g => g) // Раскрываем группы в отдельные элементы
                    .ToList();
                if (nonUniqRoleNames.Any())
                {
                    foreach (var role in nonUniqRoleNames)
                    {
                        return (false, $"Произошла ошибка при применении РОЛЕЙ пользователей: " +
                            $"роль {role.Name} несколько раз дублируется в файле {WorkingConfiguration.RolesPath}",
                            Logger.LogLevel.Critical);
                    }
                }

            }
            catch (Exception initExeption)
            {
                return (false, $"Произошла ошибка при применении РОЛЕЙ пользователей: {initExeption.Message}", Logger.LogLevel.Critical);
            }
            if (AvailebleRoles.Count == 0)
            {
                return (false, $"Произошла ошибка при применении РОЛЕЙ пользователей: верифицировано слишком мало ролей для старта бота", Logger.LogLevel.Critical);
            }
            return (true, $"Роли пользователей успешно применены. Доступно ролей: {AvailebleRoles.Count}", Logger.LogLevel.Success);
        }
        protected (bool success, string errorMessage, Logger.LogLevel logLevel) InitCommands()
        {
            Logger.Log(Logger.LogLevel.Info, "Выгрузка списока доступных действий...");
            try
            {
                var json = System.IO.File.ReadAllText(WorkingConfiguration.ActionsPath);
#pragma warning disable CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.
                AvailebleActions = JsonConvert.DeserializeObject<List<BotAction>>(json);
#pragma warning restore CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.
                if (AvailebleActions == null)
                {
                    return (false, $"Произошла ошибка при применении КОМАНД пользователей: неверный формат JSON", Logger.LogLevel.Critical);
                }
                // Поиск иднтичных команд по строке их вызова 
                var nonUniqRoleNames = AvailebleActions.GroupBy(p => p.InvokeCommand) // Группируем по имени
                    .Where(g => g.Count() > 1) // Оставляем только те группы, где больше 1 элемента
                    .SelectMany(g => g) // Раскрываем группы в отдельные элементы
                    .ToList();
                if (nonUniqRoleNames.Any())
                {
                    foreach (var comand in nonUniqRoleNames)
                    {
                        return (false, $"Произошла ошибка при применении КОМАНД пользователей: " +
                            $"команда {(string.IsNullOrEmpty(comand.InvokeCommand) ? "(пусто)" : comand.InvokeCommand)} несколько раз дублируется в файле {WorkingConfiguration.ActionsPath}",
                            Logger.LogLevel.Critical);
                    }
                }
                foreach (var comand in AvailebleActions)
                {
                    var verificationResult = comand.Verify();
                    Logger.Log(verificationResult.logLevel, verificationResult.errorMessage);
                    if (verificationResult.success && comand.ComandEnabled)
                    {
                        comand.RoleNames.ForEach(x => _roleCommandRatio.Add((x, comand.InvokeCommand, comand)));
                        Logger.Log(Logger.LogLevel.Debug, $"Успешно добавлена команда {comand.InvokeCommand} с типом {comand.Type}");
                    }
                }
            }
            catch (Exception initExeption)
            {
                return (false, $"Произошла ошибка при применении КОМАНД пользователей: {initExeption.Message}", Logger.LogLevel.Critical);
            }
            if (_roleCommandRatio.Count == 0)
            {
                return (false, $"Произошла ошибка при применении КОМАНД пользователей: верифицировано слишком мало команд для старта бота", Logger.LogLevel.Critical);
            }
            return (true, $"Команды пользователей успешно применены. Доступно команд: {AvailebleActions.Count}", Logger.LogLevel.Success);
        }
        protected (bool success, string errorMessage, Logger.LogLevel logLevel) InitTelegramBot()
        {
            Logger.Log(Logger.LogLevel.Info, "Инициализация сервиса Телеграм-бота...");
            try
            {
                _botClient = new TelegramBotClient(WorkingConfiguration.BotToken);
                _cts = new CancellationTokenSource();
                var receiverOptions = new ReceiverOptions
                {
                    AllowedUpdates = { }, // receive all update types
                };
                _botClient.StartReceiving(
                    HandleUpdateAsync,
                    HandleErrorAsync,
                    receiverOptions,
                    _cts.Token
                );

                var tokenVerificationResult = _botClient.GetMe();
                if (!string.IsNullOrEmpty(tokenVerificationResult.Result.Username))
                {
#pragma warning disable CS8622 // Допустимость значений NULL для ссылочных типов в типе параметра не соответствует целевому объекту делегирования (возможно, из-за атрибутов допустимости значений NULL).
                    timer = new System.Threading.Timer(ResetErrorsEvent, null, 0, 15000); // Интервал в миллисекундах (15 секунд)
#pragma warning restore CS8622 // Допустимость значений NULL для ссылочных типов в типе параметра не соответствует целевому объекту делегирования (возможно, из-за атрибутов допустимости значений NULL).
                    return (true, $"Успешный запуск бота {tokenVerificationResult.Result.Username}", Logger.LogLevel.Success);
                }
                else
                {
                    return (false, $"Не удалось запустить Телеграмм-бота: не удалось подтвердить переданный токен на серверах Телеграмм", Logger.LogLevel.Critical);
                }
            }
            catch (Exception botStartupException)
            {
                return (false, $"Не удалось запустить Телеграмм-бота по следующей причине: {botStartupException.Message}", Logger.LogLevel.Critical);
            }
        }
        private static void ResetErrorsEvent(object state)
        {
            BotErrorsLeft = 5;
        }
        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            // Некоторые действия
            if (update.Type == Telegram.Bot.Types.Enums.UpdateType.Message)
            {
                if (!string.IsNullOrEmpty(update.Message?.Text))
                {
                    string messageText = update.Message.Text.Trim().ToLower();
                    long chatID = update.Message.Chat.Id;
                    if (update.Message.From == null)
                    {
                        Logger.Log(Logger.LogLevel.Warn, $"Получено сообщение из чата {chatID} с пустым ID отправителя. Я не могу такое обработать");
                        return;
                    }
                    Logger.Log(Logger.LogLevel.Message, $"[{update.Message.From.Username}] {messageText}");
                    if (update.Message.From.Id == WorkingConfiguration.AdminID) _ = CheckSystemCommand(messageText, chatID).ConfigureAwait(true);
                    // Получаем объект команды
                    var targetCommand = _roleCommandRatio.FirstOrDefault(x => x.comandName.Trim().ToLower().StartsWith(messageText)).command;

                    if (targetCommand == null) return;
                    // Получаем роли пользователя по указанным ID
                    List<BotRole> grantedRoles = AvailebleRoles.FindAll(x => x.UserIds.Contains(update.Message.From.Id));
                    // Пользователь с таким ID не имеет никаких ролей на сервере
                    if (grantedRoles == null)
                    {
                        Logger.Log(Logger.LogLevel.Warn, $"Внимание! " +
                            $"Пользователь {update.Message.From.FirstName} {update.Message.From.LastName}({update.Message.From.Username}, {update.Message.From.Id})" +
                            $"попытался выполнить команду \"{messageText}\", " +
                            $"но он не пренадлежит ни к одной из доступных ролей");
                        return;
                    }
                    foreach (BotRole role in grantedRoles)
                    {
                        if (targetCommand.RoleNames.Contains(role.Name))
                        {
                            Logger.Log(Logger.LogLevel.Comand, $"Запуск команды {targetCommand.InvokeCommand} пользователем {update.Message.From.Username} ({update.Message.From.Id})");
                            var invocationResult = await CommandInvoker.InvokeComand(targetCommand, botClient, (chatID, update.Message.From.Id));
                            Logger.Log(invocationResult.logLevel,
                                $"Команда {targetCommand.InvokeCommand} завершена {(invocationResult.success ? "успешно" : "с ошибкой")}");
                            Logger.Log(Logger.LogLevel.Debug, $"Результат завершения команды {targetCommand.InvokeCommand}: {invocationResult.errorMessage}");
                            if (!invocationResult.success)
                            {
                                invocationResult = CommandInvoker.GetRandomText(targetCommand.ErrorReplyPath);
                                await botClient.SendMessage(chatID, (invocationResult.success ? invocationResult.errorMessage : "Мне не удалось выполнить эту команду"),
                                    Telegram.Bot.Types.Enums.ParseMode.Markdown,
                                    protectContent: false,
                                    replyParameters:
                                        new ReplyParameters()
                                        {
                                            AllowSendingWithoutReply = false
                                        }
                                    );
                            }
                            return;
                        }
                    }
#pragma warning disable CS8604 // Возможно, аргумент-ссылка, допускающий значение NULL.
                    await botClient.SendMessage(chatID, "Я не могу выполнить эту команду, т.к. у тебя надостаточно прав для ее выполнения", cancellationToken: cancellationToken);
#pragma warning restore CS8604 // Возможно, аргумент-ссылка, допускающий значение NULL.
                    // Логировать, что эта команда так-то не для него 

                    // Ну это пиздец, это надо переписывать...
                    //List<string> availebleCommands = new List<string>();
                    //foreach (var role in grantedRoles)
                    //{
                    //    availebleCommands.Add(AvailebleActions.FindAll(x => x.RoleNames.Contains(role.Name)));

                    //}
                    //foreach (BotAction action in AvailebleActions)
                    //{

                    //    //if (string.IsNullOrEmpty(action.RoleNames.Find(x => x.StartsWith(messageText)))) continue;
                    //    if (action.InvokeCommand.ToLower().StartsWith(messageText))
                    //    {
                    //        // Выполнение команды
                    //    }
                    //}
                }
                //var message = update.Message;
                //if (message?.Text?.ToLower() == "/start")
                //{
                //    await botClient.SendMessage(message.Chat, "Добро пожаловать на борт, добрый путник!");
                //    return;
                //}
                //await botClient.SendMessage(message.Chat, "Привет-привет!!");
            }
            else
            {
                Logger.Log(Logger.LogLevel.Debug, $"Получено обновление типа {update.Type}");
            }
        }

        private static async Task CheckSystemCommand(string messageText, long chatID)
        {
            messageText = messageText.Trim().ToLower();
            if (_botClient == null)
            {
                Logger.Log(Logger.LogLevel.Critical, "Переменная _botClient не может быть NULL. Бот не запущен!");
                return;
            }
            switch (messageText)
            {
                case "!restart_bot":
                    await _botClient.SendMessage(chatID, $"Запуск системной команды \"{messageText}\"");
                    _cts?.CancelAsync();
                    _botClient = null;
                    Storage.tGBot = null;
                    Storage.tGBot = new TGBot(_configurationPath);
                    break;
                default:
                    return;
            }
            Logger.Log(Logger.LogLevel.Info, $"Инициализирован перезапуск бота пользователем {WorkingConfiguration.AdminID} в чате {chatID}");
        }

        public static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Logger.Log(Logger.LogLevel.Error,
                $"Произошла ошибка во время работа бота: {exception.Message}.\n" +
                $"\tДо остановки бота осталось {BotErrorsLeft - 1} ошибок\n" +
                $"\tЗначение обновится не позднее чем через 15 секунд");
            if (BotErrorsLeft - 1 == 0)
            {
                Logger.Log(Logger.LogLevel.Critical, "Обработано слишком много ошибок за предыдущие 15 секунд. Остановка приложения");
            }
            return Task.CompletedTask;
        }
    }
}
