# Pokemon-Go-Rocket-API

# Pokemon Go Client API Library in C# #

General chat: https://discord.gg/5CMa3CY

GitHub issues will be deleted if they are not API related. PR's about improved logic is welcome.

Example:

```
var clientSettings = new Settings();
var client = new Client(_clientSettings);
await _client.DoGoogleLogin();
await _client.SetServer();
var profile = await _client.GetProfile();
var settings = await _client.GetSettings();
var mapObjects = await _client.GetMapObjects();

//Logic related methods, which you could also build yourself
await EvolveAllPokemonWithEnoughCandy();
await TransferDuplicatePokemon();
await RecycleItems();
await ExecuteFarmingPokestopsAndPokemons();
```

*Features*

Note: There is a list of feature requests [here](https://github.com/FeroxRev/Pokemon-Go-Rocket-API/wiki/Feature-requests).

```
#PTC Login / Google
#Get Map Objects and Inventory
#Search for gyms/pokestops/spawns
#Farm pokestops
#Farm all pokemons in neighbourhood and throw berries/use best pokeball
#Transfers duplicate pokemons
#Evolve all pokemons
#Throws away unneeded items

```

*Todo*

```
#Gotta catch them all
```

