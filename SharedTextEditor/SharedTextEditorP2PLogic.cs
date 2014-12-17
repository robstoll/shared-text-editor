using DiffMatchPatch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharedTextEditor
{
    class SharedTextEditorP2PLogic : ISharedTextEditorP2P
    {

        //p2p communication channel
        private ISharedTextEditorP2PChannel _p2pChannel;
    
        private InstanceContext _instanceContext;
        private NetPeerTcpBinding _binding;
        private ChannelFactory<ISharedTextEditorP2PChannel> _channelFactory;
        private IOnlineStatus _statusHandler;

        private readonly ISharedTextEditorC2S _clientService;

        public delegate void NoArgDelegate();
        public delegate void StringDelegate(string value);
        
        private readonly string _memberName;
 
        private readonly SharedTextEditor _editor;

        private readonly string _c2shost;


        public SharedTextEditorP2PLogic(string memberName,  SharedTextEditor editor, ISharedTextEditorC2S clientService, string c2sHost)
        {
            _memberName = memberName;
         
            _editor = editor;
            _editor.ConnectToP2P += Editor_ConnectToP2P;
            _editor.FindDocumentRequest += Editor_FindDocumentRequest;
            _clientService = clientService;
            _c2shost = c2sHost;
        }

        private void Editor_ConnectToP2P(object sender, EventArgs e)
        {
            ConnectToMesh();
        }

        private void Editor_FindDocumentRequest(object sender, string documentId)
        {
            _p2pChannel.FindDocument(_c2shost, documentId, _memberName);
        }

        private void ConnectToMesh()
        {
            try
            {
                _instanceContext = new InstanceContext(this);

                _binding = new NetPeerTcpBinding("SharedTextEditorBinding");

                _channelFactory = new DuplexChannelFactory<ISharedTextEditorP2PChannel>(_instanceContext,
                    "SharedTextEditorEndpointP2P");

                var endpointAddress = new EndpointAddress(_channelFactory.Endpoint.Address.ToString());

                _channelFactory.Endpoint.Address = endpointAddress;
                _p2pChannel = _channelFactory.CreateChannel();

                //setup the event handlers for handling online/offline events
                _statusHandler = _p2pChannel.GetProperty<IOnlineStatus>();

                if (_statusHandler != null)
                {
                    _statusHandler.Online += ostat_Online;
                    _statusHandler.Offline += ostat_Offline;
                }

                //call empty method to force connection to mesh
                _p2pChannel.InitializeMesh();
                _editor.UpdateConnectionState(true);
            }
            catch (Exception)
            {
                _editor.UpdateConnectionState(false);
            }
        }

        private void ostat_Offline(object sender, EventArgs e)
        {
            Console.WriteLine("P2P member went offline");
        }

        private void ostat_Online(object sender, EventArgs e)
        {
            Console.WriteLine("P2P member came online");
        }

        public void InitializeMesh()
        {
            Console.WriteLine("initialize mesh");
        }

        public void FindDocument(string host, string documentId, string memberName)
        {
            if (host.Equals(_c2shost))
            {
                return;
            }
            //using client/server communication to send document to given memberName
            _clientService.FindDocument(host, documentId, memberName);
            
        }
    }
}
