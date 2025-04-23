# Flood Simulator

A sophisticated flood simulation program that predicts river flooding based on weather conditions, soil moisture, and historical temperature data. The program uses real-world weather data and geocoding to provide accurate simulations for any location.

## Features

- **Location-Based Simulation**: Input any city and country to get location-specific flood predictions
- **Historical Weather Data**: Uses 5 years of historical temperature data for accurate predictions
- **Real-Time Geocoding**: Automatically fetches coordinates for any location using OpenStreetMap
- **Interactive Visualizations**:
  - River level and rainfall charts
  - Flood risk gauge
  - Temperature comparison graphs
  - Average temperature display
- **Comprehensive Analysis**:
  - Soil moisture tracking
  - Runoff coefficient calculations
  - Temperature trend analysis
  - Flood risk assessment

## Technical Details

### Dependencies

- F# (.NET)
- XPlot.Plotly (for visualizations)
- OpenStreetMap Nominatim API (for geocoding)
- Open-Meteo API (for historical weather data)

### Project Structure

- `Program.fs`: Main program logic and user interaction
- `WeatherApi.fs`: Weather data fetching and processing
- `Visualization.fs`: Chart generation and HTML output
- `Types.fs`: Type definitions for the simulation

## Usage

1. Run the program
2. Enter a date in YYYY-MM-DD format
3. Enter a city name
4. Enter a country name
5. The program will:
   - Fetch coordinates for the location
   - Retrieve historical weather data
   - Run the flood simulation
   - Generate visualizations

## Output

The program generates an HTML report in the `simulation_results` directory containing:

- River level and rainfall charts
- Current flood risk gauge
- Temperature analysis with predicted vs actual temperatures
- Projected average temperature

## Simulation Parameters

- Simulation period: 14 days
- Historical data: 5 years of temperature data
- Soil moisture: Initial value of 50%
- River capacity: 10 meters
- Initial river level: 2 meters

## Error Handling

The program includes robust error handling for:

- Invalid date formats
- Geocoding failures
- Weather API errors
- Invalid coordinate formats

## Default Values

If geocoding fails, the program defaults to:

- Latitude: 47.5°N
- Longitude: 19.0°E (Budapest, Hungary)

## Contributing

Feel free to submit issues and enhancement requests!

## License

This project is open source and available under the MIT License.
