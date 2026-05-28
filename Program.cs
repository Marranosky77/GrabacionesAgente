
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using GrabacionesAgente;

var builder = WebApplication.CreateBuilder(args);

// =========================
// OBS MANAGER
// =========================
builder.Services.AddSingleton<ObsManager>();


builder.Services.AddCors(options =>
{
	options.AddPolicy("AllowAll", policy =>
	{
		policy
			.AllowAnyOrigin()
			.AllowAnyHeader()
			.AllowAnyMethod();
	});
});

// =========================
// WORKER SINGLETON
// =========================
builder.Services.AddSingleton<Worker>();

builder.Services.AddHostedService(
	provider => provider.GetRequiredService<Worker>()
);


var app = builder.Build();

app.UseCors("AllowAll");

// =========================
// LOGIN AGENT
// =========================
app.MapPost(
	"/api/agent/login",
	async (
		AgentLoginRequest request,
		Worker worker
	) =>
	{
		Console.WriteLine(
		   $" LOGIN REQUEST: {request.AgentId}"
	   );

		await worker.UpdateAgent(
			request.AgentId
		);

		return Results.Ok();
	}
);

app.MapPost(
	"/api/call/register",
	async (
		ActiveCallInfo callInfo,
		Worker worker
	) =>
	{
		await worker.RegisterCall(callInfo);

		return Results.Ok();
	}
);


app.Run();


// =========================
// DTO
// =========================
public class AgentLoginRequest
{
	public string AgentId { get; set; }
}