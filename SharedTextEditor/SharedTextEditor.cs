using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DiffMatchPatch;

namespace SharedTextEditor
{
    public partial class SharedTextEditor : Form, ISharedTextEditorP2P, ISharedTextEditorC2S
    {
        #region Instance Fields

        //the chat member name
        //TODO how to define MemberName? define a UserName ?
        private readonly string _memberName = "UserName";

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
        private delegate void NoArgDelegate();

        private delegate void StringDelegate(string value);

        private readonly object lockObject = new Object();

        private readonly SHA1 _sha1 = new SHA1CryptoServiceProvider();
        private readonly diff_match_patch _diffMatchPatch = new diff_match_patch();

        private readonly HashSet<string> _connectedMembers = new HashSet<string>();

        private readonly Dictionary<string, Document> _documents = new Dictionary<string, Document>();
        private readonly Dictionary<string, TextBox> _textBoxes = new Dictionary<string, TextBox>();
        private readonly Dictionary<string, TabPage> _tabPages = new Dictionary<string, TabPage>();

        private readonly HashSet<string> _pendingDocumentRequests = new HashSet<string>();

        private bool _connected;
        private const int OwnMemberId = 0;

        #endregion

        public SharedTextEditor()
        {
            InitializeComponent();
        }

        ~SharedTextEditor()
        {
            _participant.Dispose();
        }

        #region WCF Methods

        //this method gets called from a background thread to 
        //connect the service client to the p2p mesh specified
        //by the binding info in the app.config
        private void ConnectToMesh()
        {
            _connected = true;

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

        #endregion

        #region IOnlineStatus Event Handlers

        private void ostat_Offline(object sender, EventArgs e)
        {
            // we could update a status bar or animate an icon to 
            //indicate to the user they have disconnected from the mesh

            int i = 0;
        }

        private void ostat_Online(object sender, EventArgs e)
        {
            //TODO how to distinguish which members are editing a certain document?

            //broadcasting a join method call to the mesh members
            _participant.Connect(_memberName);
        }

        #endregion

        #region ISharedTextEditorP2P Members

        public void Connect(string member)
        {
            _connectedMembers.Add(member);
            UpdateNumberOfEditors();
            _participant.SynchronizeMemberList(_memberName);
        }

        public void Disconnect(string member)
        {
            _connectedMembers.Remove(member);
            UpdateNumberOfEditors();
        }


        public void InitializeMesh()
        {
            //do nothing
        }

        public void UpdateNumberOfEditors()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new NoArgDelegate(UpdateNumberOfEditors));
                return;
            }

