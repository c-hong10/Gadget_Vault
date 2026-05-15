using Microsoft.EntityFrameworkCore;
using GadgetVault.Data;

DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<GadgetVault.Services.ImageService>();
builder.Services.AddScoped<GadgetVault.Services.IShippingService, GadgetVault.Services.ShippingService>();
builder.Services.AddScoped<GadgetVault.Services.IEmailService, GadgetVault.Services.EmailService>();
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options => {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });

var app = builder.Build();

// Automated User Management & Seeding (Phase 5)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        await DbInitializer.Initialize(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during database initialization.");
    }
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/Error/{0}");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

// app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
