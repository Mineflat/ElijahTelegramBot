using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElijahTelegramBot.TGBot
{
    public class BotRole
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<long> UserIds { get; set; } = new List<long>();
    }
}
