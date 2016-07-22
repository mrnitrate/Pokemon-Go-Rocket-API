using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokemonGo.RocketAPI.Logic.Utils
{
    class Statistics
    {
        private int _totalExperience;
        private int _totalPokemons;
        private int _totalItemsRemoved;
        private int _totalPokemonsTransfered;
        private int _totalStardust;
        public static string _getLevelInfos;
        public static int Currentlevel = -1;

        private DateTime _initSessionDateTime = DateTime.Now;

        private static int GetXpDiff(int Level)
        {
            switch (Level)
            {
                case 1:
                    return 0;
                case 2:
                    return 1000;
                case 3:
                    return 2000;
                case 4:
                    return 3000;
                case 5:
                    return 4000;
                case 6:
                    return 5000;
                case 7:
                    return 6000;
                case 8:
                    return 7000;
                case 9:
                    return 8000;
                case 10:
                    return 9000;
                case 11:
                    return 10000;
                case 12:
                    return 10000;
                case 13:
                    return 10000;
                case 14:
                    return 10000;
                case 15:
                    return 15000;
                case 16:
                    return 20000;
                case 17:
                    return 20000;
                case 18:
                    return 20000;
                case 19:
                    return 25000;
                case 20:
                    return 25000;
                case 21:
                    return 50000;
                case 22:
                    return 75000;
                case 23:
                    return 100000;
                case 24:
                    return 125000;
                case 25:
                    return 150000;
                case 26:
                    return 190000;
                case 27:
                    return 200000;
                case 28:
                    return 250000;
                case 29:
                    return 300000;
                case 30:
                    return 350000;
                case 31:
                    return 500000;
                case 32:
                    return 500000;
                case 33:
                    return 750000;
                case 34:
                    return 1000000;
                case 35:
                    return 1250000;
                case 36:
                    return 1500000;
                case 37:
                    return 2000000;
                case 38:
                    return 2500000;
                case 39:
                    return 1000000;
                case 40:
                    return 1000000;
            }
            return 0;
        }

        private double _getSessionRuntime()
        {
            return ((DateTime.Now - _initSessionDateTime).TotalSeconds) / 3600;
        }

        public void addExperience(int xp)
        {
            _totalExperience += xp;
        }

        public static async Task<string> _getcurrentLevelInfos(Client _client)
        {
            var inventory = await _client.GetInventory();
            var stats = inventory.InventoryDelta.InventoryItems.Select(i => i.InventoryItemData?.PlayerStats).ToArray();
            var output = string.Empty;
            foreach (var v in stats)
                if (v != null)
                {
                    int diff = GetXpDiff(v.Level);
                    long currentLvlXp = v.Experience - v.PrevLevelXp - diff;
                    long currentXpNeeded = v.NextLevelXp - v.PrevLevelXp - diff;
                    Currentlevel = v.Level;
                    output = $"{v.Level} ({currentLvlXp}/{currentXpNeeded})";
                }
            return output;
        }

        public void increasePokemons()
        {
            _totalPokemons += 1;
        }

        public void getStardust(int stardust)
        {
            _totalStardust = stardust;
        }

        public void addItemsRemoved(int count)
        {
            _totalItemsRemoved += count;
        }

        public void increasePokemonsTransfered()
        {
            _totalPokemonsTransfered += 1;
        }

        public async void updateConsoleTitle(Client _client)
        {
            _getLevelInfos = await _getcurrentLevelInfos(_client);
            Console.Title = ToString();
        }

        public override string ToString()
        {
            return string.Format("{0} - LvL: {1:0}    EXP Exp/H: {2:0.0} EXP   P/H: {3:0.0} Pokemon(s)   Stardust: {4:0}   Pokemon Transfered: {5:0}   Items Removed: {6:0}", "Statistics", _getLevelInfos, _totalExperience / _getSessionRuntime(), _totalPokemons / _getSessionRuntime(), _totalStardust, _totalPokemonsTransfered, _totalItemsRemoved);
        }
    }
}