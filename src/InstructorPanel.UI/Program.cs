using InstructorPanel.SimConnect;
using InstructorPanel.UI.Components;

namespace InstructorPanel.UI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();


            builder.Services.AddSingleton<SimConnectService>();
            builder.Services.AddSingleton<ISimConnectService>(sp => sp.GetRequiredService<SimConnectService>());
            builder.Services.AddHostedService(sp => sp.GetRequiredService<SimConnectService>());


            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();                 
        }
    }
}
