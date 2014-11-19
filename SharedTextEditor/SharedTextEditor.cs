using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SharedTextEditor
{
    public partial class SharedTextEditor : Form, ISharedTextEditor
    {
        #region Instance Fields
        //the chat member name
        private string memberName;
        //the channel instance where we execute our service methods against
        private ISharedTextEditorChannel participant;
        //the instance context which in this case is our window since it is the service host
        private InstanceContext instanceContext;
        //our binding transport for the p2p mesh
        private NetPeerTcpBinding binding;
        //the factory to create our chat channel
        private ChannelFactory<ISharedTextEditorChannel> channelFactory;
        //an interface provided by the channel exposing events to indicate
        //when we have connected or disconnected from the mesh
        private IOnlineStatus statusHandler;
        //a generic delegate to execute a thread against that accepts no args
        private delegate void NoArgDelegate();
        private HashSet<string> connectedMembers = new HashSet<string>();
        private String document;
        #endregion

        delegate void SharedTextEditorDelegate(string member);

        public SharedTextEditor()
        {
            InitializeComponent();
        }

        ~SharedTextEditor() {
            participant.Dispose();
        }
          
        #region WCF Methods
        //this method gets called from a background thread to 
        //connect the service client to the p2p mesh specified
        //by the binding info in the app.config
        private void ConnectToMesh()
        {
            //since this window is the service behavior use it as the instance context
            instanceContext = new InstanceContext(this);

            //use the binding from the app.config with default settings
            binding = new NetPeerTcpBinding("SharedTextEditorBinding");

            //create a new channel based off of our composite interface "IChatChannel" and the 
            //endpoint specified in the app.config
            channelFactory = new DuplexChannelFactory<ISharedTextEditorChannel>(instanceContext, "SharedTextEditorEndpoint");
            participant = channelFactory.CreateChannel();

            //the next 3 lines setup the event handlers for handling online/offline events
            //in the MS P2P world, online/offline is defined as follows:
            //Online: the client is connected to one or more peers in the mesh
            //Offline: the client is all alone in the mesh
            statusHandler = participant.GetProperty<IOnlineStatus>();
            statusHandler.Online += new EventHandler(ostat_Online);
            statusHandler.Offline += new EventHandler(ostat_Offline);

            //this is an empty unhandled method on the service interface.
            //why? because for some reason p2p clients don't try to connect to the mesh
            //until the first service method call.  so to facilitate connecting i call this method
            //to get the ball rolling.
            participant.InitializeMesh();
        }
        #endregion

        #region IOnlineStatus Event Handlers
        void ostat_Offline(object sender, EventArgs e)
        {
            // we could update a status bar or animate an icon to 
            //indicate to the user they have disconnected from the mesh

            //currently i don't have a "disconnect" button but adding it
            //should be trivial if you understand the rest of this code
            int i = 0;
        }

        void ostat_Online(object sender, EventArgs e)
        {
        
            //here we retrieve the chat member name
            memberName ="Test name";

            //updating the UI to show the chat window
            //this.grdLogin.Visibility = Visibility.Collapsed;
            //this.grdChat.Visibility = Visibility.Visible;
            //((Storyboard)this.Resources["OnJoinMesh"]).Begin(this);
            //this.lblConnectionStatus.Content = "Welcome to the chat room!";
            //((Storyboard)this.Resources["HideConnectStatus"]).Begin(this);

            //broadcasting a join method call to the mesh members
            participant.Connect(memberName);
        }
        #endregion

        #region IChat Members
        
        public void Connect(string member)
        {
            connectedMembers.Add(member);
            UpdateNumberOfEditors();

            //TODO 
            //participant.SynchronizeDocument()
            participant.SynchronizeMemberList(memberName);
        
        }

        public void Chat(string member, string message)
        {

            //TODO rstoll diff,match,patch

            if (InvokeRequired)
            {
                this.BeginInvoke(new SharedTextEditorDelegate(Connect));
                return;
            }

            txtDoc.Text = message;
          
        }

        public void InitializeMesh()
        {
            //do nothing
        }

        public void Disconnect(string member)
        {
            connectedMembers.Remove(member);
            UpdateNumberOfEditors();
        }

        public void UpdateNumberOfEditors(){
            if (InvokeRequired)
            {
                BeginInvoke(new NoArgDelegate(UpdateNumberOfEditors));
                return;
            }

            lblNumber.Text = connectedMembers.Count.ToString();
        }


        public void SynchronizeMemberList(string member)
        {
            connectedMembers.Add(member);
            UpdateNumberOfEditors();
        }
        #endregion

        private void SendMessage(string message)
        {
            if (!String.IsNullOrEmpty(message))
            {
                participant.Chat(memberName, message);
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            //todo document id 

            NoArgDelegate executor = new NoArgDelegate(this.ConnectToMesh);
            executor.BeginInvoke(null, null);
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            //TODO automatic send
            SendMessage(txtDoc.Text);
        }
    }
}
