using GrabacionesAgente;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Net.Http.Json;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private HubConnection _connection;
    private ClientWebSocket _obsSocket;
	private readonly ObsManager _obsManager;

	private readonly Dictionary<string, ActiveCallInfo>
	_activeCalls = new();


	private bool OBSConectado =>
	_obsSocket != null &&
	_obsSocket.State == WebSocketState.Open;

	private bool _obsConectado = false;

	private string _agentId = "A001"; 

	public Worker(ILogger<Worker> logger, ObsManager obsManager)
    {
        _logger = logger;
		_obsManager = obsManager;

	}

	// =====================================================
	// VALIDAR OBS
	// =====================================================
	private bool ObsEstaAbierto()
	{
		return Process.GetProcessesByName("obs64").Any();
	}

	// =====================================================
	// LIMPIAR OBS SI QUEDÓ COLGADO
	// =====================================================
	private async Task PrepararOBS()
	{
		var procesos = Process.GetProcessesByName("obs64");

		foreach (var p in procesos)
		{
			try
			{
				if (!p.HasExited)
				{
					Console.WriteLine("OBS abierto. Cerrando limpio...");

					p.CloseMainWindow();

					if (!p.WaitForExit(5000))
					{
						Console.WriteLine("OBS no respondió. Forzando cierre...");
						p.Kill();
					}
				}
			}
			catch { }
		}

		while (Process.GetProcessesByName("obs64").Any())
		{
			await Task.Delay(1000);
		}
		Console.WriteLine("OBS limpio.");
	}



	// 🔥 CONEXIÓN OBS (CORRECTA)
	private async Task ConectarOBS()
        
    {
        try
        {
			_obsSocket?.Dispose();

			_obsSocket = new ClientWebSocket();

            var uri = new Uri("ws://localhost:4455");

            await _obsSocket.ConnectAsync(uri, CancellationToken.None);

			_obsConectado = true;

			Console.WriteLine("🎥 Conectado a OBS");

            _ = Task.Run(EscucharOBS);
        }
		catch (Exception ex)
		{
			_obsConectado = false;

			Console.WriteLine($"❌ OBS desconectado: {ex.Message}");
		}
	}

    // ▶ INICIAR GRABACIÓN
    private async Task IniciarGrabacion()
    {

		if (!OBSConectado)
		{
			Console.WriteLine("⚠ OBS desconectado. Reconectando...");

			//await ReconnectOBS();

			var conectado = await VerificarOBS();

			if (!conectado)
			{
				Console.WriteLine("⛔ No se pudo reconectar OBS");
				return;
			}

		}


		var request = new
        {
            op = 6,
            d = new
            {
                requestType = "StartRecord",
                requestId = Guid.NewGuid().ToString()
            }
        };

        var json = JsonConvert.SerializeObject(request);
        var bytes = Encoding.UTF8.GetBytes(json);

        await _obsSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );

        Console.WriteLine("▶ Grabación iniciada");
    }

    // ⏹ DETENER GRABACIÓN
    private async Task DetenerGrabacion()
    {
        var request = new
        {
            op = 6,
            d = new
            {
                requestType = "StopRecord",
                requestId = Guid.NewGuid().ToString()
            }
        };

        var json = JsonConvert.SerializeObject(request);
        var bytes = Encoding.UTF8.GetBytes(json);

        await _obsSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );

        Console.WriteLine("⏹ Grabación detenida");
    }

    private string GenerarAuth(string password, string salt, string challenge)
    {
        using var sha256 = SHA256.Create();

        // step 1
        var secret = Convert.ToBase64String(
            sha256.ComputeHash(Encoding.UTF8.GetBytes(password + salt))
        );

        // step 2
        var auth = Convert.ToBase64String(
            sha256.ComputeHash(Encoding.UTF8.GetBytes(secret + challenge))
        );

        return auth;
    }
    private async Task EscucharOBS()
    {
        var buffer = new byte[4096];

        try
        {
            while (_obsSocket.State == WebSocketState.Open)
            {
                var result = await _obsSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    CancellationToken.None);

				if (result.MessageType == WebSocketMessageType.Close)
				{
					Console.WriteLine("⚠ OBS cerró la conexión");

					_obsConectado = false;

					return;
				}

				if (result.Count == 0)
				{
					Console.WriteLine("⚠ OBS envió mensaje vacío");

					_obsConectado = false;

					return;
				}


				var json = Encoding.UTF8.GetString(buffer, 0, result.Count);

                Console.WriteLine($"OBS dice: {json}");

				//var msg = JsonConvert.DeserializeObject<dynamic>(json);

				//int op = msg.op;
				dynamic? msg = JsonConvert.DeserializeObject<dynamic>(json);

				if (msg == null)
				{
					Console.WriteLine("⚠ Mensaje OBS inválido");
					continue;
				}

				int op = msg.op ?? -1;

				if (op == 0)
                {
					if (msg.d?.authentication == null)
					{
						Console.WriteLine("⚠ OBS aún no envía authentication");
						continue;
					}

					string challenge = msg.d.authentication.challenge;
                    string salt = msg.d.authentication.salt;

                    await IdentificarOBS(challenge, salt);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ OBS desconectado: {ex.Message}");
        }

		Console.WriteLine("🔄 Intentando reconectar OBS en 5 segundos...");

		//await Task.Delay(5000);

		//await VerificarOBS();
	}

    private async Task IdentificarOBS(string challenge, string salt)
    {
        string password = "admin12345"; // 🔥 CAMBIA ESTO

        var auth = GenerarAuth(password, salt, challenge);

        var request = new
        {
            op = 1,
            d = new
            {
                rpcVersion = 1,
                authentication = auth
            }
        };

        var json = JsonConvert.SerializeObject(request);
        var bytes = Encoding.UTF8.GetBytes(json);

        await _obsSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );

        Console.WriteLine("🔐 Identificado con OBS");
    }

	protected override async Task ExecuteAsync(
	CancellationToken stoppingToken)
	{
		Console.WriteLine("Iniciando Worker...");

		await ConnectSignalR();

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				// =========================
				// VALIDAR OBS
				// =========================
				if (!ObsEstaAbierto())
				{
					Console.WriteLine(
						"⚠ OBS no está abierto"
					);

					_obsManager.Start();

					await _obsManager.WaitForObsAsync();
				}

				// =========================
				// ESPERAR WEBSOCKET
				// =========================
				await EsperarOBSWebSocket();

				// =========================
				// CONECTAR SOCKET
				// =========================
				if (
					_obsSocket == null ||
					_obsSocket.State != WebSocketState.Open
				)
				{
					await ConectarOBS();
				}

				Console.WriteLine("✅ OBS listo");

				// =========================
				// ESCUCHAR
				// =========================
				await EscucharOBS();
			}
			catch (Exception ex)
			{
				Console.WriteLine(
					$"❌ Error OBS: {ex.Message}"
				);

				try
				{
					_obsSocket?.Dispose();
				}
				catch { }

				_obsSocket = null;

				_obsConectado = false;

				// =========================
				// MATAR OBS ZOMBIE
				// =========================
				var procesos = Process.GetProcessesByName("obs64");

				foreach (var p in procesos)
				{
					try
					{
						Console.WriteLine(
							"🧹 Cerrando OBS zombie..."
						);

						p.Kill(true);
					}
					catch { }
				}

				await Task.Delay(2000);

				// =========================
				// REINICIAR OBS
				// =========================
				Console.WriteLine(
					"🚀 Reiniciando OBS..."
				);

				_obsManager.Start();

				await _obsManager.WaitForObsAsync();
			}

			Console.WriteLine(
				"🔄 Reintentando OBS en 3 segundos..."
			);

			await Task.Delay(3000, stoppingToken);
		}
	}

	private async Task<bool> VerificarOBS_ant()
	{
		try
		{
			if (
				_obsSocket != null &&
				_obsSocket.State == WebSocketState.Open)
			{
				return true;
			}

			Console.WriteLine("🔄 Reconectando OBS...");

			await ConectarOBS();

			if (
				_obsSocket != null &&
				_obsSocket.State == WebSocketState.Open)
			{
				Console.WriteLine("✅ OBS reconectado");
				return true;
			}

			Console.WriteLine("❌ OBS sigue desconectado");

			return false;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Error reconectando OBS: {ex.Message}");

			return false;
		}
	}

	private async Task<bool> VerificarOBS()
	{
		try
		{
			// =====================================
			// SOCKET YA FUNCIONANDO
			// =====================================
			if (
				_obsSocket != null &&
				_obsSocket.State == WebSocketState.Open
			)
			{
				return true;
			}

			Console.WriteLine(
				"⚠ OBS desconectado"
			);

			// =====================================
			// OBS CERRADO
			// =====================================
			if (!_obsManager.IsRunning())
			{
				Console.WriteLine(
					"🚀 Iniciando OBS..."
				);

				_obsManager.Start();

				await _obsManager.WaitForObsAsync();
			}

			// =====================================
			// ESPERAR WEBSOCKET
			// =====================================
			await EsperarOBSWebSocket();

			// =====================================
			// RECONECTAR SOCKET
			// =====================================
			Console.WriteLine(
				"🔄 Reconectando OBS WebSocket..."
			);

			await ConectarOBS();

			// =====================================
			// VALIDAR
			// =====================================
			if (
				_obsSocket != null &&
				_obsSocket.State == WebSocketState.Open
			)
			{
				Console.WriteLine(
					"✅ OBS reconectado"
				);

				return true;
			}

			Console.WriteLine(
				"❌ OBS sigue desconectado"
			);

			return false;
		}
		catch (Exception ex)
		{
			Console.WriteLine(
				$"❌ Error reconectando OBS: {ex.Message}"
			);

			return false;
		}
	}

	private async Task ReconnectOBS()
	{
		try
		{
			_obsSocket?.Dispose();

			Console.WriteLine("🔄 Reconectando OBS...");

			await ConectarOBS();

			Console.WriteLine("✅ OBS reconectado");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Error reconectando OBS: {ex.Message}");
		}
	}

	private async Task ConnectSignalR()
	{
		_connection = new HubConnectionBuilder()
			.WithUrl(
				$"http://localhost:5192/grabacionesHub?agentId={_agentId}"
			)
			.WithAutomaticReconnect()
			.Build();

		// =========================
		// START RECORDING
		// =========================
		_connection.On<string>("StartRecording", async (callId) =>
		{
			Console.WriteLine(
				$"🔥 Start recibido: {callId}"
			);


			// =========================
			// GUARDAR LLAMADA ACTIVA
			// =========================
			_activeCalls[callId] = new ActiveCallInfo
			{
				CallId = callId,

				AgentId = _agentId,

				StartTime = DateTime.Now
			};

			Console.WriteLine(
				$"✅ Llamada agregada: {callId}"
			);


			await IniciarGrabacion();

			await _connection.InvokeAsync(
				"NotifyRecordingStarted",
				_agentId,
				callId
			);
		});

		// =========================
		// STOP RECORDING
		// =========================
		_connection.On<string>("StopRecording", async (callId) =>
		{
			Console.WriteLine(
				$"⏹ Stop recibido: {callId}"
			);

			await DetenerGrabacion();


			Console.WriteLine(
				$"⏹ Active Calls Count: {_activeCalls.Count}"
			);

			foreach (var item in _activeCalls)
			{
				Console.WriteLine(
					$"CALL => {item.Key}"
				);
			}

			// =========================
			// GUARDAR EN API
			// =========================
			try
			{
				if (_activeCalls.TryGetValue(callId, out var callInfo))
				{
					var dto = new
					{
						RecordingID = callId,

						OwnerID = callInfo.AgentId,

						CallingPartyActorID = callInfo.AgentId,

						CallingPartyActorEmail =
							callInfo.AgentEmailId,
							                                                                                                                                       
						CallingPartyName =
							callInfo.AgentName,                       

						CallingPartyNumber =
							callInfo.Ani,                        

						CalledPartyNumber =
							callInfo.Dnis,                                                                                                      

						EquipoNombre =
							callInfo.TeamName,

						CreateTime =
							callInfo.StartTime.ToString(
								"yyyy-MM-dd HH:mm:ss"
							),

						FechaCreacion = DateTime.Now,

						CallDurationSeconds =
							(int)(
								DateTime.Now -
								callInfo.StartTime
							).TotalSeconds,

						AudioDuracionSegundos =
							(int)(
								DateTime.Now -
								callInfo.StartTime
							).TotalSeconds,

						RecordingDetJSON =
							callInfo.RawJson
					};

					using var http = new HttpClient();

					var response =
						await http.PostAsJsonAsync(
							"http://localhost:5192/api/ReceivedVideoRecording/create",
							dto
						);

					if (response.IsSuccessStatusCode)
					{
						Console.WriteLine(
							$"✅ Registro guardado: {callId}"
						);
					}
					else
					{
						//Console.WriteLine(
						//	$"❌ Error API: {response.StatusCode}"
						//);
						var errorContent = await response.Content.ReadAsStringAsync();

						Console.WriteLine(
							$"❌ Error API: {response.StatusCode}"
						);

						Console.WriteLine(
							$"❌ API RESPONSE: {errorContent}"
						);
					}

					// limpiar memoria
					_activeCalls.Remove(callId);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(
					$"❌ Error guardando llamada: {ex.Message}"
				);
			}


			await _connection.InvokeAsync(
				"NotifyRecordingStopped",
				_agentId,
				callId
			);
		});

		Console.WriteLine(
			$"🔌 Conectando SignalR Agent: {_agentId}"
		);

		await _connection.StartAsync();

		Console.WriteLine(
			$"✅ SignalR conectado Agent: {_agentId}"
		);
	}

	// =========================
	// UPDATE AGENT
	// =========================
	public async Task UpdateAgent(string newAgentId)
	{
		if (
			string.IsNullOrWhiteSpace(newAgentId) ||
			newAgentId == _agentId
		)
		{
			return;
		}

		Console.WriteLine(
			$"🔄 Cambiando AgentId: {_agentId} -> {newAgentId}"
		);

		_agentId = newAgentId;

		// cerrar conexión actual
		if (_connection != null)
		{
			try
			{
				await _connection.StopAsync();

				await _connection.DisposeAsync();
			}
			catch (Exception ex)
			{
				Console.WriteLine(
					$"⚠ Error cerrando SignalR: {ex.Message}"
				);
			}
		}

		// reconectar con nuevo agentId
		await ConnectSignalR();
	}


	// =========================
	// REGISTER CALL
	// =========================
	public async Task RegisterCall(
		ActiveCallInfo callInfo
	)
	{
		try
		{
			if (
				callInfo == null ||
				string.IsNullOrWhiteSpace(callInfo.CallId)
			)
			{
				return;
			}

			_activeCalls[callInfo.CallId] =
				callInfo;

			Console.WriteLine(
				$"💾 CALL REGISTERED: {callInfo.CallId}"
			);
		}
		catch (Exception ex)
		{
			Console.WriteLine(
				$"❌ RegisterCall Error: {ex.Message}"
			);
		}
	}

	private async Task EsperarOBSWebSocket()
	{
		var timeout = DateTime.Now.AddSeconds(15);

		while (DateTime.Now < timeout)
		{
			try
			{
				using var socket = new ClientWebSocket();

				await socket.ConnectAsync(
					new Uri("ws://localhost:4455"),
					CancellationToken.None);

				Console.WriteLine("✅ WebSocket OBS disponible");

				return;
			}
			catch
			{
				Console.WriteLine("⏳ Esperando OBS WebSocket...");

				await Task.Delay(1000);
			}
		}

		throw new Exception("OBS WebSocket no respondió");
	}


}