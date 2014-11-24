using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace SharedTextEditor
{
    class SharedTextEditorP2PLogic : ISharedTextEditorP2P
    {

        //the channel instance where we execute our service methods against
        private ISharedTextEditorP2PChannel _participant;
        //the instance context which in this case is our window since it is the service host
        private InstanceContext _instanceContext;
        //our binding transport for the p2p mesh
        private NetPeerTcpBinding _binding;
        //the factory to create our chat channel
        private ChannelFactory<ISharedTextEditorP2PChannel> _channelFactory;
        //an interface provided by the channel exposing events to indicate
        //when we have _connected or disconnected from the mesh
        private IOnlineStatus _statusHandler;
        //a generic delegate to execute a thread against that accepts no args

        private readonly ISharedTextEditorC2S _clientService;

        public delegate void NoArgDelegate();
        public delegate void StringDelegate(string value);

        private readonly HashSet<string> _connectedMembers = new HashSet<string>();

        private readonly string _memberName;
        private readonly SharedTextEditor _editor;

        public SharedTextEditorP2PLogic(string memberName, SharedTextEditor editor, ISharedTextEditorC2S clientService)
        {
            _memberName = memberName;
            _editor = editor;
            _editor.ConnectToP2P += Editor_ConnectToP2P;
            _editor.DisconnectFromP2P += Editor_DisconnectFromP2P;
            _editor.FindDocumentRequest += Editor_FindDocumentRequest;
            _clientService = clientService;
        }

        private void Editor_ConnectToP2P(object sender, EventArgs e)
        {
            NoArgDelegate executor = ConnectToMesh;
            executor.BeginInvoke(null, null);
        }

        private void Editor_DisconnectFromP2P(object sender, EventArgs e)
        {
            _participant.Disconnect(_memberName);
        }

        private void Editor_FindDocumentRequest(object sender, string documentId)
        {
            _participant.FindDocument(documentId);
        }

        //this method gets called from a background thread to 
        //connect the service client to the p2p mesh specified
        //by the binding info in the app.config
        private void ConnectToMesh()
        {

            //since this window is the service behavior use it as the instance context
            _instanceContext = new InstanceContext(this);

            //use the binding from the app.config with default settings
            _binding = new NetPeerTcpBinding("SharedTextEditorBinding");

            //create a new channel based off of our composite interface "IChatChannel" and the 
            //endpoint specified in the app.config
            _channelFactory = new DuplexChannelFactory<ISharedTextEditorP2PChannel>(_instanceContext,
                "SharedTextEditorEndpointP2P");
            _participant = _channelFactory.CreateChannel();

            //the next 3 lines setup the event handlers for handling online/offline events
            //in the MS P2P world, online/offline is defined as follows:
            //Online: the client is _connected to one or more peers in the mesh
            //Offline: the client is all alone in the mesh
            _statusHandler = _participant.GetProperty<IOnlineStatus>();
            _statusHandler.Online += new EventHandler(ostat_Online);
            _statusHandler.Offline += new EventHandler(ostat_Offline);

            //this is an empty unhandled method on the service interface.
            //why? because for some reason p2p clients don't try to connect to the mesh
            //until the first service method call.  so to facilitate connecting i call this method
            //to get the ball rolling.
            _participant.InitializeMesh();
        }

        private void ostat_Offline(object sender, EventArgs e)
        {
            // we could update a status bar or animate an icon to 
            //indicate to the user they have disconnected from the mesh
            //TODO update Editor? Number of editors?
        }

        private void ostat_Online(object sender, EventArgs e)
        {
            //TODO how to distinguish which members are editing a certain document?

            //broadcasting a join method call to the mesh members
            _participant.Connect(_memberName);
        }

        public void Connect(string member)
        {
            _connectedMembers.Add(member);
            _editor.UpdateNumberOfEditors(_connectedMembers.Count);
            _participant.SynchronizeMemberList(_memberName);
        }

        public void Disconnect(string member)
        {
            _connectedMembers.Remove(member);
            _editor.UpdateNumberOfEditors(_connectedMembers.Count);
        }

        public void InitializeMesh()
        {
            //do nothing
        }

        public void SynchronizeMemberList(string member)
        {
            _connectedMembers.Add(member);
            _editor.UpdateNumberOfEditors(_connectedMembers.Count);
        }

        public void FindDocument(string documentId)
        {
            _clientService.FindDocument(documentId);
        }
    }
}
