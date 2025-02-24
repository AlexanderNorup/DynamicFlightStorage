// A STATEMENT ONLY ON ONE LINE, AND ONLY ONE PER LINE.
// Adding contstraints automatically adds indexes on the same properties
CREATE CONSTRAINT airport_icao_unique FOR (a:Airport) REQUIRE a.icao IS UNIQUE
CREATE CONSTRAINT flight_id_unique FOR (f:Flight) REQUIRE f.id IS UNIQUE
CREATE CONSTRAINT timebucket_unique FOR (t:TimeBucket) REQUIRE (t.icao, t.timeSliceStart) IS UNIQUE
CREATE INDEX timebucket_start FOR (t:TimeBucket) ON (t.timeSliceStart);
CREATE INDEX uses_dep FOR () -[r:USES]-> () ON (r.dep);
CREATE INDEX uses_arr FOR () -[r:USES]-> () ON (r.arr);
CREATE INDEX uses_weather FOR () -[r:USES]-> () ON (r.weather); 