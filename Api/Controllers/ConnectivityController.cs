using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConnectivityController : ControllerBase
    {
        [HttpGet("test-cassandra")]
        public async Task<IActionResult> TestCassandraConnectivity()
        {
            var hosts = new[] { "10.0.1.5", "10.0.1.6", "10.0.1.7" };
            var port = 9042;
            var timeout = 5000;
            var results = new List<object>();

            foreach (var host in hosts)
            {
                var result = new
                {
                    Host = host,
                    Port = port,
                    Status = "Unknown",
                    Message = "",
                    Timestamp = DateTime.UtcNow
                };

                try
                {
                    using (var client = new TcpClient())
                    {
                        var connectTask = client.ConnectAsync(host, port);
                        
                        if (await Task.WhenAny(connectTask, Task.Delay(timeout)) == connectTask)
                        {
                            if (client.Connected)
                            {
                                result = result with { Status = "Success", Message = "Connected successfully" };
                                client.Close();
                            }
                            else
                            {
                                result = result with { Status = "Failed", Message = "Could not connect" };
                            }
                        }
                        else
                        {
                            result = result with { Status = "Timeout", Message = "Connection timed out after 5 seconds" };
                        }
                    }
                }
                catch (SocketException ex)
                {
                    result = result with { Status = "Error", Message = $"Socket error: {ex.Message} (Code: {ex.SocketErrorCode})" };
                }
                catch (Exception ex)
                {
                    result = result with { Status = "Error", Message = ex.Message };
                }

                results.Add(result);
            }

            return Ok(new
            {
                TestTime = DateTime.UtcNow,
                TotalHosts = hosts.Length,
                Results = results
            });
        }
    }
}
