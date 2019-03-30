﻿using System;
using GTA;
using CherryMP.Javascript;
using CherryMP.Networking;
using CherryMPShared;
using WeaponHash = GTA.WeaponHash;
using VehicleHash = GTA.VehicleHash;

namespace CherryMP.Util
{
    public class StickyBombTracker : Script
    {
        public StickyBombTracker()
        {
            base.Tick += OnTick;
        }

        private bool _hasPlacedStickies;

        private void OnTick(object sender, EventArgs e)
        {
            var player = Game.Player.Character;

            if (player.IsShooting && player.Weapons.Current.Hash == (WeaponHash.StickyBomb))
            {
                _hasPlacedStickies = true;
            }

            if (Game.Player.IsDead)
            {
                _hasPlacedStickies = false;
            }

            if (Game.IsControlJustPressed(0, Control.Detonate) && _hasPlacedStickies)
            {
                SyncEventWatcher.SendSyncEvent(SyncEventType.StickyBombDetonation, Main.NetEntityHandler.EntityToNet(Game.Player.Character.Handle));
                JavascriptHook.InvokeCustomEvent(api => api?.invokeonPlayerDetonateStickies());
                _hasPlacedStickies = false;
            }
        }
    }
}