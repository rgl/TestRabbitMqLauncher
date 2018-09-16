using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace TestRabbitMqLauncher
{
    public class TestRabbitMqLauncher : IDisposable
    {
        private readonly string _erlangHome;
        private readonly string _rabbitMqInstallationPath;
        private readonly string _temporaryBasePath;
        private Process _rabbitMqServer;

        public TestRabbitMqLauncher(string erlangHome, string rabbitMqInstallationPath, string temporaryBasePath)
        {
            _erlangHome = erlangHome;
            _rabbitMqInstallationPath = rabbitMqInstallationPath;
            _temporaryBasePath = temporaryBasePath;
        }

        public void Dispose()
        {
            Stop();
        }

        public void Start()
        {
            // TODO allocate 4 ports.
            var amqpPort = 1230;
            var epmdPort = 1231;
            var distPort = 1232;
            var managementPort = 1233;
            var listenIp = "127.0.0.1";
            var nodeName = string.Format("test-rmq-{0}@localhost", amqpPort);
            var basePath = Path.GetFullPath(Path.Combine(_temporaryBasePath, string.Format("test-rmq-{0}", amqpPort)));
            var rabbitMqServerBatchPath = Path.Combine(_rabbitMqInstallationPath, @"sbin\rabbitmq-server.bat");
            var erlangPath = Path.Combine(_erlangHome, @"bin\erl.exe");

            if (!File.Exists(rabbitMqServerBatchPath))
            {
                throw new ApplicationException(string.Format("rabbitmq-server.bat not found at {0}", rabbitMqServerBatchPath));
            }

            if (!File.Exists(erlangPath))
            {
                throw new ApplicationException(string.Format("erl.exe not found at {0}", erlangPath));
            }

            if (Directory.Exists(basePath))
            {
                throw new ApplicationException(string.Format("the base directory already exists {0}", basePath));
            }

            Directory.CreateDirectory(basePath);

            File.WriteAllText(
                Path.Combine(basePath, "enabled_plugins"),
                "[rabbitmq_management].",
                Encoding.ASCII);

            File.WriteAllText(
                Path.Combine(basePath, "rabbitmq.config"),
                // NB escape the " (double quote) with another one.
                // NB escape { with another one.
                // NB escape } with another one.
                $@"
[
    {{rabbit, [
        {{tcp_listeners, [{{""{listenIp}"", {amqpPort}}}]}},
        {{default_user, ""admin""}},
        {{default_pass, ""admin""}}
    ]}},
    {{rabbitmq_management, [
        {{listener, [
            {{ip, ""{listenIp}""}},
            {{port, {managementPort}}}
        ]}}
    ]}},
    {{kernel, [
        {{inet_dist_use_interface, {{{listenIp.Replace('.', ',')}}}}},
        {{inet_dist_listen_min, {distPort}}},
        {{inet_dist_listen_max, {distPort}}}
    ]}}
].
", Encoding.ASCII);

            // TODO Process does not support setting the CREATE_NEW_PROCESS_GROUP flag... so we might need to use pinvoke in order to be able to call GenerateConsoleCtrlEvent on the process group.
            var p = new Process
            {
                StartInfo =
                    {
                        FileName = EscapeProcessArgument(rabbitMqServerBatchPath),
                        RedirectStandardInput = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    },
            };

            var environment = p.StartInfo.Environment;

            foreach (var k in environment.Keys.Where(k => k.StartsWith("RABBITMQ_") || k.StartsWith("ERLANG_") || k.StartsWith("ERL_")).ToArray())
            {
                environment.Remove(k);
            }

            environment.Add("ERLANG_HOME", _erlangHome);
            environment.Add("RABBITMQ_BASE", basePath);
            environment.Add("RABBITMQ_NODENAME", nodeName);

            //// configure epmd.
            //// XXX maybe we should not deal with epmd? just leave it running... and only deal with erl.exe.
            //environment.Add("ERL_EPMD_ADDRESS", listenIp);
            //environment.Add("ERL_EPMD_PORT", epmdPort.ToString(CultureInfo.InvariantCulture));
            ////environment.Add("RABBITMQ_SERVER_ADDITIONAL_ERL_ARGS", "-setcookie test -hidden");
            //// set HOMEDRIVE HOMEPATH for making sure the .erlang.cookie file is in our temporary base directory.
            //environment["HOMEDRIVE"] = basePath.Substring(0, 2);
            //environment["HOMEPATH"] = basePath.Substring(2);

            p.OutputDataReceived += (sendingProcess, e) => Console.WriteLine("{0}: {1}", nodeName, e.Data);
            p.ErrorDataReceived += (sendingProcess, e) => Console.Error.WriteLine("{0}: {1}", nodeName, e.Data);

            if (!p.Start())
            {
                throw new ApplicationException(string.Format("Failed to launch tool {0}", p.StartInfo.FileName));
            }

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            p.WaitForExit();

            Console.WriteLine("Exit Code {0}", p.ExitCode);
        }

        public void Stop()
        {
            // kill the erl.exe that descends from the launched rabbitmq-server.bat
        }

        private static string EscapeProcessArguments(string[] arguments)
        {
            var sb = new StringBuilder();
            foreach (var argument in arguments)
            {
                if (sb.Length > 0)
                    sb.Append(' ');
                EscapeProcessArgument(argument, sb);
            }
            return sb.ToString();
        }

        private static string EscapeProcessArgument(string argument)
        {
            var sb = new StringBuilder();
            EscapeProcessArgument(argument, sb);
            return sb.ToString();
        }

        private static void EscapeProcessArgument(string argument, StringBuilder sb)
        {
            // Normally, an Windows application (.NET applications too) parses
            // their command line using the CommandLineToArgvW function. Which has
            // some peculiar rules.
            // See http://msdn.microsoft.com/en-us/library/bb776391(VS.85).aspx

            // TODO how about backslashes? there seems to be a weird interaction
            //      between backslahses and double quotes...
            // TODO do test cases of this! even launch a second process that
            //      only dumps its arguments.

            if (argument.Contains('"'))
            {
                sb.Append('"');
                // escape single double quotes with another double quote.
                sb.Append(argument.Replace("\"", "\"\""));
                sb.Append('"');
            }
            else if (argument.Contains(' ')) // AND it does NOT contain double quotes! (those were catched in the previous test)
            {
                sb.Append('"');
                sb.Append(argument);
                sb.Append('"');
            }
            else
            {
                sb.Append(argument);
            }

            // TODO what about null/empty arguments?
        }
    }
}
