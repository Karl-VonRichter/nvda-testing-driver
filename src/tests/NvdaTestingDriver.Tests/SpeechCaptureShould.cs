using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NvdaTestingDriver.Tests
{
	[TestClass]
	public class SpeechCaptureShould
	{
		[TestMethod]
		[Timeout(5000)]
		public async Task BoundTlsNegotiationWhenTheLocalPeerDoesNotSpeakTls()
		{
			using var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			using var client = new TcpClient();
			Task<TcpClient> accepted = listener.AcceptTcpClientAsync();
			await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
			using var peer = await accepted;
			using var stream = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
			await Assert.ThrowsExactlyAsync<Exceptions.TimeoutException>(() => NvdaDriver.AuthenticateRemoteAsync(stream, TimeSpan.FromMilliseconds(100)));
			Assert.IsFalse(stream.CanRead, "A stalled handshake must close its transport before startup cleanup.");
		}

		[TestMethod]
		[Timeout(10000)]
		public async Task RemoveTheSpeechSubscriptionWhenTheActionFails()
		{
			await using var connection = await LoopbackConnection.CreateAsync();
			await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.Driver.GetNextSpokenMessageAsync(() => throw new InvalidOperationException("Example action failure")));
			Assert.IsNull(typeof(NvdaDriver).GetField("OnSpeakReceived", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(connection.Driver));
		}

		[TestMethod]
		[Timeout(10000)]
		public async Task WaitForSilenceAfterTheLastMessage()
		{
			await using var connection = await LoopbackConnection.CreateAsync();
			Task producer = null;
			string speech = await connection.Driver.GetNextSpokenMessageAsync(TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(250), () =>
			{
				connection.Driver.ParseMessage("{\"type\":\"speak\",\"sequence\":[\"First\"]}");
				producer = Task.Run(async () =>
				{
					await Task.Delay(180);
					connection.Driver.ParseMessage("{\"type\":\"speak\",\"sequence\":[\"Second\"]}");
					await Task.Delay(180);
					connection.Driver.ParseMessage("{\"type\":\"speak\",\"sequence\":[\"Third\"]}");
				});
				return Task.CompletedTask;
			});
			await producer;
			Assert.IsTrue(NvdaTestHelper.TextContains(speech, "First Second Third"));
		}

		[TestMethod]
		[Timeout(10000)]
		public async Task DecodeSplitUtf8AndMultipleJsonMessages()
		{
			await using var connection = await LoopbackConnection.CreateAsync();
			connection.StartReading();
			byte[] payload = Encoding.UTF8.GetBytes("{\"type\":\"speak\",\"sequence\":[\"café\"]}\n{\"type\":\"speak\",\"sequence\":[\"required\"]}\n");
			int split = Array.IndexOf(payload, (byte)0xc3) + 1;
			string speech = await connection.Driver.GetNextSpokenMessageAsync(TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(200), async () =>
			{
				await connection.Server.WriteAsync(payload.AsMemory(0, split));
				await Task.Delay(100);
				await connection.Server.WriteAsync(payload.AsMemory(split));
			});
			Assert.IsTrue(NvdaTestHelper.TextContains(speech, "café required"));
		}

		[TestMethod]
		[Timeout(10000)]
		public async Task RefuseToSendKeysWhenTheForegroundGuardFails()
		{
			await using var connection = await LoopbackConnection.CreateAsync();
			connection.Driver.BeforeSendingKeys = () => throw new InvalidOperationException("Example foreground guard");
			await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.Driver.SendKeysAsync(Key.Tab));
			using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
			await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await connection.Server.ReadAsync(new byte[1], timeout.Token));
		}

		// A loopback TLS peer exercises the transport without starting NVDA or sending OS input.
		private sealed class LoopbackConnection : IAsyncDisposable
		{
			internal NvdaDriver Driver { get; } = new NvdaDriver();
			internal SslStream Server { get; private set; }
			private TcpClient _serverClient;
			private X509Certificate2 _certificate;

			internal static async Task<LoopbackConnection> CreateAsync()
			{
				var result = new LoopbackConnection();
				using var rsa = RSA.Create(2048);
				var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
				using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(10));
				result._certificate = new X509Certificate2(generated.Export(X509ContentType.Pfx));
				var listener = new TcpListener(IPAddress.Loopback, 0);
				listener.Start();
				try
				{
					var client = new TcpClient();
					var accepting = listener.AcceptTcpClientAsync();
					await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
					result._serverClient = await accepting;
					var network = client.GetStream();
					var tls = new SslStream(network, false, (_, cert, chain, errors) => true);
					result.Server = new SslStream(result._serverClient.GetStream());
					await Task.WhenAll(tls.AuthenticateAsClientAsync("localhost"), result.Server.AuthenticateAsServerAsync(result._certificate)).WaitAsync(TimeSpan.FromSeconds(5));
					result.SetField("_tcpClient", client);
					result.SetField("_networkStream", network);
					result.SetField("_sslStream", tls);
					result.SetField("_cancellationTokenSource", new CancellationTokenSource());
					return result;
				}
				finally { listener.Stop(); }
			}

			private void SetField(string name, object value) => typeof(NvdaDriver).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Driver, value);

			internal void StartReading() => typeof(NvdaDriver).GetMethod("BeginReadMessages", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Driver, null);

			public async ValueTask DisposeAsync()
			{
				Driver.BeforeSendingKeys = null;
				await Driver.DisconnectAsync();
				Server.Dispose();
				_serverClient.Dispose();
				_certificate.Dispose();
			}
		}
	}
}
