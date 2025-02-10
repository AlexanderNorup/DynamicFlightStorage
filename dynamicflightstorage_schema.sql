-- phpMyAdmin SQL Dump
-- version 5.2.1
-- https://www.phpmyadmin.net/
--
-- Vært: hosting.alexandernorup.com
-- Genereringstid: 10. 02 2025 kl. 10:55:14
-- Serverversion: 10.5.23-MariaDB-0+deb11u1-log
-- PHP-version: 8.3.16

SET SQL_MODE = "NO_AUTO_VALUE_ON_ZERO";
START TRANSACTION;
SET time_zone = "+00:00";


/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;

--
-- Database: `dynamicflightstorage`
--

-- --------------------------------------------------------

--
-- Struktur-dump for tabellen `EventLog_Flight`
--

CREATE TABLE `EventLog_Flight` (
  `Id` int(11) NOT NULL,
  `ExperimentId` varchar(36) NOT NULL,
  `ClientId` varchar(255) DEFAULT NULL,
  `FlightData` longblob NOT NULL,
  `UtcTimeStamp` datetime NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Struktur-dump for tabellen `EventLog_Recalculation`
--

CREATE TABLE `EventLog_Recalculation` (
  `Id` int(11) NOT NULL,
  `ExperimentId` varchar(36) NOT NULL,
  `ClientId` varchar(255) NOT NULL,
  `FlightId` varchar(36) NOT NULL,
  `UtcTimeStamp` datetime NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Struktur-dump for tabellen `EventLog_Weather`
--

CREATE TABLE `EventLog_Weather` (
  `Id` int(11) NOT NULL,
  `ExperimentId` varchar(36) NOT NULL,
  `ClientId` varchar(255) DEFAULT NULL,
  `WeatherData` longblob NOT NULL,
  `UtcTimeStamp` datetime NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Struktur-dump for tabellen `Experiment`
--

CREATE TABLE `Experiment` (
  `Id` varchar(36) NOT NULL,
  `Name` varchar(50) NOT NULL,
  `DataSetName` varchar(255) NOT NULL DEFAULT 'default',
  `TimeScale` double NOT NULL,
  `LoggingEnabled` tinyint(1) NOT NULL DEFAULT 0,
  `SimulatedStartTime` datetime NOT NULL,
  `SimulatedEndTime` datetime NOT NULL,
  `SimulatedPreloadStartTime` datetime NOT NULL,
  `SimulatedPreloadEndTime` datetime NOT NULL,
  `PreloadAllFlights` tinyint(1) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Struktur-dump for tabellen `ExperimentClientResult`
--

CREATE TABLE `ExperimentClientResult` (
  `Id` int(11) NOT NULL,
  `ExperimentResultId` int(11) NOT NULL,
  `ClientId` varchar(255) NOT NULL,
  `DataStoreType` varchar(255) NOT NULL,
  `LatencyTestId` int(11) NOT NULL,
  `MaxFlightConsumerLag` int(11) NOT NULL,
  `MaxWeatherConsumerLag` int(11) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Struktur-dump for tabellen `ExperimentResult`
--

CREATE TABLE `ExperimentResult` (
  `Id` int(11) NOT NULL,
  `ExperimentId` varchar(36) NOT NULL,
  `ExperimentRunDescription` varchar(255) NOT NULL,
  `UTCStartTime` datetime DEFAULT NULL,
  `UTCEndTime` datetime DEFAULT NULL,
  `ExperimentError` varchar(255) DEFAULT NULL,
  `ExperimentSuccess` tinyint(1) NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Struktur-dump for tabellen `LatencyTest`
--

CREATE TABLE `LatencyTest` (
  `Id` int(11) NOT NULL,
  `SamplePoints` int(11) NOT NULL,
  `SampleDelayMs` int(11) NOT NULL,
  `AverageLatencyMs` double NOT NULL,
  `MedianLatencyMs` double NOT NULL,
  `StdDeviationLatency` double NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

--
-- Begrænsninger for dumpede tabeller
--

--
-- Indeks for tabel `EventLog_Flight`
--
ALTER TABLE `EventLog_Flight`
  ADD PRIMARY KEY (`Id`),
  ADD KEY `experiment_results_flightevent` (`ExperimentId`),
  ADD KEY `experiment_clientid_flight` (`ClientId`);

--
-- Indeks for tabel `EventLog_Recalculation`
--
ALTER TABLE `EventLog_Recalculation`
  ADD PRIMARY KEY (`Id`),
  ADD KEY `experiment_results_recalculationevent` (`ExperimentId`),
  ADD KEY `experiment_clientid_recalculation` (`ClientId`);

--
-- Indeks for tabel `EventLog_Weather`
--
ALTER TABLE `EventLog_Weather`
  ADD PRIMARY KEY (`Id`),
  ADD KEY `experiment_results_weatherevent` (`ExperimentId`),
  ADD KEY `experiment_clientid_weather` (`ClientId`);

--
-- Indeks for tabel `Experiment`
--
ALTER TABLE `Experiment`
  ADD PRIMARY KEY (`Id`);

--
-- Indeks for tabel `ExperimentClientResult`
--
ALTER TABLE `ExperimentClientResult`
  ADD PRIMARY KEY (`Id`),
  ADD UNIQUE KEY `one_clientresult_per_result` (`ExperimentResultId`,`ClientId`),
  ADD KEY `latency_test_link` (`LatencyTestId`),
  ADD KEY `client_id_index` (`ClientId`);

--
-- Indeks for tabel `ExperimentResult`
--
ALTER TABLE `ExperimentResult`
  ADD PRIMARY KEY (`Id`),
  ADD KEY `experiment_results` (`ExperimentId`);

--
-- Indeks for tabel `LatencyTest`
--
ALTER TABLE `LatencyTest`
  ADD PRIMARY KEY (`Id`);

--
-- Brug ikke AUTO_INCREMENT for slettede tabeller
--

--
-- Tilføj AUTO_INCREMENT i tabel `EventLog_Flight`
--
ALTER TABLE `EventLog_Flight`
  MODIFY `Id` int(11) NOT NULL AUTO_INCREMENT;

--
-- Tilføj AUTO_INCREMENT i tabel `EventLog_Recalculation`
--
ALTER TABLE `EventLog_Recalculation`
  MODIFY `Id` int(11) NOT NULL AUTO_INCREMENT;

--
-- Tilføj AUTO_INCREMENT i tabel `EventLog_Weather`
--
ALTER TABLE `EventLog_Weather`
  MODIFY `Id` int(11) NOT NULL AUTO_INCREMENT;

--
-- Tilføj AUTO_INCREMENT i tabel `ExperimentClientResult`
--
ALTER TABLE `ExperimentClientResult`
  MODIFY `Id` int(11) NOT NULL AUTO_INCREMENT;

--
-- Tilføj AUTO_INCREMENT i tabel `ExperimentResult`
--
ALTER TABLE `ExperimentResult`
  MODIFY `Id` int(11) NOT NULL AUTO_INCREMENT;

--
-- Tilføj AUTO_INCREMENT i tabel `LatencyTest`
--
ALTER TABLE `LatencyTest`
  MODIFY `Id` int(11) NOT NULL AUTO_INCREMENT;

--
-- Begrænsninger for dumpede tabeller
--

--
-- Begrænsninger for tabel `EventLog_Flight`
--
ALTER TABLE `EventLog_Flight`
  ADD CONSTRAINT `experiment_clientid_flight` FOREIGN KEY (`ClientId`) REFERENCES `ExperimentClientResult` (`ClientId`) ON DELETE CASCADE,
  ADD CONSTRAINT `experiment_results_flightevent` FOREIGN KEY (`ExperimentId`) REFERENCES `Experiment` (`Id`) ON DELETE CASCADE;

--
-- Begrænsninger for tabel `EventLog_Recalculation`
--
ALTER TABLE `EventLog_Recalculation`
  ADD CONSTRAINT `experiment_clientid_recalculation` FOREIGN KEY (`ClientId`) REFERENCES `ExperimentClientResult` (`ClientId`),
  ADD CONSTRAINT `experiment_results_recalculationevent` FOREIGN KEY (`ExperimentId`) REFERENCES `Experiment` (`Id`) ON DELETE CASCADE;

--
-- Begrænsninger for tabel `EventLog_Weather`
--
ALTER TABLE `EventLog_Weather`
  ADD CONSTRAINT `experiment_clientid_weather` FOREIGN KEY (`ClientId`) REFERENCES `ExperimentClientResult` (`ClientId`) ON DELETE CASCADE,
  ADD CONSTRAINT `experiment_results_weatherevent` FOREIGN KEY (`ExperimentId`) REFERENCES `Experiment` (`Id`) ON DELETE CASCADE;

--
-- Begrænsninger for tabel `ExperimentClientResult`
--
ALTER TABLE `ExperimentClientResult`
  ADD CONSTRAINT `experiment_result_link` FOREIGN KEY (`ExperimentResultId`) REFERENCES `ExperimentResult` (`Id`) ON DELETE CASCADE,
  ADD CONSTRAINT `latency_test_link` FOREIGN KEY (`LatencyTestId`) REFERENCES `LatencyTest` (`Id`) ON DELETE CASCADE;

--
-- Begrænsninger for tabel `ExperimentResult`
--
ALTER TABLE `ExperimentResult`
  ADD CONSTRAINT `experiment_results` FOREIGN KEY (`ExperimentId`) REFERENCES `Experiment` (`Id`) ON DELETE CASCADE;
COMMIT;

/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
