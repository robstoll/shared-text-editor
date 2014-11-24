using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Linq;

namespace SharedTextEditor
{
    public partial class SharedTextEditor : Form
    {
        private readonly Dictionary<string, TextBox> _textBoxes = new Dictionary<string, TextBox>();
        private readonly Dictionary<string, TabPage> _tabPages = new Dictionary<string, TabPage>();

        private readonly string _memberName;
        private bool _connected;
        

        public SharedTextEditor(string memberName)
        {
            InitializeComponent();
            _memberName = memberName;
        }


        public void MemberHasDocument(string documentId, string memberName)
        {
            //TODO 
        }

        private delegate void IntDelegate(int number);
        public void UpdateNumberOfEditors(int number)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new IntDelegate(UpdateNumberOfEditors));
                return;
            }

            lblNumber.Text = number.ToString();
        }

        private delegate void UpdateTextDelegate(string documentId, string content);
        public void UpdateText(string documentId, string content)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new UpdateTextDelegate(UpdateText));
                return;
            }

            _textBoxes[documentId].Text = content;
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if(!_connected)
            {
                _connected = true;
                if (ConnectToP2P != null)
                {
                    ConnectToP2P(this, EventArgs.Empty);
                }
                txtId.Enabled = true;
                btnOpen.Enabled = true;
                btnCreate.Enabled = true;
                btnConnect.Text = "Disconnect";
            }
            else
            {
                _connected = false;
                if (DisconnectFromP2P != null)
                {
                    DisconnectFromP2P(this, EventArgs.Empty);
                }
                btnConnect.Text = "Connect";
                btnCreate.Enabled = false;
                btnOpen.Enabled = false;
                txtId.Enabled = false;
            }
        }

        private void SendMessage(string documentId, string text)
        {
            if (UpdateDocument != null)
            {
                UpdateDocument(this, new UpdateDocumentRequest
                {
                    DocumentId = documentId,
                    NewContent = text
                });
            }
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            var ok = true;
            var documentId = txtId.Text;
            if (_textBoxes.ContainsKey(documentId))
            {
                //TODO ask if current document shall be closed
                if (ok)
                {
                    tabControl.TabPages.Remove(_tabPages[documentId]);
                    _tabPages.Remove(documentId);
                    _textBoxes.Remove(documentId);

                    if (RemoveDocument != null)
                    {
                        RemoveDocument(this, documentId);
                    }
                }
            }

            if (ok)
            {
                if (CreateDocument != null)
                {
                    CreateDocument(this, documentId);
                }
                OpenTab(documentId);
            }
        }

        private void OpenTab(string documentId)
        {
            var tabPage = new TabPage(documentId)
            {
                Name = documentId, 
                Text = documentId,
            };
            tabControl.Controls.Add(tabPage);
            tabControl.SelectedTab = tabPage;

            var textBox = new TextBox();
            textBox.TextChanged += (object sender, EventArgs e) => SendMessage(documentId, textBox.Text);
            textBox.Multiline = true;
            textBox.Dock = DockStyle.Fill;
            tabPage.Controls.Add(textBox);

            _textBoxes.Add(documentId, textBox);
            _tabPages.Add(documentId, tabPage);
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            var documentId = txtId.Text;
            var tabPage = new TabPage(documentId);
            tabPage.Container.Add(new Label
            {
                Text = "Searching document with id " + documentId + ".\nPlease be patient..."
            });
            _tabPages.Add(documentId, tabPage);

            if (FindDocumentRequest != null)
            {
                FindDocumentRequest(this, documentId);
            }
        }

        public void CloseTab(string documentId)
        {
            tabControl.TabPages.Remove(_tabPages[documentId]);
        }

        public string GetText(string documentId)
        {
            if (_textBoxes.ContainsKey(documentId))
            {
                return _textBoxes[documentId].Text;
            }
            return null;
        }

        public event EventHandler<EventArgs> ConnectToP2P;
        public event EventHandler<EventArgs> DisconnectFromP2P;
        public event EventHandler<string> FindDocumentRequest;
        public event EventHandler<string> CreateDocument;
        public event EventHandler<string> RemoveDocument;
        public event EventHandler<UpdateDocumentRequest> UpdateDocument;

    }

    public class UpdateDocumentRequest
    {
        public string DocumentId { get; set; }
        public string NewContent { get; set; }
    }

}