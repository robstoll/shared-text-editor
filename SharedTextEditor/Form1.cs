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
    public partial class Form1 : Form, IChat
    {
        #region Instance Fields
        //the chat member name
        private string m_Member;
        //the channel instance where we execute our service methods against
        private IChatChannel m_participant;
        //the instance context which in this case is our window since it is the service host
        private InstanceContext m_site;
        //our binding transport for the p2p mesh
        private NetPeerTcpBinding m_binding;
        //the factory to create our chat channel
        private ChannelFactory<IChatChannel> m_channelFactory;
        //an interface provided by the channel exposing events to indicate
        //when we have connected or disconnected from the mesh
        private IOnlineStatus o_statusHandler;
        //a generic delegate to execute a thread against that accepts no args
        private delegate void NoArgDelegate();
        #endregion


        public Form1()
        {
            InitializeComponent();

            NoArgDelegate executor = new NoArgDelegate(this.ConnectToMesh);
            executor.BeginInvoke(null, null);
        }

          
        #region WCF Methods
        //this method gets called from a background thread to 
        //connect the service client to the p2p mesh specified
        //by the binding info in the app.config
        private void ConnectToMesh()
        {
            //since this window is the service behavior use it as the instance context
            m_site = new InstanceContext(this);

            //use the binding from the app.config with default settings
            m_binding = new NetPeerTcpBinding("SharedTextEditorBinding");

            //create a new channel based off of our composite interface "IChatChannel" and the 
            //endpoint specified in the app.config
            m_channelFactory = new DuplexChannelFactory<IChatChannel>(m_site, "SharedTextEdtiroEndpoint");
            m_participant = m_channelFactory.CreateChannel();


            //the next 3 lines setup the event handlers for handling online/offline events
            //in the MS P2P world, online/offline is defined as follows:
            //Online: the client is connected to one or more peers in the mesh
            //Offline: the client is all alone in the mesh
            o_statusHandler = m_participant.GetProperty<IOnlineStatus>();
            o_statusHandler.Online += new EventHandler(ostat_Online);
            o_statusHandler.Offline += new EventHandler(ostat_Offline);

            //this is an empty unhandled method on the service interface.
            //why? because for some reason p2p clients don't try to connect to the mesh
            //until the first service method call.  so to facilitate connecting i call this method
            //to get the ball rolling.
            m_participant.InitializeMesh();
        }
        #endregion

        #region IOnlineStatus Event Handlers
        void ostat_Offline(object sender, EventArgs e)
        {
            // we could update a status bar or animate an icon to 
            //indicate to the user they have disconnected from the mesh

            //currently i don't have a "disconnect" button but adding it
            //should be trivial if you understand the rest of this code
        }

        void ostat_Online(object sender, EventArgs e)
        {
            //because this event handler is called from a worker thread
            //we need to use the dispatcher to sync it with the UI thread.
            //below illustrates how and is used throughout the code.
            //note that a generic handler could be used to prevent having to recode the delegate
            //each time but i didn't bother since the app is so small at this time.
        
                    //here we retrieve the chat member name
                    m_Member ="Test name";

                    //updating the UI to show the chat window
                    //this.grdLogin.Visibility = Visibility.Collapsed;
                    //this.grdChat.Visibility = Visibility.Visible;
                    //((Storyboard)this.Resources["OnJoinMesh"]).Begin(this);
                    //this.lblConnectionStatus.Content = "Welcome to the chat room!";
                    //((Storyboard)this.Resources["HideConnectStatus"]).Begin(this);


                    //broadcasting a join method call to the mesh members
                    m_participant.Join(m_Member);


        }
        #endregion

        #region IChat Members

        public void Join(string Member)
        {
            //again we need to sync the worker thread with the UI thread via Dispatcher
   
                    //add the joined member to the chatroom
                    //this.lstChatMsgs.Items.Add(Member + " joined the chatroom.");
                     Console.WriteLine(Member + " joined the chatroom.");
                    //this will retrieve any new members that have joined before the current user
                    m_participant.SynchronizeMemberList(m_Member);
        
        }

        public void Chat(string Member, string Message)
        {
            //again we need to sync the worker thread with the UI thread via Dispatcher
   
                    //we simply add the chat message to the listbox
                   // this.lstChatMsgs.Items.Add(Member + " says: " + Message);

                    Console.WriteLine(Member + " says: " + Message);
          
        }

        //again we need to sync the worker thread with the UI thread via Dispatcher
        public void Whisper(string Member, string MemberTo, string Message)
        {
       
                    //this is a rudimentary form of whisper and is flawed so should NOT be used in production.
                    //this method simply checks the sender and to address and only displays the message
                    //if it belongs to this member, however! - if there are N members with the same name
                    //they will all be whispered to from the sender since the message is broadcast to everybody.
                    //the correct way to implement this would
                    //be to instead retrieve the peer name from the mesh for the member you want to whisper to
                    //and send the message directly to that peer node via the mesh.  i may update the code to do 
                    //that in the future but for now i'm too busy with other things to mess with it hence it's
                    //left as an exercise for the reader.  good luck! ;-)
                    if (m_Member.Equals(Member) || m_Member.Equals(MemberTo))
                    {
                        //we simply add the whisper message to the listbox
                       // this.lstChatMsgs.Items.Add(Member + " whispers: " + Message);
                        Console.WriteLine(Member + " whispers: " + Message);
                    }
      
        }

        public void InitializeMesh()
        {
            //do nothing
        }

        public void Leave(string Member)
        {
            //again we need to sync the worker thread with the UI thread via Dispatcher
    
                    //notify that the user has left
                   // this.lstChatMsgs.Items.Add(Member + " left the chatroom.");
            Console.WriteLine(Member + " left the chatroom.");
    
        }

        public void SynchronizeMemberList(string Member)
        {
            //again we need to sync the worker thread with the UI thread via Dispatcher
      
                    //as member names come in we simply disregard duplicates and 
                    //add them to the member list, this way we can retrieve a list
                    //of members already in the chatroom when we enter at any time.

                    //again, since this is just an example this is the simplified
                    //way to do things.  the correct way would be to retrieve a list
                    //of peernames and retrieve the metadata from each one which would
                    //tell us what the member name is and add it.  we would want to check
                    //this list when we join the mesh to make sure our member name doesn't 
                    //conflict with someone else
                   // if (!this.lstMembers.Items.Contains(Member))
                   // {
                   //     this.lstMembers.Items.Add(Member);
                   // }
           
        }

        #endregion

        private void SendMessage(string message)
        {

            if (!String.IsNullOrEmpty(message))
            {
                m_participant.Chat(m_Member, message);
            
            }
        }
    }
}
