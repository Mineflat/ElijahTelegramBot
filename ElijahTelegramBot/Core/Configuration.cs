using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElijahTelegramBot.Core
{
    public class Configuration
    {
        public string RolesPath { get; set; } = string.Empty;
        public string ActionsPath { get; set; } = string.Empty;
        public string BotToken { get; set; } = string.Empty;
        public bool DebugMode { get; set; } = false;
        public long AdminID { get; set; } = -1;
        public string? LogPath { get; set; } = string.Empty;
        public int? ParallelScriptQueueLimit { get; set; } = null;
        // Частота очистки списка запущенных задач в минутах
        public uint ScriptQueueCleaningDelay { get; set; } = 10;
    }
}
