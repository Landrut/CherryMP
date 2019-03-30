﻿using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using WeaponHash = CherryMPShared.WeaponHash;

namespace CherryMP.Networking
{
    internal class WeaponManager
    {
        private List<WeaponHash> _playerInventory = new List<WeaponHash>
        {
            WeaponHash.Unarmed,
        };

        internal void Clear()
        {
            _playerInventory.Clear();
            _playerInventory.Add(WeaponHash.Unarmed);
        }

        internal void Update()
        {
            var weapons = Enum.GetValues(typeof (WeaponHash)).Cast<WeaponHash>();

            foreach (var hash in weapons)
            {
                if (!_playerInventory.Contains(hash))
                {
                    Game.Player.Character.Weapons.Remove((GTA.WeaponHash)(int)hash);
                }
            }
        }

        internal void Allow(WeaponHash hash)
        {
            if (!_playerInventory.Contains(hash)) _playerInventory.Add(hash);
        }

        internal void Deny(WeaponHash hash)
        {
            _playerInventory.Remove(hash);
        }
    }
}