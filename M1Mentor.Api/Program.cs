using Autofac;
using Autofac.Extensions.DependencyInjection;
using M1Mentor.Api.Utilities.Configurations;
using Utilities.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCustomControllers();

builder.Services.AddCustomApiVersioning();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwagger();

builder.Services.AddMemoryCache();

builder.Services.AddCoreSettings(builder.Configuration);
builder.Services.AddSettings(builder.Configuration);

builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(autofacConfigure =>
{
    autofacConfigure.AddCoreServices();
    autofacConfigure.AddControllerServices();
});


var app = builder.Build();
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