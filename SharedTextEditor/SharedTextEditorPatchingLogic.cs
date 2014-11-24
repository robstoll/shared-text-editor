using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using DiffMatchPatch;

namespace SharedTextEditor
{
    internal class SharedTextEditorPatchingLogic : ISharedTextEditorC2S
    {
        private const int OWN_MEMBER_ID = 0;
        private const int SUPPORTED_NUM_OF_REACTIVE_UPDATES = 20;

        private readonly HashSet<string> _pendingDocumentRequests = new HashSet<string>();
        private readonly Dictionary<string, Document> _documents = new Dictionary<string, Document>();
        private readonly SHA1 _sha1 = new SHA1CryptoServiceProvider();
        private readonly diff_match_patch _diffMatchPatch = new diff_match_patch();

        private readonly string _memberName;
        private readonly SharedTextEditor _editor;


        public SharedTextEditorPatchingLogic(string memberName, SharedTextEditor editor)
        {
            _memberName = memberName;
            _editor = editor;
            _editor.FindDocumentRequest += Editor_FindDocumentRequest;
            _editor.CreateDocument += Editor_CreateDocument;
            _editor.RemoveDocument += Editor_RemoveDocument;
            _editor.UpdateDocument += Editor_UpdateDocument;
        }

        private void Editor_UpdateDocument(object sender, UpdateDocumentRequest request)
        {
            var document = _documents[request.DocumentId];

            var updateDto = new UpdateDto
            {
                DocumentId = request.DocumentId,
                PreviousHash = document.CurrentHash,
                Patch = _diffMatchPatch.patch_make(document.Content, request.NewContent),
                MemberId = document.MyMemberId
            };

            //Am I the owner?
            if (document.MyMemberId != OWN_MEMBER_ID)
            {
                CreatePatchForUpdate(document, updateDto);
            }
            else
            {
                //TODO client/server communication -> send updateDto to server
            }
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
                MyMemberId = OWN_MEMBER_ID,
                Owner = _memberName
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

        public void FindDocument(string documentId)
        {
            //Is it our document? then we inform client about it
            if (_documents[documentId].Owner == _memberName)
            {
                //TODO inform client about the document
            }
        }

        public void MemberHasDocument(string documentId, string memberName)
        {
            //we ignore requests if we are not waiting for such a document
            if (_pendingDocumentRequests.Contains(documentId))
            {
                _editor.MemberHasDocument(documentId, memberName);
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
                CurrentHash = hash,
                Owner = dto.Owner,
                Content = dto.Content,
                MyMemberId = dto.MyMemberId,
            };
            if (dto.MyMemberId == OWN_MEMBER_ID)
            {
                document.AddRevision(new Revision
                {
                    Id = 0,
                    Content = document.Content,
                    UpdateDto = new UpdateDto
                    {
                        MemberId = OWN_MEMBER_ID,
                        PreviousHash = new byte[] { },
                        NewHash = document.CurrentHash,
                        Patch = new List<Patch>(),
                    }
                });
            }
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

        private void CreatePatchForUpdate(Document document, UpdateDto updateDto)
        {
            var currentRevision = document.GetRevision(document.CurrentHash);
            var lastUpdate = currentRevision.UpdateDto;
            var currentHash = document.CurrentHash;

            bool creationSucessfull = false;

            if (currentHash.SequenceEqual(updateDto.PreviousHash) ||
                (lastUpdate.PreviousHash.SequenceEqual(updateDto.PreviousHash) && lastUpdate.MemberId < updateDto.MemberId))
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
                        && updateDto.MemberId > nextRevision.UpdateDto.MemberId
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
                    _editor.UpdateText(updateDto.DocumentId, result.Item1);

                    document.AddRevision(new Revision
                    {
                        Id = currentRevision.Id + 1,
                        Content = document.Content,
                        UpdateDto = updateDto
                    });

                    //Is not own document
                    if (updateDto.MemberId != OWN_MEMBER_ID)
                    {
                        var acknowledgeDto = new AcknowledgeDto
                        {
                            //TODO verify whether it should be currentHash or updateDto.PreviousHash
                            PreviousHash = currentHash,
                            NewHash = document.CurrentHash,
                            DocumentId = document.Id
                        };

                        //TODO server/client communication -> send Acknowledgement to owner
                    }

                    var newUpdateDto = new UpdateDto
                    {
                        DocumentId = document.Id,
                        MemberId = updateDto.MemberId,
                        NewHash = document.CurrentHash,
                        PreviousHash = currentHash,
                        Patch = updateDto.Patch,
                    };
                    //TODO server/client communication -> send Update to others
                }
            }
            else if (updateDto.MemberId != OWN_MEMBER_ID)
            {
                //TODO error handling -> inform client, shall reload the document
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
            
            //TODO reload document from server
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
            return document.Owner != _memberName && document.PendingUpdate.PreviousHash == dto.PreviousHash;
        }

    }
}
