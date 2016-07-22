using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using AllEnum;
using PokemonGo.RocketAPI.Enums;
using PokemonGo.RocketAPI.Exceptions;
using PokemonGo.RocketAPI.Extensions;
using PokemonGo.RocketAPI.GeneratedCode;
using PokemonGo.RocketAPI.Logic.Utils;
using PokemonGo.RocketAPI.Helpers;

namespace PokemonGo.RocketAPI.Logic
{
    public class Logic
    {
        private readonly Client _client;
        private readonly ISettings _clientSettings;
        private readonly Inventory _inventory;
        private readonly Statistics _stats;
        private readonly Navigation _navigation;

        public Logic(ISettings clientSettings)
        {
            _clientSettings = clientSettings;
            _client = new Client(_clientSettings);
            _inventory = new Inventory(_client);
            _navigation = new Navigation(_client);
            _stats = new Statistics();
        }

        public async Task Execute()
        {
            Logger.Write($"Starting Execute on login server: {_clientSettings.AuthType}", LogLevel.Info);

            while (true)
            {
                try
                {
                    if (_clientSettings.AuthType == AuthType.Ptc)
                        await _client.DoPtcLogin(_clientSettings.PtcUsername, _clientSettings.PtcPassword);
                    else if (_clientSettings.AuthType == AuthType.Google)
                        await _client.DoGoogleLogin();

                    await _client.SetServer();

                    await PostLoginExecute();
                }
                catch (AccessTokenExpiredException)
                {
                    Logger.Write($"Access token expired", LogLevel.Info);
                }
                catch (Exception ex)
                {
                    Logger.Write($"Exception: {ex}", LogLevel.Error);
                }
                await Task.Delay(10000);
            }
        }

        public async Task PostLoginExecute()
        {
            while (true)
            {
                try
                {
                    await EvolveAllPokemonWithEnoughCandy(_clientSettings.PokemonsToEvolve);
                    await TransferDuplicatePokemon();
                    await RecycleItems();
                    await ExecuteFarmingPokestopsAndPokemons();

                    /*
            * Example calls below
            *
            var profile = await _client.GetProfile();
            var settings = await _client.GetSettings();
            var mapObjects = await _client.GetMapObjects();
            var inventory = await _client.GetInventory();
            var pokemons = inventory.InventoryDelta.InventoryItems.Select(i => i.InventoryItemData?.Pokemon).Where(p => p != null && p?.PokemonId > 0);
            */
                }
                catch (AccessTokenExpiredException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw;
                }

                await Task.Delay(2000);
            }
        }

        public static float CalculatePokemonPerfection(PokemonData poke)
        {
            return ((float)(poke.IndividualAttack*2 + poke.IndividualDefense + poke.IndividualStamina) / (4.0f * 15.0f)) * 100.0f;
        }

        public async Task RepeatAction(int repeat, Func<Task> action)
        {
            for (int i = 0; i < repeat; i++)
                await action();
        }

        
        private async Task ExecuteFarmingPokestopsAndPokemons()
        {
            var mapObjects = await _client.GetMapObjects();

            var pokeStops = mapObjects.MapCells.SelectMany(i => i.Forts).Where(i => i.Type == FortType.Checkpoint && i.CooldownCompleteTimestampMs < DateTime.UtcNow.ToUnixTime()).OrderBy(i => LocationUtils.CalculateDistanceInMeters(new Navigation.Location(_client.CurrentLat, _client.CurrentLng), new Navigation.Location(i.Latitude, i.Longitude)));
            var pokeStopsList = Navigation.generatePath(pokeStops.ToList());
            foreach (var pokeStop in pokeStopsList)
            {
                var distance = Navigation.DistanceBetween2Coordinates(_client.CurrentLat, _client.CurrentLng, pokeStop.Latitude, pokeStop.Longitude);

                var update = await _navigation.HumanLikeWalking(new Navigation.Location(pokeStop.Latitude, pokeStop.Longitude), _clientSettings.WalkingSpeedInKilometerPerHour, ExecuteCatchAllNearbyPokemons);

                var fortInfo = await _client.GetFort(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);
                var fortSearch = await _client.SearchFort(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);

                Logger.Write($"{fortInfo.Name} in ({Math.Round(distance)}m)", LogLevel.Pokestop);
                if (fortSearch.ExperienceAwarded > 0)
                    Logger.Write(
                        $"XP: {fortSearch.ExperienceAwarded}, Gems: {fortSearch.GemsAwarded}, Eggs: {fortSearch.PokemonDataEgg} Items: {StringUtils.GetSummedFriendlyNameOfItemAwardList(fortSearch.ItemsAwarded)}",
                        LogLevel.Pokestop);

                _stats.addExperience(fortSearch.ExperienceAwarded);
                _stats.updateConsoleTitle(_client);

                await RandomHelper.RandomDelay(700, 1000);
                await RecycleItems();
                await ExecuteCatchAllNearbyPokemons();
                await TransferDuplicatePokemon();
            }
        }

