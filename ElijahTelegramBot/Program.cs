using ElijahTelegramBot.Core;
using ElijahTelegramBot.TGBot;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using static System.Net.Mime.MediaTypeNames;

namespace ElijahTelegramBot
{
    internal class Program
    {
        public static ManualResetEvent resetEvent { get; protected set; } = new ManualResetEvent(false);
        public static void OnPanic()
        {
            resetEvent.Set();
        }
        private static void ShowStructure()
        {
            List<KeyValuePair<string, string>> serialized = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("Конфигурация", JsonConvert.SerializeObject(new Configuration(), Formatting.Indented)),
                new KeyValuePair<string, string>("Роль", JsonConvert.SerializeObject(new BotRole(), Formatting.Indented)),
                new KeyValuePair<string, string>("Роль (массив)", JsonConvert.SerializeObject(new List<BotRole>()
                {
                    new BotRole(),
                    new BotRole(),
                    new BotRole(),
                    new BotRole()
                }, Formatting.Indented)),
                new KeyValuePair<string, string>("Действия", JsonConvert.SerializeObject(new BotAction(), Formatting.Indented)),
                new KeyValuePair<string, string>("Действия (массив)", JsonConvert.SerializeObject(
                    new List<BotAction>()
                    {
                        new BotAction(),
                        new BotAction(),
                        new BotAction(),
                        new BotAction()
                    },
                Formatting.Indented))
            };
            foreach (var item in serialized)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n------------------------------\n");
                Console.ResetColor();
                Console.WriteLine($"\t{item.Key}\n{item.Value}");
            }
        }
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine($"Использование приложения:\n\t./{AppDomain.CurrentDomain.FriendlyName} [путь_к_конфигурационному_файлу]\n" +
                    $"Информация о структуре коонфигурационных файлов:");
                ShowStructure();
                Environment.Exit(1);
            }
            Console.Clear();
            Console.ResetColor();
            Storage.tGBot = new TGBot.TGBot(args[0]);
            resetEvent.WaitOne();
        }
    }
}