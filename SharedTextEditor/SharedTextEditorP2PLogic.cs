using DiffMatchPatch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace SharedTextEditor
{
    class SharedTextEditorP2PLogic : ISharedTextEditorP2P
    {
        
        private const int SUPPORTED_NUM_OF_REACTIVE_UPDATES = 20;

        //the channel instance where we execute our service methods against
        private ISharedTextEditorP2PChannel _p2pChannel;
        //the instance context which in this case is our window since it is the service host
        private InstanceContext _instanceContext;

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
        private readonly Dictionary<string, Document> _documents = new Dictionary<string, Document>();

        private readonly string _memberName;
 
        private readonly SharedTextEditor _editor;

        private readonly SHA1 _sha1 = new SHA1CryptoServiceProvider();

        private readonly diff_match_patch _diffMatchPatch = new diff_match_patch();
      

        public SharedTextEditorP2PLogic(string memberName,  SharedTextEditor editor, ISharedTextEditorC2S clientService)
        {
            _memberName = memberName;
         
            _editor = editor;
            _editor.ConnectToP2P += Editor_ConnectToP2P;
            _editor.DisconnectFromP2P += Editor_DisconnectFromP2P;
            _editor.FindDocumentRequest += Editor_FindDocumentRequest;
            //_editor.UpdateDocument += Editor_UpdateDocument;
            //_editor.CreateDocument += Editor_CreateDocument;
            //_editor.RemoveDocument += Editor_RemoveDocument;
            _clientService = clientService;
        }

        private void Editor_RemoveDocument(object sender, string documentId)
        {
            _documents.Remove(documentId);
        }

        private void Editor_UpdateDocument(object sender, UpdateDocumentRequest request)
        {
            var document = _documents[request.DocumentId];

            var updateDto = new UpdateDto
            {
                DocumentId = request.DocumentId,
                PreviousHash = document.CurrentHash,
                Patch = _diffMatchPatch.patch_make(document.Content, request.NewContent),
                //MemberId = document.MyMemberId,
                //TODO set correct hash
                NewHash = document.CurrentHash
            };

            CreatePatchForUpdate(document, updateDto);

            ////Am I the owner?
            //if (document.Owner.Equals(_memberName))
            //{
              
            //}
            //else
            //{
            //    _p2pChannel.UpdateRequest(updateDto);
            //}
        }

        private void Editor_CreateDocument(object sender, string documentId)
        {
            AddDocument(new DocumentDto
            {
                Content = "",
                DocumentId = documentId,
                //MyMemberId = 1,
                Owner = _memberName
            });
        }

        private void Editor_ConnectToP2P(object sender, EventArgs e)
        {
            ConnectToMesh();
        }

        private void Editor_DisconnectFromP2P(object sender, EventArgs e)
        {
            _p2pChannel.Disconnect(_memberName);
        }

        private void Editor_FindDocumentRequest(object sender, string documentId)
        {
            _p2pChannel.FindDocument(documentId, _memberName);
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

            var endpointAddress = new EndpointAddress(_channelFactory.Endpoint.Address.ToString());

            _channelFactory.Endpoint.Address = endpointAddress;
            _p2pChannel = _channelFactory.CreateChannel();

            //the next 3 lines setup the event handlers for handling online/offline events
            //in the MS P2P world, online/offline is defined as follows:
            //Online: the client is _connected to one or more peers in the mesh
            //Offline: the client is all alone in the mesh
            _statusHandler = _p2pChannel.GetProperty<IOnlineStatus>();
            _statusHandler.Online += new EventHandler(ostat_Online);
            _statusHandler.Offline += new EventHandler(ostat_Offline);

            //this is an empty unhandled method on the service interface.
            //why? because for some reason p2p clients don't try to connect to the mesh
            //until the first service method call.  so to facilitate connecting i call this method
            //to get the ball rolling.
            _p2pChannel.InitializeMesh();
        }

        private void ostat_Offline(object sender, EventArgs e)
        {
            // we could update a status bar or animate an icon to 
            //indicate to the user they have disconnected from the mesh
            //TODO update Editor? Number of editors?
            Console.WriteLine("offline");
        }

        private void ostat_Online(object sender, EventArgs e)
        {
            //TODO how to distinguish which members are editing a certain document?
            Console.WriteLine("online");
            //broadcasting a join method call to the mesh members
            _p2pChannel.Connect(_memberName);
        }

        public void Connect(string member)
        {
            _connectedMembers.Add(member);
            _editor.UpdateNumberOfEditors(_connectedMembers.Count);
            _p2pChannel.SynchronizeMemberList(_memberName);
        }

        public void Disconnect(string member)
        {
            _connectedMembers.Remove(member);
            _editor.UpdateNumberOfEditors(_connectedMembers.Count);
        }

        public void InitializeMesh()
        {
            Console.WriteLine("initialize mash");
        }

        public void SynchronizeMemberList(string member)
        {
            _connectedMembers.Add(member);
            _editor.UpdateNumberOfEditors(_connectedMembers.Count);
        }


        private void AddDocument(DocumentDto dto)
        {
            var hash = GetHash(dto.Content);
            var document = new Document
            {
                Id = dto.DocumentId,
                CurrentHash = hash,
                Owner = dto.Owner,
                Content = dto.Content,
                //MyMemberId = dto.MyMemberId,
            };
            if (dto.Owner.Equals(_memberName))
            {
                document.AddRevision(new Revision
                {
                    Id = 0,
                    Content = document.Content,
                    UpdateDto = new UpdateDto
                    {
                        //MemberId = 1,
                        PreviousHash = new byte[] { },
                        NewHash = document.CurrentHash,
                        Patch = new List<Patch>(),
                    }
                });
            }

            if (_documents.ContainsKey(dto.DocumentId))
            {
                _documents[dto.DocumentId] = document;
            }
            else
            {
                _documents.Add(dto.DocumentId, document);
            }
            
        }

        private byte[] GetHash(string content)
        {
            return _sha1.ComputeHash(Encoding.UTF8.GetBytes(content));
        }

        public void FindDocument(string documentId, string memberName)
        {
            //using client/server communication to send document to given memberName
            _clientService.FindDocument(documentId, memberName);

            //if (_documents.ContainsKey(documentId) && _documents[documentId].Owner.Equals(_memberName))
            //{
            //    var dto =  new DocumentDto
            //            {
            //                Content = _editor.GetText(documentId),
            //                DocumentId = documentId,
            //                //MyMemberId = 1,
            //                Owner = _memberName
            //            };
                
            //    _p2pChannel.DocumentDiscoveryResponse(dto);
            //}
        }


        public void DocumentDiscoveryResponse( DocumentDto document)
        {
            AddDocument(document);
          
        }

        public void UpdateRequest(UpdateDto dto)
        {
            bool isUpdateForOwnDocument = _documents.ContainsKey(dto.DocumentId) &&
                                         _documents[dto.DocumentId].Owner.Equals(_memberName);
            //do we know such a document?
            if (!isUpdateForOwnDocument && _documents.ContainsKey(dto.DocumentId))
            {
                var document = _documents[dto.DocumentId];
                //I am the owner/server?
                if (document.Owner.Equals(_memberName))
                {
                    CreatePatchForUpdate(document, dto);
                }
                else
                {
                    ApplyUpdate(document, dto);
                }
            }
        }

        private void CreatePatchForUpdate(Document document, UpdateDto updateDto)
        {
            bool isUpdateForOwnDocument = _documents.ContainsKey(updateDto.DocumentId) &&
                                          _documents[updateDto.DocumentId].Owner.Equals( _memberName);

            var currentRevision = document.GetRevision(document.CurrentHash);

            if (currentRevision == null)
            {

                currentRevision = new Revision
                {
                    Id = 1,
                    Content = document.Content,
                    UpdateDto = updateDto
                };

                document.AddRevision(currentRevision);
            }
            var lastUpdate = currentRevision.UpdateDto;
            var currentHash = document.CurrentHash;

            bool creationSucessfull = false;

            if (currentHash.SequenceEqual(updateDto.PreviousHash))
                //(lastUpdate.PreviousHash.SequenceEqual(updateDto.PreviousHash) && lastUpdate.MemberId < updateDto.MemberId))
            {
                var result = _diffMatchPatch.patch_apply(updateDto.Patch, document.Content);
                if (result.Item2.All(x => x))
                {
                    document.CurrentHash = GetHash(result.Item1);
                    document.Content = result.Item1;
                    updateDto.NewHash = document.CurrentHash;
                    creationSucessfull = true;
                }
            }
            else
            {
                var revision = document.GetRevision(updateDto.PreviousHash);
                if (revision.Id + SUPPORTED_NUM_OF_REACTIVE_UPDATES >= currentRevision.Id)
                {
                    var nextRevision = document.GetRevision(revision.Id + 1);
                    //move to next revision as long as 
                    while (
                        nextRevision.UpdateDto.PreviousHash.SequenceEqual(updateDto.PreviousHash)
                        //&& updateDto.MemberId > nextRevision.UpdateDto.MemberId
                        && nextRevision.Id < currentRevision.Id)
                    {
                        revision = nextRevision;
                        nextRevision = document.GetRevision(nextRevision.Id + 1);
                    }

                    var content = revision.Content;
                    var tmpRevision = revision;
                    var patch = updateDto.Patch;

                    //apply all patches on top of the found revision
                    while (tmpRevision.Id <= currentRevision.Id)
                    {
                        var result = _diffMatchPatch.patch_apply(patch, content);
                        if (result.Item2.All(x => x))
                        {
                            content = result.Item1;
                            if (tmpRevision.Id == currentRevision.Id)
                            {
                                break;
                            }
                            tmpRevision = document.GetRevision(tmpRevision.Id + 1);
                            patch = tmpRevision.UpdateDto.Patch;
                        }
                        else
                        {
                            //TODO error handling
                        }
                    }

                    document.CurrentHash = GetHash(content);
                    document.Content = content;
                    updateDto.Patch = _diffMatchPatch.patch_make(currentRevision.Content, content);
                    updateDto.NewHash = document.CurrentHash;
                    creationSucessfull = true;
                }
            }

            if (creationSucessfull)
            {
                var result = _diffMatchPatch.patch_apply(updateDto.Patch, _editor.GetText(updateDto.DocumentId));
                if (result.Item2.All(x => x))
                {
                    //var currentText = _editor.GetText(updateDto.DocumentId);
                    if (!isUpdateForOwnDocument)
                    {
                        _editor.UpdateText(updateDto.DocumentId, result.Item1);
                    }
                   

                    document.AddRevision(new Revision
                    {
                        Id = currentRevision.Id + 1,
                        Content = document.Content,
                        UpdateDto = updateDto
                    });

                    //Is not own document
                    if (isUpdateForOwnDocument)
                    {
                        var acknowledgeDto = new AcknowledgeDto
                        {
                            //TODO verify whether it should be currentHash or updateDto.PreviousHash
                            PreviousHash = currentHash,
                            NewHash = document.CurrentHash,
                            DocumentId = document.Id
                        };

                        _p2pChannel.AckRequest(acknowledgeDto);
                    }

                    var newUpdateDto = new UpdateDto
                    {
                        DocumentId = document.Id,
                        //MemberId = updateDto.MemberId,
                        NewHash = document.CurrentHash,
                        PreviousHash = currentHash,
                        Patch = updateDto.Patch,
                    };
    
                    _p2pChannel.UpdateRequest(updateDto);
                }
            }
            else if (isUpdateForOwnDocument)
            {
                _p2pChannel.ForceReload(updateDto);
            }
        }

        private void ApplyUpdate(Document document, UpdateDto dto)
        {
            if (dto.PreviousHash.SequenceEqual(document.CurrentHash))
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
            _editor.CloseTab(documentId);
            _p2pChannel.FindDocument(documentId, _memberName);
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
            if (document.OutOfSyncUpdate.PreviousHash.SequenceEqual(document.CurrentHash))
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
                //if (pendingUpdate.MemberId > updateDto.MemberId)
                //{
                //    everythingOk = MergePendingUpdateAfterGivenUpdate(updateDto, pendingUpdate);
                //}
                //else if (pendingUpdate.MemberId < updateDto.MemberId)
                //{
                //    everythingOk = MergePendingUpdateBeforeGivenUpdate(document, updateDto, pendingUpdate, resultAppliedGivenUpdate);
                //}
                //else
                //{
                //    //TODO should we care? we should not get an update from our own
                //    everythingOk = false;
                //}
            }
            return everythingOk;
        }

        private bool MergePendingUpdateAfterGivenUpdate(UpdateDto updateDto, UpdateDto pendingUpdate)
        {
            // it's enough to set the previous hash to the hash of the given update
            pendingUpdate.PreviousHash = updateDto.NewHash;

            // Merge screen and update
            var documentId = updateDto.DocumentId;
            var result = _diffMatchPatch.patch_apply(updateDto.Patch, _editor.GetText(documentId));
            var everythingOk = CheckResultIsValidOtherwiseReOpen(result, documentId);
            if (everythingOk)
            {
                _editor.UpdateText(documentId, result.Item1);
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
                    var mergePatch = _diffMatchPatch.patch_make(resultAppliedPendingUpdate.Item1, _editor.GetText(documentId));
                    var resultMerge = _diffMatchPatch.patch_apply(mergePatch, resultAfterPatches.Item1);
                    everythingOk = CheckResultIsValidOtherwiseReOpen(resultMerge, documentId);
                    if (everythingOk)
                    {
                        _editor.UpdateText(documentId, resultMerge.Item1);
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
            return (document.Owner.Equals(_memberName)) && document.PendingUpdate.PreviousHash == dto.PreviousHash;
        }



        public void ForceReload(UpdateDto dto)
        {
            if (_documents.ContainsKey(dto.DocumentId))
            {
                _p2pChannel.FindDocument(dto.DocumentId, _memberName);
            }
        }
    }
}
