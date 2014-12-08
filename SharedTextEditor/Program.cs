using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SharedTextEditor
{

    static class Program
    {

        

        [STAThread]
        static void Main(string[] args)
        {
            var memberName = new Guid().ToString();
            if (args.Length == 1)
            {
                memberName = args[0];
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var serverPort = GetRandomPortForServer();

            var editor = new SharedTextEditor(memberName);
            var patchingLogic = new SharedTextEditorPatchingLogic(memberName, editor);

            new SharedTextEditorP2PLogic(memberName, editor, patchingLogic, ServiceHostAddress(serverPort));


            StartServerHost(serverPort, memberName, editor, patchingLogic);

            Application.Run(editor);         
        }


        private static void StartServerHost(int port,  string memberName, SharedTextEditor editor, SharedTextEditorPatchingLogic patchingLogic)
        {
            var patchingService = new SharedTextEditorPatchingLogic(memberName, editor);

            var serviceHost = ServiceHostAddress(port);
            var serviceUrl = new Uri(serviceHost);

            var serviceAddress = serviceHost + "/SharedTextEditor";

            using (var host = new ServiceHost(patchingLogic, serviceUrl))
            {
                host.AddServiceEndpoint(typeof(ISharedTextEditorC2S), new NetTcpBinding(), serviceAddress);
                host.AddServiceEndpoint(typeof(ISharedTextEditorC2S), new BasicHttpBinding(), "http://localhost/SharedTextEditorTest");
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
            };
        }


        private static string ServiceHostAddress(int port )
        {
           return "net.tcp://127.0.0.1:" + port; 
        }


        private static int GetRandomPortForServer()
        {
            var random = new Random();
            return random.Next(4000, 6000);
        }
    }
}
