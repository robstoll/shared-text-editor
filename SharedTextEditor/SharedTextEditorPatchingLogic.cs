using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DiffMatchPatch;

namespace SharedTextEditor
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class SharedTextEditorPatchingLogic : ISharedTextEditorC2S
    {
        private const int SUPPORTED_NUM_OF_REACTIVE_UPDATES = 10;
        private const int FIRST_VALID_REVISON_ID = 1;

        private readonly HashSet<string> _pendingDocumentRequests = new HashSet<string>();
        private readonly Dictionary<string, Document> _documents = new Dictionary<string, Document>();
        private readonly SHA1 _sha1 = new SHA1CryptoServiceProvider();
        private readonly diff_match_patch _diffMatchPatch = new diff_match_patch(); 
        private readonly string _memberName;
        private readonly string _serverHost;
        private readonly SharedTextEditor _editor;
        private readonly IClientServerCommunication _communication;

        public SharedTextEditorPatchingLogic(string memberName, string serverHost, SharedTextEditor editor, IClientServerCommunication clientServerCommunication)
        {
            _memberName = memberName;
            _serverHost = serverHost;
            _editor = editor;
            _communication = clientServerCommunication;
            _editor.FindDocumentRequest += Editor_FindDocumentRequest;
            _editor.CreateDocument += Editor_CreateDocument;
            _editor.RemoveDocument += Editor_RemoveDocument;
            _editor.UpdateDocument += Editor_UpdateDocument;
            _editor.TakeOwnershipForDocument += Editor_TakeOwnershipForDocument;
          ;
        }

        private void Editor_UpdateDocument(object sender, UpdateDocumentRequest request)
        {
            var document = _documents[request.DocumentId];

            var updateDto = CreateUpdateDto(document, request.NewContent);

            //Am I the owner?
            if (document.Owner == _memberName)
            {
                CreatePatchForUpdate(document, updateDto);
            }
            else if(document.PendingUpdate == null)
            {                
                document.PendingUpdate = updateDto;
                SendUpdateToDocumentOwner(document, updateDto);
            }
        }

        private UpdateDto CreateUpdateDto(Document document, string content)
        {
            var updateDto = new UpdateDto
            {
                DocumentId = document.Id,
                PreviousRevisionId = document.CurrentRevisionId,
                PreviousHash = document.CurrentHash,
                Patch = _diffMatchPatch.patch_make(document.Content, content),
                MemberName = _memberName,
                MemberHost = _serverHost
            };
            return updateDto;
        }

        private void Editor_FindDocumentRequest(object sender, string documentId)
        {
            _pendingDocumentRequests.Add(documentId);
        }

        private void Editor_CreateDocument(object sender, string documentId)
        {
            AddDocument(new DocumentDto
            {
                Content = "",
                DocumentId = documentId,
                RevisionId = 1,
                Owner = _memberName,
                OwnerHost = _serverHost,
                EditorCount = 1
            });
        }

        private void Editor_RemoveDocument(object sender, string documentId)
        {
            if (_pendingDocumentRequests.Contains(documentId))
            {
                _pendingDocumentRequests.Remove(documentId);
            }
            if (_documents.ContainsKey(documentId))
            {
                _documents.Remove(documentId);
            }
        }

        private void Editor_TakeOwnershipForDocument(object sender, string documentId)
        {
            TakeOwnershipForDocument(documentId);
        }


        public void FindDocument(string host, string documentId, string memberName)
        {
            //Is it our document? then we inform client about it
            if (_documents.ContainsKey(documentId) && _documents[documentId].Owner == _memberName)
            {
                Document document = _documents[documentId];
                document.AddEditor(memberName, host);

                _editor.UpdateNumberOfEditors(document.Id, document.EditorCount); 
                _communication.OpenDocument(host, new DocumentDto
                {
                    Content = document.Content,
                    DocumentId = documentId,
                    RevisionId = document.CurrentRevisionId,
                    Owner = _memberName,
                    OwnerHost = _serverHost,
                    EditorCount = document.EditorCount
                });

            }
        }

        public void OpenDocument(DocumentDto dto)
        {
            //we ignore requests if we are not waiting for such a document
            if (_pendingDocumentRequests.Contains(dto.DocumentId))
            {
                AddDocument(dto);
                _editor.UpdateText(dto.DocumentId, dto.Content);
                _pendingDocumentRequests.Remove(dto.DocumentId);
            }
        }

        private void AddDocument(DocumentDto dto)
        {
            var hash = GetHash(dto.Content);
            var document = new Document
            {
                Id = dto.DocumentId,
                CurrentRevisionId = dto.RevisionId,
                CurrentHash = hash,
                Owner = dto.Owner,
                OwnerHost = dto.OwnerHost,
                Content = dto.Content,
            };
            if (dto.Owner == _memberName)
            {
                document.AddRevision(new Revision
                {
                    Id = dto.RevisionId,
                    Content = document.Content,
                    UpdateDto = new UpdateDto
                    {
                        MemberName = _memberName,
                        MemberHost = _serverHost,
                        PreviousRevisionId = 0,
                        PreviousHash = new byte[] { },
                        NewHash = hash,
                        NewRevisionId = dto.RevisionId,
                        Patch = new List<Patch>(),
                    }
                });
            }

            _editor.UpdateNumberOfEditors(document.Id, document.EditorCount); 

            _documents.Add(dto.DocumentId, document);
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
  
                _editor.UpdateNumberOfEditors(document.Id, dto.EditorCount); 

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

        private void TakeOwnershipForDocument(string documenId)
        {
            _documents[documenId].Owner = _memberName;
        }

        private void CreatePatchForUpdate(Document document, UpdateDto updateDto)
        {
            var currentRevision = document.GetCurrentRevision();
            var lastUpdate = currentRevision.UpdateDto;
            //non existing revision - used as null object
            var secondLastUpdate = new UpdateDto {NewRevisionId = -1};
            if (document.CurrentRevisionId > FIRST_VALID_REVISON_ID)
            {
                secondLastUpdate=document.GetRevision(document.CurrentRevisionId - 1).UpdateDto;   
            }

            bool creationSucessfull = false;

            //update is either based on current version 
            //or on previous version where 
            //   - the member which initialised the previous version was the owner itself
            //   - or a member with a lower member name (and thus the given update will be applied afterwards)
            //)
            if (IsFirstPreviousOfSecond(lastUpdate, updateDto) ||
                (IsFirstPreviousOfSecond(secondLastUpdate, updateDto)
                   && MemberOfFirstUpdateIsOwnerOrLowerMember(lastUpdate, updateDto)
                )
            )
            {
                var result = _diffMatchPatch.patch_apply(updateDto.Patch, document.Content);
                if (result.Item2.All(x => x))
                {
                    document.Content = result.Item1;
                    creationSucessfull = true;
                }else
                {
                   HandleErrorOnUpdate(updateDto);
                }
            }
            else
            {
                var revision = document.GetRevision(updateDto.PreviousRevisionId);
                if (revision.Id + SUPPORTED_NUM_OF_REACTIVE_UPDATES >= currentRevision.Id)
                {
                    var nextRevision = document.GetRevision(revision.Id + 1);
                    //move to next revision as long as 
                    while (
                        nextRevision.UpdateDto.PreviousHash.SequenceEqual(updateDto.PreviousHash)
                        && MemberOfFirstUpdateIsNotOwnerAndHigherMember(updateDto, nextRevision.UpdateDto)
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
                            HandleErrorOnUpdate(updateDto);
                        }
                    }
                    document.Content = content;
                    updateDto.Patch = _diffMatchPatch.patch_make(currentRevision.Content, content);
                    creationSucessfull = true;
                }
            }

            if (creationSucessfull)
            {
                
                document.CurrentRevisionId = currentRevision.Id + 1;
                document.CurrentHash = GetHash(document.Content);
                updateDto.NewRevisionId = document.CurrentRevisionId;
                updateDto.NewHash = document.CurrentHash;
                document.AddRevision(new Revision
                {
                    Id = document.CurrentRevisionId,
                    Content = document.Content,
                    UpdateDto = updateDto
                });

                if (IsNotOwnUpdate(updateDto))
                {
                    _editor.UpdateText(updateDto.DocumentId, document.Content);

                    var acknowledgeDto = new AcknowledgeDto
                    {
                        PreviousRevisionId = updateDto.PreviousRevisionId,
                        PreviousHash = updateDto.PreviousHash,
                        NewRevisionId = updateDto.NewRevisionId,
                        NewHash = updateDto.NewHash,
                        DocumentId = document.Id
                    };

                    _communication.AckRequest(updateDto.MemberHost, acknowledgeDto);
                }

                var newUpdateDto = new UpdateDto
                {
                    DocumentId = document.Id,
                    MemberName = updateDto.MemberName,
                    MemberHost = _serverHost,
                    PreviousRevisionId = updateDto.PreviousRevisionId,
                    PreviousHash = updateDto.PreviousHash,
                    NewRevisionId = updateDto.NewRevisionId,
                    NewHash = updateDto.NewHash,
                    Patch = updateDto.Patch,
                    EditorCount = document.EditorCount
                };

                foreach (var editorHost in document.Editors().Values)
                {
                    if (updateDto.MemberHost != editorHost)
                    {
                        try
                        {
                            _communication.UpdateRequest(editorHost, newUpdateDto);
                        }
                        catch (EndpointNotFoundException)
                        {
                            document.Editors().Remove(editorHost);
                        }
                    }
                }
            }
            else if (IsNotOwnUpdate(updateDto))
            {
                HandleErrorOnUpdate(updateDto);
            }
        }

        private bool IsFirstPreviousOfSecond(UpdateDto lastUpdate, UpdateDto updateDto)
        {
            return lastUpdate.NewRevisionId == updateDto.PreviousRevisionId &&
                   lastUpdate.NewHash.SequenceEqual(updateDto.PreviousHash);
        }

        private bool IsNotOwnUpdate(UpdateDto updateDto)
        {
            return updateDto.MemberName != _memberName;
        }

        private bool MemberOfFirstUpdateIsOwnerOrLowerMember(UpdateDto firstUpdateDto, UpdateDto secondUpdateDto)
        {
            return !IsNotOwnUpdate(firstUpdateDto) || string.Compare(firstUpdateDto.MemberName, secondUpdateDto.MemberName) < 0;
        }

        private bool MemberOfFirstUpdateIsNotOwnerAndHigherMember(UpdateDto firstUpdateDto, UpdateDto secondUpdateDto)
        {
            return IsNotOwnUpdate(firstUpdateDto) && string.Compare(firstUpdateDto.MemberName, secondUpdateDto.MemberName) > 0;
        }

        private void ApplyUpdate(Document document, UpdateDto dto)
        {
            if (document.CurrentRevisionId == dto.PreviousRevisionId && document.CurrentHash.SequenceEqual(dto.PreviousHash))
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
            _editor.ReloadDocument(documentId);
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
            if (document.OutOfSyncUpdate != null && IsFirstPreviousOfSecond(updateDto, document.OutOfSyncUpdate))
            {
                var outOfSynUpdate = document.OutOfSyncUpdate;
                document.OutOfSyncUpdate = null;
                MergeUpdate(document, outOfSynUpdate);
            }

            var outOfSyncAcknowledge = document.OutOfSyncAcknowledge;
            if (outOfSyncAcknowledge != null && IsFirstPreviousOfSecond(updateDto, document.PendingUpdate))
            {
                ConfirmPendingUpdate(document, outOfSyncAcknowledge);
                document.OutOfSyncAcknowledge = null;   
            }
        }

        private void UpdateDocument(Document document, UpdateDto updateDto, Tuple<string, bool[]> resultAppliedGivenUpdate)
        {
            document.Content = resultAppliedGivenUpdate.Item1;
            document.CurrentRevisionId = updateDto.NewRevisionId;
            document.CurrentHash = GetHash(document.Content);
            if (!document.CurrentHash.SequenceEqual(updateDto.NewHash))
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
                if (MemberOfFirstUpdateIsNotOwnerAndHigherMember(pendingUpdate, updateDto))
                {
                    everythingOk = MergePendingUpdateAfterGivenUpdate(updateDto, pendingUpdate);
                }
                else if (MemberOfFirstUpdateIsOwnerOrLowerMember(pendingUpdate, updateDto))
                {
                    everythingOk = MergePendingUpdateBeforeGivenUpdate(document, updateDto, pendingUpdate,
                        resultAppliedGivenUpdate);
                }
            }
            else
            {
                //no mergin needed, only update the editor
                _editor.UpdateText(updateDto.DocumentId, resultAppliedGivenUpdate.Item1);
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
                if (document.Owner != _memberName && document.PendingUpdate != null)
                {
                    if (document.PendingUpdate.PreviousRevisionId == dto.PreviousRevisionId && document.PendingUpdate.PreviousHash.SequenceEqual(dto.PreviousHash))
                    {
                        ConfirmPendingUpdate(document, dto);
                    }
                    else if (document.OutOfSyncAcknowledge != null)
                    {
                        document.OutOfSyncAcknowledge = dto;
                    }
                    else
                    {
                        ReOpenDocument(document.Id);
                    }
                }
            }
        }

        private void ConfirmPendingUpdate(Document document, AcknowledgeDto dto)
        {
            var result = _diffMatchPatch.patch_apply(document.PendingUpdate.Patch, document.Content);
            if (CheckResultIsValidOtherwiseReOpen(result, dto.DocumentId))
            {
                document.PendingUpdate.NewRevisionId = dto.NewRevisionId;
                document.PendingUpdate.NewHash = dto.NewHash;
                UpdateDocument(document, document.PendingUpdate, result);
            }
            var currentText = _editor.GetText(dto.DocumentId);
            if (document.Content != currentText)
            {
                //send next update
                var updateDto = CreateUpdateDto(document, currentText);
                document.PendingUpdate = updateDto;

                SendUpdateToDocumentOwner(document, updateDto);
            }
            else
            {
                document.PendingUpdate = null;
            }
        }

        private void SendUpdateToDocumentOwner(Document document, UpdateDto dto)
        {
            try
            {
                _communication.UpdateRequest(document.OwnerHost, dto);
            }
            catch (EndpointNotFoundException)
            {
                _editor.ServerUnreachable(document.Id);
            }
        }

        private void HandleErrorOnUpdate(UpdateDto dto)
        {
            if (IsNotOwnUpdate(dto))
            {
                _editor.ReloadDocument(dto.DocumentId);
            }
        }
    }
}
