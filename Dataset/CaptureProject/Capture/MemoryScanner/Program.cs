using System.Reflection.Metadata.Ecma335;
using Capture;

namespace MemoryScanner;

public static class Program {

    public static void Main(string[] args) {
        List<Window> windows = WindowManager.GetOpenWindows();
        Window? cs = windows.FirstOrDefault(x => x.Title == "Counter-Strike 2");
        if (cs is null) {
            // could not find automagically, prompt user
            Console.WriteLine("Selecione a janela para capturar");
            for (int i = 0; i < windows.Count; i++) {
                Console.WriteLine($"{i+1}. {windows[i].Title}");
            }
            Console.Write("> ");
            string input = Console.ReadLine() ?? string.Empty;
            int windowIndex = int.Parse(input)-1;
            cs = windows[windowIndex];
        }
        else {
            Console.WriteLine("Found CS2 window");
        }

        Scanner scanner = new(cs.Hwnd);

        var data = GetInput();
        scanner.SetData(data);
        Console.WriteLine("First scan started");
        scanner.FirstScan();
        Console.WriteLine($"Matches: {scanner.Count(0):0000} / {scanner.Count(1):0000} / {scanner.Count(2):0000}\n" +
                          $"         {scanner.Count(3):0000} / {scanner.Count(4):0000} / {scanner.Count(5):0000}");

        while (!IsResolved(scanner)) {
            data = GetInput();
            Console.WriteLine("Starting scan");
            scanner.NextScan();
            Console.WriteLine($"Matches: {scanner.Count(0):0000} / {scanner.Count(1):0000} / {scanner.Count(2):0000}\n" +
                              $"         {scanner.Count(3):0000} / {scanner.Count(4):0000} / {scanner.Count(5):0000}");
        }

        Console.WriteLine(
            $"""
            Addresses: 
                Position: 
                    X: {scanner.GetMatches(0).First()}
                    Y: {scanner.GetMatches(1).First()} 
                    Z: {scanner.GetMatches(2).First()}
                Rotation:
                    X: {scanner.GetMatches(3).First()}
                    Y: {scanner.GetMatches(4).First()}
                    Z: {scanner.GetMatches(5).First()}
            """
            );
    }

    private static bool IsResolved(Scanner scanner) {
        for (int i = 0; i < 6; i++) {
            int count = scanner.Count(i);
            if (count != 1) {
                return false;
            }
        }

        return true;
    }

    private static (int, int, int, int, int, int) GetInput() {
        while (true) {
            Console.WriteLine("Type Position and Rotation as 6 integers");
            Console.WriteLine("> ");
            string[] positions = (Console.ReadLine() ?? string.Empty).Split(' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (positions.Length < 6) {
                Console.WriteLine("Less than 6 values. Try Again");
                continue;
            }
            List<int> values = positions.Select(int.Parse).ToList();
            return (values[0], values[1], values[2], values[3], values[4], values[5]);
        }
    }
}