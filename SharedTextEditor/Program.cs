using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SharedTextEditor
{

    static class Program
    {

        

        [STAThread]
        static void Main(string[] args)
        {
            var memberName = Guid.NewGuid().ToString();
            if (args.Length == 1)
            {
                memberName = args[0];
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var serverPort = GetRandomPortForServer();

            var editor = new SharedTextEditor(memberName);

            var patchingClientLogic = new SharedTextEditorPatchingLogic(memberName, ServiceHostEndpoint(serverPort), editor);

            new SharedTextEditorP2PLogic(memberName, editor, patchingClientLogic, ServiceHostEndpoint(serverPort));


            StartServerHost(serverPort, memberName, editor, patchingClientLogic);

            Application.Run(editor);         
        }


        private static void StartServerHost(int port,  string memberName, SharedTextEditor editor, SharedTextEditorPatchingLogic patchingLogic)
        {
           // var patchingService = new SharedTextEditorPatchingLogic(memberName, ServiceHostEndpoint(port), editor);

            var serviceHost = ServiceHostAddress(port);
            var serviceUrl = new Uri(serviceHost);

            var serviceAddress = ServiceHostEndpoint(port);

            var host = new ServiceHost(patchingLogic, serviceUrl);
            
               // host.Description.Behaviors.Add(new ServiceMetadataBehavior { HttpGetEnabled = true });
                host.Description.Behaviors.Find<ServiceDebugBehavior>().IncludeExceptionDetailInFaults = true;
                host.AddServiceEndpoint(typeof(ISharedTextEditorC2S), new NetTcpBinding(), serviceAddress);
                host.OpenTimeout = new TimeSpan(10000);
                
                host.Closed += (sender, args) =>
                {
                    Console.WriteLine("host closed");
                };

                host.Faulted += (sender, args) =>
                {
                    Console.WriteLine("host faulted");
                };
                
                host.Open();
               
                Console.WriteLine("Service up and running at:");
                foreach (var ea in host.Description.Endpoints)
                {
                    Console.WriteLine(ea.Address);
                }
      
        }


        private static string ServiceHostAddress(int port )
        {
           return "net.tcp://localhost:" + port; 
        }

        private static string ServiceHostEndpoint(int port)
        {
            return ServiceHostAddress(port) + "/SharedTextEditor";
        }


        private static int GetRandomPortForServer()
        {
            var random = new Random();
            return random.Next(4000, 6000);
        }
    }
}
