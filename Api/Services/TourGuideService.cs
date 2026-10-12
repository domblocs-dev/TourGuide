using GpsUtil.Location;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using TourGuide.Dtos;
using TourGuide.LibrairiesWrappers.Interfaces;
using TourGuide.Services.Interfaces;
using TourGuide.Users;
using TourGuide.Utilities;
using TripPricer;
using System.Collections.Concurrent;

namespace TourGuide.Services;

public class TourGuideService : ITourGuideService
{
    private readonly ILogger _logger;
    private readonly IGpsUtil _gpsUtil;
    private readonly IRewardsService _rewardsService;
    private readonly TripPricer.TripPricer _tripPricer;
    public Tracker Tracker { get; private set; }
    private readonly ConcurrentDictionary<string, User> _internalUserMap = new();
    private const string TripPricerApiKey = "test-server-api-key";
    private const int NombreAttractionsProches = 5;
    public const int NombreSuivisSimultanes = 100;
    private bool _testMode = true;

    public TourGuideService(ILogger<TourGuideService> logger, IGpsUtil gpsUtil, IRewardsService rewardsService, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _tripPricer = new();
        _gpsUtil = gpsUtil;
        _rewardsService = rewardsService;

        if (_testMode)
        {
            _logger.LogInformation("TestMode enabled");
            _logger.LogDebug("Initializing users");
            InitializeInternalUsers();
            _logger.LogDebug("Finished initializing users");
        }

        var trackerLogger = loggerFactory.CreateLogger<Tracker>();

        Tracker = new Tracker(this, trackerLogger);
        AddShutDownHook();
    }

    public List<UserReward> GetUserRewards(User user)
    {
        return user.UserRewards;
    }

    public VisitedLocation GetUserLocation(User user)
    {
        return user.GetLastVisitedLocationOrDefault() ?? TrackUserLocation(user);
    }

    public User GetUser(string userName)
    {
        return _internalUserMap.TryGetValue(userName, out User? user) ? user : null;
    }

    public List<User> GetAllUsers()
    {
        return _internalUserMap.Values.ToList();
    }

    public void AddUser(User user)
    {
        _internalUserMap.TryAdd(user.UserName, user);
    }

    public List<Provider> GetTripDeals(User user)
    {
        int cumulativeRewardPoints = user.UserRewards.Sum(i => i.RewardPoints);
        List<Provider> providers = _tripPricer.GetPrice(TripPricerApiKey, user.UserId,
            user.UserPreferences.NumberOfAdults, user.UserPreferences.NumberOfChildren,
            user.UserPreferences.TripDuration, cumulativeRewardPoints);
        user.TripDeals = providers;
        return providers;
    }

    public VisitedLocation TrackUserLocation(User user)
    {
        VisitedLocation visitedLocation = _gpsUtil.GetUserLocation(user.UserId);
        user.AddToVisitedLocations(visitedLocation);
        _rewardsService.CalculateRewards(user);
        return visitedLocation;
    }

    public void TrackAllUsersLocation(List<User> users)
    {
        var options = new ParallelOptions { MaxDegreeOfParallelism = NombreSuivisSimultanes };
        Parallel.ForEach(users, options, user => TrackUserLocation(user));
    }


    public List<Attraction> GetNearByAttractions(VisitedLocation visitedLocation)
    {
        return _gpsUtil.GetAttractions()
            .OrderBy(a => _rewardsService.GetDistance(a, visitedLocation.Location))
            .Take(NombreAttractionsProches)
            .ToList();
    }

    public List<NearbyAttractionDto> GetNearbyAttractionDtos(User user)
    {
        VisitedLocation visitedLocation = GetUserLocation(user);

        return GetNearByAttractions(visitedLocation)
            .AsParallel()
            .AsOrdered()
            .WithDegreeOfParallelism(NombreAttractionsProches)
            .Select(attraction => new NearbyAttractionDto
            {
                AttractionName = attraction.AttractionName,
                AttractionLatitude = attraction.Latitude,
                AttractionLongitude = attraction.Longitude,
                UserLatitude = visitedLocation.Location.Latitude,
                UserLongitude = visitedLocation.Location.Longitude,
                DistanceInMiles = _rewardsService.GetDistance(attraction, visitedLocation.Location),
                RewardPoints = _rewardsService.GetRewardPoints(attraction, user)
            })
            .ToList();
    }



    private void AddShutDownHook()
    {
        AppDomain.CurrentDomain.ProcessExit += (sender, e) => Tracker.StopTracking();
    }

    /**********************************************************************************
    * 
    * Methods Below: For Internal Testing
    * 
    **********************************************************************************/

    private void InitializeInternalUsers()
    {
        for (int i = 0; i < InternalTestHelper.GetInternalUserNumber(); i++)
        {
            var userName = $"internalUser{i}";
            var user = new User(Guid.NewGuid(), userName, "000", $"{userName}@tourGuide.com");
            GenerateUserLocationHistory(user);
            _internalUserMap.TryAdd(userName, user);
        }

        _logger.LogDebug($"Created {InternalTestHelper.GetInternalUserNumber()} internal test users.");
    }

    private void GenerateUserLocationHistory(User user)
    {
        for (int i = 0; i < 3; i++)
        {
            var visitedLocation = new VisitedLocation(user.UserId, new Locations(GenerateRandomLatitude(), GenerateRandomLongitude()), GetRandomTime());
            user.AddToVisitedLocations(visitedLocation);
        }
    }

    private static readonly Random random = new Random();

    private double GenerateRandomLongitude()
    {
        return new Random().NextDouble() * (180 - (-180)) + (-180);
    }

    private double GenerateRandomLatitude()
    {
        return new Random().NextDouble() * (90 - (-90)) + (-90);
    }

    private DateTime GetRandomTime()
    {
        return DateTime.UtcNow.AddDays(-new Random().Next(30));
    }
}
