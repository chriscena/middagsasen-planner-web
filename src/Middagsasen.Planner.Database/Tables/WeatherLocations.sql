CREATE TABLE [dbo].[WeatherLocations]
(
  [WeatherLocationId] INT NOT NULL
  constraint PK_WeatherLocations PRIMARY key (WeatherLocationId),
  LocationName NVARCHAR(400) NOT NULL
)
