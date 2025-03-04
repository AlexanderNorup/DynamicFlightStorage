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
        public int FlightCount => NativeMethods.GetFlightCount(_handle);

        /// <summary>
        /// Creates a new flight system
        /// </summary>
        public CudaFlightSystem()
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
        public bool Initialize()
        {
            return NativeMethods.InitializeFlights(_handle);
        }

        /// <summary>
        /// Add new flights to the system
        /// </summary>
        /// <param name="positions">Array of positions (x,y,z triplets)</param>
        /// <returns>True if addition succeeded</returns>
        public bool AddFlights(int[] positions)
        {
            if (positions is null || positions.Length % 3 != 0)
            {
                throw new ArgumentException("Positions must be a multiple of 3 (x,y,z)", nameof(positions));
            }

            int count = positions.Length / 3;
            bool result = false;

            // Pin the array
            GCHandle positionsHandle = GCHandle.Alloc(positions, GCHandleType.Pinned);
            try
            {
                result = NativeMethods.AddFlights(_handle, positionsHandle.AddrOfPinnedObject(), count);
            }
            finally
            {
                positionsHandle.Free();
            }

            return result;
        }

        /// <summary>
        /// Remove flights by their indices
        /// </summary>
        /// <param name="indices">Indices of flights to remove</param>
        /// <returns>True if removal succeeded</returns>
        public bool RemoveFlights(int[] indices)
        {
            if (indices is null || indices.Length is 0)
            {
                throw new ArgumentException("Must provide at least one index", nameof(indices));
            }

            bool result = false;

            // Pin the array
            GCHandle indicesHandle = GCHandle.Alloc(indices, GCHandleType.Pinned);
            try
            {
                result = NativeMethods.RemoveFlights(_handle, indicesHandle.AddrOfPinnedObject(), indices.Length);
            }
            finally
            {
                indicesHandle.Free();
            }

            return result;
        }

        /// <summary>
        /// Update specific flights with new positions
        /// </summary>
        /// <param name="indices">Indices of flights to update</param>
        /// <param name="newPositions">New positions (x,y,z triplets)</param>
        /// <returns>True if update succeeded</returns>
        public bool UpdateFlights(int[] indices, int[] newPositions, int[] newDurations)
        {
            if (indices is null || newPositions is null || newDurations is null || indices.Length * 3 != newPositions.Length)
            {
                throw new ArgumentException("Must have 3 position values (x,y,z) for each index");
            }

            bool result = false;

            // Pin arrays in memory
            GCHandle indicesHandle = GCHandle.Alloc(indices, GCHandleType.Pinned);
            GCHandle positionsHandle = GCHandle.Alloc(newPositions, GCHandleType.Pinned);
            GCHandle durationsHandle = GCHandle.Alloc(newDurations, GCHandleType.Pinned);

            try
            {
                result = NativeMethods.UpdateFlights(
                    _handle,
                    indicesHandle.AddrOfPinnedObject(),
                    positionsHandle.AddrOfPinnedObject(),
                    durationsHandle.AddrOfPinnedObject(),
                    indices.Length);
            }
            finally
            {
                indicesHandle.Free();
                positionsHandle.Free();
                durationsHandle.Free();
            }

            return result;
        }

        /// <summary>
        /// Detect collisions between flights and a bounding box
        /// </summary>
        /// <param name="boxMin">Minimum corner of bounding box (x,y,z)</param>
        /// <param name="boxMax">Maximum corner of bounding box (x,y,z)</param>
        /// <returns>Array of boolean values indicating collision status for each flight</returns>
        public bool[] DetectCollisions(int[] boxMin, int[] boxMax)
        {
            if (boxMin is null || boxMin.Length != 3 || boxMax is null || boxMax.Length != 3)
            {
                throw new ArgumentException("Box min and max must be arrays of 3 values (x,y,z)");
            }

            int flightCount = FlightCount;
            if (flightCount <= 0)
            {
                throw new InvalidOperationException("Flight system not initialized or empty");
            }

            int[] results = new int[flightCount];
            bool success = false;

            // Pin arrays in memory
            GCHandle boxMinHandle = GCHandle.Alloc(boxMin, GCHandleType.Pinned);
            GCHandle boxMaxHandle = GCHandle.Alloc(boxMax, GCHandleType.Pinned);
            GCHandle resultsHandle = GCHandle.Alloc(results, GCHandleType.Pinned);

            try
            {
                success = NativeMethods.DetectCollisions(
                    _handle,
                    boxMinHandle.AddrOfPinnedObject(),
                    boxMaxHandle.AddrOfPinnedObject(),
                    resultsHandle.AddrOfPinnedObject());
            }
            finally
            {
                boxMinHandle.Free();
                boxMaxHandle.Free();
                resultsHandle.Free();
            }

            if (!success)
            {
                throw new InvalidOperationException("Failed to detect collisions");
            }

            // Convert results to boolean array
            bool[] collisions = new bool[flightCount];
            for (int i = 0; i < flightCount; i++)
            {
                collisions[i] = results[i] != 0;
            }

            return collisions;
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
        public static extern bool AddFlights(IntPtr flightSystem, IntPtr positions, int count);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool RemoveFlights(IntPtr flightSystem, IntPtr indices, int count);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool UpdateFlights(IntPtr flightSystem, IntPtr indices, IntPtr newPositions, IntPtr newDurations, int updateCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool DetectCollisions(IntPtr flightSystem, IntPtr boxMin, IntPtr boxMax, IntPtr results);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetFlightCount(IntPtr flightSystem);
    }
}
