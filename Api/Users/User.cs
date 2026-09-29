using GpsUtil.Location;
using TripPricer;

namespace TourGuide.Users;

public class User
{
    private readonly List<VisitedLocation> _visitedLocations = new();
    private readonly List<UserReward> _userRewards = new();
    private readonly object _verrou = new();

    public Guid UserId { get; }
    public string UserName { get; }
    public string PhoneNumber { get; set; }
    public string EmailAddress { get; set; }
    public DateTime LatestLocationTimestamp { get; set; }
    public List<VisitedLocation> VisitedLocations
    {
        get { lock (_verrou) { return new List<VisitedLocation>(_visitedLocations); } }
    }

    public List<UserReward> UserRewards
    {
        get { lock (_verrou) { return new List<UserReward>(_userRewards); } }
    }

    public UserPreferences UserPreferences { get; set; } = new UserPreferences();
    public List<Provider> TripDeals { get; set; } = new List<Provider>();

    public User(Guid userId, string userName, string phoneNumber, string emailAddress)
    {
        UserId = userId;
        UserName = userName;
        PhoneNumber = phoneNumber;
        EmailAddress = emailAddress;
    }

    public void AddToVisitedLocations(VisitedLocation visitedLocation)
    {
        lock (_verrou)
        {
            _visitedLocations.Add(visitedLocation);
        }
    }

    public void ClearVisitedLocations()
    {
        lock (_verrou)
        {
            _visitedLocations.Clear();
        }
    }

    public void AddUserReward(UserReward userReward)
    {
        lock (_verrou)
        {
            if (!_userRewards.Exists(r => r.Attraction.AttractionName == userReward.Attraction.AttractionName))
            {
                _userRewards.Add(userReward);
            }
        }
    }

    public VisitedLocation GetLastVisitedLocation()
    {
        lock (_verrou)
        {
            return _visitedLocations[^1];
        }
    }
}