        private async Task ExecuteCatchAllNearbyPokemons()
        {
            var mapObjects = await _client.GetMapObjects();

            var pokemons = mapObjects.MapCells.SelectMany(i => i.CatchablePokemons).OrderBy(i => LocationUtils.CalculateDistanceInMeters(new Navigation.Location(_client.CurrentLat, _client.CurrentLng), new Navigation.Location(i.Latitude, i.Longitude)));

            foreach (var pokemon in pokemons)
            {
                var distance = Navigation.DistanceBetween2Coordinates(_client.CurrentLat, _client.CurrentLng, pokemon.Latitude, pokemon.Longitude);
                if (distance > 100)
                    await Task.Delay((int)distance * 100);
                else
                    await Task.Delay((int)distance * 6);

                var encounter = await _client.EncounterPokemon(pokemon.EncounterId, pokemon.SpawnpointId);

                if (encounter.Status == EncounterResponse.Types.Status.EncounterSuccess)
                await CatchEncounter(encounter, pokemon);
                else
                    Logger.Write($"Encounter problem: {encounter?.Status}");
            }
            await RandomHelper.RandomDelay(7000,12000);
        }

        private async Task CatchEncounter(EncounterResponse encounter, MapPokemon pokemon)
        {

            CatchPokemonResponse caughtPokemonResponse;
            do
            {
                var probability = encounter?.CaptureProbability?.CaptureProbability_?.FirstOrDefault();
                if (probability.HasValue && probability.Value < 0.35)
                {
                    //Throw berry is we can
                    await UseBerry(pokemon.EncounterId, pokemon.SpawnpointId);
                }

                var pokeball = await GetBestBall(encounter?.WildPokemon);
                var distance = Navigation.DistanceBetween2Coordinates(_client.CurrentLat, _client.CurrentLng, pokemon.Latitude, pokemon.Longitude);
                caughtPokemonResponse = await _client.CatchPokemon(pokemon.EncounterId, pokemon.SpawnpointId, pokemon.Latitude, pokemon.Longitude, pokeball);

                Logger.Write(
                   caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchSuccess
                       ? $"{pokemon.PokemonId} ({encounter?.WildPokemon?.PokemonData?.Cp} CP) ({Math.Round(CalculatePokemonPerfection(encounter?.WildPokemon?.PokemonData)).ToString("0.00")}% perfection) | Chance: {encounter?.CaptureProbability.CaptureProbability_.First()} | {Math.Round(distance)}m distance | with {pokeball} "
                       : $"{pokemon.PokemonId} ({encounter?.WildPokemon?.PokemonData?.Cp} CP) Chance: {Math.Round(Convert.ToDouble(encounter?.CaptureProbability?.CaptureProbability_.First()))} | {Math.Round(distance)}m distance {caughtPokemonResponse.Status} | with {pokeball}",
                   LogLevel.Caught);
                if (caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchSuccess)
                {
                    foreach (int xp in caughtPokemonResponse.Scores.Xp)
                        _stats.addExperience(xp);
                    _stats.increasePokemons();
                    var profile = await _client.GetProfile();
                    _stats.getStardust(profile.Profile.Currency.ToArray()[1].Amount);
                }

                await RandomHelper.RandomDelay(1700, 2200);
            }
            while (caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchMissed || caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchEscape) ;
        }
        
        private async Task EvolveAllPokemonWithEnoughCandy(IEnumerable<PokemonId> filter = null)
        {
            var pokemonToEvolve = await _inventory.GetPokemonToEvolve(filter);
            foreach (var pokemon in pokemonToEvolve)
            {
                var evolvePokemonOutProto = await _client.EvolvePokemon((ulong)pokemon.Id);

                if (evolvePokemonOutProto.Result == EvolvePokemonOut.Types.EvolvePokemonStatus.PokemonEvolvedSuccess)
                {
                    _stats.addExperience(evolvePokemonOutProto.ExpAwarded);
                    _stats.updateConsoleTitle(_client);
                    Logger.Write($"{pokemon.PokemonId} successfully for {evolvePokemonOutProto.ExpAwarded}xp",
                        LogLevel.Evolve);
                }
                else
                    Logger.Write(
                        $"Failed {pokemon.PokemonId}. EvolvePokemonOutProto.Result was {evolvePokemonOutProto.Result}, stopping evolving {pokemon.PokemonId}",
                        LogLevel.Evolve);
                await RandomHelper.RandomDelay(2500, 3500);
            }
        }

