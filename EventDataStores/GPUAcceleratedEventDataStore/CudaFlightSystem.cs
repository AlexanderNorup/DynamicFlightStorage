using DynamicFlightStorageDTOs;
using System.Runtime.InteropServices;

namespace GPUAcceleratedEventDataStore
{
    /// <summary>
    /// C# wrapper for the CUDA flight system
    /// </summary>
    public class CudaFlightSystem : IDisposable
    {
        // Handle to the native flight system
        private IntPtr _handle;
        private bool _disposed = false;

        /// <summary>
        /// Number of flights in the system
        /// </summary>
        internal int FlightCount => NativeMethods.GetFlightCount(_handle);

        /// <summary>
        /// Creates a new flight system
        /// </summary>
        internal CudaFlightSystem()
        {
            _handle = NativeMethods.CreateFlightSystem();
            if (_handle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create flight system");
            }
        }

        /// <summary>
        /// Initialize the flight system with the given positions
        /// </summary>
        /// <returns>True if initialization succeeded</returns>
        internal bool Initialize()
        {
            return NativeMethods.InitializeFlights(_handle);
        }

        /// <summary>
        /// Add new flights to the system
        /// </summary>
        /// <returns>True if addition succeeded</returns>
        internal bool AddFlights(params GPUFlight[] flights)
        {
            ArgumentNullException.ThrowIfNull(flights);

            if (flights.Length is 0)
            {
                return true;
            }

            int[] ids = new int[flights.Length];
            int[] newDurations = new int[flights.Length];
            List<int> positions = new(flights.Length * 3);

            for (int i = 0; i < flights.Length; i++)
            {
                ids[i] = flights[i].InternalId;
                newDurations[i] = GetFlightDurationInSeconds(flights[i].Flight);
                positions.AddRange(GetFlightPositions(flights[i].Flight, flights[i].Weather));
            }

            int[] newPositions = positions.ToArray();
            bool result = false;

            // Pin the array
            GCHandle idHandle = GCHandle.Alloc(ids, GCHandleType.Pinned);
            GCHandle positionsHandle = GCHandle.Alloc(newPositions, GCHandleType.Pinned);
            GCHandle durationsHandle = GCHandle.Alloc(newDurations, GCHandleType.Pinned);
            try
            {
                result = NativeMethods.AddFlights(_handle,
                    idHandle.AddrOfPinnedObject(),
                    positionsHandle.AddrOfPinnedObject(),
                    durationsHandle.AddrOfPinnedObject(),
                    flights.Length,
                    newPositions.Length);
            }
            finally
            {
                idHandle.Free();
                positionsHandle.Free();
                durationsHandle.Free();
            }

            return result;
        }

        /// <summary>
        /// Remove flights by their ids
        /// </summary>
        /// <param name="ids">Ids of flights to remove</param>
        /// <returns>True if removal succeeded</returns>
        internal bool RemoveFlights(params int[] ids)
        {
            ArgumentNullException.ThrowIfNull(ids);
            if (ids.Length is 0)
            {
                return true;
            }

            bool result = false;

            // Pin the array
            GCHandle idsHandle = GCHandle.Alloc(ids, GCHandleType.Pinned);
            try
            {
                result = NativeMethods.RemoveFlights(_handle, idsHandle.AddrOfPinnedObject(), ids.Length);
            }
            finally
            {
                idsHandle.Free();
            }

            return result;
        }

        /// <summary>
        /// Update specific flights with new positions
        /// </summary>
        /// <returns>True if update succeeded</returns>
        internal bool UpdateFlights(params GPUFlight[] flights)
        {
            ArgumentNullException.ThrowIfNull(flights);

            if (flights.Length is 0)
            {
                return true;
            }

            int[] ids = new int[flights.Length];
            int[] newDurations = new int[flights.Length];
            List<int> positions = new(flights.Length * 3);

            for (int i = 0; i < flights.Length; i++)
            {
                ids[i] = flights[i].InternalId;
                newDurations[i] = GetFlightDurationInSeconds(flights[i].Flight);
                positions.AddRange(GetFlightPositions(flights[i].Flight, flights[i].Weather));
            }

            int[] newPositions = positions.ToArray();

            bool result = false;

            // Pin arrays in memory
            GCHandle idsHandle = GCHandle.Alloc(ids, GCHandleType.Pinned);
            GCHandle positionsHandle = GCHandle.Alloc(newPositions, GCHandleType.Pinned);
            GCHandle durationsHandle = GCHandle.Alloc(newDurations, GCHandleType.Pinned);

            try
            {
                result = NativeMethods.UpdateFlights(
                    _handle,
                    idsHandle.AddrOfPinnedObject(),
                    positionsHandle.AddrOfPinnedObject(),
                    durationsHandle.AddrOfPinnedObject(),
                    flights.Length,
                    newPositions.Length);
            }
            finally
            {
                idsHandle.Free();
                positionsHandle.Free();
                durationsHandle.Free();
            }

            return result;
        }

