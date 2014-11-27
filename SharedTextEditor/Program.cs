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

            var editor = new SharedTextEditor(memberName);
            //var patchingLogic = new SharedTextEditorPatchingLogic(memberName, editor);
        
            new SharedTextEditorP2PLogic(memberName, editor, null);
            
            Application.Run(editor);
        }
    }
}
