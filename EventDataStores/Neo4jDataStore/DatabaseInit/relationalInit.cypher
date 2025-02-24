// A STATEMENT ONLY ON ONE LINE, AND ONLY ONE PER LINE.
// Adding contstraints automatically adds indexes on the same properties
CREATE CONSTRAINT airport_icao_unique FOR (a:Airport) REQUIRE a.icao IS UNIQUE
CREATE CONSTRAINT flight_id_unique FOR (f:Flight) REQUIRE f.id IS UNIQUE
CREATE INDEX airport_dep FOR () -[r:USES]-> () ON (r.dep);
CREATE INDEX airport_arr FOR () -[r:USES]-> () ON (r.arr);
CREATE INDEX airport_weather FOR () -[r:USES]-> () ON (r.weather); 