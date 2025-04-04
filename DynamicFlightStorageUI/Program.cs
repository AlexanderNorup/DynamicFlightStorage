using DynamicFlightStorageDTOs;
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
                .AddInteractiveServerComponents()
                .AddCircuitOptions(o =>
                {
                    o.DisconnectedCircuitRetentionPeriod = TimeSpan.FromSeconds(5);
                });

            builder.AddExperimentServices();

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

            app.AddExperimentLogEndpoints();

            var notifier = app.Services.GetRequiredService<IExperimentNotifier>();
            var config = app.Services.GetRequiredService<IOptions<PushoverOptions>>().Value;
            if (config.EnableNotiticationOnBoot)
            {
                notifier.SetNotificationEnabled(true);
            }

            app.Lifetime.ApplicationStopping.Register(() =>
            {
                notifier.SendNotification("Dynamic Flight Storage stopping", "The dynamic flight storage application is shutting down.").GetAwaiter().GetResult();
            });

            app.Run();
        }
    }
}
