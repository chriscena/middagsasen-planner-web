CREATE TABLE [dbo].[WeatherMeasurementValues]
(
  [WeatherMeasurementValueId] INT NOT NULL identity, 
    constraint PK_WeatherMeasurementValues PRIMARY key (WeatherMeasurementValueId),
  WeatherLocationId INT NOT NULL,
    constraint FK_WeatherMeasurementValues_WeatherLocations FOREIGN KEY (WeatherLocationId) REFERENCES WeatherLocations(WeatherLocationId) on delete cascade,
  WeatherMeasurementId INT NOT NULL,
    constraint FK_WeatherMeasurementValues_WeatherMeasurements FOREIGN KEY (WeatherMeasurementId) REFERENCES WeatherMeasurements(WeatherMeasurementId) on delete cascade,
  MeasuredValue DECIMAL(15,5) NOT NULL,
  MeasuredTime DATETIME NOT NULL,
)
