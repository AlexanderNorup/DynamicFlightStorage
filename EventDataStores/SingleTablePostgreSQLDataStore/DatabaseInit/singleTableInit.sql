CREATE TABLE flight_events (
    flightIdentification VARCHAR(36) NOT NULL,
    isRecalculating BOOL NOT NULL DEFAULT (FALSE),
    lastWeather INT,
    departure TIMESTAMP NOT NULL,
    arrival TIMESTAMP NOT NULL,
    icao CHAR(4) NOT NULL,
    PRIMARY KEY (flightIdentification, icao)
);

-- Optimizes SELECT
CREATE INDEX flight_events_idx
    ON flight_events (icao, lastWeather, isRecalculating, departure, arrival, flightIdentification);

-- Optimizes UPDATE (Partial Index for efficiency)
CREATE INDEX flight_recalc_idx
    ON flight_events (flightIdentification, isRecalculating) WHERE isRecalculating = FALSE;