using Autofac;
using Autofac.Extensions.DependencyInjection;
using M1Mentor.Api.Utilities.Configurations;
using Monjo;
using Monjo.MongoDB;
using Utilities.Configuration;
using Utilities.Constants;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCustomControllers();

builder.Services.AddCustomApiVersioning();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwagger();

builder.Services.AddMemoryCache();

builder.Services.AddCoreSettings(builder.Configuration);
builder.Services.AddSettings(builder.Configuration);

// ---------------------------------------------------------------------------
// Monjo persistence.
//
// Provider selection comes from configuration (section "Monjo", or the legacy
// "MonjoSettings" section for this app):  "Provider": "MongoDB" | "PostgreSQL" | "SQLite".
// The provider factory is registered explicitly below (one line, no if/else in app code);
// the provider itself is resolved once at startup (singleton) and shared by all repositories.
// ---------------------------------------------------------------------------
builder.Services.AddMonjo(builder.Configuration);
builder.Services.UseMonjoMongoDB();

// Bridge the application's request user context into Monjo's audit-field source.
MonjoActorContext.SetProvider(() =>
{
    var user = CurrentRequestContext.User ?? new RequestUserInfo();
    return new MonjoActor(user.PublicKey, user.DisplayInfo);
});

builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(autofacConfigure =>
{
    autofacConfigure.AddCoreServices();
    autofacConfigure.AddControllerServices();
});


var app = builder.Build();

// Resolve the provider eagerly so configuration errors surface at startup, not at first use.
app.Services.GetRequiredService<IMonjoProvider>();

app.UseCustomCors();

app.UseHsts(app.Environment);

app.UseDeveloperExceptionPage(app.Environment);

app.UseSwaggerAndUI();

app.UseCustomExceptionHandler();

app.UseLogger();

app.UseFirewall();

app.UseSignature();

app.UseJwt();

app.UseRouting();

app.UseSecurityStamp();

app.UseAntiXss();

app.UseCustomRateLimiting();

app.UseAuthorization();

app.UseEndpoints();

app.Run();
