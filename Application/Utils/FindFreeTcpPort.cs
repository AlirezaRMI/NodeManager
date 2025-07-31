using System.Net;
using System.Net.Sockets;

namespace Application.Utils;

public static class FindTcpPort
{
    public static int FindFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}