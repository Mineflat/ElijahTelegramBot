using ElijahTelegramBot.Core;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElijahTelegramBot.TGBot
{
    internal class BotAction
    {
        // Команда для бота, по которой запустится действие
        public string InvokeCommand { get; set; } = string.Empty;
        // Тип команды:
        // random_text - отправить случайную часть текста в чат. Для разделения текста используется формат JSON
        // fill_text - отправить содерживое текстового файла в чат
        // file - отправить конкретный файл в чат
        // image - отправить конкретную картинку в чат
        // random_image - отправить случайную картинку в чат
        // random_file - отправить случайный файл в чат
        // random_script- выполнить случайный скрипт из указанной директории
        // comand - исполнить какую-то команду или скрипт
        public string Type { get; set; } = string.Empty;
        // Путь к файлу или директории, с которой необходимо произвести действие 
        public string FilePath { get; set; } = string.Empty;
        // Текст сообщения, который будет отправлен вместе с командой
        public string ReplyText { get; set; } = string.Empty;
        // Если вы хотите использовать произвольные ответы после выполнения команды, заполните этот праметр - путь к файлу с рандомными ответами (JSON)
        public string RandomReplyPath { get; set; } = string.Empty;
        // Если используется тип действия "comand", то через этот парамтер вы можете передать какие-то аргументы запускаемому скрипту. Работает только с типом действия "command"
        public string CommandArgs { get; set; } = string.Empty;
        // Действие, которое необходимо выполнить после того, как команда отработает (конфигурация аналогична этому действию)
        public BotAction? PostAction { get; set; }

        // Назвения ролей, члены которых могут выполнить эту команду
        public List<string> RoleNames { get; set; } = new List<string>();
        // Произвольные ответы пользователю в случае, если он попытался выполнить команду,
        // выполнение которой запрещено - пользователь не пренадлежит ни одной роли, для которой доступна эта команда (JSON)
        public string ErrorReplyPath { get; set; } = string.Empty;
        // Разрешает или запрещает использование этой команды всеми пользователями. Если false, команда считается неактивной и ее  вызов не предполагается
        public bool ComandEnabled { get; set; } = false;
        public (bool success, string errorMessage, Logger.LogLevel logLevel) Verify()
        {
            if(!ComandEnabled)
            {
                return (true, $"Действие \"{InvokeCommand}\" помечено как неиспользуемое, поэтому не проверяется", Logger.LogLevel.Warn);
            }
            string errorList = string.Empty;
            if (string.IsNullOrEmpty(InvokeCommand)) errorList += "\t\tНе задано ключевое свлово для вызова команды (параметр InvokeCommand)\n";
            if (string.IsNullOrEmpty(Type)) errorList += "\t\tНе задан тип команды (параметр Type)\n";
            if (string.IsNullOrEmpty(ReplyText) && !File.Exists(RandomReplyPath))
                errorList += "\t\tНе заданы параметры ReplyText и RandomReplyPath: как минимум 1 из них должен быть задан\n";
            if (RoleNames == null || RoleNames.Count == 0) errorList += "\t\tНе заданы роли, которым эта команда доступна. Это обязательный параметр\n";
            if (!File.Exists(ErrorReplyPath)) errorList += $"\t\tНе задан параметр ErrorReplyPath или не существует такого файла (\"{ErrorReplyPath}\"). Это обязательный параметр\n";
            
            if (!string.IsNullOrEmpty(errorList))
            {
                ComandEnabled = false;
                return (false, $"Эта команда содержит ошибки, поэтому не может быть активирована: {InvokeCommand}. Список ошибок:\n{errorList}", Logger.LogLevel.Warn);
            }
            if (PostAction != null)
            {
                Logger.Log(Logger.LogLevel.Info, $"Обнаружено вложенное действие: \"{InvokeCommand}\" => \"{PostAction.InvokeCommand}\"");
                var postActionVerificationResult = PostAction.Verify();
                Logger.Log(postActionVerificationResult.logLevel, postActionVerificationResult.errorMessage);
                if (!postActionVerificationResult.success)
                {
                    return postActionVerificationResult;
                }
            }
            //if (
            //    string.IsNullOrEmpty(Type)
            //    || !File.Exists(FilePath)
            //    || (string.IsNullOrEmpty(ReplyText) || !File.Exists(RandomReplyPath))
            //    || (RoleNames == null || RoleNames.Count == 0)
            //    || !File.Exists(ErrorReplyPath)
            //    )
            //{
            //    ComandEnabled = false;
            //    return (false, $"Эта команда содержит ошибки, поэтому не может быть активирована: {InvokeCommand}", Logger.LogLevel.Warn);
            //}

#pragma warning disable CS8602 // Разыменование вероятной пустой ссылки.
            foreach (string roleName in RoleNames)
            {
                //if (string.IsNullOrEmpty(roleName))
                //    return (false, $"Эта команда не может быть активирована, т.к. в ней указана несуществующая роль: {InvokeCommand}", Logger.LogLevel.Warn);
                if (TGBot.AvailebleRoles.FirstOrDefault(x => x.Name?.ToLower().Trim() != roleName?.ToLower().Trim()) == null)
                {
                    return (false, $"Эта команда не может быть активирована, т.к. в ней указана несуществующая роль: {roleName}", Logger.LogLevel.Warn);
                }
            }
#pragma warning restore CS8602 // Разыменование вероятной пустой ссылки.

            switch (Type.Trim().ToLower())
            {
                case "script" or "image" or "file" or "full_text" or "random_text":
                    if (!File.Exists(FilePath))
                    {
                        ComandEnabled = false;
                        return (false, $"Не удалось верифицировать действие {InvokeCommand}: файл \"{FilePath}\" не существует или недоступен", Logger.LogLevel.Error);
                    }
                    break;
                case "random_image" or "random_file" or "random_script":
                    if (!Directory.Exists(FilePath))
                    {
                        ComandEnabled = false;
                        return (false, $"Не удалось верифицировать действие {InvokeCommand}: Директория \"{FilePath}\" не существует или недоступна", Logger.LogLevel.Error);
                    }
                    break;
                default:
                    ComandEnabled = false;
                    return (false, $"Эта команда не можетбыть активирована, т.к. имеет неизвестный тип выполнения \"{Type}\": {InvokeCommand}", Logger.LogLevel.Warn);
            }
            ComandEnabled = true;
            return (true, $"Успешно добавлено действие {InvokeCommand}", Logger.LogLevel.Info);
        }
    }
}
