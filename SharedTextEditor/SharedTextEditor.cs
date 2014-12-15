using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        private bool _isUpdatingEditor = false;
        private DateTime _lastUpdate;
        private DateTime _delayedUpdate;
        private const string DocumentNamePlaceholder = "Document name";

        public SharedTextEditor(string memberName)
        {
            InitializeComponent();
            _memberName = memberName;
        }

        private delegate void IntDelegate(int number);
        public void UpdateNumberOfEditors(int number)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new IntDelegate(UpdateNumberOfEditors), new object[] { number });
                return;
            }

            lblNumber.Text = number.ToString();
        }

        public void ServerUnreachable(string documentId)
        {
            var title = "Server unreachable";
            var result = MessageBox.Show(
                 "The server responsible for your document '" + documentId +
                 "' has become unreachable. Do you want to take ownership?",
                 title,
                 MessageBoxButtons.YesNo,
                 MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                if (TakeOwnershipForDocument != null)
                {
                    TakeOwnershipForDocument(this, documentId);
                }
               
                return;
            }

            result = MessageBox.Show(
                 "Do you want to try reloading the document from another server? This will close your current version of the document.",
                 title,
                 MessageBoxButtons.YesNo,
                 MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                ReloadDocument(documentId);
            }
        }

        private delegate void UpdateConnectionStateDelegate(bool connected);
      
        public void UpdateConnectionState(bool connected)
        {
             if (InvokeRequired)
            {
                BeginInvoke(new UpdateConnectionStateDelegate(UpdateConnectionState), new object[] { connected });
                return;
            }

            btnConnect.Enabled = true;
            if (connected)
            {
                txtId.Enabled = true;
                txtId.Text = DocumentNamePlaceholder;
                btnOpen.Enabled = true;
                btnCreate.Enabled = true;
                btnConnect.Text = "Disconnect";
            }
            else
            {
                MessageBox.Show(
                    "Can't connect to Mesh! Please try again..",
                    "P2P Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                btnConnect.Text = "Connect";
            }
        }

        private delegate void UpdateTextDelegate(string documentId, string content);
        public void UpdateText(string documentId, string content)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new UpdateTextDelegate(UpdateText), new object[] { documentId, content });
                return;
            }

            if (_tabPages.ContainsKey(documentId))
            {
                _isUpdatingEditor = true;
                if (_textBoxes.ContainsKey(documentId))
                {
                    _textBoxes[documentId].Text = content;     
                }
                else
                {
                    //opened new document
                    CloseTab(documentId);
                    OpenTab(documentId);
                    _textBoxes[documentId].Text = content;
                }
                _isUpdatingEditor = false;
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if(!_connected)
            {
                _connected = true;
                if (ConnectToP2P != null)
                {
                    var that = this;
                    Task.Run(()=>ConnectToP2P(that, EventArgs.Empty));
                }
          
                btnConnect.Enabled = false;
                btnConnect.Text = "Connecting";
            }
            else
            {
                _connected = false;
                if (DisconnectFromP2P != null)
                {
                    Task.Run(()=>DisconnectFromP2P(this, EventArgs.Empty));
                }
                btnConnect.Text = "Connect";
                btnCreate.Enabled = false;
                btnOpen.Enabled = false;
                txtId.Enabled = false;
            }
        }

        private void SendMessage(string documentId)
        {
            if (_lastUpdate <= DateTime.Now.AddMilliseconds(-150))
            {
                SendMessageIfNotUpdating(documentId);
            }
            else if (_lastUpdate > _delayedUpdate)
            {
                _delayedUpdate = _lastUpdate.AddMilliseconds(150);
                Task.Delay(TimeSpan.FromMilliseconds(150)).ContinueWith(x =>
                {
                    if (_lastUpdate <= _delayedUpdate)
                    {
                        SendMessageIfNotUpdating(documentId);
                    }
                });
            }
        }

        private void SendMessageIfNotUpdating(string documentId)
        {
            if (!_isUpdatingEditor && UpdateDocument != null)
            {
                var text = _textBoxes[documentId].Text;
                System.Diagnostics.Debug.Print(text);
                _lastUpdate = DateTime.Now;
                UpdateDocument(this, new UpdateDocumentRequest
                {
                    DocumentId = documentId,
                    NewContent = text
                });
            }
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            var documentId = txtId.Text;
            var ok = ValidateDocumentId(
                documentId,
                "A document with the same id \"" + documentId +
                "\" is already open. Do you want to close the current document and create a new one?",
                "Close current document?"
                );

            if (ok)
            {
                if (CreateDocument != null)
                {
                    CreateDocument(this, documentId);
                }
                OpenTab(documentId);
            }
        }

        private bool ValidateDocumentId(string documentId, string message, string title)
        {
            
            bool ok = !string.IsNullOrEmpty(documentId);
            if (!ok)
            {
                MessageBox.Show(
                    "Please provide a document Id - document id was empty.",
                    "Document Id missing",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            else if (_tabPages.ContainsKey(documentId))
            {
                var result = MessageBox.Show(
                    message,
                    title,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                ok = result == DialogResult.Yes;
                if (ok)
                {
                    CloseDocument(documentId);
                }
            }

            return ok;
        }

        public void ReloadDocument(string documentId)
        {
            CloseDocument(documentId);
            OpenFindDocumentTab(documentId,
                "\n Need to reload the document with id \"" + documentId + "\", was out of synch for too long."
                + "\n Please be patient ...");
        }

        public void CloseDocument(string documentId)
        {
            CloseTab(documentId);

            if (RemoveDocument != null)
            {
                RemoveDocument(this, documentId);
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

            var textBox = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                ShortcutsEnabled = true
            };
            textBox.TextChanged += (object sender, EventArgs e) => SendMessage(documentId);
            textBox.KeyDown += (sender, e) =>
            {
                if (e.Control && e.KeyCode == Keys.A)
                {
                    textBox.SelectAll();
                }
            };
            tabPage.Controls.Add(textBox);

            _textBoxes.Add(documentId, textBox);
            _tabPages.Add(documentId, tabPage);
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            
            var documentId = txtId.Text;
            var ok = ValidateDocumentId(
                documentId,
                "A document with the same id " + documentId +
                " is already open. Do you want to close the current document and open the new one?",
                "Close current document?"
                );

            if (ok)
            {
                OpenFindDocumentTab(documentId, "\n Searching document with id \"" + documentId + "\"."
                       + "\n Please be patient ...");
            }
        }

        private void OpenFindDocumentTab(string documentId, string text)
        {
            var tabPage = new TabPage(documentId)
            {
                Name = documentId,
                Text = documentId,
            };
            tabControl.Controls.Add(tabPage);
            tabControl.SelectedTab = tabPage;

            var label = new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
            };
            tabPage.Controls.Add(label);

            _tabPages.Add(documentId, tabPage);
            if (FindDocumentRequest != null)
            {
                FindDocumentRequest(this, documentId);
            }
        }

        private delegate void StringDelegate(string documentId);

        private void CloseTab(string documentId)
        {
            tabControl.TabPages.Remove(_tabPages[documentId]);
            _tabPages.Remove(documentId);
            _textBoxes.Remove(documentId);
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
        public event EventHandler<string> TakeOwnershipForDocument;
        public event EventHandler<UpdateDocumentRequest> UpdateDocument;

        private void SharedTextEditor_KeyDown(object sender, KeyEventArgs e)
        {
            var index = tabControl.SelectedIndex;
            if (index!= -1 && e.Control && e.KeyCode == Keys.W )
            {
                CloseDocument(tabControl.GetControl(index).Name);
            }
        }

        private void txtId_Enter(object sender, EventArgs e)
        {
            if (txtId.Text == DocumentNamePlaceholder)
            {
                txtId.Text = "";
            }
        }

        private void txtId_Leave(object sender, EventArgs e)
        {
            if (txtId.Text == "")
            {
                txtId.Text = DocumentNamePlaceholder;
            }
        }
    }

    public class UpdateDocumentRequest
    {
        public string DocumentId { get; set; }
        public string NewContent { get; set; }
    }

}