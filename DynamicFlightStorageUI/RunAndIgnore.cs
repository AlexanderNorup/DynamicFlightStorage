namespace DynamicFlightStorageUI
{
    public static class RunAndIgnore
    {
        public async static Task IgnoringExceptions(this Task action)
        {
            try
            {
                await action;
            }
            catch
            {
                // Ignore
            }
        }
    }
}