        /// <summary>
        /// Detect collisions between flights and a bounding box
        /// </summary>
        /// <returns>Array of ids of the flights that need recalculation</returns>
        internal int[] FindFlightsAffectedByWeather(Weather weather)
        {
            ArgumentNullException.ThrowIfNull(weather);

            if (FlightCount <= 0)
            {
                return [];
            }

            // X = TIME
            // Y = WEATHER
            // Z = AIRPORT
            int icaoAsInt = IcaoConversionHelper.ConvertIcaoToInt(weather.Airport);
            int[] boxMin = [weather.ValidFrom.ToUnixTimeSeconds(), (int)WeatherCategory.Undefined, icaoAsInt];
            int[] boxMax = [weather.ValidTo.ToUnixTimeSeconds(), (int)weather.WeatherLevel - 1, icaoAsInt];

            // Pin arrays in memory
            GCHandle boxMinHandle = GCHandle.Alloc(boxMin, GCHandleType.Pinned);
            GCHandle boxMaxHandle = GCHandle.Alloc(boxMax, GCHandleType.Pinned);
            IntPtr results = IntPtr.Zero;

            int[] affectedFlights = [];
            try
            {
                results = NativeMethods.DetectCollisions(
                    _handle,
                    boxMinHandle.AddrOfPinnedObject(),
                    boxMaxHandle.AddrOfPinnedObject());
                if (results == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to detect collisions");
                }
                int affectedCount = Marshal.ReadInt32(results);
                if (affectedCount > 0)
                {
                    affectedFlights = new int[affectedCount];
                    Marshal.Copy(results + 1, affectedFlights, 0, affectedCount);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Collision detection threw an exception of type {e.GetType().FullName}: {e.Message}");
                throw;
            }
            finally
            {
                boxMinHandle.Free();
                boxMaxHandle.Free();
                if (results != IntPtr.Zero)
                {
                    NativeMethods.ReleaseCollisionResults(_handle, results);
                }
            }

            return affectedFlights;
        }

        private static int GetFlightDurationInSeconds(Flight flight)
        {
            return (int)(flight.ScheduledTimeOfArrival - flight.ScheduledTimeOfDeparture).TotalSeconds;
        }

        const int EndOfArraySignalNumber = -1337; // This is dumb, but it works. "What if null-termination but different?"
        private static IEnumerable<int> GetFlightPositions(Flight flight, Dictionary<string, WeatherCategory> flightWeather)
        {
            // X: TIME
            yield return flight.ScheduledTimeOfDeparture.ToUnixTimeSeconds();

            // For each X position, we have multiple Y and Z position
            foreach (var airport in flight.GetAllAirports().Distinct())
            {
                // Y: WEATHER
                yield return (int)flightWeather.GetValueOrDefault(airport, WeatherCategory.Undefined);
                // Z: ICAO
                yield return IcaoConversionHelper.ConvertIcaoToInt(airport);
            }
            yield return EndOfArraySignalNumber; // We signal the end of z-positions for a flight with the special signal number
        }

        #region IDisposable Implementation

        /// <summary>
        /// Disposes the flight system
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_handle != IntPtr.Zero)
                {
                    NativeMethods.DestroyFlightSystem(_handle);
                    _handle = IntPtr.Zero;
                }

                _disposed = true;
            }
        }

        ~CudaFlightSystem()
        {
            Dispose(false);
        }

        #endregion
    }

    /// <summary>
    /// P/Invoke definitions for native methods
    /// </summary>
    internal static class NativeMethods
    {
        private const string DllName = "CudaFlightSystem";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CreateFlightSystem();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void DestroyFlightSystem(IntPtr flightSystem);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool InitializeFlights(IntPtr flightSystem);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool AddFlights(IntPtr flightSystem, IntPtr ids,
            IntPtr positions, IntPtr durations, int flightCount, int positionCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool RemoveFlights(IntPtr flightSystem, IntPtr ids, int count);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool UpdateFlights(IntPtr flightSystem, IntPtr ids, IntPtr newPositions, IntPtr newDurations, int updateCount, int positionCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr DetectCollisions(IntPtr flightSystem, IntPtr boxMin, IntPtr boxMax);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool ReleaseCollisionResults(IntPtr flightSystem, IntPtr results);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetFlightCount(IntPtr flightSystem);
    }
}
