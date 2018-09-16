using Serilog;

namespace TestRabbitMqLauncher
{
    class Program
    {
        static void Main(string[] args)
        {
            ConfigureSerilog();

            const string erlangHome = @"C:\Program Files\erl10.0.1";
            const string rabbitMqhome = @"C:\vagrant\Projects\RabbitMqLauncher\rabbitmq-server-3.7.7";

            using (var rmqOne = new TestRabbitMqLauncher(erlangHome, rabbitMqhome, "test"))
            {
                rmqOne.Start();
            }
        }

        private static void ConfigureSerilog()
        {
            var configuration = new LoggerConfiguration()
                .ReadFrom.AppSettings();

            Log.Logger = configuration.CreateLogger();
        }
    }
}
