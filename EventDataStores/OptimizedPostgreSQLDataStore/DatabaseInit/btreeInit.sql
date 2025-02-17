
CREATE TABLE Flights(
    flightIdentification varchar(36) unique primary key not null, -- Because table is unique, already uses an index
    departureTime timestamp not null,
    arrivalTime timestamp not null,
    isRecalculating bool not null default (false)
);

CREATE TABLE Airports(
    id serial primary key,
    icao char(4) not null,
    flightIdentification varchar(36) references Flights(flightIdentification) on delete cascade,
    lastSeenWeather int not null
);


-- Create indexes here

CREATE INDEX airport_icao ON Airports USING BTREE(icao, lastSeenWeather);
CREATE UNIQUE INDEX airport_flight ON Airports USING BTREE(icao, flightIdentification);
