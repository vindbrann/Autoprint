using Autoprint.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Autoprint.Web.Services;
using Radzen;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider => provider.GetRequiredService<CustomAuthStateProvider>());
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<Autoprint.Web.Services.ISyncService, Autoprint.Web.Services.SyncService>();
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<TooltipService>();
builder.Services.AddScoped<ContextMenuService>();
builder.Services.AddRadzenComponents();
builder.Services.AddScoped<Autoprint.Web.Services.NetworkScannerService>();

builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy("ADMIN_ACCESS", policy => policy.RequireClaim("Permission", "ADMIN_ACCESS"));
    options.AddPolicy("PRINTER_READ", policy => policy.RequireClaim("Permission", "PRINTER_READ"));
    options.AddPolicy("PRINTER_WRITE", policy => policy.RequireClaim("Permission", "PRINTER_WRITE"));
    options.AddPolicy("PRINTER_DELETE", policy => policy.RequireClaim("Permission", "PRINTER_DELETE"));
    options.AddPolicy("PRINTER_SYNC", policy => policy.RequireClaim("Permission", "PRINTER_SYNC"));
    options.AddPolicy("LOCATION_READ", policy => policy.RequireClaim("Permission", "LOCATION_READ"));
    options.AddPolicy("LOCATION_WRITE", policy => policy.RequireClaim("Permission", "LOCATION_WRITE"));
    options.AddPolicy("LOCATION_DELETE", policy => policy.RequireClaim("Permission", "LOCATION_DELETE"));
    options.AddPolicy("BRAND_READ", policy => policy.RequireClaim("Permission", "BRAND_READ"));
    options.AddPolicy("BRAND_WRITE", policy => policy.RequireClaim("Permission", "BRAND_WRITE"));
    options.AddPolicy("BRAND_DELETE", policy => policy.RequireClaim("Permission", "BRAND_DELETE"));
    options.AddPolicy("MODEL_READ", policy => policy.RequireClaim("Permission", "MODEL_READ"));
    options.AddPolicy("MODEL_WRITE", policy => policy.RequireClaim("Permission", "MODEL_WRITE"));
    options.AddPolicy("MODEL_DELETE", policy => policy.RequireClaim("Permission", "MODEL_DELETE"));
    options.AddPolicy("DRIVER_READ", policy => policy.RequireClaim("Permission", "DRIVER_READ"));
    options.AddPolicy("DRIVER_SCAN", policy => policy.RequireClaim("Permission", "DRIVER_SCAN"));
    options.AddPolicy("USER_READ", policy => policy.RequireClaim("Permission", "USER_READ"));
    options.AddPolicy("USER_WRITE", policy => policy.RequireClaim("Permission", "USER_WRITE"));
    options.AddPolicy("USER_DELETE", policy => policy.RequireClaim("Permission", "USER_DELETE"));
    options.AddPolicy("ROLE_READ", policy => policy.RequireClaim("Permission", "ROLE_READ"));
    options.AddPolicy("ROLE_WRITE", policy => policy.RequireClaim("Permission", "ROLE_WRITE"));
    options.AddPolicy("ROLE_DELETE", policy => policy.RequireClaim("Permission", "ROLE_DELETE"));
    options.AddPolicy("SETTINGS_MANAGE", policy => policy.RequireClaim("Permission", "SETTINGS_MANAGE"));
    options.AddPolicy("AUDIT_READ", policy => policy.RequireClaim("Permission", "AUDIT_READ"));
});

await builder.Build().RunAsync();
