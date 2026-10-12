using TourGuide.LibrairiesWrappers;
using TourGuide.LibrairiesWrappers.Interfaces;
using TourGuide.Services;
using TourGuide.Services.Interfaces;

ThreadPool.SetMinThreads(TourGuideService.NombreSuivisSimultanes, TourGuideService.NombreSuivisSimultanes);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IRewardsService, RewardsService>();
builder.Services.AddSingleton<ITourGuideService, TourGuideService>();
builder.Services.AddSingleton<IGpsUtil, GpsUtilWrapper>();
builder.Services.AddSingleton<IRewardCentral, RewardCentralWrapper>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
