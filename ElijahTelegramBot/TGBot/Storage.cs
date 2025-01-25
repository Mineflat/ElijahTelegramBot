using ElijahTelegramBot.Core;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElijahTelegramBot.TGBot
{
    internal class Storage
    {
        public static TGBot? tGBot { get; set; }
        public static List<ScriptInvocationUnit> ScriptInvokationList { get; protected set; } = new List<ScriptInvocationUnit>();
        #region Элементы пользовательского интерфейса
        public static Table ScriptQueueTable = new Table(); // Обновляется UpdateCLITable
        #endregion
        #region Эта часть нужна чтобы контролировать очистку очереди скриптов после N минут времени
        public static System.Threading.Timer? ResetScriptStoryTimer { get; protected set; }
        public static (bool success, string errorMessage, Logger.LogLevel logLevel) SetupResetTimer()
        {
            ScriptInvokationList = new List<ScriptInvocationUnit>();
            ResetScriptStoryTimer?.Dispose();
#pragma warning disable CS8622 // Допустимость значений NULL для ссылочных типов в типе параметра не соответствует целевому объекту делегирования (возможно, из-за атрибутов допустимости значений NULL).
            ResetScriptStoryTimer = new System.Threading.Timer(ResetScriptStoryEvent, null, 0, TGBot.WorkingConfiguration.ScriptQueueCleaningDelay * 60 * 60 * 1000); // Переводим минуты в миллисекунды
#pragma warning restore CS8622 // Допустимость значений NULL для ссылочных типов в типе параметра не соответствует целевому объекту делегирования (возможно, из-за атрибутов допустимости значений NULL).
            return (true, $"Таймер очистки очереди скриптов успешно установлен на {TGBot.WorkingConfiguration.ScriptQueueCleaningDelay} минут", Logger.LogLevel.Info);
        }
        private static void ResetScriptStoryEvent(object state)
        {
            if (ScriptInvokationList.Count == 0) return;
            //List <ScriptInvocationUnit> updatedScriptQueue = Storage.ScriptInvokationList
            //    .Where(x => x.IsRunning == true || (x.CleanTime ?? DateTime.Now) >= DateTime.Now)
            //    .ToList();
            //List<ScriptInvocationUnit> deletedItems = Storage.ScriptInvokationList.Except(updatedScriptQueue).ToList();
            List<ScriptInvocationUnit> deletedItems = ScriptInvokationList
                .Where(x => x.EndTime != null && (x.CleanTime ?? DateTime.Now) < DateTime.Now)
                .ToList();
            if (deletedItems.Count > 0)
            {
                foreach (ScriptInvocationUnit item in deletedItems)
                {
                    Logger.Log(Logger.LogLevel.Debug, $"Успешно очищен объект команды:\n" +
                        $"\n\t-\tID задачи: {item.ScriptID}\t-\tПуть к скрипту: {item.ComandLine}" +
                        $"\n\t-\tID пользователя, который запустил задачу: {item.InvokeUID}" +
                        $"\n\t-\tЗадача запущена в: {item.StartTime}\t-\tЗадача закончена в: {item.EndTime}" +
                        $"\n\t-\tНеуязвимость к очистке из пула сохранялась до: {item.CleanTime}");
                }
                //Storage.ScriptInvokationList = updatedScriptQueue;
                ScriptInvokationList = ScriptInvokationList.Except(deletedItems).ToList();
                Logger.Log(Logger.LogLevel.Debug, $"Доступное количество вызовов команды Script в очереди после ее очистки: " +
                    $"{(TGBot.WorkingConfiguration.ParallelScriptQueueLimit == null ?
                    "(БЕСКОНЕЧНОСТЬ)" : TGBot.WorkingConfiguration.ParallelScriptQueueLimit - ScriptInvokationList.Count)}");
                UpdateScriptQueueTable();
            }
        }
        private static void UpdateScriptQueueTable()
        {
            ScriptQueueTable = new Table();
            ScriptQueueTable.Title("");
            ScriptQueueTable.AddColumns([
                "[thistle1]ID задачи[/]",
                "[thistle1]Путь к файлу[/]",
                "[thistle1]ID пользователя в ТГ[/]",
                "[thistle1]Дата запуска[/]",
                "[thistle1]Дата остановки[/]",
                "[thistle1]Дата очистки[/]"
            ]);
            foreach (ScriptInvocationUnit item in ScriptInvokationList)
            {
                ScriptQueueTable.AddRow([
                    $"{item.ScriptID}",
                    $"{item.ComandLine}",
                    $"{item.InvokeUID}",
                    $"{item.StartTime}",
                    $"{(item.EndTime == null ? "[greenyellow]Запущена[/]": item.EndTime)}",
                    $"{(item.CleanTime == null ? "[lightsteelblue3]Определится после окончания[/]": item.CleanTime)}",
                ]);
            }
        }
        #endregion
        #region Эта часть контролирует создание записей в списке скриптов на очереди
        public static (bool success, string errorMessage, Logger.LogLevel logLevel) AddScriptInQueue(long replyChatID, long invokeUID, string comandLine)
        {
            if (TGBot.WorkingConfiguration.ParallelScriptQueueLimit == null)
            {
                return (false, 
                    "Невозможно выполнить метод Storage.AddScriptInQueue - " +
                    "не задан максимальный размер очереди " +
                    "(TGBot.WorkingConfiguration.ParallelScriptQueueLimit)", Logger.LogLevel.Critical);
            }
            if ((int)TGBot.WorkingConfiguration.ParallelScriptQueueLimit >= ScriptInvokationList.Count)
            {
                return (false, $"Невозможно поставить скрипт в очередь - достигнут лимит " +
                    $"({ScriptInvokationList.Count} из {TGBot.WorkingConfiguration.ParallelScriptQueueLimit} скриптов)", 
                    Logger.LogLevel.Warn);
            }
            ScriptInvokationList.Add(new ScriptInvocationUnit(ScriptInvokationList.Count, replyChatID, invokeUID, comandLine));
            return (true, "Добавлена команда в очередь:\t", Logger.LogLevel.Critical);
        }
        #endregion
    }
}