            lblNumber.Text = _connectedMembers.Count.ToString();
        }


        public void SynchronizeMemberList(string member)
        {
            _connectedMembers.Add(member);
            UpdateNumberOfEditors();
        }

        public void FindDocument(string documentId)
        {
            //Is it our document? then we inform client about it
            if (_documents[documentId].Owner == _memberName)
            {
                //TODO inform client about the document
            }
        }

        #endregion

        #region ISharedTextEditorC2S members

        public void MemberHasDocument(string documentId, string memberName)
        {
            //we ignore requests if we are not waiting for such a document
            if (_pendingDocumentRequests.Contains(documentId))
            {

            }
        }

        public void DocumentRequest(string documentId)
        {
            //Is it our document? then we send the document to the requesting client
            if (_documents[documentId].Owner == _memberName)
            {
                //TODO send document to client
            }
            else
            {
                //TODO error handling -> document closed -> inform clients
            }
        }

        public void OpenDocument(DocumentDto dto)
        {
            //we ignore requests if we are not waiting for such a document
            if (_pendingDocumentRequests.Contains(dto.DocumentId))
            {
                AddDocument(dto);
                UpdateText(dto.DocumentId, dto.Content);
                _pendingDocumentRequests.Remove(dto.DocumentId);
            }
        }

        private void AddDocument(DocumentDto dto)
        {
            var hash = GetHash(dto.Content);
            _documents.Add(dto.DocumentId, new Document
            {
                Id = dto.DocumentId,
                CurrentHash = hash,
                Owner = dto.Owner,
                Content = "",
                MyMemberId = dto.MyMemberId
            });
        }

        private byte[] GetHash(string content)
        {
            return _sha1.ComputeHash(Encoding.UTF8.GetBytes(content));
        }


        public void UpdateRequest(UpdateDto dto)
        {
            //do we know such a document?
            if (_documents.ContainsKey(dto.DocumentId))
            {
                var document = _documents[dto.DocumentId];
                //I am the owner/server?
                if (document.Owner == _memberName)
                {
                    CreatePatchForUpdate(document, dto);
                }
                else
                {
                    ApplyUpdate(document, dto);
                }
            }
        }

        private void CreatePatchForUpdate(Document document, UpdateDto dto)
        {
            if (document.CurrentHash == dto.PreviousHash)
            {
                
            }
            else
            {
                //TODO search old commit
            }

            //Is not own document
            if (dto.MemberId != OwnMemberId)
            {
                //TODO server/client communication -> send Acknowledgement to owner
            }
            //TODO server/client communication -> send Update to others
        }

        private void ApplyUpdate(Document document, UpdateDto dto)
        {
            if (dto.PreviousHash == document.CurrentHash)
            {
                MergeUpdate(document, dto);
            }
            else if (document.OutOfSyncUpdate == null)
            {
                //update out of sync. Got an update which is not based on previous revision
                document.OutOfSyncUpdate = dto;
            }
            else
            {
                //too many out of sync updates, need to re-open the document
                ReOpenDocument(dto.DocumentId);
            }
        }

        private void ReOpenDocument(string documentId)
        {
            //error has occured, need to re-open the document
            _documents.Remove(documentId);
            tabControl.TabPages.Remove(_tabPages[documentId]);
            OpenTab(documentId);
        }

        private void MergeUpdate(Document document, UpdateDto updateDto)
        {
            var resultAppliedGivenUpdate = _diffMatchPatch.patch_apply(updateDto.Patch, document.Content);
            if (CheckResultIsValidOtherwiseReOpen(resultAppliedGivenUpdate, updateDto.DocumentId))
            {
                if (MergePendingUpdate(document, updateDto, resultAppliedGivenUpdate))
                {
                    UpdateDocument(document, updateDto, resultAppliedGivenUpdate);
                }
            }

            //check whether we have an out of sync update which is based on the given update (so we could apply it as well)
            if (document.OutOfSyncUpdate.PreviousHash == document.CurrentHash)
            {
                var outOfSynUpdate = document.OutOfSyncUpdate;
                document.OutOfSyncUpdate = null;
                MergeUpdate(document, outOfSynUpdate);
            }
        }

        private void UpdateDocument(Document document, UpdateDto updateDto, Tuple<string, bool[]> resultAppliedGivenUpdate)
        {
            document.Content = resultAppliedGivenUpdate.Item1;
            document.CurrentHash = GetHash(document.Content);
            if (document.CurrentHash != updateDto.NewHash)
            {
                //oho... should be the same, something went terribly wrong
                ReOpenDocument(document.Id);
            }
        }

        private bool MergePendingUpdate(Document document, UpdateDto updateDto, Tuple<string, bool[]> resultAppliedGivenUpdate)
        {
            var everythingOk = true;
            var pendingUpdate = document.PendingUpdate;

            if (pendingUpdate != null)
            {
                //will the pending update be applied after the given update?
                if (pendingUpdate.MemberId > updateDto.MemberId)
                {
                    everythingOk = MergePendingUpdateAfterGivenUpdate(updateDto, pendingUpdate);
                }
                else if (pendingUpdate.MemberId < updateDto.MemberId)
                {
                    everythingOk = MergePendingUpdateBeforeGivenUpdate(document, updateDto, pendingUpdate, resultAppliedGivenUpdate);
                }
                else
                {
                    //TODO should we care? we should not get an update from our own
                    everythingOk = false;
                }
            }
            return everythingOk;
        }

        private bool MergePendingUpdateAfterGivenUpdate(UpdateDto updateDto, UpdateDto pendingUpdate)
        {
            // it's enough to set the previous hash to the hash of the given update
            pendingUpdate.PreviousHash = updateDto.NewHash;

            // Merge screen and update
            var documentId = updateDto.DocumentId;
            var result = _diffMatchPatch.patch_apply(updateDto.Patch, _textBoxes[documentId].Text);
            var everythingOk = CheckResultIsValidOtherwiseReOpen(result, documentId);
            if (everythingOk)
            {
                UpdateText(documentId, result.Item1);
            }
            return everythingOk;
        }

        private bool MergePendingUpdateBeforeGivenUpdate(Document document, UpdateDto updateDto, UpdateDto pendingUpdate, Tuple<string, bool[]> resultAppliedGivenUpdate)
        {
            // merging happens as follows, first apply pending patch on document, then given update = new content
            // yet, since server has already applied given update we need to set pending's hash to the has of the update
            // and we need to recompute the patch of the pending patch in order that we get the same text when the ACK follows
            pendingUpdate.PreviousHash = updateDto.NewHash;

            var documentId = updateDto.DocumentId;
            var resultAppliedPendingUpdate = _diffMatchPatch.patch_apply(pendingUpdate.Patch, document.Content);
            bool everythingOk = CheckResultIsValidOtherwiseReOpen(resultAppliedPendingUpdate, documentId);
            if (everythingOk)
            {
                var resultAfterPatches = _diffMatchPatch.patch_apply(updateDto.Patch, resultAppliedPendingUpdate.Item1);
                everythingOk = CheckResultIsValidOtherwiseReOpen(resultAfterPatches, documentId);
                if (everythingOk)
                {
                    //compute new patch for pendin gupdate
                    pendingUpdate.Patch = _diffMatchPatch.patch_make(resultAppliedGivenUpdate.Item1, resultAfterPatches.Item1);

                    // Merge screen and update -> screen is built up on pending update, thus create patch pending -> screen
                    // then apply patch to new content
                    var mergePatch = _diffMatchPatch.patch_make(resultAppliedPendingUpdate.Item1, _textBoxes[documentId].Text);
                    var resultMerge = _diffMatchPatch.patch_apply(mergePatch, resultAfterPatches.Item1);
                    everythingOk = CheckResultIsValidOtherwiseReOpen(resultMerge, documentId);
                    if (everythingOk)
                    {
                        UpdateText(documentId, resultMerge.Item1);
                    }
                }
            }
            return everythingOk;
        }

        private bool CheckResultIsValidOtherwiseReOpen(Tuple<string, bool[]> result, string documentId)
        {
            bool ok = result.Item2.All(x => x);
            if (!ok)
            {
                ReOpenDocument(documentId);
            }
            return ok;
        }


        private delegate void UpdateTextDelegate(string documentId, string content);

        private void UpdateText(string documentId, string content)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new UpdateTextDelegate(UpdateText));
                return;
            }

            _textBoxes[documentId].Text = content;
        }

        public void AckRequest(AcknowledgeDto dto)
        {
            //do we know the document?
            if (_documents.ContainsKey(dto.DocumentId))
            {
                var document = _documents[dto.DocumentId];
                if (WeAreOwnerAndCorrespondsToPendingUpdate(dto, document))
                {
                    var result = _diffMatchPatch.patch_apply(document.PendingUpdate.Patch, document.Content);
                    if (CheckResultIsValidOtherwiseReOpen(result, dto.DocumentId))
                    {
                        UpdateDocument(document, document.PendingUpdate, result);
                    }
                }
            }
        }

        private bool WeAreOwnerAndCorrespondsToPendingUpdate(AcknowledgeDto dto, Document document)
        {
            return document.Owner != _memberName && document.PendingUpdate.PreviousHash == dto.PreviousHash;
        }

        #endregion

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if(!_connected)
            {
                txtId.Enabled = true;
                btnOpen.Enabled = true;
                btnCreate.Enabled = true;
                btnConnect.Text = "Disconnect";

                NoArgDelegate executor = ConnectToMesh;
                executor.BeginInvoke(null, null);
            }
            else
            {
                _participant.Disconnect(_memberName);
                _connected = false;
                btnConnect.Text = "Connect";
                btnCreate.Enabled = false;
                btnOpen.Enabled = false;
                txtId.Enabled = false;
            }
        }

        private void SendMessage(string documentId, string text)
        {
            var document = _documents[documentId];
            var updateDto = new UpdateDto
            {
                DocumentId = documentId,
                PreviousHash = document.CurrentHash,
                Patch = _diffMatchPatch.patch_make(document.Content, text),
                MemberId = document.MyMemberId
            };

            //I am the owner?
            if (document.MyMemberId != OwnMemberId)
            {
                CreatePatchForUpdate(document, updateDto);
            }
            else
            {
                //TODO client/server communication -> send updateDto to server
            }
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            var ok = true;
            var documentId = txtId.Text;
            if (_documents.ContainsKey(documentId))
            {
                //TODO ask if current document shall be closed
                if (ok)
                {
                    tabControl.TabPages.Remove(_tabPages[documentId]);
                    _tabPages.Remove(documentId);
                    _textBoxes.Remove(documentId);
                    _documents.Remove(documentId);
                }
            }

            if (ok)
            {
                OpenTab(documentId);
                AddDocument(new DocumentDto
                {
                    Content="",
                    DocumentId = documentId,
                    MyMemberId = OwnMemberId,
                    Owner = _memberName
                });
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
            _pendingDocumentRequests.Add(documentId);
            _participant.FindDocument(documentId);
        }
    }
}