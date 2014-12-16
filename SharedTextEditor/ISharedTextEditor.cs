using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Runtime.Serialization;
using DiffMatchPatch;

namespace SharedTextEditor
{
    [ServiceContract(CallbackContract = typeof(ISharedTextEditorP2P))]
    public interface ISharedTextEditorP2P
    {
        [OperationContract(IsOneWay = true)]
        void InitializeMesh();
        
        [OperationContract(IsOneWay = true)]
        void FindDocument(string host, string documentId, string memberName);
    }

    public interface ISharedTextEditorP2PChannel : ISharedTextEditorP2P, IClientChannel
    {
    }

    [ServiceContract(SessionMode = SessionMode.Allowed)]
    public interface ISharedTextEditorC2S
    {
        [OperationContract(IsOneWay = true)]
        void FindDocument(string host, string documentId, string memberName);

        [OperationContract(IsOneWay = true)]
        void UpdateRequest(UpdateDto dto);

        [OperationContract(IsOneWay = true)]
        void AckRequest(AcknowledgeDto dto);

         [OperationContract(IsOneWay = true)]
         void OpenDocument(DocumentDto dto);
    }

    [DataContract(Namespace = "http://com.sharedtexteditor")]
    public class DocumentDto
    {
        [DataMember]
        public string DocumentId { get; set; }

        [DataMember]
        public int RevisionId { get; set; }

        [DataMember]
        public string Content { get; set; }

        [DataMember]
        public string Owner { get; set; }

        [DataMember]
        public string OwnerHost { get; set; }

        [DataMember]
        public int EditorCount { get; set; }
    }

    [DataContract(Namespace = "http://com.sharedtexteditor")]
    public class AcknowledgeDto
    {
        [DataMember]
        public string DocumentId { get; set; }

        [DataMember]
        public int PreviousRevisionId { get; set; }

        [DataMember]
        public byte[] PreviousHash { get; set; }

        [DataMember]
        public byte[] NewHash { get; set; }

        [DataMember]
        public int NewRevisionId { get; set; }
    }


    [DataContract(Namespace = "http://com.sharedtexteditor")]
    [Serializable]
    public class UpdateDto
    {
        [DataMember]
        public String MemberName { get; set; }

        [DataMember]
        public String MemberHost { get; set; }

        [DataMember]
        public string DocumentId { get; set; }

        [DataMember]
        public int PreviousRevisionId { get; set; }

        [DataMember]
        public byte[] PreviousHash { get; set; }

        [DataMember]
        public byte[] NewHash { get; set; }

        [DataMember]
        public int NewRevisionId { get; set; }

        [DataMember]
        public List<Patch> Patch { get; set; }

        [DataMember]
        public int EditorCount { get; set; }
    }
}
