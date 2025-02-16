using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Text;

namespace IEDSimulator.Infrastructure
{
    public class IedServer
    {
        private TcpListener _tcpListener;
        private bool _isRunning;
        private readonly int _port;

        public IedServer(int port)
        {
            _port = port;
        }

        public void Start()
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Parse("127.0.0.1"), _port);
                _tcpListener.Start();
                _isRunning = true;

                Console.WriteLine($"IED Server started on port {_port}");
                
                // Start accepting connections
                Task.Run(() => AcceptConnections());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start server on port {_port}: {ex.Message}");
                throw;
            }
        }

        private void AcceptConnections()
        {
            while (_isRunning)
            {
                try
                {
                    var client = _tcpListener.AcceptTcpClient();
                    Console.WriteLine($"Client connected from {client.Client.RemoteEndPoint}");
                    
                    // Handle client in background
                    Task.Run(() => HandleClientAsync(client));
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        Console.WriteLine($"Error accepting client: {ex.Message}");
                    }
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            Console.WriteLine($"[INFO] New IEC61850 client handler started for {client.Client.RemoteEndPoint}");
            
            try
            {
                using var stream = client.GetStream();
                var buffer = new byte[8192]; // Larger buffer for IEC61850 messages

                while (_isRunning)
                {
                    Console.WriteLine($"[INFO] Waiting for IEC61850 data from {client.Client.RemoteEndPoint}");
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    
                    if (bytesRead == 0)
                    {
                        Console.WriteLine($"[INFO] Client {client.Client.RemoteEndPoint} disconnected");
                        break;
                    }

                    // Log received data in hex format
                    var hexData = BitConverter.ToString(buffer, 0, bytesRead).Replace("-", " ");
                    Console.WriteLine($"[INFO] Received {bytesRead} bytes from {client.Client.RemoteEndPoint}:");
                    Console.WriteLine($"HEX: {hexData}");

                    // Send MMS initiate response
                    var response = MmsMessage.CreateInitMessage();
                    Console.WriteLine($"[INFO] Sending MMS response to {client.Client.RemoteEndPoint}");
                    await stream.WriteAsync(response, 0, response.Length);
                    
                    await stream.FlushAsync();
                    Console.WriteLine($"[INFO] MMS Response sent to {client.Client.RemoteEndPoint}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error handling IEC61850 client {client.Client.RemoteEndPoint}: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            }
            finally
            {
                Console.WriteLine($"[INFO] Closing IEC61850 connection for {client.Client.RemoteEndPoint}");
                client?.Close();
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _tcpListener?.Stop();
        }
    }
}