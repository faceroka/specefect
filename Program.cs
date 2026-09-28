using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PacketDotNet;
using SharpPcap;

namespace LinuxTrafficMonitor;

class Program
{
    private static readonly ConcurrentDictionary<int, string> PortProcessCache = new();
    private static DateTime _lastCacheUpdate = DateTime.MinValue;

    private static readonly BlockingCollection<string> PacketLogQueue = new();
    private static readonly CancellationTokenSource Cts = new();

    private const string Banner = """
▄█████   ▄▄▄▄    ▄▄▄▄▄    ▄▄▄▄   ▄▄▄▄▄   ▄▄▄▄▄   ▄▄▄▄▄    ▄▄▄▄   ▄▄▄▄▄▄ 
▀▀▀▄▄▄   ██▄█▀   ██▄▄    ██▀▀▀   ██▄▄    ██▄▄    ██▄▄    ██▀▀▀     ██   
█████▀   ██      ██▄▄▄   ▀████   ██▄▄▄   ██      ██▄▄▄   ▀████     ██   

Created: @milarimo / Website: faceroka.skrinvex.com
""";

    static void Main()
    {
        Console.Title = "Specefect v1 | by: @milarimo";
        Console.CursorVisible = false;

        var devices = CaptureDeviceList.Instance;
        if (devices.Count == 0)
        {
            PrintBanner();
            Console.WriteLine("[-] Сетевые интерфейсы не найдены. Запустите через sudo.");
            Console.CursorVisible = true;
            return;
        }

        int selectedIndex = SelectInterfaceMenu(devices);
        var device = devices[selectedIndex];

        Console.Clear();
        PrintBanner();
        Console.WriteLine($"[+] Слушаем {device.Name}\n");
        Console.WriteLine($"{"Время",-10} | {"Протокол",-6} | {"Направление",-42} | {"Приложение"}");
        Console.WriteLine(new string('-', 85));

        var renderTask = Task.Run(() => SmoothLogRenderer(Cts.Token));

        device.OnPacketArrival += Device_OnPacketArrival;
        device.Open(DeviceModes.Promiscuous, 1000);
        device.Filter = "ip or ip6";
        device.StartCapture();

        Console.CancelKeyPress += (sender, args) =>
        {
            args.Cancel = true;
            Cts.Cancel();
            PacketLogQueue.CompleteAdding();
            device.StopCapture();
            device.Close();
        };

        renderTask.Wait();
        Console.CursorVisible = true;
    }

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine(Banner);
        Console.WriteLine();
        Console.ResetColor();
    }

    private static int SelectInterfaceMenu(CaptureDeviceList devices)
    {
        int currentIndex = 0;
        int totalItems = devices.Count + 1;
        ConsoleKey key;

        do
        {
            Console.Clear();
            PrintBanner();
            Console.WriteLine("Выберите интерфейс или действие (↑/↓, Enter):\n");

            for (int i = 0; i < devices.Count; i++)
            {
                string desc = string.IsNullOrEmpty(devices[i].Description) ? "" : $" — {devices[i].Description}";
                string line = $"[{i}] {devices[i].Name}{desc}";

                RenderMenuItem(line, i == currentIndex);
            }

            int readmeIndex = devices.Count;
            string readmeLine = "[?] Readme";
            RenderMenuItem(readmeLine, currentIndex == readmeIndex);

            key = Console.ReadKey(true).Key;

            if (key == ConsoleKey.UpArrow)
            {
                currentIndex = (currentIndex == 0) ? totalItems - 1 : currentIndex - 1;
            }
            else if (key == ConsoleKey.DownArrow)
            {
                currentIndex = (currentIndex == totalItems - 1) ? 0 : currentIndex + 1;
            }
            else if (key == ConsoleKey.Enter)
            {
                if (currentIndex == readmeIndex)
                {
                    ShowReadme();
                    currentIndex = 0;
                    key = ConsoleKey.None;
                }
                else
                {
                    return currentIndex;
                }
            }

        } while (key != ConsoleKey.Enter);

        return currentIndex;
    }

    private static void RenderMenuItem(string text, bool isSelected)
    {
        if (isSelected)
        {
            Console.BackgroundColor = ConsoleColor.DarkMagenta;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.WriteLine($" > {text} ");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"   {text}");
            Console.ResetColor();
        }
    }

    private static void ShowReadme()
    {
        Console.Clear();
        PrintBanner();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("1. Данное программное обеспечение создано исключительно в образовательных");
        Console.WriteLine("   целях и для отладки/мониторинга собственного сетевого трафика.");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("2. Автор программы НЕ НЕСЕТ ОТВЕТСТВЕННОСТИ за ваши действия, любые прямые");
        Console.WriteLine("   или косвенные убытки, нарушения законодательства или неправомерное");
        Console.WriteLine("   использование данного инструмента третьими лицами.");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("3. Перехват и анализ чужого трафика без предварительного согласия владельца");
        Console.WriteLine("   сети может являться нарушением законодательства вашей страны.");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(new string('-', 76));
        Console.ResetColor();
        Console.WriteLine("\nЧтобы вернуться нажми любую клавишу");
        Console.ReadKey(true);
    }

    private static void SmoothLogRenderer(CancellationToken token)
    {
        try
        {
            foreach (var logLine in PacketLogQueue.GetConsumingEnumerable(token))
            {
                Console.WriteLine(logLine);
                Thread.Sleep(30);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static void Device_OnPacketArrival(object sender, PacketCapture e)
    {
        try
        {
            var rawPacket = e.GetPacket();
            var packet = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
            var ipPacket = packet.Extract<IPPacket>();

            if (ipPacket == null) return;

            string proto = ipPacket.Protocol.ToString();
            int srcPort = 0, dstPort = 0;

            if (packet.Extract<TcpPacket>() is { } tcp)
            {
                srcPort = tcp.SourcePort;
                dstPort = tcp.DestinationPort;
                proto = "TCP";
            }
            else if (packet.Extract<UdpPacket>() is { } udp)
            {
                srcPort = udp.SourcePort;
                dstPort = udp.DestinationPort;
                proto = "UDP";
            }
            else
            {
                return;
            }

            string srcIp = ipPacket.SourceAddress.ToString();
            string dstIp = ipPacket.DestinationAddress.ToString();

            string processName = ResolveProcessByPort(srcPort);
            if (processName == "Unknown")
            {
                processName = ResolveProcessByPort(dstPort);
            }

            if (processName == "Unknown")
            {
                if (dstIp.StartsWith("224.0.0.") || dstIp.StartsWith("239.255.") || dstIp.StartsWith("ff02::"))
                {
                    processName = dstPort switch
                    {
                        5353 => "mDNS",
                        1900 => "SSDP",
                        5355 => "LLMNR",
                        _ => "Multicast"
                    };
                }
                else if (dstPort == 443 && proto == "UDP")
                {
                    processName = "QUIC / HTTP3";
                }
                else if (dstPort == 53 || srcPort == 53)
                {
                    processName = "DNS";
                }
            }

            string flow = $"{srcIp}:{srcPort} -> {dstIp}:{dstPort}";
            string time = DateTime.Now.ToString("HH:mm:ss");

            string logEntry = $"{time,-10} | {proto,-6} | {flow,-42} | {processName}";

            if (!PacketLogQueue.IsAddingCompleted)
            {
                PacketLogQueue.Add(logEntry);
            }
        }
        catch
        {
        }
    }

    private static string ResolveProcessByPort(int port)
    {
        if (port <= 0) return "Unknown";

        if ((DateTime.Now - _lastCacheUpdate).TotalSeconds > 2)
        {
            RefreshPortMappings();
            _lastCacheUpdate = DateTime.Now;
        }

        return PortProcessCache.TryGetValue(port, out var proc) ? proc : "Unknown";
    }

    private static void RefreshPortMappings()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ss",
                Arguments = "-tunp",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var regex = new Regex(@":(\d+)\s+.*users:\(\(""([^""]+)""");

            foreach (var line in lines)
            {
                var match = regex.Match(line);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int port))
                {
                    PortProcessCache[port] = match.Groups[2].Value;
                }
            }
        }
        catch
        {
        }
    }
}