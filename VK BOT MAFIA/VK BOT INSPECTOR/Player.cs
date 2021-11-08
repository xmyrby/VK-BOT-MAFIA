using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VK_BOT_INSPECTOR
{
    class Player
    {
        public long? Id { get; set; }

        public bool InLobby { get; set; }

        public int LobbyId { get; set; }
        public string Role { get; set; }

        public int State { get; set; }
    }
}
