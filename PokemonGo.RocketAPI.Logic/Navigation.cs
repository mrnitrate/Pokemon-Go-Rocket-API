using PokemonGo.RocketAPI.GeneratedCode;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using PokemonGo.RocketAPI.Logic.Utils;

namespace PokemonGo.RocketAPI.Logic
{
    public class Navigation
    {

        private static readonly double  speedDownTo = 10 /3.6;
        private readonly Client _client;

        public Navigation(Client client)
        {
            _client = client;
        }

        public async Task<PlayerUpdateResponse> HumanLikeWalking(Location targetLocation, double walkingSpeedInKilometersPerHour)
        {
            double speedInMetersPerSecond = walkingSpeedInKilometersPerHour / 3.6;

            Location sourceLocation = new Location(_client.CurrentLat, _client.CurrentLng);
            var distanceToTarget = LocationUtils.CalculateDistanceInMeters(sourceLocation, targetLocation);
            Logger.Write($"Distance to target location: {distanceToTarget:0.##} meters. Will take {distanceToTarget/speedInMetersPerSecond:0.##} seconds!", LogLevel.Info);

            double nextWaypointBearing = LocationUtils.DegreeBearing(sourceLocation, targetLocation);
            double nextWaypointDistance = speedInMetersPerSecond;
            Location waypoint = LocationUtils.CreateWaypoint(sourceLocation, nextWaypointDistance, nextWaypointBearing);

            //Initial walking
            DateTime requestSendDateTime = DateTime.Now;
            var result = await _client.UpdatePlayerLocation(waypoint.Latitude, waypoint.Longitude, _client.Settings.DefaultAltitude);

            do
            {
                double millisecondsUntilGetUpdatePlayerLocationResponse = (DateTime.Now - requestSendDateTime).TotalMilliseconds;

                sourceLocation = new Location(_client.CurrentLat, _client.CurrentLng);
                var currentDistanceToTarget = LocationUtils.CalculateDistanceInMeters(sourceLocation, targetLocation);

                if (currentDistanceToTarget < 40)
                {
                    if (speedInMetersPerSecond > speedDownTo)
                    {
                        Logger.Write("We are within 40 meters of the target. Speeding down to 10 km/h to not pass the target.", LogLevel.Info);
                        speedInMetersPerSecond = speedDownTo;
                    }
                    else
                    {
                        Logger.Write("We are within 40 meters of the target, attempting to interact.", LogLevel.Info);
                    }
                }
                else
                {
                    Logger.Write($"Distance to target location: {LocationUtils.CalculateDistanceInMeters(sourceLocation, targetLocation):0.##} meters.", LogLevel.Debug);
                }

                nextWaypointDistance = Math.Min(currentDistanceToTarget, millisecondsUntilGetUpdatePlayerLocationResponse / 1000 * speedInMetersPerSecond);
                nextWaypointBearing = LocationUtils.DegreeBearing(sourceLocation, targetLocation);
                waypoint = LocationUtils.CreateWaypoint(sourceLocation, nextWaypointDistance, nextWaypointBearing);

                requestSendDateTime = DateTime.Now;
                result = await _client.UpdatePlayerLocation(waypoint.Latitude, waypoint.Longitude, _client.Settings.DefaultAltitude);
                await Task.Delay(Math.Min((int)(distanceToTarget / speedInMetersPerSecond * 1000), 3000));
            } while (LocationUtils.CalculateDistanceInMeters(sourceLocation, targetLocation) >= 30);

            return result;
        }

        public class Location
        {
            public double Latitude { get; set; }
            public double Longitude { get; set; }

            public Location(double latitude, double longitude)
            {
                Latitude = latitude;
                Longitude = longitude;
            }
        }

        public static double DistanceBetween2Coordinates(double Lat1, double Lng1, double Lat2, double Lng2)
        {
            double r_earth = 6378137;
            double d_lat = (Lat2 - Lat1) * Math.PI / 180;
            double d_lon = (Lng2 - Lng1) * Math.PI / 180;
            double alpha = Math.Sin(d_lat / 2) * Math.Sin(d_lat / 2)
                + Math.Cos(Lat1 * Math.PI / 180) * Math.Cos(Lat2 * Math.PI / 180)
                * Math.Sin(d_lon / 2) * Math.Sin(d_lon / 2);
            double d = 2 * r_earth * Math.Atan2(Math.Sqrt(alpha), Math.Sqrt(1 - alpha));
            return d;
        }

        private static double getWeight(List<FortData> nodes)
        {
            double weight = 0;
            FortData previousNode = nodes.First();
            foreach (FortData node in nodes)
            {
                weight += DistanceBetween2Coordinates(previousNode.Latitude, previousNode.Longitude, node.Latitude, node.Longitude);
                previousNode = node;
            }
            weight += DistanceBetween2Coordinates(nodes.First().Latitude, nodes.First().Longitude, nodes.Last().Latitude, nodes.Last().Longitude);
            return weight;
        }

        private static void Swap(List<FortData> list, int indexA, int indexB)
        {
            FortData tmp = list[indexA];
            list[indexA] = list[indexB];
            list[indexB] = tmp;
        }

        public static List<FortData> generatePath(List<FortData> nodes)
        {
            bool improvement;
            double bestWeight = getWeight(nodes);
            Logger.Write($"Reducing path length from {bestWeight}");
            List<FortData> bestSolutionOverall = nodes;


            do
            {
                improvement = false;
                List<FortData> bestSolutionThisRun = new List<FortData>(bestSolutionOverall);
                for (int i = 0; i < nodes.Count; i++)
                    for (int ii = 0; ii < nodes.Count; ii++)
                    {
                        List<FortData> nodesCopy = new List<FortData>(bestSolutionThisRun);
                        Swap(nodesCopy, i, ii);
                        double newWeight = getWeight(nodesCopy);
                        if (newWeight < bestWeight)
                        {
                            bestWeight = newWeight;
                            bestSolutionThisRun = nodesCopy;
                            improvement = true;
                        }
                    }
                if (improvement)
                {
                    Logger.Write($"New reduced length: {bestWeight}");
                    bestSolutionOverall = bestSolutionThisRun;
                }
            } while (improvement);

            return bestSolutionOverall;
        }
    }
}
