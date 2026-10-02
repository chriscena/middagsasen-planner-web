// Håndskrevne typer for /api/weather. WeatherController mangler
// [ProducesResponseType], så OpenAPI-dokumentet har ingen schema for svaret.
// Speiler Services/Weather/LocationMeasurementResponse.cs — hold i synk manuelt.

export interface MeasurementValueResponse {
  value: number;
  measuredTime: string;
}

export interface MeasurementResponse {
  measurementName: string;
  unit?: null | string;
  values: MeasurementValueResponse[];
  lastValue?: null | MeasurementValueResponse;
}

export interface LocationMeasurementResponse {
  locationName: string;
  measurements: MeasurementResponse[];
}
