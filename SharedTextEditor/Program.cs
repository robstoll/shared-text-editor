using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SharedTextEditor
{


    #region Service Interfaces

    [ServiceContract(Namespace = "http://com.sharedtexteditor", CallbackContract = typeof(IChat))]
    public interface IChat
    {
        [OperationContract(IsOneWay = true)]
        void Join(string Member);

        [OperationContract(IsOneWay = true)]
        void Chat(string Member, string Message);

        [OperationContract(IsOneWay = true)]
        void Whisper(string Member, string MemberTo, string Message);

        [OperationContract(IsOneWay = true)]
        void Leave(string Member);

        [OperationContract(IsOneWay = true)]
        void InitializeMesh();

        [OperationContract(IsOneWay = true)]
        void SynchronizeMemberList(string Member);
    }

    //this channel interface provides a multiple inheritence adapter for our channel factory
    //that aggregates the two interfaces need to create the channel
    public interface IChatChannel : IChat, IClientChannel
    {
    }

    #endregion

    static class Program
    {


        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());

           
        }

     
    }
}
