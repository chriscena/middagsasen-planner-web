CREATE TABLE [dbo].[WeatherMeasurements]
(
  [WeatherMeasurementId] INT NOT NULL,
  constraint PK_WeatherMeasurements PRIMARY key (WeatherMeasurementId),
  MeasurementLabel NVARCHAR(400) NOT NULL,
  MeasurementUnit NVARCHAR(50) NULL,
)