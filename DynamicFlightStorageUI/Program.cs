using BasicEventDataStore;
using DynamicFlightStorageDTOs;
using DynamicFlightStorageSimulation;
using DynamicFlightStorageUI.Components;
using Microsoft.Extensions.Options;

namespace DynamicFlightStorageUI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.AddOptions<EventBusConfig>()
                .Bind(builder.Configuration.GetSection("EventBusConfig"))
                .ValidateDataAnnotations();

            builder.Services.AddTransient((s) =>
            {
                var config = s.GetService<IOptions<EventBusConfig>>()!.Value;
                return new SimulationEventBus(config, s.GetRequiredService<ILogger<SimulationEventBus>>());
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles();
            app.UseAntiforgery();

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }
    }
}
