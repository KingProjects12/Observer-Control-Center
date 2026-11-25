using System.Net;
using System.Net.Sockets;

namespace Observer.ControlCenter.Services;

public static class RouterHealthProbe
{
    public static async Task<string> CheckAsync(string address)
    {
        if (!IPAddress.TryParse(address.Trim(), out var ip))
            throw new ArgumentException(UiText.T("A valid managed device IP address is required.", "Нужен корректный IP-адрес управляемого устройства."));
        // Exactly two short TCP connect probes, without credentialed requests or port scanning.
        var p80 = CheckPort(ip, 80);
        var p443 = CheckPort(ip, 443);
        await Task.WhenAll(p80, p443);
        return $"{address}  //  TCP 80: {(p80.Result ? UiText.T("OPEN", "ОТКРЫТ") : UiText.T("NO REPLY", "НЕТ ОТВЕТА"))}  |  TCP 443: {(p443.Result ? UiText.T("OPEN", "ОТКРЫТ") : UiText.T("NO REPLY", "НЕТ ОТВЕТА"))}";
    }

    private static async Task<bool> CheckPort(IPAddress address, int port)
    {
        using var client = new TcpClient(address.AddressFamily);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            await client.ConnectAsync(address, port, cts.Token);
            return client.Connected;
        }
        catch (SocketException) { return false; }
        catch (OperationCanceledException) { return false; }
    }
}
