using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;  // Tambahkan ini untuk Dictionary
using System.Text;

namespace IEDSimulator.Infrastructure
{
    public class IedServer
    {
        private TcpListener _tcpListener;
        private bool _isRunning;
        private readonly int _port;
        private readonly Dictionary<string, Func<byte[], byte[]>> _mmsHandlers;

        public IedServer(int port)
        {
            _port = port;
            _mmsHandlers = new Dictionary<string, Func<byte[], byte[]>>
            {
                { "initiate", HandleInitiate },
                { "getNameList", HandleGetNameList },
                { "getVariableAccessAttributes", HandleGetVariableAccessAttributes },
                { "read", HandleRead }
            };
        }

        private byte[] HandleInitiate(byte[] request)
        {
            // Respon MMS Initiate yang lebih lengkap
            return new byte[] 
            { 
                0x03, 0x00, 0x00, 0x16, 
                0x11, 0xE0, 0x00, 0x00, 
                0x00, 0x01, 0x00, 0xA1, 
                0x07, 0x02, 0x01, 0x03,
                0xA2, 0x02, 0x80, 0x00
            };
        }

        private byte[] HandleGetNameList(byte[] request)
        {
            // Implementasi daftar objek yang tersedia
            // Sesuaikan dengan model data IED Anda
            return new byte[] { /* response bytes */ };
        }

        private byte[] HandleGetVariableAccessAttributes(byte[] request)
        {
            // Kirim atribut variabel yang diminta
            return new byte[] { /* response bytes */ };
        }

        private byte[] HandleRead(byte[] request)
        {
            // Implementasi membaca nilai dari DataPoint
            return new byte[] { /* response bytes */ };
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
            try
            {
                using var stream = client.GetStream();
                var buffer = new byte[8192];

                while (_isRunning)
                {
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    // Log data yang diterima
                    var hexData = BitConverter.ToString(buffer, 0, bytesRead).Replace("-", " ");
                    Console.WriteLine($"[INFO] Received {bytesRead} bytes: {hexData}");

                    // Identifikasi tipe pesan MMS
                    string mmsType = IdentifyMmsMessageType(buffer, bytesRead);
                    
                    // Proses pesan sesuai tipenya
                    if (_mmsHandlers.TryGetValue(mmsType, out var handler))
                    {
                        var response = handler(buffer);
                        await stream.WriteAsync(response, 0, response.Length);
                        await stream.FlushAsync();
                        
                        Console.WriteLine($"[INFO] Sent {response.Length} bytes response for {mmsType}");
                    }
                    else
                    {
                        Console.WriteLine($"[WARN] Unhandled MMS message type: {mmsType}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Client handler error: {ex.Message}");
            }
            finally
            {
                client.Close();
                Console.WriteLine($"[INFO] Closing connection for {client.Client.RemoteEndPoint}");
            }
        }

        private string IdentifyMmsMessageType(byte[] buffer, int length)
        {
            // Implementasi identifikasi tipe pesan MMS
            // Berdasarkan byte pattern
            if (length >= 4 && buffer[0] == 0x03 && buffer[3] == 0x16)
                return "initiate";
                
            // Tambahkan identifikasi untuk tipe pesan lainnya
            return "unknown";
        }

        public void Stop()
        {
            _isRunning = false;
            _tcpListener?.Stop();
        }
    }
}