        private async Task TransferDuplicatePokemon(bool keepPokemonsThatCanEvolve = false)
        {
            var duplicatePokemons = await _inventory.GetDuplicatePokemonToTransfer(keepPokemonsThatCanEvolve, _clientSettings.PokemonsNotToTransfer);

            foreach (var duplicatePokemon in duplicatePokemons)
            {
                if (CalculatePokemonPerfection(duplicatePokemon) >= _clientSettings.KeepMinIVPercentage || duplicatePokemon.Cp > _clientSettings.KeepMinCP)
                    continue;
                
                var transfer = await _client.TransferPokemon(duplicatePokemon.Id);
                var bestPokemonOfType = await _inventory.GetHighestCPofType(duplicatePokemon);
                Logger.Write(
                   $"{duplicatePokemon.PokemonId} with {duplicatePokemon.Cp} CP (Best: {bestPokemonOfType})",
                   LogLevel.Transfer);
                _stats.increasePokemonsTransfered();
                _stats.updateConsoleTitle(_client);

                await RandomHelper.RandomDelay(350, 750);
            }
        }

        private async Task RecycleItems()
        {
            var items = await _inventory.GetItemsToRecycle(_clientSettings);

            foreach (var item in items)
            {
                var transfer = await _client.RecycleItem((AllEnum.ItemId)item.Item_, item.Count);
                Logger.Write($"{item.Count}x {(ItemId)item.Item_}", LogLevel.Recycling);
                _stats.addItemsRemoved(item.Count);
                _stats.updateConsoleTitle(_client);

                await RandomHelper.RandomDelay(400, 800);
            }
        }

        private async Task<MiscEnums.Item> GetBestBall(WildPokemon pokemon)
        {
            var pokemonCp = pokemon?.PokemonData?.Cp;

            var pokeBallsCount = await _inventory.GetItemAmountByType(MiscEnums.Item.ITEM_POKE_BALL);
            var greatBallsCount = await _inventory.GetItemAmountByType(MiscEnums.Item.ITEM_GREAT_BALL);
            var ultraBallsCount = await _inventory.GetItemAmountByType(MiscEnums.Item.ITEM_ULTRA_BALL);
            var masterBallsCount = await _inventory.GetItemAmountByType(MiscEnums.Item.ITEM_MASTER_BALL);

            if (masterBallsCount > 0 && pokemonCp >= 1000)
                return MiscEnums.Item.ITEM_MASTER_BALL;
            else if (ultraBallsCount > 0 && pokemonCp >= 1000)
                return MiscEnums.Item.ITEM_ULTRA_BALL;
            else if (greatBallsCount > 0 && pokemonCp >= 1000)
                return MiscEnums.Item.ITEM_GREAT_BALL;

            if (ultraBallsCount > 0 && pokemonCp >= 600)
                return MiscEnums.Item.ITEM_ULTRA_BALL;
            else if (greatBallsCount > 0 && pokemonCp >= 600)
                return MiscEnums.Item.ITEM_GREAT_BALL;

            if (greatBallsCount > 0 && pokemonCp >= 350)
                return MiscEnums.Item.ITEM_GREAT_BALL;

            if (pokeBallsCount > 0)
                return MiscEnums.Item.ITEM_POKE_BALL;
            if (greatBallsCount > 0)
                return MiscEnums.Item.ITEM_GREAT_BALL;
            if (ultraBallsCount > 0)
                return MiscEnums.Item.ITEM_ULTRA_BALL;
            if (masterBallsCount > 0)
                return MiscEnums.Item.ITEM_MASTER_BALL;

            return MiscEnums.Item.ITEM_POKE_BALL;
        }

        public async Task UseBerry(ulong encounterId, string spawnPointId)
        {
            var inventoryBalls = await _inventory.GetItems();
            var berries = inventoryBalls.Where(p => (ItemId) p.Item_ == ItemId.ItemRazzBerry);
            var berry = berries.FirstOrDefault();

            if (berry == null)
                return;
            
            var useRaspberry = await _client.UseCaptureItem(encounterId, AllEnum.ItemId.ItemRazzBerry, spawnPointId);
            Logger.Write($"Used, remaining: {berry.Count}", LogLevel.Berry);
            await RandomHelper.RandomDelay(1000, 2500);
        }
    }
}
