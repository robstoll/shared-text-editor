using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SharedTextEditor
{


    #region Service Interfaces

    [ServiceContract(Namespace = "http://com.sharedtexteditor", CallbackContract = typeof(ISharedTextEditor))]
    public interface ISharedTextEditor
    {
        [OperationContract(IsOneWay = true)]
        void Connect(string member);

        [OperationContract(IsOneWay = true)]
        void Chat(string member, string message);

        [OperationContract(IsOneWay = true)]
        void Disconnect(string member);

        [OperationContract(IsOneWay = true)]
        void InitializeMesh();

        [OperationContract(IsOneWay = true)]
        void SynchronizeMemberList(string member);
    }

    public interface ISharedTextEditorChannel : ISharedTextEditor, IClientChannel
    {
    }
    #endregion

    static class Program
    {

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SharedTextEditor());
        }
    }
}
