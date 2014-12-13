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
        private bool _isUpdatingEditor = false;
        

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
            if (!_isUpdatingEditor && UpdateDocument != null)
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
            textBox.TextChanged += (object sender, EventArgs e) => SendMessage(documentId, textBox.Text);
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
                var tabPage = new TabPage(documentId)
                {
                    Name = documentId,
                    Text = documentId,
                };
                tabControl.Controls.Add(tabPage);
                tabControl.SelectedTab = tabPage;

                var label = new Label
                {
                    Text = "\n Searching document with id " + documentId + "."
                           +"\n Please be patient ...",
                    Dock = DockStyle.Fill,
                };
                tabPage.Controls.Add(label);

                _tabPages.Add(documentId, tabPage);
                if (FindDocumentRequest != null)
                {
                    FindDocumentRequest(this, documentId);
                }
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
        public event EventHandler<UpdateDocumentRequest> UpdateDocument;

        private void SharedTextEditor_KeyDown(object sender, KeyEventArgs e)
        {
            var index = tabControl.SelectedIndex;
            if (index!= -1 && e.Control && e.KeyCode == Keys.W )
            {
                CloseDocument(tabControl.GetControl(index).Name);
            }
        }

    }

    public class UpdateDocumentRequest
    {
        public string DocumentId { get; set; }
        public string NewContent { get; set; }
    }

}