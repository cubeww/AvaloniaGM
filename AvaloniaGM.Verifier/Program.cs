using Avalonia;

AppBuilder.Configure<AvaloniaGM.App>()
    .UsePlatformDetect()
    .WithInterFont()
    .SetupWithoutStarting();

Console.WriteLine("AvaloniaGM.Verifier is ready.");
Console.WriteLine("Write ad-hoc verification code in Program.cs when needed.");
