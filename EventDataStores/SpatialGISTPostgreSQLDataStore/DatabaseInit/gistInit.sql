CREATE EXTENSION IF NOT EXISTS cube;

CREATE TABLE flight_events (
    flightIdentification VARCHAR(36) NOT NULL,
    isRecalculating BOOL NOT NULL DEFAULT (FALSE),
    lastWeather INT,
    departure TIMESTAMP NOT NULL,
    arrival TIMESTAMP NOT NULL,
    icao CHAR(4) NOT NULL,
    line3d CUBE,  -- Store the [(weather, departure, name), (weather, arrival, name)] as 3D
    PRIMARY KEY (flightIdentification, icao)
);

CREATE INDEX flight_events_gist_idx ON flight_events USING GIST (line3d);