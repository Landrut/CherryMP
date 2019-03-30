using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CherryMP.GUI;
using CherryMP.Javascript;
using CherryMP.Misc;
using CherryMP.Networking;
using CherryMP.Util;
using CherryMPShared;

using DiscordRPC;

namespace CherryDiscord
{
    public static class CherryDiscord
    {
        public static DiscordRpcClient InMenuDiscordClient;
        public static DiscordRpcClient OnServerDiscordClient;

        #region InMenu
        public static void InMenuDiscordInitialize(string version)
        {
            InMenuDiscordClient = new DiscordRpcClient("561209218766471177");

            var timer = new System.Timers.Timer(1507665886);
            timer.Elapsed += (sender, args) => { InMenuDiscordClient.Invoke(); };
            timer.Start();

            // DiscordClient.UpdateStartTime();

            InMenuDiscordClient.Initialize();

            InMenuDiscordClient.SetPresence(new RichPresence()
            {
                State = "In the menu",
                Details = "Dev Build | Ver: " + version,
                Assets = new Assets()
                {
                    LargeImageKey = "avatarka_cherry_rp",
                    LargeImageText = "Cherry Multiplayer"
                }
            });
        }

        public static void InMenuDiscordUpdatePresence()
        {
            InMenuDiscordClient.Invoke();
            InMenuDiscordClient.UpdateStartTime();
        }

        public static void InMenuDiscordDeinitializePresence()
        {
            InMenuDiscordClient.Dispose();
        }

        #endregion

        public static void OnServerDiscordInitialize(string PlayerName, string ServerName)
        {
            OnServerDiscordClient = new DiscordRpcClient("561209218766471177");

            var timer = new System.Timers.Timer(1507665886);
            timer.Elapsed += (sender, args) => { InMenuDiscordClient.Invoke(); };
            timer.Start();

            // DiscordClient.UpdateStartTime();

            OnServerDiscordClient.Initialize();

            OnServerDiscordClient.SetPresence(new RichPresence()
            {
                State = "Name: " + PlayerName,
                Details = "Server: " + ServerName,
                Assets = new Assets()
                {
                    LargeImageKey = "avatarka_cherry_rp",
                    LargeImageText = "Cherry Multiplayer"
                }
            });
        }

        public static void OnServerDiscordUpdatePresence()
        {
            OnServerDiscordClient.Invoke();
            OnServerDiscordClient.UpdateStartTime();
        }

        public static void OnServerDiscordDeinitializePresence()
        {
            OnServerDiscordClient.Dispose();
        }

    }
}
