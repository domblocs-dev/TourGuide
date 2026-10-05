namespace TourGuide.Dtos;

public class NearbyAttractionDto
{
    public string AttractionName { get; init; } = string.Empty;
    public double AttractionLatitude { get; init; }
    public double AttractionLongitude { get; init; }
    public double UserLatitude { get; init; }
    public double UserLongitude { get; init; }
    public double DistanceInMiles { get; init; }
    public int RewardPoints { get; init; }






}