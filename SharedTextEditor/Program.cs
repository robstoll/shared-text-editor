using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SharedTextEditor
{

    static class Program
    {

        

        [STAThread]
        static void Main(string[] args)
        {
            var memberName = GetMemberName(args);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var serverPort = GetRandomPortForServer();

            var editor = new SharedTextEditor(memberName);

            var patchingClientLogic = new SharedTextEditorPatchingLogic(memberName, ServiceHostEndpoint(serverPort), editor, new ClientServerCommunication());

            new SharedTextEditorP2PLogic(memberName, editor, patchingClientLogic, ServiceHostEndpoint(serverPort));

            StartServerHost(serverPort, memberName, editor, patchingClientLogic);

            Application.Run(editor);         
        }


        private static void StartServerHost(int port,  string memberName, SharedTextEditor editor, SharedTextEditorPatchingLogic patchingLogic)
        {
            var serviceHost = ServiceHostAddress(port);
            var serviceUrl = new Uri(serviceHost);

            var serviceAddress = ServiceHostEndpoint(port);

            var host = new ServiceHost(patchingLogic, serviceUrl);
            
                host.Description.Behaviors.Find<ServiceDebugBehavior>().IncludeExceptionDetailInFaults = true;
                host.AddServiceEndpoint(typeof(ISharedTextEditorC2S), new BasicHttpBinding(), serviceAddress);
                host.OpenTimeout = new TimeSpan(10000);
                
                host.Closed += (sender, args) =>
                {
                    Console.WriteLine("host closed");
                };

                host.Faulted += (sender, args) =>
                {
                    Console.WriteLine("host faulted");
                };


                try
                {
                    host.Open();

                    Console.WriteLine("Service up and running at:");
                    foreach (var ea in host.Description.Endpoints)
                    {
                        Console.WriteLine(ea.Address);
                    }
                }
                catch (AddressAlreadyInUseException){
                    MessageBox.Show(
                      "Unable to start service on " + port + " because the port is already in use",
                      "Port already taken",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    Application.Exit();
                }
        }


        private static string ServiceHostAddress(int port )
        {
           return "http://" + ServerIp() + ":" + port; 
        }

        private static string ServiceHostEndpoint(int port)
        {
            return ServiceHostAddress(port) + "/SharedTextEditor";
        }


        private static IPAddress ServerIp()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            return host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        }

        private static int GetRandomPortForServer()
        {
            var random = new Random();
            return random.Next(9000, 9010);
        }

        private static string GetMemberName(string[] args )
        {
            return args.Length == 1 ? args[0] : Guid.NewGuid().ToString();
        }
    }
}
