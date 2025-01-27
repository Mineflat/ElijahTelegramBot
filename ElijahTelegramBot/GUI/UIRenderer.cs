using ElijahTelegramBot.Core;
using ElijahTelegramBot.TGBot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElijahTelegramBot.GUI
{
    internal class UIRenderer
    {
        private static System.Threading.Timer? _RenderRestartTimer;

        public static (bool success, string errorMessage, Logger.LogLevel logLevel) StartRenderer()
        {
            try
            {
                _RenderRestartTimer = new Timer(Render, null, 0, (int)TGBot.TGBot.WorkingConfiguration.RenderDelaySec * 1000);
                return (true, "Успешно запущен рендер", Logger.LogLevel.Success);

            }
            catch (Exception e)
            {
                return (false, $"Не удалось запустить графический интерфейс: {e.Message}", Logger.LogLevel.Critical);
            }
        }
        public static void Render()
        {

        }
    }
}
